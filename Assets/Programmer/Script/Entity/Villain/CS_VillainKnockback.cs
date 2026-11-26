using System;
using Unity.Netcode;
using UnityEngine;

/*
 * 悪人のノックバックを行うクラス
 * 攻撃してきた位置から離れる方向へ少し下がり、その間は他の行動(追跡・攻撃・移動)を止める
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・IKnockbackable.Knockbackで受け付ける(プレイヤー・警察・爆発アイテムなどから呼ばれる)
 * ・ノックバックの流れ
 *   moveTime 秒かけて、distance × power(m) だけ下がる(NavMeshAgent.Moveで動かすので壁は越えない)
 *   → stunTime 秒たつまで(下がり始めてから数える)行動を止める。くらいモーションはこの時間に合わせる
 *   ノックバック中にもう一度受けたら、その時点からやり直す
 * ・isKnockedBackの間、CS_VillainCombatは何もしない
 *   攻撃中に(アーマーを付けずに)ノックバックした場合は、CS_VillainCombatが攻撃を中断する
 * ・superArmorWhileAttackingがオンの間は、攻撃中(溜め・攻撃判定)にノックバックしない(ダメージは受ける)
 * ・撃退済み・逃走中(CS_VillainCombatが無効)の悪人はノックバックしない
 * ・処理はサーバー(オフライン時はその場)でのみ行う。位置はNetworkTransformで同期する
 * ・ノックバックした回数(knockbackCount)はNetworkVariableで全クライアントに同期する
 *   見た目側(CS_VillainVisual)は、この値が増えたらくらいモーションを再生する
 * ・奇襲(CS_PlayerAmbush)を受けた時は、BlowAwayで通常のノックバックより大きく吹き飛ぶ(仕様「奇襲を食らった際の処理」)
 *   1. 攻撃してきた位置の方を一瞬で向く
 *   2. ヒットストップ(呼び出し側が渡す hitStopTime 秒。CS_PlayerAmbushの_hitStopTime)の間、その場に止まる
 *      連続奇襲のスロー中は、無限(float.PositiveInfinity)を渡して止めたままにし、スローを解除する時にReleaseHoldで吹き飛ばし始める
 *   3. blowAwayMoveTime 秒かけて、背中側へ blowAwayDistance(m) 吹き飛ぶ(壁は越えない)
 *      進み方は blowAwayCurve(横軸: 経過の割合0〜1 / 縦軸: 進んだ割合0〜1)。既定は最初に一気に進み、最後にゆっくり止まる
 *   4. 見た目だけ(モデル。visualRoot)を、吹き飛ぶ間 blowAwayArcHeight(m) の山なりに浮かせ、着地で landBounceHeight(m) だけ跳ねさせる
 *      当たり判定・移動は地面(NavMesh)の上のまま。動き始めた回数(blowAwayStartCount)を全員に同期し、各画面で同じ動きを再生する
 *      見た目はゲームの時間で動かす(スロー中はスローで浮く)
 *   奇襲は必ず倒すので、撃退済み(死亡モーション中)・逃走中の悪人も吹き飛ぶ。攻撃中のスーパーアーマーも効かない
 *   死亡モーションを見せるため、くらいモーション(knockbackCount)は増やさない
 */
// ========================================

[RequireComponent(typeof(CS_VillainMove))]
[RequireComponent(typeof(CS_VillainCombat))]
[RequireComponent(typeof(CS_VillainHealth))]
public class CS_VillainKnockback : NetworkBehaviour, IKnockbackable
{
    [SerializeField, Min(0f)]
    [Tooltip("ノックバックで下がる標準の距離(m)。呼び出し側の倍率(power)が掛かる")]
    private float _distance = 0.25f;

    [SerializeField, Min(0.01f)]
    [Tooltip("下がりきるまでの時間(秒)")]
    private float _moveTime = 0.15f;

    [SerializeField, Min(0f)]
    [Tooltip("ノックバックを受けてから行動を再開するまでの時間(秒)。くらいモーションの長さに合わせる")]
    private float _stunTime = 0.3f;

