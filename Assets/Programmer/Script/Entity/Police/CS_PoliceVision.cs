/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警察の視界を管理するクラス
/// 視界に映っている標的(プレイヤー・悪人)の中から、追うべき標的を選ぶ
/// </summary>
public class CS_PoliceVision : MonoBehaviour
{
    // 標的の優先度(大きいほど優先。要件: プレイヤー > 悪人)
    public const int noTargetPriority = 0;
    public const int villainPriority = 1;
    public const int playerPriority = 2;

    // 変身中のプレイヤーに付くタグ(CS_PlayerTransformationが変身状態に合わせて切り替える)
    private const string _transformedPlayerTag = "PlayerTransformation";

    // 周囲のコライダーを集める際に使い回すバッファ(判定のたびに配列を確保しないため)
    private readonly Collider[] _overlapBuffer = new Collider[32];

    // 視野角度(視野全体の幅)
    private float _viewAngle = 0.0f;

    // 視野距離
    private float _viewDistance = 0.0f;

    // 初期化処理を行わずに判定するのを防ぐためのフラグ
    private bool _isInitialized = false;

    [Header("＝＝＝ 視界 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("transformの位置から目までの高さ")]
    private float _eyeHeight = 0.6f;

    [SerializeField, Min(0f)]
    [Tooltip("この距離以内なら、視野角に関係なく(背後でも)気付く")]
    private float _noticeDistance = 1.5f;

    // 視野角度(視野全体の幅)
    public float viewAngle => _viewAngle;

    // 視野距離
    public float viewDistance => _viewDistance;

    // 視野角に関係なく気付く距離
    public float noticeDistance => _noticeDistance;

    /// <summary>
    /// 警察の視界に関する初期化メソッド
    /// </summary>
    /// <param name="viewAngle">視野角度(視野全体の幅)</param>
    /// <param name="viewDistance">視野距離</param>
    public void Setting(float viewAngle, float viewDistance)
    {
        _viewAngle = viewAngle;
        _viewDistance = viewDistance;
        _isInitialized = true;
    }

    /// <summary>
    /// 視界に映っている標的の中から、追うべき標的を探すメソッド
    /// 優先度が高い標的 → 今追っている標的 → 近い標的 の順に選ぶ
    /// </summary>
    /// <param name="currentTarget">今追っている標的(いなければnull)</param>
    /// <param name="priority">見つけた標的の優先度</param>
    /// <returns>追うべき標的(見えていなければnull)</returns>
    public Transform FindTarget(Transform currentTarget, out int priority)
    {
        Transform bestTarget = null;
        float bestSqrDistance = float.MaxValue;
        priority = noTargetPriority;

        if (!_isInitialized) return null;

        // 標的はEntityレイヤーのキャラクターの中から探す(警察自身もEntityだが、HPを持たないので標的にはならない)
        int count = Physics.OverlapSphereNonAlloc(transform.position, _viewDistance, _overlapBuffer, CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider targetCollider = _overlapBuffer[i];
            Transform candidate = GetTargetRoot(targetCollider, out int candidatePriority);
            if (candidate == null || !IsVisible(targetCollider, candidate)) continue;

            // 今追っている標的は距離を最小として扱い、同じ優先度なら追い続ける(標的が頻繁に切り替わるのを防ぐ)
            float sqrDistance = candidate == currentTarget ? -1.0f : (candidate.position - transform.position).sqrMagnitude;

            if (candidatePriority < priority) continue;
            if (candidatePriority == priority && sqrDistance >= bestSqrDistance) continue;

            bestTarget = candidate;
            bestSqrDistance = sqrDistance;
            priority = candidatePriority;
        }

        return bestTarget;
    }

    /// <summary>
    /// コライダーから、標的の本体(HPを持つオブジェクト)と優先度を取得するメソッド
    /// </summary>
    /// <param name="targetCollider">調べるコライダー</param>
    /// <param name="priority">標的の優先度</param>
    /// <returns>標的の本体(標的でなければnull)</returns>
    private Transform GetTargetRoot(Collider targetCollider, out int priority)
    {
        CS_PlayerHealth player = targetCollider.GetComponentInParent<CS_PlayerHealth>();
        if (player != null)
        {
            priority = playerPriority;
            return IsPlayerTargetable(player) ? player.transform : null;
        }

        CS_VillainHealth villain = targetCollider.GetComponentInParent<CS_VillainHealth>();
        if (villain != null && !villain.isDefeated)
        {
            priority = villainPriority;
            return villain.transform;
        }

        priority = noTargetPriority;
        return null;
    }

    /// <summary>
    /// プレイヤーが変身中かを判定するメソッド
    /// タグで判定する(変身状態の確定はサーバーで行われ、サーバーを含む全員のタグが切り替わる)
    /// </summary>
    /// <param name="player">判定するプレイヤー</param>
    /// <returns>変身中ならtrue</returns>
    public static bool IsTransformed(CS_PlayerHealth player)
    {
        return player.CompareTag(_transformedPlayerTag);
    }

    /// <summary>
    /// プレイヤーが警察の攻撃対象になるかを判定するメソッド(生きていて、変身中のプレイヤーだけが対象)
    /// 視界(標的を探す)と攻撃判定(ダメージを与える)の両方で使う
    /// </summary>
    /// <param name="player">判定するプレイヤー</param>
    /// <returns>攻撃対象ならtrue</returns>
    public static bool IsPlayerTargetable(CS_PlayerHealth player)
    {
        return !player.isDead && IsTransformed(player);
    }

    /// <summary>
    /// 標的が視界に映っているかを判定するメソッド
    /// </summary>
    /// <param name="targetCollider">標的のコライダー</param>
    /// <param name="targetRoot">標的の本体</param>
    /// <returns>見えていればtrue</returns>
    private bool IsVisible(Collider targetCollider, Transform targetRoot)
    {
        Vector3 eyePosition = transform.position + Vector3.up * _eyeHeight;
        Vector3 targetPosition = targetCollider.bounds.center;

        // 距離と角度は水平方向だけで判定する(高さの差で見えなくならないように)
        Vector3 toTarget = targetPosition - eyePosition;
        Vector3 flatToTarget = new Vector3(toTarget.x, 0.0f, toTarget.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0.0f, transform.forward.z);

        float sqrDistance = flatToTarget.sqrMagnitude;
        if (sqrDistance > _viewDistance * _viewDistance) return false;

        // Vector3.Angleは正面からのずれ(0〜180度)を返すので、視野全体の幅の半分と比べる
        bool isNear = sqrDistance <= _noticeDistance * _noticeDistance;
        if (!isNear && Vector3.Angle(flatForward, flatToTarget) > _viewAngle * 0.5f) return false;

        // 目から標的までの間に遮るものが無ければ見えている
        return CS_PoliceLineOfSight.IsClear(eyePosition, targetPosition, targetRoot);
    }
}
