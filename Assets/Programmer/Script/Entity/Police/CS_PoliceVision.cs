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
    // 陽動ホログラムは、気付いたら引き付けられるよう最優先にする
    public const int noTargetPriority = 0;
    public const int villainPriority = 1;
    public const int playerPriority = 2;
    public const int hologramPriority = 3;

    // 変身中のプレイヤーに付くタグ(CS_PlayerTransformationが変身状態に合わせて切り替える)
    private const string _transformedPlayerTag = "PlayerTransformation";

    // 周囲のコライダーを集める際に使い回すバッファ(判定のたびに配列を確保しないため)
    private readonly Collider[] _overlapBuffer = new Collider[32];

    // 発見視界の視野角度(視野全体の幅)
    private float _viewAngle = 0.0f;

    // 発見視界の視野距離(実際に目視でき、見つけてしまう視界)
    private float _viewDistance = 0.0f;

    // 巡回時の視界の視野角度・視野距離(判定の大きい視界。プレイヤーが入ると、怪しんで近づいてくる。発見視界より大きい時だけ使う)
    private float _suspicionAngle = 0.0f;
    private float _suspicionDistance = 0.0f;

    // 視野の高さ(目の高さから、上下にこの距離以内の標的だけが見える)
    private float _viewHeight = 0.0f;

    // 初期化処理を行わずに判定するのを防ぐためのフラグ
    private bool _isInitialized = false;

    [Header("＝＝＝ 視界 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("transformの位置から目までの高さ")]
    private float _eyeHeight = 0.6f;

    [SerializeField, Min(0f)]
    [Tooltip("この距離以内なら、視野角に関係なく(背後でも)気付く")]
    private float _noticeDistance = 1.5f;

    // 発見視界の視野角度(視野全体の幅)
    public float viewAngle => _viewAngle;

    // 発見視界の視野距離
    public float viewDistance => _viewDistance;

    // 巡回時の視界の視野角度・視野距離
    public float suspicionAngle => _suspicionAngle;
    public float suspicionDistance => _suspicionDistance;

    // 視野の高さ
    public float viewHeight => _viewHeight;

    // 視野角に関係なく気付く距離
    public float noticeDistance => _noticeDistance;

    /// <summary>
    /// 警察の視界に関する初期化メソッド
    /// </summary>
    /// <param name="viewAngle">視野角度(視野全体の幅)</param>
    /// <param name="viewDistance">視野距離</param>
    /// <param name="viewHeight">視野の高さ(目の高さから、上下にこの距離以内の標的だけが見える)</param>
    public void Setting(float viewAngle, float viewDistance, float viewHeight)
    {
        Setting(viewAngle, viewDistance, viewHeight, viewAngle, 0.0f);
    }

    /// <summary>
    /// 警察の視界に関する初期化メソッド(巡回時の視界つき)
    /// 発見視界: 実際に目視でき、見つけてしまう視界(ゲーム中に見える視界) / 巡回時の視界: 判定の大きい視界(プレイヤーが入ると、怪しんで近づいてくる)
    /// </summary>
    /// <param name="viewAngle">発見視界の視野角度(視野全体の幅)</param>
    /// <param name="viewDistance">発見視界の視野距離</param>
    /// <param name="viewHeight">視野の高さ(目の高さから、上下にこの距離以内の標的だけが見える。2つの視界で共通)</param>
    /// <param name="suspicionAngle">巡回時の視界の視野角度(視野全体の幅)</param>
    /// <param name="suspicionDistance">巡回時の視界の視野距離(発見視界より大きい時だけ使う。0なら使わない)</param>
    public void Setting(float viewAngle, float viewDistance, float viewHeight, float suspicionAngle, float suspicionDistance)
    {
        _viewAngle = viewAngle;
        _viewDistance = viewDistance;
        _viewHeight = viewHeight;
        _suspicionAngle = suspicionAngle;
        _suspicionDistance = suspicionDistance;
        _isInitialized = true;
    }

    /// <summary>
    /// 巡回時の視界の中にいるプレイヤーのうち、一番近いプレイヤー(怪しいプレイヤー)を探すメソッド
    /// 巡回時の視界が、発見視界より大きい時だけ使う。見つけた(発見視界に入った)ことにはならず、近づいて確かめるのに使う
    /// 悪人・ホログラムは対象にしない
    /// </summary>
    /// <returns>怪しいプレイヤー(いなければnull)</returns>
    public Transform FindSuspect()
    {
        if (!_isInitialized || _suspicionDistance <= _viewDistance) return null;

        Transform best = null;
        float bestSqrDistance = float.MaxValue;
        int count = Physics.OverlapSphereNonAlloc(transform.position, _suspicionDistance, _overlapBuffer, CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform candidate = GetTargetRoot(_overlapBuffer[i], out int candidatePriority);
            if (candidate == null || candidatePriority != playerPriority) continue;
            if (!IsVisible(_overlapBuffer[i].bounds.center, candidate, false, _suspicionDistance, _suspicionAngle)) continue;

            float sqrDistance = (candidate.position - transform.position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance) continue;

            best = candidate;
            bestSqrDistance = sqrDistance;
        }
        return best;
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

        // キャラクター(プレイヤー・悪人)は、Entityレイヤーのコライダーから探す
        // (警察自身もEntityだが、HPを持たないので標的にはならない)
        int count = Physics.OverlapSphereNonAlloc(transform.position, _viewDistance, _overlapBuffer, CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider targetCollider = _overlapBuffer[i];
            Transform candidate = GetTargetRoot(targetCollider, out int candidatePriority);
            if (candidate == null || !IsVisible(targetCollider.bounds.center, candidate, candidate == currentTarget, _viewDistance, _viewAngle)) continue;

            SelectBetterTarget(candidate, candidatePriority, currentTarget, ref bestTarget, ref bestSqrDistance, ref priority);
        }

        // 陽動ホログラムはコライダーを持たないので、展開中のホログラムから探す
        foreach (CS_Hologram hologram in CS_Hologram.activeHolograms)
        {
            Transform candidate = hologram.transform;
            if (!IsVisible(candidate.position, candidate, candidate == currentTarget, _viewDistance, _viewAngle)) continue;

            SelectBetterTarget(candidate, hologramPriority, currentTarget, ref bestTarget, ref bestSqrDistance, ref priority);
        }

        return bestTarget;
    }

    /// <summary>
    /// 候補が今までの一番良い標的より優先すべきなら、一番良い標的を候補に置き換えるメソッド
    /// 優先度が高い標的 → 今追っている標的 → 近い標的 の順に優先する
    /// </summary>
    /// <param name="candidate">候補の標的</param>
    /// <param name="candidatePriority">候補の優先度</param>
    /// <param name="currentTarget">今追っている標的(いなければnull)</param>
    /// <param name="bestTarget">今までの一番良い標的</param>
    /// <param name="bestSqrDistance">今までの一番良い標的までの距離の2乗</param>
    /// <param name="bestPriority">今までの一番良い標的の優先度</param>
    private void SelectBetterTarget(Transform candidate, int candidatePriority, Transform currentTarget,
        ref Transform bestTarget, ref float bestSqrDistance, ref int bestPriority)
    {
        // 今追っている標的は距離を最小として扱い、同じ優先度なら追い続ける(標的が頻繁に切り替わるのを防ぐ)
        float sqrDistance = candidate == currentTarget ? -1.0f : (candidate.position - transform.position).sqrMagnitude;

        if (candidatePriority < bestPriority) return;
        if (candidatePriority == bestPriority && sqrDistance >= bestSqrDistance) return;

        bestTarget = candidate;
        bestSqrDistance = sqrDistance;
        bestPriority = candidatePriority;
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
    /// <param name="targetPosition">標的の中心の位置</param>
    /// <param name="targetRoot">標的の本体</param>
    /// <param name="isChasing">今追っている標的か(追っている標的は、階段を上り下りして高さが離れても見失わないよう、視野の高さを見ない)</param>
    /// <param name="viewDistance">使う視界の視野距離</param>
    /// <param name="viewAngle">使う視界の視野角度(視野全体の幅)</param>
    /// <returns>見えていればtrue</returns>
    private bool IsVisible(Vector3 targetPosition, Transform targetRoot, bool isChasing, float viewDistance, float viewAngle)
    {
        Vector3 eyePosition = transform.position + Vector3.up * _eyeHeight;

        // 目の高さから視野の高さより離れている標的は見えない(二階にいる標的など)
        Vector3 toTarget = targetPosition - eyePosition;
        if (!isChasing && Mathf.Abs(toTarget.y) > _viewHeight) return false;

        // 距離と角度は水平方向だけで判定する(高さの差で見えなくならないように)
        Vector3 flatToTarget = new Vector3(toTarget.x, 0.0f, toTarget.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0.0f, transform.forward.z);

        float sqrDistance = flatToTarget.sqrMagnitude;
        if (sqrDistance > viewDistance * viewDistance) return false;

        // Vector3.Angleは正面からのずれ(0〜180度)を返すので、視野全体の幅の半分と比べる
        bool isNear = sqrDistance <= _noticeDistance * _noticeDistance;
        if (!isNear && Vector3.Angle(flatForward, flatToTarget) > viewAngle * 0.5f) return false;

        // 煙幕の中にいる標的・煙幕越しの標的は見えない(近くにいても気付かない)
        if (CS_SmokeScreen.IsLineBlocked(eyePosition, targetPosition)) return false;

        // ハイドポイントに張り付いているプレイヤーは見えない(ハイドを抜けるか、近づくと見える)
        if (CS_PlayerHide.IsConcealedFrom(targetRoot, transform.position)) return false;

        // ランダムイベントの範囲の中にいる標的は見えない(警察に探知されないイベントの開催中のみ。近くにいても気付かない)
        if (CS_RandomEventManager.IsPoliceDetectionBlocked(targetPosition)) return false;

        // 目から標的までの間に遮るものが無ければ見えている
        return CS_PoliceLineOfSight.IsClear(eyePosition, targetPosition, targetRoot);
    }
}