    [SerializeField]
    [Tooltip("攻撃中(溜め・攻撃判定)はノックバックしない(ダメージは受ける)")]
    private bool _superArmorWhileAttacking = true;

    [Header("奇襲で吹き飛ぶ")]
    [SerializeField, Min(0f)]
    [Tooltip("奇襲を受けた時に、背中側へ吹き飛ぶ距離(m)")]
    private float _blowAwayDistance = 3f;

    [SerializeField, Min(0.01f)]
    [Tooltip("奇襲を受けて吹き飛びきるまでの時間(秒)")]
    private float _blowAwayMoveTime = 0.3f;

    [SerializeField]
    [Tooltip("吹き飛ぶ進み方(横軸: 経過の割合0〜1 / 縦軸: 進んだ割合0〜1)。既定は最初に一気に進み、最後にゆっくり止まる")]
    private AnimationCurve _blowAwayCurve = new AnimationCurve(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));

    [SerializeField, Min(0f)]
    [Tooltip("吹き飛ぶ間に、見た目(モデル)を浮かせる高さ(m)。0で浮かせない")]
    private float _blowAwayArcHeight = 1.2f;

    [SerializeField, Min(0f)]
    [Tooltip("着地した時に、見た目(モデル)が跳ねる高さ(m)。0で跳ねない")]
    private float _landBounceHeight = 0.3f;

    [SerializeField, Min(0.01f)]
    [Tooltip("着地して跳ねる時間(秒)")]
    private float _landBounceTime = 0.2f;

    [SerializeField]
    [Tooltip("浮かせる見た目(モデル)。空なら子のAnimatorのオブジェクト")]
    private Transform _visualRoot;

    private CS_VillainMove _move;
    private CS_VillainCombat _combat;
    private CS_VillainHealth _health;

    private Vector3 _velocity;     // 下がる速さ(m/秒、水平方向)
    private float _elapsed;        // ノックバックを受けてからの経過時間
    private float _currentMoveTime;   // 今のノックバックで下がりきるまでの時間(秒)
    private float _currentStunTime;   // 今のノックバックで行動を止める時間(秒)
    private float _currentHoldTime;   // 今のノックバックで、動き始める前にその場で止まる時間(秒。奇襲のヒットストップ)
    private bool _isKnockedBack;
    private Vector3 _currentOffset;   // 今の動きで進む量(m)
    private AnimationCurve _currentCurve;   // 今の動きの進み方(nullなら一定の速さ)
    private bool _isBlowAway;         // 今の動きが奇襲の吹き飛びか
    private bool _hasBlowStarted;     // 吹き飛びが動き始めたか(止まる時間が終わったか)

    // 見た目(モデル)を浮かせる(全クライアント)
    private int _localBlowAwayStartCount;   // オフライン用の、吹き飛びが動き始めた回数
    private int _seenBlowAwayStartCount;    // この画面で再生した回数
    private float _arcTimer = -1f;          // 浮かせ始めてからの時間(秒。負なら浮かせていない)
    private float _visualBaseY;             // 見た目の元の高さ(ローカル)
    private bool _hasVisualBaseY;

    // 吹き飛びが動き始めた回数。書き込みはサーバーのみ。見た目を浮かせるのに全クライアントで使う
    private readonly NetworkVariable<int> _blowAwayStartCount = new NetworkVariable<int>();

    // ノックバックした回数。書き込みはサーバーのみ(NetworkVariableのデフォルト)。見た目の再生に全クライアントで使う
    private readonly NetworkVariable<int> _knockbackCount = new NetworkVariable<int>();

    public bool isKnockedBack => _isKnockedBack;   // ノックバック中か(この間は他の行動を止める)
    public int knockbackCount => _knockbackCount.Value;   // ノックバックした回数(全クライアントで参照可)

    // ノックバックを始めた時に呼ばれる(サーバーのみ)。攻撃の中断やくらいモーションの再生に使う
    public event Action onKnockbackStarted;

    // このマシンが悪人を動かす権威を持つか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    private void Awake()
    {
        _move = GetComponent<CS_VillainMove>();
        _combat = GetComponent<CS_VillainCombat>();
        _health = GetComponent<CS_VillainHealth>();
        if (_visualRoot == null)
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.transform != transform) _visualRoot = animator.transform;
        }
    }

    public override void OnNetworkSpawn()
    {
        // 途中参加などで、生成前の吹き飛びを再生しないようにする
        _seenBlowAwayStartCount = _blowAwayStartCount.Value;
    }

    public void Knockback(Vector3 sourcePosition, float power = 1f)
    {
        if (!hasAuthority) return;
        if (!CanBeKnockedBack()) return;

        Vector3 direction = GetAwayDirection(sourcePosition);
        StartMove(direction * (_distance * Mathf.Max(0f, power)), _moveTime, _stunTime);
        _knockbackCount.Value++;

        onKnockbackStarted?.Invoke();
    }

    // 奇襲を受けた時に呼ぶ(サーバー、またはオフライン)。攻撃してきた位置の方を一瞬で向き、背中側へ大きく吹き飛ぶ
    // hitStopTime: 吹き飛ぶ前に、その場で止まる時間(秒。奇襲のヒットストップ)
    // 奇襲は必ず倒すので、撃退済み・逃走中・攻撃中でも吹き飛ぶ(くらいモーションは再生しない)
    public void BlowAway(Vector3 sourcePosition, float hitStopTime = 0f)
    {
        if (!hasAuthority || !enabled) return;

        Vector3 direction = GetAwayDirection(sourcePosition);
        transform.rotation = Quaternion.LookRotation(-direction);
        StartMove(direction * _blowAwayDistance, _blowAwayMoveTime, _blowAwayMoveTime, hitStopTime);
        _currentCurve = _blowAwayCurve != null && _blowAwayCurve.length > 0 ? _blowAwayCurve : null;
        _isBlowAway = true;
    }

    // 攻撃してきた位置から離れる方向(水平)。真上・真下から攻撃された時は後ろへ下がる
    private Vector3 GetAwayDirection(Vector3 sourcePosition)
    {
        Vector3 direction = transform.position - sourcePosition;
        direction.y = 0f;
        return direction.sqrMagnitude > 0f ? direction.normalized : -transform.forward;
    }

    // 吹き飛ぶ前に止めている時間を、今からdelay秒後までに縮める(連続奇襲のスローを解除した時に、CS_PlayerAmbushが呼ぶ)
    public void ReleaseHold(float delay)
    {
        if (!_isKnockedBack) return;

        _currentHoldTime = Mathf.Min(_currentHoldTime, _elapsed + Mathf.Max(0f, delay));
    }

    // holdTime 秒その場で止まってから、moveTime 秒かけて offset だけ動き、stunTime 秒たつまで(動き始めてから数える)行動を止める
    private void StartMove(Vector3 offset, float moveTime, float stunTime, float holdTime = 0f)
    {
        _velocity = offset / moveTime;
        _currentOffset = offset;
        _currentCurve = null;
        _isBlowAway = false;
        _hasBlowStarted = false;
        _currentMoveTime = moveTime;
        _currentStunTime = stunTime;
        _currentHoldTime = Mathf.Max(0f, holdTime);
        _elapsed = 0f;
        _isKnockedBack = true;
    }

    private void FixedUpdate()
    {
        if (!_isKnockedBack) return;

        float previous = _elapsed;
        _elapsed += Time.fixedDeltaTime;

        // 止まる時間(holdTime)の後、下がりきるまでの間だけ動かす(最後のフレームは下がりすぎないよう、残りの時間分だけ動かす)
        float now = Mathf.Clamp(_elapsed - _currentHoldTime, 0f, _currentMoveTime);
        float before = Mathf.Clamp(previous - _currentHoldTime, 0f, _currentMoveTime);
        if (now > before)
        {
            if (_currentCurve != null)
            {
                // 進み方のカーブで、進んだ割合の差だけ動かす
                float rate = _currentCurve.Evaluate(now / _currentMoveTime) - _currentCurve.Evaluate(before / _currentMoveTime);
                _move.Push(_currentOffset * rate);
            }
            else
            {
                _move.Push(_velocity * (now - before));
            }
        }

        // 吹き飛びが動き始めたら、見た目を浮かせるよう全員に伝える
        if (_isBlowAway && !_hasBlowStarted && _elapsed > _currentHoldTime)
        {
            _hasBlowStarted = true;
            if (IsSpawned) _blowAwayStartCount.Value++;
            else _localBlowAwayStartCount++;
        }

        if (_elapsed >= _currentHoldTime + Mathf.Max(_currentMoveTime, _currentStunTime)) _isKnockedBack = false;
    }

    // 見た目(モデル)を、吹き飛ぶ間は山なりに浮かせ、着地で跳ねさせる(全クライアント・オフライン)
    private void LateUpdate()
    {
        if (_visualRoot == null) return;

        int count = IsSpawned ? _blowAwayStartCount.Value : _localBlowAwayStartCount;
        if (count != _seenBlowAwayStartCount)
        {
            _seenBlowAwayStartCount = count;
            _arcTimer = 0f;
        }
        if (_arcTimer < 0f) return;

        if (!_hasVisualBaseY)
        {
            _visualBaseY = _visualRoot.localPosition.y;
            _hasVisualBaseY = true;
        }

        _arcTimer += Time.deltaTime;
        float height = 0f;
        if (_arcTimer < _blowAwayMoveTime)
        {
            // 吹き飛ぶ間: 山なり
            float t = _arcTimer / _blowAwayMoveTime;
            height = 4f * _blowAwayArcHeight * t * (1f - t);
        }
        else if (_arcTimer < _blowAwayMoveTime + _landBounceTime)
        {
            // 着地した後: 小さく跳ねる
            float t = (_arcTimer - _blowAwayMoveTime) / _landBounceTime;
            height = 4f * _landBounceHeight * t * (1f - t);
        }
        else
        {
            _arcTimer = -1f;
        }

        Vector3 local = _visualRoot.localPosition;
        local.y = _visualBaseY + height;
        _visualRoot.localPosition = local;
    }

    private void OnDisable()
    {
        _isKnockedBack = false;
        if (_visualRoot != null && _hasVisualBaseY)
        {
            Vector3 local = _visualRoot.localPosition;
            local.y = _visualBaseY;
            _visualRoot.localPosition = local;
        }
        _arcTimer = -1f;
    }

    private bool CanBeKnockedBack()
    {
        if (!enabled || _health.isDefeated) return false;
        if (!_combat.enabled) return false;   // 犯罪完遂で逃走中
        if (_superArmorWhileAttacking && _combat.isAttacking) return false;
        return true;
    }

