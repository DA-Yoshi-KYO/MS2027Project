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
/// 警察1人の行動を判断するクラス
/// 一定間隔で状況を判断し、CS_PoliceMove(移動)とCS_PoliceAttack(攻撃)に指示を出す
/// 判断の優先順位: 標的が見えている(追跡・攻撃) > 駆け付け > 探索 > 巡回・待機(所属先が決める)
/// </summary>
[RequireComponent(typeof(CS_PoliceMove))]
[RequireComponent(typeof(CS_PoliceVision))]
[RequireComponent(typeof(CS_PoliceAttack))]
public class CS_PoliceBrain : MonoBehaviour
{
    private CS_PoliceMove _move = null;
    private CS_PoliceVision _vision = null;
    private CS_PoliceAttack _attack = null;

    // 所属先(巡回するグループ、または手配度で出現した増援のまとまり)
    private IPoliceGroup _group = null;

    // 現在の行動状態
    private CSE_PoliceMoveState _state = CSE_PoliceMoveState.Patrol;

    // 追跡中の標的(追跡していなければnull)
    private Transform _currentTarget = null;

    // 標的を最後に見た位置(見失った時に探しに行く場所)
    private Vector3 _lastSeenPosition = Vector3.zero;

    // 駆け付けの指示を受けているか・駆け付ける現場
    private bool _hasRushRequest = false;
    private Vector3 _rushPosition = Vector3.zero;

    // 駆け付け中に位置を追い続けるプレイヤー(出現直後の増援が持ち主へ向かうのに使う。いなければnull)
    private CS_PlayerHealth _rushTarget = null;

    // 探索を続ける残り時間
    private float _searchTimer = 0.0f;

    // 次に判断するまでの残り時間
    private float _thinkTimer = 0.0f;

    // 異常事態(詰まり・巡回ルートに戻れない)をグループに報告済みか
    private bool _hasReportedAbnormal = false;

    // 初期化処理を行わずに行動するのを防ぐためのフラグ
    private bool _isInitialized = false;

    [Header("＝＝＝ 判断 ＝＝＝")]
    [SerializeField, Min(0.01f)]
    [Tooltip("状況を判断する間隔(秒)")]
    private float _thinkInterval = 0.2f;

    [SerializeField, Min(0f)]
    [Tooltip("見失った場所・駆け付けた現場を探す時間(秒)")]
    private float _searchDuration = 3.0f;

    [SerializeField, Min(0f)]
    // 大勢が同じ場所を目指したり、その場所にプレイヤーが立っていたりすると、ぴったりの位置には着けないため
    [Tooltip("駆け付けた現場・探す場所にこの距離まで近づいたら、着いたとみなす")]
    private float _gatherDistance = 2.0f;

    // 現在の行動状態
    public CSE_PoliceMoveState state => _state;

    // 追跡・駆け付け・探索・攻撃のどれもしていない(巡回・待機している)か
    public bool isIdle => (_state == CSE_PoliceMoveState.Patrol || _state == CSE_PoliceMoveState.Wait)
        && !_hasRushRequest && !_attack.isCharging;

    private void Awake()
    {
        _move = GetComponent<CS_PoliceMove>();
        _vision = GetComponent<CS_PoliceVision>();
        _attack = GetComponent<CS_PoliceAttack>();

        // 判断と移動はサーバーだけで行う。クライアントでは位置をNetworkTransformの同期に任せる
        if (CS_PoliceSquad.IsNetworkClientOnly())
        {
            GetComponent<NavMeshAgent>().enabled = false;
            enabled = false;
        }
    }

    /// <summary>
    /// 警察の行動に関する初期化メソッド
    /// </summary>
    /// <param name="group">所属先</param>
    /// <param name="speedTable">警察の移動状態に応じた速度を格納した辞書</param>
    /// <param name="status">警察のステータス</param>
    public void Setting(IPoliceGroup group, Dictionary<CSE_PoliceMoveState, float> speedTable, CSO_PoliceStatus status)
    {
        _group = group;
        _move.Setting(speedTable);
        _vision.Setting(status.viewAngle, status.viewDistance);
        _attack.Setting(status.attackPower);

        _state = CSE_PoliceMoveState.Patrol;
        _isInitialized = true;
    }

    /// <summary>
    /// 事件の現場へ駆け付けるよう指示するメソッド
    /// </summary>
    /// <param name="position">事件の現場</param>
    public void RequestRush(Vector3 position)
    {
        _rushPosition = position;
        _hasRushRequest = true;
        _rushTarget = null;
    }

    /// <summary>
    /// プレイヤーのいる場所へ、プレイヤーが動いても位置を追いながら駆け付けるよう指示するメソッド
    /// 見つけたら追跡に切り替わる。変身を解いた・倒れた場合は、その時の位置を探索する
    /// </summary>
    /// <param name="player">向かう先のプレイヤー</param>
    public void RequestPursuit(CS_PlayerHealth player)
    {
        RequestRush(player.transform.position);
        _rushTarget = player;
    }

    /// <summary>
    /// 移動速度と視野距離を変更するメソッド(手配度の変化など)
    /// </summary>
    /// <param name="speedTable">警察の移動状態に応じた速度を格納した辞書</param>
    /// <param name="viewDistance">視野距離</param>
    public void ChangeAbility(Dictionary<CSE_PoliceMoveState, float> speedTable, float viewDistance)
    {
        _move.ChangeSpeedTable(speedTable);
        _vision.ChangeViewDistance(viewDistance);
    }

