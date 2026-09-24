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
/// 経路探索・移動・回転はNavMeshAgentに任せ、このクラスは目的地と速度の管理を行う
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

    // 警察が追跡する対象のTransform
    private Transform _targetTransform = null;

    // 目的地の更新が必要かどうか(追跡対象が変わった時に立てる)
    private bool _isDestinationDirty = false;

    // 追跡中に次に目的地を更新するまでの残り時間
    private float _repathTimer = 0.0f;

    [Header("巡回ポイントに到達したとみなす距離"), Min(0f)]
    [SerializeField]
    private float _patrolPointCompleteDistance = 0.1f;

    [Header("追跡中に目的地を更新する間隔(秒)"), Min(0.01f)]
    [SerializeField]
    private float _repathInterval = 0.2f;

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
        // 初期化が完了していない、または追跡対象が設定されていない場合は何もしない
        if (!_isInitialized || _targetTransform == null) return;

        // 追跡中は対象が動き続けるため、一定間隔で目的地を更新する
        // (SetDestinationは呼ぶたびに経路を再計算するので毎フレームは呼ばない)
        if (_currentMoveState == CSE_PoliceMoveState.Chase)
        {
            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0.0f) _isDestinationDirty = true;
        }

        if (!_isDestinationDirty) return;

        // 目的地を設定すると、NavMeshAgentが経路探索・移動・回転を行う
        _agent.SetDestination(_targetTransform.position);
        _isDestinationDirty = false;
        _repathTimer = _repathInterval;
    }

    /// <summary>
    /// 警察の追跡対象を設定するメソッド
    /// </summary>
    /// <param name="target">追跡対象のTransform</param>
    public void SetTarget(Transform target)
    {
        // targetがPlayerの場合、追跡状態に変更
        if (target.GetComponent<CS_Player>() != null)
        {
            _targetTransform = target;
            ChangeMoveState(CSE_PoliceMoveState.Chase);
        }
        // targetが巡回ポイントの場合
        else
        {
            // 巡回中に、今の巡回ポイントへ到達していない場合は追跡対象を変更しない
            if (_currentMoveState == CSE_PoliceMoveState.Patrol && _targetTransform != null && !HasArrived()) return;

            _targetTransform = target;
            ChangeMoveState(CSE_PoliceMoveState.Patrol);
        }

        // 次のUpdateで新しい目的地を設定する
        _isDestinationDirty = true;
    }

    /// <summary>
    /// 現在の目的地に到達したかを判定するメソッド
    /// </summary>
    /// <returns>到達していればtrue</returns>
    private bool HasArrived()
    {
        // 目的地の設定待ち・経路の計算中はまだ到達していない
        if (_isDestinationDirty || _agent.pathPending) return false;

        // 直線距離ではなく、経路に沿った残りの距離で判定する(高低差・障害物を考慮するため)
        return _agent.remainingDistance <= Mathf.Max(_patrolPointCompleteDistance, _agent.stoppingDistance);
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
