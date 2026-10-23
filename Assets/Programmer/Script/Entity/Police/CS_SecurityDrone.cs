/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 巡回ドローン(警察陣営の防犯装置)のクラス
/// ・足が遅く、攻撃もできない。空中に浮いているので、3Dの巡回ポイントを順に回る(立体的に巡回できる)
/// ・警察と同じ視界(視野角・距離・高さ)を持つ。プレイヤーを見つけたら、警察陣営を警戒状態にする
///   見つけている間は、目が青と赤で点滅する。見失ったあとも、しばらく(設定の時間)ついてくる
/// ・背中から攻撃されると、一定時間動かなくなる(スタン)。目も消える。パリーの反撃(IStunnable)でもスタンする
///   空中にいても殴れるよう、ドローンの下から地面までの「当たり判定の柱」を持つ(攻撃の判定にだけ当たり、物理的にはぶつからない)
/// ・巡回範囲(任意のコライダー)を指定すると、その中から出ない(「2Fだけ」「裏道だけ」などの条件に使う)
/// 判断と移動はサーバー(またはオフライン)だけで行い、位置はNetworkTransformで、状態はNetworkVariableで全員へ同期する
/// 出現とデータの受け渡しはCS_SecurityDroneSpawnerが行う
/// </summary>
[RequireComponent(typeof(CS_PoliceVision))]
public class CS_SecurityDrone : NetworkBehaviour, IDamageable, IStunnable
{
    // 状態(書き込みはサーバーのみ。見た目を全員で合わせるために同期する)
    private readonly NetworkVariable<CSE_SecurityDroneState> _state = new NetworkVariable<CSE_SecurityDroneState>();

    // サーバー(またはオフライン)が判断に使う状態
    private CSE_SecurityDroneState _currentState = CSE_SecurityDroneState.Patrol;

    private CS_PoliceVision _vision = null;
    private CSO_SecurityDroneData _data = null;

    // 速度(m/秒)
    private float _patrolSpeed = 0.0f;
    private float _chaseSpeed = 0.0f;

    // 巡回ポイントと、今向かっているポイント・巡回範囲(無ければnull)
    private Transform[] _patrolPoints = new Transform[0];
    private int _patrolIndex = 0;
    private Collider _patrolArea = null;

    // 追っているプレイヤーと、最後に見た位置・追い続ける残り時間
    private Transform _target = null;
    private Vector3 _lastSeenPosition = Vector3.zero;
    private float _chaseTimer = 0.0f;

    // スタンの残り時間・次に判断するまでの残り時間・障害物で進めない(避け続けている)時間
    private float _stunTimer = 0.0f;
    private float _thinkTimer = 0.0f;
    private float _blockedTimer = 0.0f;

    // 障害物を避ける時に、先に試す側(1: 右 / -1: 左。避けられた側を覚えて、左右に迷わないようにする)
    private float _detourSide = 1.0f;

    // 避けると決めた向きと、その向きで進み続ける残り時間(障害物のそばで、避ける向きが細かく入れ替わらないようにする)
    private Vector3 _detourDirection = Vector3.forward;
    private float _detourHold = 0.0f;

    // 避けると決めた向きで、最低限進み続ける時間(秒)
    private const float _detourHoldTime = 0.5f;

    // 障害物を避ける時に、まっすぐな向きから外す角度(度。小さい角度から順に試す)と、上へ避ける角度(度)
    private static readonly float[] _detourYawAngles = { 30.0f, 60.0f, 90.0f, 120.0f };
    private const float _detourPitchAngle = 45.0f;

    // 初期化済みか(Settingが呼ばれた=サーバー、またはオフラインだけがtrue)
    private bool _isInitialized = false;

    [Header("＝＝＝ 動き ＝＝＝")]
    [SerializeField, Min(0.01f)]
    [Tooltip("状況を判断する間隔(秒)")]
    private float _thinkInterval = 0.2f;

    [SerializeField, Min(0f)]
    [Tooltip("巡回ポイントに、この距離まで近づいたら着いたとみなす(m)")]
    private float _arriveDistance = 0.3f;

    [SerializeField, Min(0f)]
    [Tooltip("追う時に、プレイヤーにこの水平距離まで近づいたら止まる(m。攻撃はしないので、触れるほどは近づかない)")]
    private float _stopDistance = 1.5f;