    /// <summary>
    /// 判断・移動・攻撃をすべて止めるメソッド(消える演出の間に動かないようにする)
    /// </summary>
    public void StopActing()
    {
        _move.SetStopped(true);
        _attack.enabled = false;
        enabled = false;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        // 攻撃のチャージ中はその場で止まり、チャージを始めた時に標的がいた位置の方を向く
        // (判断は続け、チャージが終わったらその時の目的地へ移動を再開する)
        _move.SetStopped(_attack.isCharging);
        if (_attack.isCharging) _move.TurnTowards(_attack.chargeTargetPosition);

        // 毎フレームではなく一定間隔で判断する(視界判定や経路計算の負荷を抑えるため)
        _thinkTimer -= Time.deltaTime;
        if (_thinkTimer > 0.0f) return;
        _thinkTimer = _thinkInterval;

        Think();
        CheckAbnormal();
    }

    /// <summary>
    /// 状況を判断し、優先順位の高い行動を1つ選んで実行するメソッド
    /// </summary>
    private void Think()
    {
        // 1. 標的が見えていれば、他の指示に関係なく追跡・攻撃する
        Transform target = FindTarget();
        if (target != null)
        {
            Chase(target);
            return;
        }

        // 追っていた標的を見失った場合は、最後に見た場所を探しに行く
        if (_currentTarget != null)
        {
            _currentTarget = null;
            StartSearch(_lastSeenPosition);
        }

        // 2. 駆け付けの指示があれば現場へ向かう
        if (_hasRushRequest)
        {
            Rush();
            return;
        }

        // 3. 探索中なら探索を続ける
        if (_state == CSE_PoliceMoveState.Search)
        {
            UpdateSearch();
            return;
        }

        // 4. どれでもなければ、所属先に合わせて巡回・待機する
        MoveIdle();
    }

    /// <summary>
    /// 自分の視界と仲間の情報から、追うべき標的を探すメソッド
    /// </summary>
    /// <returns>追うべき標的(いなければnull)</returns>
    private Transform FindTarget()
    {
        Transform ownTarget = _vision.FindTarget(_currentTarget, out int ownPriority);
        if (ownTarget != null) _group.ReportTarget(ownTarget, ownPriority);

        // 自分には見えていなくても、仲間が見つけた標的の方が優先度が高ければそちらを追う
        Transform sharedTarget = _group.GetSharedTarget(out int sharedPriority);
        if (sharedTarget != null && sharedPriority > ownPriority) return sharedTarget;

        return ownTarget != null ? ownTarget : sharedTarget;
    }

    /// <summary>
    /// 標的を追跡し、攻撃範囲内なら攻撃のチャージを始めるメソッド
    /// </summary>
    /// <param name="target">追跡する標的</param>
    private void Chase(Transform target)
    {
        _state = CSE_PoliceMoveState.Chase;
        _currentTarget = target;
        _lastSeenPosition = target.position;

        // 標的を見つけた時点で、駆け付けの指示は済んだものとする
        _hasRushRequest = false;
        _rushTarget = null;

        _move.SetDestination(target.position, CSE_PoliceMoveState.Chase);
        _attack.TryStartCharge(target);
    }

    /// <summary>
    /// 事件の現場へ向かい、着いたら現場を探索するメソッド
    /// </summary>
    private void Rush()
    {
        UpdateRushTarget();

        _state = CSE_PoliceMoveState.Rush;
        _move.SetDestination(_rushPosition, CSE_PoliceMoveState.Rush);

        if (!_move.IsNearDestination(_gatherDistance)) return;

        _hasRushRequest = false;
        StartSearch(_rushPosition);
    }

    /// <summary>
    /// 位置を追っているプレイヤーがいれば、駆け付ける場所をそのプレイヤーの今の位置に更新するメソッド
    /// </summary>
    private void UpdateRushTarget()
    {
        if (_rushTarget == null) return;

        // 変身を解いた・倒れたプレイヤーは追わない(駆け付ける場所は、最後に分かっていた位置のままにする)
        if (!CS_PoliceVision.IsPlayerTargetable(_rushTarget))
        {
            _rushTarget = null;
            return;
        }

        _rushPosition = _rushTarget.transform.position;
    }

    /// <summary>
    /// 指定した場所の探索を始めるメソッド
    /// </summary>
    /// <param name="position">探索する場所</param>
    private void StartSearch(Vector3 position)
    {
        _state = CSE_PoliceMoveState.Search;
        _searchTimer = _searchDuration;
        _move.SetDestination(position, CSE_PoliceMoveState.Search);
    }

    /// <summary>
    /// 探索場所に着いてから一定時間経ったら、巡回・待機に戻るメソッド
    /// </summary>
    private void UpdateSearch()
    {
        if (!_move.IsNearDestination(_gatherDistance)) return;

        _searchTimer -= _thinkInterval;
        if (_searchTimer > 0.0f) return;

        MoveIdle();
    }

    /// <summary>
    /// 所属先に合わせて巡回・待機するメソッド(巡回するグループなら巡回ルート、増援ならその場で待機)
    /// </summary>
    private void MoveIdle()
    {
        _state = _group.MoveIdle(this, _move);
    }

    /// <summary>
    /// 異常事態(詰まり・巡回ルートに戻れない)を検知したら、所属先に再スポーンを依頼するメソッド
    /// </summary>
    private void CheckAbnormal()
    {
        if (_hasReportedAbnormal) return;

        // 巡回ルートにたどり着けるかは、ルートを直接目指す先頭の警察だけが判定する
        // (後ろの警察の目的地は隊列の位置なので、たどり着けなくても異常ではない)
        bool cannotReturnToRoute = _state == CSE_PoliceMoveState.Patrol && _group.IsLeader(this) && _move.isPathUnreachable;
        if (!_move.isStuck && !cannotReturnToRoute) return;

        _hasReportedAbnormal = true;
        _group.RequestRespawn(this);
    }
}
