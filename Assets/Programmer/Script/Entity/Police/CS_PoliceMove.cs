/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 警察の移動を管理するクラス
/// どこへ向かうか・いつ止めるかの判断はCS_PoliceBrainが行い、このクラスは指示どおりに移動・停止・向きの変更を行う
/// 経路探索・移動・移動中の回転はNavMeshAgentに任せ、止めている間の向きの変更だけTurnTowardsで行う
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class CS_PoliceMove : MonoBehaviour
{
    // 経路探索と移動を行うNavMeshAgent
    private NavMeshAgent _agent = null;

    // 警察の移動状態
    private CSE_PoliceMoveState _currentMoveState = CSE_PoliceMoveState.Patrol;

    // 警察の移動状態に応じた速度を格納する辞書
    private Dictionary<CSE_PoliceMoveState, float> _moveStateSpeeds = new Dictionary<CSE_PoliceMoveState, float>();

    // 初期化処理を行わずに警察が移動するのを防ぐためのフラグ
    private bool _isInitialized = false;

    // 目的地が一度でも指示されたか
    private bool _hasDestination = false;

    // 最後に指示された目的地(同じ目的地で経路を再計算しないために使う)
    private Vector3 _requestedDestination = Vector3.zero;

    // 目的地を指示したフレーム(そのフレームはまだ経路が古いので到達判定をしない)
    private int _destinationSetFrame = -1;

    // 移動できない状態が続いている時間
    private float _stuckTimer = 0.0f;

    // 意図的にその場で止めているか(攻撃のチャージ中など)。止めている間は詰まりと判定しない
    private bool _isStopped = false;

    [Header("＝＝＝ 到達判定 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("目的地に到達したとみなす距離")]
    private float _arriveDistance = 0.3f;

    [Header("＝＝＝ 詰まり判定 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("この速さ未満なら止まっているとみなす")]
    private float _stuckSpeed = 0.1f;

    [SerializeField, Min(0.1f)]
    [Tooltip("目的地に着いていないのに止まったまま、この秒数が経つと詰まりと判定する")]
    private float _stuckTime = 3.0f;

    // 最後に指示された目的地に到達したか
    public bool hasArrived => IsNearDestination(Mathf.Max(_arriveDistance, _agent.stoppingDistance));

    // 詰まって移動できない状態が一定時間続いているか
    public bool isStuck => _stuckTimer >= _stuckTime;

    // 目的地までの経路が途中までしか作れない(たどり着けない)か
    // 目的地を指示した直後・経路の計算中は、前の経路の結果が残っている可能性があるので判定しない
    public bool isPathUnreachable => _hasDestination && _agent.isOnNavMesh
        && _destinationSetFrame != Time.frameCount && !_agent.pathPending
        && _agent.pathStatus != NavMeshPathStatus.PathComplete;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    /// <summary>
    /// 警察の移動に関する初期化メソッド
    /// </summary>
    /// <param name="speedTable">警察の移動状態に応じた速度を格納した辞書</param>
    public void Setting(Dictionary<CSE_PoliceMoveState, float> speedTable)
    {
        // 警察の移動状態に応じた速度を設定
        _moveStateSpeeds.Clear();
        foreach (var kvp in speedTable)
        {
            _moveStateSpeeds[kvp.Key] = kvp.Value;
        }

        // 初期状態を巡回に設定
        _currentMoveState = CSE_PoliceMoveState.Patrol;
        ApplyCurrentSpeed();

        // 初期化完了フラグを立てる
        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        // 移動できない状態が続いた時間を数える(途切れたらリセット)
        _stuckTimer = IsBlocked() ? _stuckTimer + Time.deltaTime : 0.0f;
    }

    /// <summary>
    /// 指定した目的地へ、指定した移動状態の速度で移動するメソッド
    /// </summary>
    /// <param name="position">目的地</param>
    /// <param name="moveState">移動状態(速度の切り替えに使う)</param>
    public void SetDestination(Vector3 position, CSE_PoliceMoveState moveState)
    {
        if (!_isInitialized) return;

        ChangeMoveState(moveState);

        // NavMeshの外にいる時にSetDestinationを呼ぶとエラーになる(この状態は詰まりとして扱う)
        if (!_agent.isOnNavMesh) return;

        // 同じ目的地を何度も指示された場合は、経路の再計算をしない
        if (_hasDestination && (_requestedDestination - position).sqrMagnitude < 0.01f) return;

        _agent.SetDestination(position);
        _requestedDestination = position;
        _destinationSetFrame = Time.frameCount;
        _hasDestination = true;
    }

    /// <summary>
    /// その場で止める・止めるのをやめるメソッド
    /// 止めている間も目的地の指示は受け付け、止めるのをやめるとその目的地へ移動を再開する
    /// </summary>
    /// <param name="isStopped">止める場合はtrue</param>
    public void SetStopped(bool isStopped)
    {
        // NavMeshの外にいる時にisStoppedを変更するとエラーになる
        if (_isStopped == isStopped || !_agent.isOnNavMesh) return;

        _isStopped = isStopped;
        _agent.isStopped = isStopped;

        // 止めている間はNavMeshAgentの自動回転を切り、TurnTowardsで向きを変えられるようにする
        _agent.updateRotation = !isStopped;

        // 止める時は、残っている速度も消してその場ですぐ止まるようにする
        if (isStopped) _agent.velocity = Vector3.zero;
    }

    /// <summary>
    /// 指定した位置の方へ、水平方向に少しずつ向きを変えるメソッド(毎フレーム呼ぶ)
    /// 回転の速さはNavMeshAgentのAngular Speedを使う
    /// </summary>
    /// <param name="position">向く位置</param>
    public void TurnTowards(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0.0f;

        // 真上・真下など、水平方向の向きが無い場合は回転しない
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, _agent.angularSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 最後に指示された目的地まで、指定した距離以内に近づいたかを判定するメソッド
    /// </summary>
    /// <param name="distance">近づいたとみなす距離</param>
    /// <returns>近づいていればtrue</returns>
    public bool IsNearDestination(float distance)
    {
        if (!_hasDestination || !_agent.isOnNavMesh) return false;

        // 目的地を指示した直後・経路の計算中は、まだ近づいていない
        if (_destinationSetFrame == Time.frameCount || _agent.pathPending) return false;

        // 直線距離ではなく、経路に沿った残りの距離で判定する(高低差・障害物を考慮するため)
        return _agent.remainingDistance <= distance;
    }

    /// <summary>
    /// 移動したいのに移動できていない状態かを判定するメソッド
    /// </summary>
    /// <returns>移動できていなければtrue</returns>
    private bool IsBlocked()
    {
        // NavMeshの外に出てしまった場合は移動できない
        if (!_agent.isOnNavMesh) return true;

        // 意図的に止めている・目的地がない・経路の計算中・到達済みの場合は、止まっていて当然なので詰まりではない
        if (_isStopped || !_hasDestination || _agent.pathPending || hasArrived) return false;

        return _agent.velocity.sqrMagnitude < _stuckSpeed * _stuckSpeed;
    }

    /// <summary>
    /// 警察の移動状態を変更するメソッド
    /// </summary>
    /// <param name="newState">新しい移動状態</param>
    private void ChangeMoveState(CSE_PoliceMoveState newState)
    {
        // 現在の移動状態と新しい移動状態が同じ場合は何もしない
        if (_currentMoveState == newState) return;

        _currentMoveState = newState;
        ApplyCurrentSpeed();
    }

    /// <summary>
    /// 現在の移動状態に応じた速度をNavMeshAgentに適用するメソッド
    /// </summary>
    private void ApplyCurrentSpeed()
    {
        if (!_moveStateSpeeds.TryGetValue(_currentMoveState, out float speed))
        {
            Debug.LogError($"{_currentMoveState} の速度が設定されていません");
            return;
        }

        _agent.speed = speed;
    }
}