    [SerializeField, Min(0f)]
    [Tooltip("追う時に、プレイヤーの位置からどれだけ上を飛ぶか(m)")]
    private float _hoverHeight = 2.0f;

    [SerializeField, Min(0f)]
    [Tooltip("向きを変える速さ(度/秒)")]
    private float _turnSpeed = 180.0f;

    [SerializeField, Min(0.05f)]
    [Tooltip("壁などにぶつかるかを調べる、体の半径(m)")]
    private float _bodyRadius = 0.4f;

    [SerializeField, Min(0.1f)]
    [Tooltip("進む向きにこの距離まで障害物が近づいたら、少しルートから外れて避け始める(m)")]
    private float _avoidDistance = 1.5f;

    [SerializeField, Min(0.1f)]
    [Tooltip("巡回中に、避けられない・避け続けたまま、この時間が経ったら、次の巡回ポイントへ向かう(秒)")]
    private float _blockedSkipTime = 6.0f;

    [Header("＝＝＝ 当たり判定の柱 ＝＝＝")]
    [SerializeField, Min(0.05f)]
    [Tooltip("当たり判定の柱の半径(m)。プレイヤーの攻撃の判定が、この柱に触れれば当たる")]
    private float _hitColumnRadius = 0.5f;

    [SerializeField, Min(0.1f)]
    [Tooltip("当たり判定の柱の最大の長さ(m)。ドローンの下の地面がこれより遠い時は、この長さまで")]
    private float _hitColumnMaxLength = 8.0f;

    [Header("＝＝＝ 見た目 ＝＝＝")]
    [SerializeField]
    [Tooltip("目(顔)の色を変えるクラス")]
    private CS_SecurityBlink _faceBlink = null;

    [SerializeField]
    [Tooltip("目(顔)の見た目。スタン中は消す")]
    private Renderer _faceRenderer = null;

    // ドローンの下から地面までの、攻撃の判定に当たる柱(起動時に作る。ドローンの高さに合わせて、長さを変える)
    private CapsuleCollider _hitColumn = null;

    // 今の状態(全クライアントで分かる。ネットワークに出現していない時は、判断に使っている状態)
    public CSE_SecurityDroneState state => IsSpawned ? _state.Value : _currentState;

    private void Awake()
    {
        _vision = GetComponent<CS_PoliceVision>();
        CreateHitColumn();
    }

    public override void OnNetworkSpawn()
    {
        _state.OnValueChanged += OnStateChanged;
        ApplyVisual(_state.Value);
    }

    public override void OnNetworkDespawn()
    {
        _state.OnValueChanged -= OnStateChanged;
    }

    /// <summary>
    /// 出現した時のデータと巡回の設定を渡すメソッド(サーバー、またはオフラインで呼ぶこと)
    /// </summary>
    /// <param name="data">巡回ドローンのデータ</param>
    /// <param name="playerBaseStats">速度の基準にするプレイヤーのステータス</param>
    /// <param name="patrolPoints">巡回ポイント(3D。順に回る)</param>
    /// <param name="patrolArea">巡回範囲(動ける範囲。無ければnull)</param>
    public void Setting(CSO_SecurityDroneData data, CSO_PlayerStats playerBaseStats, Transform[] patrolPoints, Collider patrolArea)
    {
        _data = data;
        _patrolSpeed = playerBaseStats.moveSpeed * data.patrolSpeedMultiplier;
        _chaseSpeed = playerBaseStats.moveSpeed * data.chaseSpeedMultiplier;
        _patrolPoints = patrolPoints;
        _patrolArea = patrolArea;
        _patrolIndex = FindNearestPatrolPoint();

        _vision.Setting(data.viewAngle, data.viewDistance, data.viewHeight);
        _thinkTimer = Random.Range(0.0f, _thinkInterval);
        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        if (_currentState == CSE_SecurityDroneState.Stunned)
        {
            UpdateStun();
            return;
        }

        // 見つけたかの判断は、一定間隔で行う(視界判定の負荷を抑えるため)
        _thinkTimer -= Time.deltaTime;
        if (_thinkTimer <= 0.0f)
        {
            _thinkTimer = _thinkInterval;
            Think();
        }

        UpdateMovement();
        UpdateHitColumn();
    }

