using System.Collections.Generic;
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
 * ・巡回ルート(CreatePatrolPaths)
 *   子に置いた巡回ルート(CS_VillainPatrolRoute)を、上から順に使う(グループのメンバーへ生成した順に割り当てる)
 *   使えるルートが1本も無い時は、路地裏の両端を往復するルート(GetPatrolEnds)を全員で使う
 *   端で見回す時間(lookAroundTime)と、同じルートを回る人同士の間隔(patrolSpacing)はスポーン位置ごとに決める
 * ・路地裏の両端(GetPatrolEnds)
 *   路地裏の範囲(Patrol Area)の、
 *   長い方向の両端(端から patrolEndMargin 内側)を巡回の端とする
 *   → 路地裏の範囲が無い時は、この位置から前後(Z方向)に fallbackPatrolDistance 離れた2点
 *   → 端はNavMeshの上に合わせる(見つからない時はそのままの位置)
 *   Patrol Areaが空なら、親のNavMeshModifierVolume、無ければこの位置を真上から見て含む路地裏
 *   (BackAlleyのNavMeshModifierVolume)を探して使う
 *   出現点はField_Multi・Field_Solo1などに直接置き、路地裏(Field_Common)とは別のプレハブになるため
 * ・Sceneビューで選択すると、生成範囲が赤い円、子の巡回ルートが水色の線(無ければ路地裏の両端が黄色い線)で表示される
 */
// ========================================

public class CS_VillainSpawnPoint : MonoBehaviour
{
    private const float _navMeshSampleRadius = 2f;   // 巡回の端をNavMeshの上に合わせる時に探す半径(m)

    [SerializeField, Min(0f)]
    [Tooltip("グループのメンバーを並べる円の半径(m)")]
    private float _groupRadius = 1.5f;

    [Header("巡回")]
    [SerializeField, Min(0f)]
    [Tooltip("巡回ルートの端に着いてから、グループ全員で見回す時間(秒)")]
    private float _lookAroundTime = 3f;

    [SerializeField, Min(0f)]
    [Tooltip("同じルートを回る人同士の間隔(m)")]
    private float _patrolSpacing = 2f;

    [Header("巡回ルートが無い時(路地裏の両端を往復)")]
    [SerializeField]
    [Tooltip("巡回する路地裏の範囲。空なら親、無ければこの位置を含む路地裏から探す")]
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
    public float lookAroundTime => _lookAroundTime;
    public float patrolSpacing => _patrolSpacing;

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

    // 巡回ルートの形を、子の巡回ルートの上から順に作る
    // 使えるルートが1本も無い時は、路地裏の両端を往復するルート1本にする
    public List<CS_VillainPatrolPath> CreatePatrolPaths()
    {
        List<CS_VillainPatrolPath> paths = new List<CS_VillainPatrolPath>();
        foreach (CS_VillainPatrolRoute route in GetComponentsInChildren<CS_VillainPatrolRoute>())
        {
            CS_VillainPatrolPath path = route.CreatePath();
            if (path != null) paths.Add(path);
        }

        if (paths.Count == 0)
        {
            GetPatrolEnds(out Vector3 endA, out Vector3 endB);
            paths.Add(new CS_VillainPatrolPath(new[] { endA, endB }));
        }
        return paths;
    }

    // 路地裏の範囲の両端(NavMeshの上に合わせた位置)。巡回ルートが無い時に往復する
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

        // 子の巡回ルートがあればそれを、無ければ路地裏の両端を表示する
        bool hasRoute = false;
        foreach (CS_VillainPatrolRoute route in GetComponentsInChildren<CS_VillainPatrolRoute>())
        {
            route.DrawGizmos();
            hasRoute |= route.isValid;
        }
        if (hasRoute) return;

        GetRawPatrolEnds(out Vector3 endA, out Vector3 endB);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(endA, endB);
        Gizmos.DrawWireSphere(endA, 0.3f);
        Gizmos.DrawWireSphere(endB, 0.3f);
    }
}
