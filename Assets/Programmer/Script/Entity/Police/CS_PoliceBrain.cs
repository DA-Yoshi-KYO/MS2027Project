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

    // 駆け付けの指示を受けているか
    private bool _hasRushRequest = false;

    // 駆け付ける現場の目印(同じ事件へ向かう警察は同じ目印を持ち、現場の周りの別々の位置へ割り当てられる)と、現場の位置
    private object _rushKey = null;
    private Vector3 _rushCenter = Vector3.zero;

    // 駆け付け中に位置を追い続けるプレイヤー(出現直後の増援が持ち主へ向かうのに使う。いなければnull)
    private CS_PlayerHealth _rushPlayer = null;

    // 探索する場所の目印と位置(探索中の警察が、同じ場所の周りの別々の位置へ割り当てられる)
    private object _searchKey = null;
    private Vector3 _searchCenter = Vector3.zero;

    // 待機中に歩き回っているか・歩き回る中心・今の行き先(無ければ立ち止まっている)
    private bool _isWandering = false;
    private Vector3 _wanderCenter = Vector3.zero;
    private Vector3 _wanderTarget = Vector3.zero;
    private bool _hasWanderTarget = false;

    // 次の行き先を選ぶまでの立ち止まる残り時間・今の行き先へ歩き始めてからの時間
    private float _wanderPauseTimer = 0.0f;
    private float _wanderWalkTimer = 0.0f;

    // 歩き回る行き先が、今の位置から歩いて行けるかを調べる際に使い回す経路
    private NavMeshPath _wanderPath = null;

    // 歩き回る行き先を探す回数
    private const int _wanderPickAttempts = 5;

    // 歩き回る時の、今の位置から行き先までの最小の距離(少しは歩くようにする)
    private const float _wanderMinStep = 1.5f;

    // 今加わっている「中心の周りの別々の位置を割り当てる円」と、その目印・種類(加わっていなければnull)
    private CS_PoliceSlotRing _ring = null;
    private object _ringKey = null;
    private int _ringKind = 0;

    // 円の種類(追跡: 標的の周り / 集合: 駆け付け・探索の現場の周り。同じ目印で別の円を分ける)
    private const int _chaseRingKind = 0;
    private const int _gatherRingKind = 1;

    // 割り当てられた位置の近くの歩ける場所(NavMesh)を探す距離
    private const float _slotSampleDistance = 2.5f;

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

    [Header("＝＝＝ 周りを囲む位置 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("割り当てられた位置にこの距離まで近づいたら、着いたとみなす(位置は警察ごとに別々なので、小さくてよい)")]
    private float _arriveDistance = 1.0f;

    [SerializeField, Min(0.5f)]
    [Tooltip("駆け付けた現場・探す場所の周りを囲む半径(m)。警察の視野距離より遠くにならないよう、視野距離×下の割合を上限にする")]
    private float _gatherRadius = 3.5f;

    [SerializeField, Range(0.1f, 1.0f)]
    [Tooltip("現場を囲む半径の上限を決める、視野距離に対する割合(現場の中心にいるプレイヤーが、視界に入るようにするため)")]
    private float _gatherViewRatio = 0.7f;

    [SerializeField, Range(0.1f, 1.0f)]
    [Tooltip("標的を追う時に囲む半径を決める、攻撃の射程に対する割合(射程の内側に収まるようにする)")]
    private float _chaseRadiusRatio = 0.9f;

    [Header("＝＝＝ 待機中の歩き回り ＝＝＝")]
    [SerializeField, Min(0.5f)]
    [Tooltip("待機中(標的を見失って探し終えたあと)に、探していた場所を中心として歩き回る範囲の半径(m)")]
    private float _wanderRadius = 4.0f;

    [SerializeField, Min(0f)]
    [Tooltip("行き先に着いてから、次の行き先を選ぶまでに立ち止まる時間の最小(秒)")]
    private float _wanderPauseMin = 1.0f;

    [SerializeField, Min(0f)]
    [Tooltip("行き先に着いてから、次の行き先を選ぶまでに立ち止まる時間の最大(秒)")]
    private float _wanderPauseMax = 3.0f;

    [SerializeField, Min(1f)]
    [Tooltip("1つの行き先へ歩き始めてから、この時間が経っても着かない(詰まった・遠回りしている)時は、行き先を選び直す(秒)")]
    private float _wanderMaxWalkTime = 8.0f;

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
        _wanderPath = new NavMeshPath();

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

        // 全員が同じフレームで判断・経路計算をしないよう、最初の判断のタイミングをずらす
        _thinkTimer = Random.Range(0.0f, _thinkInterval);
    }

    /// <summary>
    /// 事件の現場へ駆け付けるよう指示するメソッド
    /// </summary>
    /// <param name="point">事件の現場(同じ事件へ向かう警察には、同じインスタンスを渡す)</param>
    public void RequestRush(CS_PoliceGatherPoint point)
    {
        _rushKey = point;
        _rushCenter = point.position;
        _rushPlayer = null;
        _hasRushRequest = true;
    }

    /// <summary>
    /// プレイヤーのいる場所へ、プレイヤーが動いても位置を追いながら駆け付けるよう指示するメソッド
    /// 見つけたら追跡に切り替わる。変身を解いた・倒れた場合は、その時の位置を探索する
    /// </summary>
    /// <param name="player">向かう先のプレイヤー</param>
    public void RequestPursuit(CS_PlayerHealth player)
    {
        // 同じプレイヤーへ向かう警察は、プレイヤーを目印にして、プレイヤーの周りの別々の位置へ割り当てられる
        _rushKey = player;
        _rushCenter = player.transform.position;
        _rushPlayer = player;
        _hasRushRequest = true;
    }

    /// <summary>
    /// 移動速度・視野距離・攻撃の溜め時間を変更するメソッド(手配度の変化など)
    /// </summary>
    /// <param name="speedTable">警察の移動状態に応じた速度を格納した辞書</param>
    /// <param name="viewDistance">視野距離</param>
    /// <param name="chargeTime">攻撃の溜め時間(秒)。0以下なら、警察のステータスの溜め時間のまま</param>
    public void ChangeAbility(Dictionary<CSE_PoliceMoveState, float> speedTable, float viewDistance, float chargeTime)
    {
        _move.ChangeSpeedTable(speedTable);
        _vision.ChangeViewDistance(viewDistance);
        _attack.ChangeChargeTime(chargeTime);
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
            Transform lostTarget = _currentTarget;
            _currentTarget = null;
            StartSearch(lostTarget, _lastSeenPosition);
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
        // 新しく標的にした(見つけた)瞬間に、プレイヤーのスコア(暗躍ボーナス)へ知らせる
        if (target != _currentTarget) ReportFound(target);

        _state = CSE_PoliceMoveState.Chase;
        _currentTarget = target;
        _lastSeenPosition = target.position;

        // 標的を見つけた時点で、駆け付けの指示は済んだものとする
        _hasRushRequest = false;
        _rushPlayer = null;
        _isWandering = false;

        // 全員が標的の一点に押し合わないよう、標的を囲む位置(攻撃の射程の内側)を目指す
        float chaseRadius = _attack.attackRange * _chaseRadiusRatio;
        _move.SetDestination(GetSlotDestination(target, _chaseRingKind, target.position, chaseRadius), CSE_PoliceMoveState.Chase);
        _attack.TryStartCharge(target);
    }

    /// <summary>
    /// 標的がプレイヤーなら、警察に見つかったことをプレイヤーのスコアへ知らせるメソッド(暗躍ボーナスがリセットされる)
    /// 判断はサーバー(またはオフライン)だけで行うので、ここもサーバーで呼ばれる
    /// </summary>
    /// <param name="target">新しく標的にしたもの</param>
    private void ReportFound(Transform target)
    {
        CS_PlayerScoring scoring = target.GetComponentInParent<CS_PlayerScoring>();
        if (scoring != null) scoring.ReportFoundByPolice();
    }

    /// <summary>
    /// 事件の現場へ向かい、着いたら現場を探索するメソッド
    /// </summary>
    private void Rush()
    {
        UpdateRushTarget();

        _state = CSE_PoliceMoveState.Rush;
        _isWandering = false;

        // 現場の一点ではなく、現場の周りの自分の位置を目指す(着いても、全員が中心に押し合わない)
        _move.SetDestination(GetGatherDestination(_rushKey, _rushCenter), CSE_PoliceMoveState.Rush);

        if (!_move.IsNearDestination(_arriveDistance)) return;

        _hasRushRequest = false;
        StartSearch(_rushKey, _rushCenter);
    }

    /// <summary>
    /// 位置を追っているプレイヤーがいれば、駆け付ける場所をそのプレイヤーの今の位置に更新するメソッド
    /// </summary>
    private void UpdateRushTarget()
    {
        if (_rushPlayer == null) return;

        // 変身を解いた・倒れたプレイヤーは追わない(駆け付ける場所は、最後に分かっていた位置のままにする)
        // 目印は同じプレイヤーのままなので、同じプレイヤーへ向かう警察は、引き続き別々の位置を目指す
        if (!CS_PoliceVision.IsPlayerTargetable(_rushPlayer))
        {
            _rushPlayer = null;
            return;
        }

        _rushCenter = _rushPlayer.transform.position;
    }

    /// <summary>
    /// 指定した場所の探索を始めるメソッド
    /// </summary>
    /// <param name="key">探索する場所の目印(同じ場所を探す警察は同じ目印を持ち、場所の周りの別々の位置へ割り当てられる)</param>
    /// <param name="center">探索する場所</param>
    private void StartSearch(object key, Vector3 center)
    {
        _state = CSE_PoliceMoveState.Search;
        _isWandering = false;
        _searchTimer = _searchDuration;
        _searchKey = key;
        _searchCenter = center;
        _move.SetDestination(GetGatherDestination(_searchKey, _searchCenter), CSE_PoliceMoveState.Search);
    }

    /// <summary>
    /// 探索場所に着いてから一定時間経ったら、巡回・待機に戻るメソッド
    /// </summary>
    private void UpdateSearch()
    {
        // 割り当てられた位置が変わることがあるので、毎回求め直す(変わらなければ経路の再計算はされない)
        _move.SetDestination(GetGatherDestination(_searchKey, _searchCenter), CSE_PoliceMoveState.Search);

        if (!_move.IsNearDestination(_arriveDistance)) return;

        _searchTimer -= _thinkInterval;
        if (_searchTimer > 0.0f) return;

        MoveIdle();
    }

    /// <summary>
    /// 所属先に合わせて巡回・待機するメソッド(巡回するグループなら巡回ルート、増援なら探していた場所の付近を歩き回る)
    /// </summary>
    private void MoveIdle()
    {
        _state = _group.MoveIdle(this, _move);

        // 巡回に戻る時は、現場・標的の周りの割り当てから外れる(待機の時は、割り当てられた位置のまま残る)
        if (_state == CSE_PoliceMoveState.Patrol)
        {
            LeaveRing();
            _isWandering = false;
            return;
        }

        if (_state == CSE_PoliceMoveState.Wait) UpdateWander();
    }

    /// <summary>
    /// 待機中に、探していた場所の付近を少し歩き回るメソッド
    /// 行き先に着いたら少し立ち止まり、また付近の別の行き先へ歩く(立ち止まったままにならないようにする)
    /// </summary>
    private void UpdateWander()
    {
        // 待機に入った最初に、歩き回る中心を決める(探していた場所。無ければ今の位置)
        if (!_isWandering)
        {
            _isWandering = true;
            _wanderCenter = _searchKey != null ? _searchCenter : transform.position;
            _hasWanderTarget = false;
            _wanderPauseTimer = 0.0f;
        }

        // 行き先へ歩いている間
        if (_hasWanderTarget)
        {
            _wanderWalkTimer += _thinkInterval;
            _move.SetDestination(_wanderTarget, CSE_PoliceMoveState.Wait);

            // 着いたか、なかなか着けない時は、少し立ち止まって次の行き先を選ぶ
            if (!_move.IsNearDestination(_arriveDistance) && _wanderWalkTimer < _wanderMaxWalkTime) return;

            _hasWanderTarget = false;
            _wanderPauseTimer = Random.Range(_wanderPauseMin, Mathf.Max(_wanderPauseMin, _wanderPauseMax));
            return;
        }

        // 立ち止まっている間
        _wanderPauseTimer -= _thinkInterval;
        if (_wanderPauseTimer > 0.0f) return;

        // 次の行き先を選んで歩き出す(選べなければ、もう少し立ち止まってからやり直す)
        if (!TryPickWanderPoint(out Vector3 point))
        {
            _wanderPauseTimer = _wanderPauseMin;
            return;
        }

        _wanderTarget = point;
        _hasWanderTarget = true;
        _wanderWalkTimer = 0.0f;
        _move.SetDestination(point, CSE_PoliceMoveState.Wait);
    }

    /// <summary>
    /// 歩き回る範囲の中から、歩いて行ける次の行き先をランダムに選ぶメソッド
    /// </summary>
    /// <param name="point">選んだ行き先</param>
    /// <returns>選べたらtrue</returns>
    private bool TryPickWanderPoint(out Vector3 point)
    {
        point = transform.position;

        // 経路の計算は、足元のNavMesh上の位置から行う(警察の位置は、NavMeshから少し浮いているため)
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, _slotSampleDistance, NavMesh.AllAreas)) return false;

        for (int i = 0; i < _wanderPickAttempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * _wanderRadius;
            Vector3 candidate = _wanderCenter + new Vector3(offset.x, 0.0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _slotSampleDistance, NavMesh.AllAreas)) continue;

            // 少しは歩くよう、今の位置に近すぎる行き先は選ばない
            Vector3 step = hit.position - startHit.position;
            step.y = 0.0f;
            if (step.sqrMagnitude < _wanderMinStep * _wanderMinStep) continue;

            // 壁の向こうなど、歩いて行けない行き先は選ばない(詰まりと判定されないようにする)
            if (!NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, _wanderPath)) continue;
            if (_wanderPath.status != NavMeshPathStatus.PathComplete) continue;

            point = hit.position;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 駆け付け・探索で目指す、現場の周りの自分の位置を求めるメソッド
    /// </summary>
    /// <param name="key">現場の目印</param>
    /// <param name="center">現場の位置</param>
    /// <returns>目指す位置</returns>
    private Vector3 GetGatherDestination(object key, Vector3 center)
    {
        // 視野距離より遠くで止まると、現場の中心にいるプレイヤーが視界に入らないので、半径を視野距離の範囲に収める
        float radius = Mathf.Max(0.8f, Mathf.Min(_gatherRadius, _vision.viewDistance * _gatherViewRatio));
        return GetSlotDestination(key, _gatherRingKind, center, radius);
    }

    /// <summary>
    /// 中心の周りの、自分に割り当てられた位置(NavMesh上)を求めるメソッド
    /// 位置が歩ける場所(NavMesh)に無い時は、中心を目指す
    /// </summary>
    /// <param name="key">中心の目印</param>
    /// <param name="kind">円の種類</param>
    /// <param name="center">中心の位置</param>
    /// <param name="radius">中心から離れる距離</param>
    /// <returns>目指す位置</returns>
    private Vector3 GetSlotDestination(object key, int kind, Vector3 center, float radius)
    {
        // 別の中心の円に加わっていたら、そちらへ移る
        if (_ring == null || _ringKey != key || _ringKind != kind)
        {
            LeaveRing();
            _ring = CS_PoliceSlotRegistry.Join(key, kind, this);
            _ringKey = key;
            _ringKind = kind;
        }

        Vector3 slotPosition = _ring.GetPosition(this, center, radius, radius);
        if (NavMesh.SamplePosition(slotPosition, out NavMeshHit hit, _slotSampleDistance, NavMesh.AllAreas)) return hit.position;

        return center;
    }

    /// <summary>
    /// 中心の周りの位置の割り当てから外れるメソッド
    /// </summary>
    private void LeaveRing()
    {
        if (_ring == null) return;

        CS_PoliceSlotRegistry.Leave(_ringKey, _ringKind, this);
        _ring = null;
        _ringKey = null;
    }

    private void OnDestroy()
    {
        LeaveRing();
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
