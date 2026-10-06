using UnityEngine;
using UnityEngine.AI;

/*
 * NPCの移動先までの経路を、NavMeshで求めてたどるクラス
 * CS_NpcBrainが持ち、各行動(CS_NpcAction)が目的地を指定する
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・プレイヤーはRigidbodyで動くので、NavMeshAgentは使わず、経路(曲がり角の一覧)だけをNavMesh.CalculatePathで求める
 *   NPCは「次の曲がり角の方向」へ視点を向けて前進入力を出すことで、人と同じ移動処理(CS_Player)で動く
 * ・目的地が大きく動いた時と、一定時間ごとに経路を求め直す(悪人など、動く相手を追うため)
 * ・NavMeshの上にいない時(空中など)は、目的地へ直接向かう
 */
// ========================================

public class CS_NpcNavigator
{
    private const float _repathInterval = 0.5f;     // 経路を求め直す間隔(秒)
    private const float _repathDistance = 1f;       // 目的地がこれ以上動いたら求め直す(m)
    private const float _cornerReachDistance = 0.6f;    // 曲がり角にこの距離まで近づいたら次へ(m)
    private const float _sampleDistance = 2f;       // NavMesh上の点を探す範囲(m)

    private readonly NavMeshPath _path = new NavMeshPath();
    private Vector3 _destination;
    private bool _hasDestination;
    private bool _hasPath;
    private int _cornerIndex;
    private float _repathTimer;

    public bool hasDestination => _hasDestination;
    public Vector3 destination => _destination;

    // 目的地を設定する(毎フレーム呼んでもよい。大きく動いた時だけ経路を求め直す)
    public void SetDestination(Vector3 destination)
    {
        if (_hasDestination && (destination - _destination).sqrMagnitude < _repathDistance * _repathDistance) return;

        _destination = destination;
        _hasDestination = true;
        _repathTimer = 0f;
    }

    public void Stop()
    {
        _hasDestination = false;
        _hasPath = false;
    }

    // 目的地にこの距離まで近づいたか
    public bool HasArrived(Vector3 position, float distance)
    {
        if (!_hasDestination) return true;

        Vector3 offset = _destination - position;
        offset.y = 0f;
        return offset.sqrMagnitude <= distance * distance;
    }

    // 今進むべき方向(水平)を返す。目的地が無ければVector3.zero
    public Vector3 GetSteerDirection(Vector3 position, float deltaTime)
    {
        if (!_hasDestination) return Vector3.zero;

        _repathTimer -= deltaTime;
        if (_repathTimer <= 0f)
        {
            _repathTimer = _repathInterval;
            CalculatePath(position);
        }

        Vector3 target = GetNextCorner(position);
        Vector3 direction = target - position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private void CalculatePath(Vector3 position)
    {
        _hasPath = false;
        _cornerIndex = 0;

        if (!NavMesh.SamplePosition(position, out NavMeshHit from, _sampleDistance, NavMesh.AllAreas)) return;
        if (!NavMesh.SamplePosition(_destination, out NavMeshHit to, _sampleDistance, NavMesh.AllAreas)) return;
        if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _path)) return;
        if (_path.status == NavMeshPathStatus.PathInvalid || _path.corners.Length == 0) return;

        _hasPath = true;
    }

    // 経路の次の曲がり角(経路が無ければ目的地そのもの)
    private Vector3 GetNextCorner(Vector3 position)
    {
        if (!_hasPath) return _destination;

        Vector3[] corners = _path.corners;
        while (_cornerIndex < corners.Length - 1)
        {
            Vector3 offset = corners[_cornerIndex] - position;
            offset.y = 0f;
            if (offset.sqrMagnitude > _cornerReachDistance * _cornerReachDistance) break;

            _cornerIndex++;
        }

        return corners[_cornerIndex];
    }
}
