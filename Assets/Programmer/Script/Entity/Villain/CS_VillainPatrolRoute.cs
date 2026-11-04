using UnityEngine;
using UnityEngine.AI;

/*
 * 悪人1人分の巡回ルート
 * スポーン位置(CS_VillainSpawnPoint)の子に人数分置き、Pointsに巡回する地点を順番に並べる
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・置き方
 *   VillainSpawnPoint
 *   ├ PatrolRoute_0   ← このコンポーネント。Pointsに地点を順番に入れる(最初と最後が端)
 *   │  ├ Point_0
 *   │  ├ Point_1
 *   │  └ Point_2
 *   └ PatrolRoute_1   ← 2人目のルート
 * ・グループのメンバーは、生成した順に子の上から順のルートへ割り当てられる(ルートより人数が多ければ、また最初のルートから)
 *   同じルートに割り当てられた人同士は、間を空けて並んで回る(間隔はCS_VillainSpawnPointのPatrol Spacing)
 * ・回り方は往復。端に着いたらグループ全員で見回してから、折り返す(CS_VillainGroup参照)
 * ・地点が2つ未満、または空の要素があるルートは使わない(警告を出す)
 * ・地点はNavMeshの上に合わせて使う(見つからない時はそのままの位置)
 * ・Sceneビューで、このルートかスポーン位置を選ぶと、ルートが水色の線で表示される
 */
// ========================================

public class CS_VillainPatrolRoute : MonoBehaviour
{
    private const float _navMeshSampleRadius = 2f;   // 地点をNavMeshの上に合わせる時に探す半径(m)

    [SerializeField]
    [Tooltip("巡回する地点(上から順に通り、最後まで行ったら折り返す)。2つ以上入れる")]
    private Transform[] _points = new Transform[0];

    // 使えるルートか(地点が2つ以上あり、空の要素が無い)
    public bool isValid
    {
        get
        {
            if (_points == null || _points.Length < 2) return false;

            foreach (Transform point in _points)
            {
                if (point == null) return false;
            }
            return true;
        }
    }

    // ルートの形を作る(地点はNavMeshの上に合わせる)。使えないルートならnull
    public CS_VillainPatrolPath CreatePath()
    {
        if (!isValid)
        {
            Debug.LogWarning($"CS_VillainPatrolRoute: {name} は地点が2つ未満か、空の要素があるため使いません", this);
            return null;
        }

        Vector3[] positions = new Vector3[_points.Length];
        for (int i = 0; i < _points.Length; i++)
        {
            positions[i] = SnapToNavMesh(_points[i].position);
        }
        return new CS_VillainPatrolPath(positions);
    }

    private static Vector3 SnapToNavMesh(Vector3 position)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, _navMeshSampleRadius, NavMesh.AllAreas) ? hit.position : position;
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmos();
    }

    // ルートを線で描く(スポーン位置を選んだ時にも、CS_VillainSpawnPointから呼ばれる)
    public void DrawGizmos()
    {
        if (_points == null) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < _points.Length; i++)
        {
            if (_points[i] == null) continue;

            Gizmos.DrawWireSphere(_points[i].position, 0.3f);
            if (i > 0 && _points[i - 1] != null) Gizmos.DrawLine(_points[i - 1].position, _points[i].position);
        }
    }
}
