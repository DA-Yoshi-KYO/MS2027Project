using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/*
 * パリー(ブリンクのオートカウンター)を行うクラス
 * 攻撃が当たる直前にダッシュ(ブリンク)ボタンを押していれば、その攻撃を無効にして自動で反撃する
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様書「プレイヤー」のブリンク
 *   敵の攻撃に合わせていいタイミング(判定緩め)でブリンクボタンを押すと自動で反撃が行われる。
 *   この反撃を決めると警察すらスタンさせることができ、逃げることができる
 * ・誰でも簡単にできるよう、「攻撃が当たる瞬間」に判定する
 *   1. 操作している本人の手元でブリンクが始まった時(CS_Player.onDashStarted)に、サーバーへ知らせる(押した時刻を覚える)
 *   2. 攻撃が当たる瞬間(CS_PlayerHealth.TakeDamage)に、直前_parryWindow秒以内にブリンクを押していればパリー成功
 *      → その攻撃のダメージは無効になる(TryParryHitがtrueを返す)
 *   3. 成功すると、自分は少しの間無敵になり、攻撃してきた相手に反撃する。全員へ成功を知らせる
 *      (onParried。演出はCS_PlayerParryEffect)
 *   悪人・他のプレイヤー・警察のどれの攻撃でも、同じ「当たる直前に押す」でパリーできる
 *   (悪人の溜め中など、早めに押しても受付時間内なら成功する。1回押すとパリーは1回だけ)
 * ・反撃する相手
 *   ダメージと一緒に攻撃者が渡される攻撃(他のプレイヤー・警察)はその相手
 *   攻撃者が渡されない攻撃(悪人)は、近くで攻撃判定を出している悪人を探す
 *   (CS_VillainCombatが公開している攻撃の段階・判定の中心を読むだけ。悪人のコードは変えていない)
 * ・反撃
 *   他のプレイヤー: _playerStunTime秒スタンさせる(CS_PlayerStun。攻撃も中断される)。変身中なら_counterDamageのダメージも与える
 *   悪人: ノックバックさせて行動を止める(攻撃もキャンセルされる)。変身中なら_counterDamageのダメージも与える
 *         (「攻撃は変身中だけ」のルールに合わせ、変身していない時はダメージを与えない)
 *   警察: IStunnableを持っていればStun(_policeStunTime)を呼ぶ(警察側の対応が必要。CS_IStunnable.cs参照)
 *   反撃する相手が見つからない時も、ダメージの無効・無敵・演出は入る
 * ・自分の攻撃(爆弾など)で自分が受けたダメージはパリーしない
 * ・オンラインでは、ブリンクを押したことがサーバーに届くまでに少しかかるため、受付を長め(既定0.5秒)にしている
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerHealth))]
[RequireComponent(typeof(CS_PlayerTransformation))]
public class CS_PlayerParry : NetworkBehaviour
{
    [Header("受付")]
    [SerializeField][Min(0f)] private float _parryWindow = 0.5f;     // 攻撃が当たる何秒前までにブリンクを押していれば成功か
    [SerializeField][Min(0f)] private float _searchRadius = 4f;      // 反撃する悪人を探す範囲(m)
    [SerializeField] private LayerMask _searchLayers = ~0;          // 反撃する悪人を探すレイヤー

    [Header("成功した時")]
    [SerializeField][Min(0f)] private float _invincibleTime = 0.6f;  // 無敵になる時間(秒)
    [SerializeField][Min(0f)] private float _counterDamage = 1f;     // 反撃のダメージ(変身中のみ。CS_PlayerStats.attackPowerを掛ける)
    [SerializeField][Min(0f)] private float _counterKnockbackPower = 2f;   // 悪人を下がらせる強さ(1で通常攻撃と同じ)
    [SerializeField][Min(0f)] private float _policeStunTime = 2f;    // 警察をスタンさせる時間(秒)
    [SerializeField][Min(0f)] private float _playerStunTime = 1f;    // 他のプレイヤーをスタンさせる時間(秒)

    private const int _bufferSize = 32;

    private CS_Player _player;
    private CS_PlayerHealth _health;
    private CS_PlayerTransformation _transformation;
    private CS_PlayerStats _stats;
    private CS_PlayerScoring _scoring;
    private readonly Collider[] _buffer = new Collider[_bufferSize];
    private Coroutine _invincibleCoroutine;
    private double _lastAttemptTime = double.MinValue;   // 最後にブリンクを押した時刻(サーバー、またはオフラインのみ)
    private bool _hasWarnedNoStun;

    // パリーに成功した時に、全クライアントで発生する(引数は反撃した相手。見つからなければnull。演出用)
    public event Action<GameObject> onParried;

    public float parryWindow => _parryWindow;   // 受付時間(秒)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _health = GetComponent<CS_PlayerHealth>();
        _transformation = GetComponent<CS_PlayerTransformation>();
        _stats = GetComponent<CS_PlayerStats>();
        _scoring = GetComponent<CS_PlayerScoring>();
    }

    private void OnEnable()
    {
        _player.onDashStarted += HandleDashStarted;
    }

    private void OnDisable()
    {
        _player.onDashStarted -= HandleDashStarted;
        ClearInvincible();   // 無敵の途中で無効になってもコルーチンが止まるので、無敵が残らないようにする
    }

    public override void OnNetworkDespawn()
    {
        ClearInvincible();
    }

    // ========================================
    // ブリンクを押した
    // ========================================

    // ブリンクが始まった(操作している本人の手元でだけ発生する)
    private void HandleDashStarted()
    {
        if (!IsSpawned)
        {
            RecordAttempt();
            return;
        }

        ParryAttemptRpc();
    }

    // Ownerからサーバーへ、ブリンクを押したことを知らせる
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ParryAttemptRpc()
    {
        RecordAttempt();
    }

    // 押した時刻を覚える(サーバー、またはオフラインで実行される)
    private void RecordAttempt()
    {
        _lastAttemptTime = Time.timeAsDouble;
    }

    // ========================================
    // 攻撃が当たる瞬間(CS_PlayerHealthから呼ばれる)
    // ========================================

    // 攻撃が当たる直前にブリンクを押していればパリー成功(trueならダメージを無効にする。サーバー、またはオフラインで呼ぶ)
    public bool TryParryHit(GameObject attacker)
    {
        if (!enabled || _health.isDead) return false;
        if (Time.timeAsDouble - _lastAttemptTime > _parryWindow) return false;
        if (attacker != null && attacker.transform.IsChildOf(transform)) return false;   // 自分の攻撃(爆弾など)

        _lastAttemptTime = double.MinValue;   // 1回押すとパリーは1回だけ

        GameObject target = attacker != null ? attacker : FindNearbyAttackingVillain();
        StartInvincible();
        if (target != null) Counter(target);
        NotifyParried(target);
        return true;
    }

    // 近くで攻撃判定を出している悪人のうち、判定の中心が一番近いもの(悪人の攻撃は攻撃者が渡されないため)。いなければnull
    private GameObject FindNearbyAttackingVillain()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _searchRadius, _buffer, _searchLayers, QueryTriggerInteraction.Collide);

        GameObject best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            CS_VillainCombat villain = _buffer[i].GetComponentInParent<CS_VillainCombat>();
            if (villain == null || villain.attackPhase != CSE_VillainAttackPhase.Hit) continue;

            float distance = HorizontalDistance(villain.hitCenter, transform.position);
            if (distance >= bestDistance) continue;

            best = villain.gameObject;
            bestDistance = distance;
        }

        return best;
    }

    // 全員へ、パリーの成功を知らせる
    private void NotifyParried(GameObject target)
    {
        if (!IsSpawned)
        {
            onParried?.Invoke(target);
            return;
        }

        NetworkObject targetObject = target != null ? target.GetComponentInParent<NetworkObject>() : null;
        ParriedRpc(targetObject != null && targetObject.IsSpawned ? targetObject : default);
    }

    [Rpc(SendTo.Everyone)]
    private void ParriedRpc(NetworkObjectReference targetReference)
    {
        GameObject target = targetReference.TryGet(out NetworkObject targetObject) ? targetObject.gameObject : null;
        onParried?.Invoke(target);
    }

    // ========================================
    // 反撃
    // ========================================

    // 相手に反撃する(プレイヤー: スタン + 変身中ならダメージ / 悪人: ノックバック + 変身中ならダメージ / 警察: スタン)
    private void Counter(GameObject target)
    {
        CS_PlayerHealth player = target.GetComponentInParent<CS_PlayerHealth>();
        if (player != null)
        {
            if (player.TryGetComponent(out IStunnable playerStun)) playerStun.Stun(_playerStunTime);
            DealCounterDamage(player);
            return;
        }

        CS_VillainHealth villain = target.GetComponentInParent<CS_VillainHealth>();
        if (villain != null)
        {
            DealCounterDamage(villain);
            CS_AttackHitDetector.TryKnockback(villain, transform.position, _counterKnockbackPower);
            return;
        }

        IStunnable stunnable = target.GetComponentInParent<IStunnable>();
        if (stunnable != null)
        {
            stunnable.Stun(_policeStunTime);
            return;
        }

        if (!_hasWarnedNoStun)
        {
            _hasWarnedNoStun = true;
            Debug.LogWarning($"CS_PlayerParry: {target.name} はIStunnableを実装していないため、スタンさせられません(警察側の対応待ち)", target);
        }
    }

    // 変身中なら反撃のダメージを与える。スコア(CS_PlayerScoring)があればそれを通し、倒したらスコアに入るようにする
    private void DealCounterDamage(IDamageable target)
    {
        if (!_transformation.isTransformed || _counterDamage <= 0f) return;

        float damage = _counterDamage * _stats.attackPower;
        if (_scoring != null)
        {
            _scoring.DealDamage(target, damage);
            return;
        }

        target.TakeDamage(damage, gameObject);
    }

    // ========================================
    // 無敵
    // ========================================

    // 少しの間だけ無敵になる(続けて成功した時は、そこから数え直す)
    private void StartInvincible()
    {
        if (_invincibleTime <= 0f) return;

        if (_invincibleCoroutine != null) StopCoroutine(_invincibleCoroutine);
        _invincibleCoroutine = StartCoroutine(InvincibleRoutine());
    }

    private IEnumerator InvincibleRoutine()
    {
        _health.SetInvincible(this, true);
        yield return new WaitForSeconds(_invincibleTime);
        _health.SetInvincible(this, false);
        _invincibleCoroutine = null;
    }

    // パリーの無敵をやめる
    private void ClearInvincible()
    {
        if (_invincibleCoroutine != null)
        {
            StopCoroutine(_invincibleCoroutine);
            _invincibleCoroutine = null;
        }

        if (_health != null && (!IsSpawned || IsServer)) _health.SetInvincible(this, false);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // 選択中に、反撃する悪人を探す範囲をシーンビューへ表示する(調整用)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, _searchRadius);
    }
}
