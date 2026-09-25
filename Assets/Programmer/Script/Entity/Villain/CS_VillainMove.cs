using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/*
 * 悪人の移動を管理するクラス
 * どこへ向かうかの判断はCS_VillainCombatが行い、このクラスは指示された目的地へ移動するだけ
 * 経路探索・移動はNavMeshAgentに任せる
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・移動はサーバー(オフライン時はその場)だけで行う
 *   クライアントではNavMeshAgentを無効にし、位置はNetworkTransformの同期に任せる
 * ・移動中の向きはNavMeshAgentが変える(回転の速さはrotationSpeed)
 *   止まっている時に向きを変えたい場合はFaceTowardsを使う
 * ・追いかける相手は毎回位置が変わるので、目的地が repathDistance 以上ずれた時だけ経路を再計算する
 * ・NavMeshの外にいる時は移動しない(スポーン位置はNavMeshの上に置くこと)
 */
// ========================================

[RequireComponent(typeof(NavMeshAgent))]
public class CS_VillainMove : MonoBehaviour
{
    [SerializeField, Min(0f)]
    [Tooltip("向きを変える速さ(度/秒)")]
    private float _rotationSpeed = 720f;

    [SerializeField, Min(0f)]
    [Tooltip("目的地がこの距離(m)以上ずれた時だけ経路を再計算する")]
    private float _repathDistance = 0.5f;

    private static bool _hasWarnedNotOnNavMesh;   // NavMeshが無い警告を出したか(全悪人で共有)

    private NavMeshAgent _agent;
    private bool _hasDestination;
    private Vector3 _requestedDestination;
    private int _destinationSetFrame = -1;   // 目的地を指示したフレーム(そのフレームはまだ経路が古いので到達判定をしない)

    // 移動の指示を受け付けられるか
    private bool canMove => _agent != null && _agent.enabled && _agent.isOnNavMesh;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.angularSpeed = _rotationSpeed;

        // 移動はサーバーだけで行う。クライアントでは位置をNetworkTransformの同期に任せる
        if (IsNetworkClientOnly())
        {
            _agent.enabled = false;
        }
    }

    // 目的地へ、指定した速さで移動する
    public void MoveTo(Vector3 destination, float speed)
    {
        if (!canMove)
        {
            WarnNotOnNavMesh();
            return;
        }

        _agent.speed = speed;
        _agent.isStopped = false;

        // 目的地がほとんど変わっていなければ、経路を再計算しない
        if (_hasDestination && (_requestedDestination - destination).sqrMagnitude < _repathDistance * _repathDistance) return;

        _agent.SetDestination(destination);
        _requestedDestination = destination;
        _destinationSetFrame = Time.frameCount;
        _hasDestination = true;
    }

    // その場で止まる(止まっている時は何もしない。毎フレーム呼ばれても経路を消し直さないため)
    public void Stop()
    {
        if (!_hasDestination) return;

        _hasDestination = false;
        if (!canMove) return;

        _agent.isStopped = true;
        _agent.ResetPath();
    }

    // 止まったまま、指定した方向へ向きを変える
    public void FaceTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0f) return;

        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _rotationSpeed * Time.deltaTime);
    }

    // 最後に指示された目的地まで、経路に沿って指定した距離以内に近づいたか
    public bool IsNearDestination(float distance)
    {
        if (!_hasDestination || !canMove) return false;

        // 目的地を指示した直後・経路の計算中は、まだ近づいていない
        if (_destinationSetFrame == Time.frameCount || _agent.pathPending) return false;

        return _agent.remainingDistance <= distance;
    }

    // NavMeshの上にいないと移動できないことを知らせる(悪人は大量に生成されるので1回だけ)
    private void WarnNotOnNavMesh()
    {
        if (!_agent.enabled || _hasWarnedNotOnNavMesh) return;

        Debug.LogWarning("CS_VillainMove: NavMeshの上にいないため移動できません。NavMeshをベイクしてください", this);
        _hasWarnedNotOnNavMesh = true;
    }

    private static bool IsNetworkClientOnly()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager != null && manager.IsListening && !manager.IsServer;
    }
}
