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

    private CS_VillainMove _move;
    private CS_VillainCombat _combat;
    private CS_VillainHealth _health;

    private Vector3 _velocity;     // 下がる速さ(m/秒、水平方向)
    private float _elapsed;        // ノックバックを受けてからの経過時間
    private bool _isKnockedBack;

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
    }

    public void Knockback(Vector3 sourcePosition, float power = 1f)
    {
        if (!hasAuthority) return;
        if (!CanBeKnockedBack()) return;

        // 攻撃してきた位置から離れる方向(水平)。真上・真下から攻撃された時は後ろへ下がる
        Vector3 direction = transform.position - sourcePosition;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0f ? direction.normalized : -transform.forward;

        _velocity = direction * (_distance * Mathf.Max(0f, power) / _moveTime);
        _elapsed = 0f;
        _isKnockedBack = true;
        _knockbackCount.Value++;

        onKnockbackStarted?.Invoke();
    }

    private void FixedUpdate()
    {
        if (!_isKnockedBack) return;

        float previous = _elapsed;
        _elapsed += Time.fixedDeltaTime;

        // moveTimeの間だけ下がる(最後のフレームは下がりすぎないよう、残りの時間分だけ動かす)
        float moveDelta = Mathf.Min(_elapsed, _moveTime) - Mathf.Min(previous, _moveTime);
        if (moveDelta > 0f) _move.Push(_velocity * moveDelta);

        if (_elapsed >= Mathf.Max(_moveTime, _stunTime)) _isKnockedBack = false;
    }

    private void OnDisable()
    {
        _isKnockedBack = false;
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