#if UNITY_EDITOR
    // ---- テスト用(再生中にInspectorでこのコンポーネントを右クリックして実行する。ビルドには含まれない) ----

    [ContextMenu("テスト/一番近いプレイヤーからノックバック")]
    private void DebugKnockbackFromNearestPlayer()
    {
        DebugKnockback(1f);
    }

    [ContextMenu("テスト/一番近いプレイヤーからノックバック(4倍・見やすい)")]
    private void DebugKnockbackFromNearestPlayerStrong()
    {
        DebugKnockback(4f);
    }

    private void DebugKnockback(float power)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("CS_VillainKnockback: テストは再生中に実行してください", this);
            return;
        }

        // プレイヤーがいなければ、正面から攻撃されたことにする
        Vector3 source = transform.position + transform.forward;
        float nearestSqr = float.MaxValue;
        foreach (CS_PlayerHealth player in FindObjectsByType<CS_PlayerHealth>(FindObjectsSortMode.None))
        {
            float sqr = (player.transform.position - transform.position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            source = player.transform.position;
            nearestSqr = sqr;
        }

        // ノックバックした場合は経過時間が0から始まり直す。そうでなければ受け付けられなかった
        Knockback(source, power);
        if (!_isKnockedBack || _elapsed > 0f)
        {
            Debug.Log("CS_VillainKnockback: ノックバックしませんでした(攻撃中のスーパーアーマー・撃退済み・逃走中のいずれか)", this);
        }
    }
#endif
}
