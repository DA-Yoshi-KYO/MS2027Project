using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/*
 * 悪人グループのスポーン位置
 * プランナーがフィールド(路地裏など、警察が巡回しないエリア)に配置する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・グループのメンバーは、この位置を中心に半径groupRadiusの円周上に並べて生成される
 * ・生成されたグループが1人でも残っている間は使用中になり、次のグループは生成されない
 *   (使用中かどうかの管理はCS_VillainSpawnerが行う)
 * ・グループがいなくなる(全滅・犯罪完遂の逃走)と、しばらくリスポーンを待つ状態(isCoolingDown)になる
 *   待ち時間の開始と、待ち中でも生成してよいかの判断はCS_VillainSpawnerが行う
 * ・見張り役の巡回ルート(GetPatrolEnds)
 *   路地裏の範囲(Patrol Area)の、
 *   長い方向の両端(端から patrolEndMargin 内側)を巡回の端とする
 *   → 路地裏の範囲が無い時は、この位置から前後(Z方向)に fallbackPatrolDistance 離れた2点
 *   → 端はNavMeshの上に合わせる(見つからない時はそのままの位置)
 *   Patrol Areaが空なら、親のNavMeshModifierVolume、無ければこの位置を真上から見て含む路地裏
 *   (BackAlleyのNavMeshModifierVolume)を探して使う
 *   出現点はField_Multi・Field_Solo1などに直接置き、路地裏(Field_Common)とは別のプレハブになるため
 * ・Sceneビューで選択すると、生成範囲が赤い円、巡回ルートが黄色い線で表示される
 */
// ========================================

public class CS_VillainSpawnPoint : MonoBehaviour
{
    private const float _navMeshSampleRadius = 2f;   // 巡回の端をNavMeshの上に合わせる時に探す半径(m)

    [SerializeField, Min(0f)]
    [Tooltip("グループのメンバーを並べる円の半径(m)")]
    private float _groupRadius = 1.5f;

    [Header("見張り役の巡回")]
    [SerializeField]
    [Tooltip("見張り役が巡回する路地裏の範囲。空なら親、無ければこの位置を含む路地裏から探す")]
    private NavMeshModifierVolume _patrolArea;

    [SerializeField, Min(0f)]
    [Tooltip("路地裏の端から、どれだけ内側を巡回の端にするか(m)")]
    private float _patrolEndMargin = 1f;

    [SerializeField, Min(0f)]
    [Tooltip("路地裏の範囲が無い時に、この位置から前後へ巡回する距離(m)")]
    private float _fallbackPatrolDistance = 5f;

    private NavMeshModifierVolume _foundPatrolArea;   // 自動で探した路地裏の範囲(一度見つけたら使い回す)
    private bool _isOccupied;
    private float _cooldownEndTime;   // リスポーンの待ちが終わる時刻(Time.time)

    public float groupRadius => _groupRadius;

    // グループが生成されていて、まだ残っているか
    public bool isOccupied
    {
        get => _isOccupied;
        set => _isOccupied = value;
    }

    // グループがいなくなった後、リスポーンを待っている間か
    public bool isCoolingDown => Time.time < _cooldownEndTime;
    public float cooldownEndTime => _cooldownEndTime;

    private NavMeshModifierVolume patrolArea
    {
        get
        {
            if (_patrolArea != null) return _patrolArea;
            if (_foundPatrolArea == null) _foundPatrolArea = FindPatrolArea();
            return _foundPatrolArea;
        }
    }

    // グループがいなくなったので、duration秒間リスポーンを待つ
    public void StartCooldown(float duration)
    {
        _cooldownEndTime = Time.time + duration;
    }

    // グループのi人目(0始まり)を置く位置を返す
    public Vector3 GetMemberPosition(int index, int memberCount)
    {
        float angle = 360f / memberCount * index;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _groupRadius;
        return transform.position + offset;
    }

    // 見張り役が巡回する両端(NavMeshの上に合わせた位置)
    public void GetPatrolEnds(out Vector3 endA, out Vector3 endB)
    {
        GetRawPatrolEnds(out endA, out endB);
        endA = SnapToNavMesh(endA);
        endB = SnapToNavMesh(endB);
    }

    // 路地裏の範囲の長い方向の両端(高さはこの位置に合わせる)
    private void GetRawPatrolEnds(out Vector3 endA, out Vector3 endB)
    {
        NavMeshModifierVolume area = patrolArea;
        if (area == null)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
            endA = transform.position + forward * _fallbackPatrolDistance;
            endB = transform.position - forward * _fallbackPatrolDistance;
            return;
        }

        // 範囲の中心と、X・Z方向の半分の長さ(ワールド座標。路地裏のScale・回転込み)
        Transform areaTransform = area.transform;
        Vector3 center = areaTransform.TransformPoint(area.center);
        Vector3 halfX = Flat(areaTransform.TransformVector(Vector3.right * area.size.x * 0.5f));
        Vector3 halfZ = Flat(areaTransform.TransformVector(Vector3.forward * area.size.z * 0.5f));
        Vector3 half = halfX.sqrMagnitude >= halfZ.sqrMagnitude ? halfX : halfZ;

        float length = Mathf.Max(0f, half.magnitude - _patrolEndMargin);
        Vector3 direction = half.sqrMagnitude > 0f ? half.normalized : Vector3.forward;
        center.y = transform.position.y;
        endA = center + direction * length;
        endB = center - direction * length;
    }

    // 親の路地裏、無ければこの位置を真上から見て含む路地裏のうち、高さが一番近いものを返す
    private NavMeshModifierVolume FindPatrolArea()
    {
        NavMeshModifierVolume parentArea = GetComponentInParent<NavMeshModifierVolume>();
        if (parentArea != null) return parentArea;

        NavMeshModifierVolume nearest = null;
        float nearestHeight = float.MaxValue;
        foreach (NavMeshModifierVolume area in FindObjectsByType<NavMeshModifierVolume>(FindObjectsSortMode.None))
        {
            Vector3 local = area.transform.InverseTransformPoint(transform.position) - area.center;
            if (Mathf.Abs(local.x) > area.size.x * 0.5f || Mathf.Abs(local.z) > area.size.z * 0.5f) continue;

            float height = Mathf.Abs(area.transform.TransformPoint(area.center).y - transform.position.y);
            if (height >= nearestHeight) continue;
            nearest = area;
            nearestHeight = height;
        }
        return nearest;
    }

    private static Vector3 Flat(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    private static Vector3 SnapToNavMesh(Vector3 position)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, _navMeshSampleRadius, NavMesh.AllAreas) ? hit.position : position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _groupRadius);

        GetRawPatrolEnds(out Vector3 endA, out Vector3 endB);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(endA, endB);
        Gizmos.DrawWireSphere(endA, 0.3f);
        Gizmos.DrawWireSphere(endB, 0.3f);
    }
}