    /// <summary>
    /// IDamageable実装。攻撃者が分からないダメージでは、何も起きない(ダメージでは壊れない)
    /// </summary>
    public void TakeDamage(float damage)
    {
    }

    /// <summary>
    /// IDamageable実装(攻撃者付き)。攻撃者が背中側にいれば、スタンする
    /// </summary>
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (!_isInitialized || attacker == null) return;
        if (!IsBehind(attacker.transform.position)) return;

        Stun(_data.stunDuration);
    }

    /// <summary>
    /// IStunnable実装。パリーの反撃などで、指定した時間の間、動かなくする
    /// </summary>
    /// <param name="duration">動かなくなる時間(秒)</param>
    public void Stun(float duration)
    {
        if (!_isInitialized) return;

        _stunTimer = duration;
        _target = null;
        SetState(CSE_SecurityDroneState.Stunned);
    }

    /// <summary>
    /// 視界を調べ、プレイヤーを見つけた・見失った時の状態の移り変わりを決めるメソッド
    /// </summary>
    private void Think()
    {
        Transform seen = FindPlayer();
        if (seen != null)
        {
            OnSeen(seen);
            return;
        }

        if (_currentState == CSE_SecurityDroneState.Detected) SetState(CSE_SecurityDroneState.Chase);
        if (_currentState != CSE_SecurityDroneState.Chase) return;

        // 見失ったあとも、設定の時間はついてくる
        _chaseTimer -= _thinkInterval;
        if (_chaseTimer <= 0.0f) EndChase();
    }

    /// <summary>
    /// 視界に映っているプレイヤーを探すメソッド(悪人・ホログラムは対象にしない)
    /// </summary>
    /// <returns>見えているプレイヤー(いなければnull)</returns>
    private Transform FindPlayer()
    {
        Transform found = _vision.FindTarget(_target, out int priority);
        if (found == null || found.GetComponent<CS_PlayerHealth>() == null) return null;

        return found;
    }

    /// <summary>
    /// プレイヤーを見つけた時の処理(追う対象を決め、警察陣営を警戒状態にする)
    /// </summary>
    private void OnSeen(Transform player)
    {
        _target = player;
        _lastSeenPosition = player.position;
        _chaseTimer = _data.chaseDuration;
        SetState(CSE_SecurityDroneState.Detected);

        CS_PoliceAlertManager.Report(player.GetComponent<CS_PlayerHealth>(), player.position);
    }

    /// <summary>
    /// 追うのをやめて、巡回に戻るメソッド(一番近い巡回ポイントへ向かう)
    /// </summary>
    private void EndChase()
    {
        _target = null;
        _patrolIndex = FindNearestPatrolPoint();
        SetState(CSE_SecurityDroneState.Patrol);
    }

    /// <summary>
    /// スタンの残り時間を進め、終わったら巡回に戻るメソッド
    /// </summary>
    private void UpdateStun()
    {
        _stunTimer -= Time.deltaTime;
        if (_stunTimer > 0.0f) return;

        EndChase();
    }

    /// <summary>
    /// 攻撃の判定に当たる、ドローンの下から地面までの柱を作るメソッド
    /// 柱は、他の物との物理的なぶつかりを全部無視する(プレイヤーが柱にぶつかって進めなくならないようにする)
    /// 攻撃の判定(Physics.Overlap系)は、ぶつかりの設定に関係なく当たる
    /// </summary>
    private void CreateHitColumn()
    {
        GameObject column = new GameObject("HitColumn");
        column.layer = gameObject.layer;
        column.transform.SetParent(transform, false);

        _hitColumn = column.AddComponent<CapsuleCollider>();
        _hitColumn.direction = 1;
        _hitColumn.radius = _hitColumnRadius;
        _hitColumn.height = _hitColumnRadius * 2.0f;
        _hitColumn.excludeLayers = ~0;
    }

    /// <summary>
    /// 当たり判定の柱を、ドローンの位置から真下の地面までの長さに合わせるメソッド
    /// </summary>
    private void UpdateHitColumn()
    {
        float length = _hitColumnMaxLength;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, _hitColumnMaxLength,
            CS_PoliceLayers.obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            length = hit.distance;
        }

        // ドローンの体(中心)から地面まで。カプセルの高さは、両端の半球を含むので、半径の2倍以上にする
        _hitColumn.height = Mathf.Max(length, _hitColumnRadius * 2.0f);
        _hitColumn.center = new Vector3(0.0f, -_hitColumn.height * 0.5f, 0.0f);
    }

    /// <summary>
    /// 状態に応じた目的地へ、移動するメソッド
    /// </summary>
    private void UpdateMovement()
    {
        bool isChasing = _currentState == CSE_SecurityDroneState.Detected || _currentState == CSE_SecurityDroneState.Chase;
        if (!isChasing && _patrolPoints.Length == 0) return;

        Vector3 destination = isChasing ? GetChaseDestination() : _patrolPoints[_patrolIndex].position;
        MoveToward(destination, isChasing ? _chaseSpeed : _patrolSpeed);

        if (!isChasing) CheckArrivedAtPatrolPoint(destination);
    }

    /// <summary>
    /// 追う時の目的地を求めるメソッド(プレイヤーの少し上。水平方向に近づきすぎたら、その場で止まる)
    /// </summary>
    private Vector3 GetChaseDestination()
    {
        Vector3 targetPosition = _target != null ? _target.position : _lastSeenPosition;
        Vector3 destination = targetPosition + Vector3.up * _hoverHeight;

        Vector3 flat = destination - transform.position;
        flat.y = 0.0f;
        if (flat.magnitude < _stopDistance)
        {
            destination.x = transform.position.x;
            destination.z = transform.position.z;
        }
        return destination;
    }

    /// <summary>
    /// 目的地へ向かって、1フレーム分だけ進むメソッド(巡回範囲の外へは出ない)
    /// 進む向きに障害物があれば、少しルートから外れて、通れる向きへ進む(避けられなければ、その場で止まる)
    /// </summary>
    private void MoveToward(Vector3 destination, float speed)
    {
        Vector3 delta = destination - transform.position;
        float stepLength = Mathf.Min(delta.magnitude, speed * Time.deltaTime);
        if (stepLength < 0.0001f) return;

        if (!TryChooseDirection(delta.normalized, out Vector3 direction, out bool isDetouring))
        {
            _blockedTimer += Time.deltaTime;
            return;
        }

        // 避けている間も、時間を数える(避け続けても抜けられない時に、巡回ポイントを飛ばすため)
        _blockedTimer = isDetouring ? _blockedTimer + Time.deltaTime : 0.0f;

        TurnToward(direction);
        Vector3 next = transform.position + direction * stepLength;
        transform.position = _patrolArea != null ? _patrolArea.ClosestPoint(next) : next;
    }

    /// <summary>
    /// 実際に進む向きを決めるメソッド(障害物が無ければ本来の向き。あれば避ける向き。避けると決めた向きは、しばらく保つ)
    /// </summary>
    /// <param name="desired">本来進みたい向き</param>
    /// <param name="direction">実際に進む向き</param>
    /// <param name="isDetouring">障害物を避けているか</param>
    /// <returns>進める向きが見つかればtrue(避けられなければfalse)</returns>
    private bool TryChooseDirection(Vector3 desired, out Vector3 direction, out bool isDetouring)
    {
        direction = desired;
        isDetouring = false;

        // 避けると決めた向きが、まだ通れるなら、しばらくその向きで進む
        if (_detourHold > 0.0f && !IsBlocked(_detourDirection))
        {
            _detourHold -= Time.deltaTime;
            direction = _detourDirection;
            isDetouring = true;
            return true;
        }

        _detourHold = 0.0f;
        if (!IsBlocked(desired)) return true;

        isDetouring = true;
        if (!TryFindDetour(desired, out direction)) return false;

        _detourDirection = direction;
        _detourHold = _detourHoldTime;
        return true;
    }

    /// <summary>
    /// 進む向きに、障害物(壁など)があるかを判定するメソッド(避け始める距離の先まで調べる)
    /// </summary>
    private bool IsBlocked(Vector3 direction)
    {
        return Physics.SphereCast(transform.position, _bodyRadius, direction, out RaycastHit hit,
            _avoidDistance, CS_PoliceLayers.obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// 障害物を避けて進める向きを探すメソッド
    /// まっすぐな向きに近い順に、左右(先に試すのは、前回避けられた側)・上へ角度をずらして試す
    /// </summary>
    /// <param name="direction">本来進みたい向き</param>
    /// <param name="detour">避けて進める向き</param>
    /// <returns>通れる向きが見つかればtrue</returns>
    private bool TryFindDetour(Vector3 direction, out Vector3 detour)
    {
        foreach (float angle in _detourYawAngles)
        {
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? _detourSide : -_detourSide;
                detour = Quaternion.AngleAxis(angle * side, Vector3.up) * direction;
                if (IsBlocked(detour)) continue;

                _detourSide = side;
                return true;
            }

            // 最初の角度では、横の次に上へ避けられるかも試す(空中なので、上を越えられる)
            if (angle > _detourYawAngles[0]) continue;

            detour = Vector3.RotateTowards(direction, Vector3.up, _detourPitchAngle * Mathf.Deg2Rad, 0.0f);
            if (!IsBlocked(detour)) return true;
        }

        detour = direction;
        return false;
    }

    /// <summary>
    /// 進む向きへ、水平方向に少しずつ向きを変えるメソッド
    /// </summary>
    private void TurnToward(Vector3 direction)
    {
        direction.y = 0.0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _turnSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 巡回ポイントに着いた(または、進めないまま時間が経った)ら、次のポイントへ向かうメソッド
    /// </summary>
    private void CheckArrivedAtPatrolPoint(Vector3 destination)
    {
        bool isArrived = (destination - transform.position).magnitude <= _arriveDistance;
        if (!isArrived && _blockedTimer < _blockedSkipTime) return;

        _blockedTimer = 0.0f;
        _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
    }

    /// <summary>
    /// 攻撃者の位置が、ドローンの背中側(設定の角度の範囲)かを判定するメソッド
    /// </summary>
    private bool IsBehind(Vector3 attackerPosition)
    {
        Vector3 toAttacker = attackerPosition - transform.position;
        toAttacker.y = 0.0f;
        if (toAttacker.sqrMagnitude < 0.0001f) return true;

        Vector3 back = -transform.forward;
        back.y = 0.0f;
        return Vector3.Angle(back, toAttacker) <= _data.backAngle * 0.5f;
    }

    /// <summary>
    /// 今いる位置から一番近い巡回ポイントの番号を求めるメソッド
    /// </summary>
    private int FindNearestPatrolPoint()
    {
        int nearest = 0;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < _patrolPoints.Length; i++)
        {
            float sqr = (_patrolPoints[i].position - transform.position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            nearest = i;
            nearestSqr = sqr;
        }
        return nearest;
    }

    /// <summary>
    /// 状態を変えるメソッド(ネットワーク接続中はサーバーが全員へ同期する。サーバー、またはオフラインで呼ぶ)
    /// </summary>
    private void SetState(CSE_SecurityDroneState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;
        if (IsSpawned && IsServer) _state.Value = newState;
        ApplyVisual(newState);
    }

    // 状態が変わった時(クライアント)
    private void OnStateChanged(CSE_SecurityDroneState previous, CSE_SecurityDroneState current)
    {
        ApplyVisual(current);
    }

    /// <summary>
    /// 状態に合わせて、目の見た目を変えるメソッド(見つけている間は点滅、スタン中は消える)
    /// </summary>
    private void ApplyVisual(CSE_SecurityDroneState current)
    {
        if (_faceRenderer != null) _faceRenderer.enabled = current != CSE_SecurityDroneState.Stunned;
        if (_faceBlink == null) return;

        if (current == CSE_SecurityDroneState.Detected && !_faceBlink.isFlashing) _faceBlink.StartFlash(float.MaxValue);
        else if (current != CSE_SecurityDroneState.Detected) _faceBlink.StopFlash();
    }

    private void OnDrawGizmosSelected()
    {
        // 視野(距離と角度)をSceneビューに表示する
        float distance = _data != null ? _data.viewDistance : 5.0f;
        float halfAngle = (_data != null ? _data.viewAngle : 120.0f) * 0.5f;

        Gizmos.color = new Color(1.0f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, distance);
        Vector3 left = Quaternion.Euler(0.0f, -halfAngle, 0.0f) * transform.forward * distance;
        Vector3 right = Quaternion.Euler(0.0f, halfAngle, 0.0f) * transform.forward * distance;
        Gizmos.DrawLine(transform.position, transform.position + left);
        Gizmos.DrawLine(transform.position, transform.position + right);
    }
}
