using UnityEngine;
using UnityEngine.AI;

/*
 * NPCの移動経路を求めて、次に進む方向を返すクラス
 * NavMeshで経路(曲がり角の一覧)を計算し、角を順にたどる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・プレイヤーはNavMeshAgentではなくRigidbodyで動くため、経路の計算だけNavMeshに任せ、
 *   進む方向を「移動入力」としてCS_PlayerNpcInputに渡す(人と同じ移動の仕組みで動く)
 * ・目的地が大きく変わった時と、_repathInterval秒ごとに経路を計算し直す(目標が動いても追える)
 * ・NavMesh上に経路が見つからない時は、目的地へまっすぐ向かう
 */
// ========================================

public class CS_NpcNavigator
{
    private const float _repathInterval = 0.5f;     // 経路を計算し直す間隔(秒)
    private const float _repathDistance = 1f;       // 目的地がこれ以上動いたら計算し直す(m)
    private const float _cornerReachDistance = 0.6f;  // 曲がり角に着いたとみなす距離(m)
    private const float _sampleDistance = 4f;       // NavMesh上の点を探す範囲(m)

    private readonly NavMeshPath _path = new NavMeshPath();   // ※ このクラスはMonoBehaviourのフィールド初期化で作らないこと(NavMeshPathはAwake以降でしか作れない)
    private Vector3 _destination;
    private int _cornerIndex;
    private bool _hasPath;
    private float _nextRepathTime;

    public Vector3 destination => _destination;

    // 目的地を決める(必要な時だけ経路を計算し直す)
    public void SetDestination(Vector3 from, Vector3 to)
    {
        bool moved = (to - _destination).sqrMagnitude > _repathDistance * _repathDistance;
        if (!moved && Time.time < _nextRepathTime) return;

        _destination = to;
        _nextRepathTime = Time.time + _repathInterval;
        CalculatePath(from, to);
    }

    // 次の経路計算を早める(引っかかった時など)
    public void ForceRepath()
    {
        _nextRepathTime = 0f;
    }

    // 今いる位置から次に進む方向(水平、長さ1)。着いていればVector3.zero
    public Vector3 GetDirection(Vector3 position, float arriveDistance)
    {
        if (HorizontalDistance(position, _destination) <= arriveDistance) return Vector3.zero;

        Vector3 next = _destination;
        if (_hasPath)
        {
            // 着いた曲がり角は飛ばす
            while (_cornerIndex < _path.corners.Length - 1
                && HorizontalDistance(position, _path.corners[_cornerIndex]) <= _cornerReachDistance)
            {
                _cornerIndex++;
            }

            next = _path.corners[_cornerIndex];
        }

        Vector3 direction = next - position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    // ランダムな行き先(centerからradius以内のNavMesh上の点)。見つからなければcenter
    public static Vector3 RandomPoint(Vector3 center, float radius)
    {
        Vector2 offset = Random.insideUnitCircle * radius;
        Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
        return NavMesh.SamplePosition(candidate, out NavMeshHit hit, _sampleDistance, NavMesh.AllAreas) ? hit.position : center;
    }

    private void CalculatePath(Vector3 from, Vector3 to)
    {
        _hasPath = false;
        _cornerIndex = 0;

        if (!NavMesh.SamplePosition(from, out NavMeshHit start, _sampleDistance, NavMesh.AllAreas)) return;
        if (!NavMesh.SamplePosition(to, out NavMeshHit end, _sampleDistance, NavMesh.AllAreas)) return;
        if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path)) return;
        if (_path.status == NavMeshPathStatus.PathInvalid || _path.corners.Length == 0) return;

        _hasPath = true;
        _cornerIndex = _path.corners.Length > 1 ? 1 : 0;   // 0番目は出発点
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
