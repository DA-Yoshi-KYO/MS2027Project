/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-26 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 巡回ドローンの制圧モード(プレイヤーを攻撃する動き)を管理するクラス
/// ドローンがプレイヤーを見ている時間が、一定の長さたまると、ドローン(CS_SecurityDrone)が制圧モードに入れる
/// 流れ(サーバー、またはオフラインだけで進む。見た目は、状態の同期と、RPCで全員に出す)
///   1. テーザーの溜め: プレイヤーを追いながら溜める
///   2. テーザーの狙い: 撃つ向きを決め、赤い線で知らせる(避けられる)。時間が来たら、その向きに光線を撃つ
///      外れたら、短い間隔のあと、もう一度溜める(当たるまで繰り返す)
///   3. 当たったら: プレイヤーはスタン(動けない)。ドローンは近づいて、即死攻撃を溜める。地面に範囲の赤い円が出る
///      プレイヤーがスタンから抜けられたかに関係なく、ドローンは溜めの最後まで動く
///   4. 溜め終わりに、範囲の中にいるプレイヤーは即死する
///   5. 攻撃後の隙: ドローンは動かない。プレイヤーは逃げられる。終わったら、ドローンは追跡に戻る
/// ドローンがスタンされたら、制圧モードは中断される(ドローン側がCancelを呼ぶ)
/// </summary>
[RequireComponent(typeof(CS_SecurityDrone))]
public class CS_SecurityDroneSuppression : NetworkBehaviour
{
    // 制圧モードの段階
    private enum Phase
    {
        None,
        TaserCharge,
        TaserAim,
        TaserRetry,
        Approach,
        KillCharge,
        Recovery,
    }

    // HDRP/Unlitの色のプロパティ
    private static readonly int _unlitColorId = Shader.PropertyToID("_UnlitColor");

    // 光の線・円の色(明るさも含む)
    private static readonly Color _warningColor = new Color(3.0f, 0.1f, 0.1f);
    private static readonly Color _beamColor = new Color(3.0f, 2.6f, 0.6f);
    private static readonly Color _ringAlternateColor = new Color(3.0f, 1.6f, 0.1f);

    // 地面の円を、何分割して描くか・地面から浮かせる高さ(m)・色が入れ替わる間隔(秒)
    private const int _ringSegments = 48;
    private const float _ringLift = 0.08f;
    private const float _ringBlinkInterval = 0.2f;

    // プレイヤーを警戒状態にし続ける間隔(秒)
    private const float _reportInterval = 0.2f;

    private CS_SecurityDrone _drone = null;
    private Phase _phase = Phase.None;
    private float _timer = 0.0f;
    private float _reportTimer = 0.0f;

    // 攻撃する相手(最初はドローンが追っていたプレイヤー。テーザーが当たったら、そのプレイヤー)
    private Transform _target = null;

    // テーザーを撃つ向きと、即死攻撃の中心(溜め始めたドローンの真下の地面)
    private Vector3 _aimDirection = Vector3.forward;
    private Vector3 _killCenter = Vector3.zero;

    // 当たりを調べる際に使い回すバッファ
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
    private readonly Collider[] _overlapBuffer = new Collider[16];

    // 地面に出す、即死攻撃の範囲の円
    private LineRenderer _ring = null;
    private MaterialPropertyBlock _propertyBlock = null;

    [Header("＝＝＝ テーザー ＝＝＝")]
    [SerializeField, Min(0.0f)]
    [Tooltip("テーザーを撃つ向きを決めてから、撃つまでの時間(秒)。この間、赤い線が出る。線の上から出れば避けられる")]
    private float _taserAimTime = 0.3f;

    [SerializeField, Min(0.5f)]
    [Tooltip("テーザーの光線が届く距離(m)")]
    private float _taserRange = 12.0f;

    [SerializeField, Min(0.05f)]
    [Tooltip("テーザーの光線の太さ(半径。m)。この範囲にプレイヤーがいれば当たる")]
    private float _taserRadius = 0.5f;

    [SerializeField, Min(0.0f)]
    [Tooltip("テーザーが外れた時に、もう一度溜め始めるまでの間隔(秒)")]
    private float _taserRetryInterval = 1.0f;

    [Header("＝＝＝ 即死攻撃 ＝＝＝")]
    [SerializeField, Min(0.0f)]
    [Tooltip("テーザーが当たったプレイヤーへ、この水平距離まで近づいたら、即死攻撃を溜め始める(m)")]
    private float _killApproachDistance = 2.5f;

    [SerializeField, Min(0.5f)]
    [Tooltip("即死攻撃の範囲の半径(m)。溜め始めた時のドローンの真下を中心にした円。この外へ出れば助かる")]
    private float _killRadius = 3.0f;

    [SerializeField, Min(0.5f)]
    [Tooltip("即死攻撃の範囲の高さ(m)。地面から、この高さまでにいるプレイヤーが対象")]
    private float _killHeight = 3.0f;

    [SerializeField, Min(1.0f)]
    [Tooltip("即死攻撃のダメージ(プレイヤーのHPより大きい値にする)")]
    private float _killDamage = 10000.0f;

    [Header("＝＝＝ 見た目 ＝＝＝")]
    [SerializeField]
    [Tooltip("光の線・円のマテリアル(Unlit。MT_SecurityLaserなど)")]
    private Material _lineMaterial = null;

    // 制圧モード中か(サーバー、またはオフラインでだけ正しい)
    public bool isActive => _phase != Phase.None;

    private void Awake()
    {
        _drone = GetComponent<CS_SecurityDrone>();
        _propertyBlock = new MaterialPropertyBlock();
        CreateRing();
    }

    private void Update()
    {
        UpdateRing();
    }

    /// <summary>
    /// 制圧モードを始めるメソッド(サーバー、またはオフラインで呼ぶこと)
    /// </summary>
    /// <param name="target">攻撃するプレイヤー</param>
    public void Begin(Transform target)
    {
        _target = target;
        _reportTimer = 0.0f;
        StartPhase(Phase.TaserCharge, _drone.data.taserChargeTime, CSE_SecurityDroneState.TaserCharge);
    }

    /// <summary>
    /// 制圧モードを中断するメソッド(ドローンがスタンされた時などに呼ぶ)
    /// </summary>
    public void Cancel()
    {
        _phase = Phase.None;
        _target = null;
    }

    /// <summary>
    /// 制圧モードを1フレーム分進めるメソッド(制圧モード中、ドローンが毎フレーム呼ぶ。サーバー、またはオフライン)
    /// </summary>
    /// <param name="deltaTime">前のフレームからの経過時間</param>
    public void Tick(float deltaTime)
    {
        if (!IsTargetAvailable())
        {
            Cancel();
            return;
        }

        ReportTarget(deltaTime);
        _timer -= deltaTime;

        switch (_phase)
        {
            case Phase.TaserCharge: TickTaserCharge(); break;
            case Phase.TaserAim: TickTaserAim(); break;
            case Phase.TaserRetry: TickTaserRetry(); break;
            case Phase.Approach: TickApproach(); break;
            case Phase.KillCharge: TickKillCharge(); break;
            case Phase.Recovery: TickRecovery(); break;
        }
    }

    // 1. テーザーの溜め: プレイヤーを追いながら溜める。溜め終わったら、撃つ向きを決める
    private void TickTaserCharge()
    {
        ChaseTarget();
        if (_timer > 0.0f) return;

        _aimDirection = (GetTargetCenter() - _drone.transform.position).normalized;
        StartPhase(Phase.TaserAim, _taserAimTime, CSE_SecurityDroneState.TaserAim);
        ShowLine(_drone.transform.position, _drone.transform.position + _aimDirection * _taserRange, _warningColor, 0.04f, _taserAimTime);
    }

    // 2. テーザーの狙い: その場で止まって、赤い線を見せる。時間が来たら撃つ
    private void TickTaserAim()
    {
        _drone.FaceToward(_drone.transform.position + _aimDirection);
        if (_timer > 0.0f) return;

        FireTaser();
    }

    // テーザーが外れたあと、少しの間、追いながら待つ
    private void TickTaserRetry()
    {
        ChaseTarget();
        if (_timer > 0.0f) return;

        StartPhase(Phase.TaserCharge, _drone.data.taserChargeTime, CSE_SecurityDroneState.TaserCharge);
    }

    // 3. 近づく: テーザーが当たったプレイヤーへ近づき、近づいたら、即死攻撃を溜め始める
    private void TickApproach()
    {
        ChaseTarget();

        Vector3 offset = _target.position - _drone.transform.position;
        offset.y = 0.0f;
        if (offset.magnitude > _killApproachDistance) return;

        _killCenter = GetGroundPosition(_drone.transform.position);
        StartPhase(Phase.KillCharge, _drone.data.killChargeTime, CSE_SecurityDroneState.KillCharge);
    }

    // 4. 即死攻撃の溜め: プレイヤーがスタンから抜けられたかに関係なく、最後まで続ける。終わったら、範囲の中のプレイヤーを即死させる
    private void TickKillCharge()
    {
        if (_timer > 0.0f) return;

        KillPlayersInRange();
        StartPhase(Phase.Recovery, _drone.data.recoveryTime, CSE_SecurityDroneState.Recovery);
    }

    // 5. 攻撃後の隙: 動かない。終わったら、制圧モードを終える(ドローンが追跡に戻る)
    private void TickRecovery()
    {
        if (_timer > 0.0f) return;

        Cancel();
    }

    /// <summary>
    /// 段階を変え、ドローンの状態(見た目)も合わせるメソッド
    /// </summary>
    private void StartPhase(Phase phase, float duration, CSE_SecurityDroneState droneState)
    {
        _phase = phase;
        _timer = duration;
        _drone.ChangeState(droneState);
    }

    /// <summary>
    /// 攻撃する相手が、まだ攻撃できるか(いる・倒れていない)を判定するメソッド
    /// 段階が「攻撃後の隙」の時は、相手が倒れていても、隙を続ける
    /// </summary>
    private bool IsTargetAvailable()
    {
        if (_phase == Phase.Recovery) return true;
        if (_target == null) return false;

        CS_PlayerHealth health = _target.GetComponent<CS_PlayerHealth>();
        return health != null && !health.isDead;
    }

    /// <summary>
    /// 攻撃している間、プレイヤーを警戒状態にし続けるメソッド
    /// </summary>
    private void ReportTarget(float deltaTime)
    {
        if (_target == null) return;

        _reportTimer -= deltaTime;
        if (_reportTimer > 0.0f) return;

        _reportTimer = _reportInterval;
        CS_PlayerHealth health = _target.GetComponent<CS_PlayerHealth>();
        if (health != null) CS_PoliceAlertManager.Report(health, _target.position);
    }

    /// <summary>
    /// 攻撃する相手を、追いながら見るメソッド(ドローンの追跡と同じ速さ・高さで動く)
    /// </summary>
    private void ChaseTarget()
    {
        _drone.SteerTo(_drone.GetChaseDestination(), _drone.chaseSpeed);
        _drone.FaceToward(_target.position);
    }

    /// <summary>
    /// 相手の体の中心の位置を取得するメソッド(コライダーがあればその中心。無ければ位置)
    /// </summary>
    private Vector3 GetTargetCenter()
    {
        return _target.TryGetComponent(out Collider targetCollider) ? targetCollider.bounds.center : _target.position;
    }

    /// <summary>
    /// テーザーの光線を撃つメソッド(決めた向きに、まっすぐ。壁があれば、そこまで)
    /// 最初に当たったプレイヤーをスタンさせ、当たったら即死攻撃へ、外れたら、少し待ってもう一度溜める
    /// </summary>
    private void FireTaser()
    {
        Vector3 origin = _drone.transform.position;
        float distance = _taserRange;
        if (Physics.Raycast(origin, _aimDirection, out RaycastHit wall, _taserRange, CS_PoliceLayers.obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            distance = wall.distance;
        }

        CS_PlayerHealth hitPlayer = FindFirstPlayerOnBeam(origin, distance);
        ShowLine(origin, origin + _aimDirection * distance, _beamColor, 0.12f, 0.15f);

        if (hitPlayer == null)
        {
            StartPhase(Phase.TaserRetry, _taserRetryInterval, CSE_SecurityDroneState.TaserCharge);
            return;
        }

        if (hitPlayer.TryGetComponent(out IStunnable stunnable)) stunnable.Stun(_drone.data.taserStunTime);
        _target = hitPlayer.transform;
        _drone.SetTarget(_target);
        StartPhase(Phase.Approach, 0.0f, CSE_SecurityDroneState.KillApproach);
    }

    /// <summary>
    /// 光線の上にいる、一番近いプレイヤーを探すメソッド(ドローン自身は除く)
    /// </summary>
    private CS_PlayerHealth FindFirstPlayerOnBeam(Vector3 origin, float distance)
    {
        int count = Physics.SphereCastNonAlloc(origin, _taserRadius, _aimDirection, _hitBuffer, distance,
            CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);

        CS_PlayerHealth nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            CS_PlayerHealth player = _hitBuffer[i].collider.GetComponentInParent<CS_PlayerHealth>();
            if (player == null || player.isDead || _hitBuffer[i].distance >= nearestDistance) continue;

            nearest = player;
            nearestDistance = _hitBuffer[i].distance;
        }
        return nearest;
    }

    /// <summary>
    /// 即死攻撃の範囲(円柱)の中にいるプレイヤーを、即死させるメソッド
    /// </summary>
    private void KillPlayersInRange()
    {
        Vector3 center = _killCenter + Vector3.up * (_killHeight * 0.5f);
        Vector3 halfExtents = new Vector3(_killRadius, _killHeight * 0.5f, _killRadius);
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, Quaternion.identity,
            CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            CS_PlayerHealth player = _overlapBuffer[i].GetComponentInParent<CS_PlayerHealth>();
            if (player == null || player.isDead || !IsInsideKillRadius(player.transform.position)) continue;

            player.TakeDamage(_killDamage, gameObject);
        }
    }

    // 位置が、即死攻撃の円の中(水平方向の距離が半径以内)か
    private bool IsInsideKillRadius(Vector3 position)
    {
        Vector3 offset = position - _killCenter;
        offset.y = 0.0f;
        return offset.sqrMagnitude <= _killRadius * _killRadius;
    }

    /// <summary>
    /// 位置の真下の地面の位置を求めるメソッド(地面が見つからなければ、そのままの位置)
    /// </summary>
    private static Vector3 GetGroundPosition(Vector3 position)
    {
        bool isHit = Physics.Raycast(position, Vector3.down, out RaycastHit hit, 30.0f,
            CS_PoliceLayers.obstacleLayers, QueryTriggerInteraction.Ignore);
        return isHit ? hit.point : position;
    }

    // ---------------------------------------------------------------
    // 見た目(全員の画面)
    // ---------------------------------------------------------------

    /// <summary>
    /// 光の線を、短い間だけ、全員の画面に出すメソッド(ネットワークに出現していなければ、自分の画面だけ)
    /// </summary>
    private void ShowLine(Vector3 from, Vector3 to, Color color, float width, float duration)
    {
        if (IsSpawned) ShowLineRpc(from, to, color, width, duration);
        else CreateLine(from, to, color, width, duration);
    }

    [Rpc(SendTo.Everyone)]
    private void ShowLineRpc(Vector3 from, Vector3 to, Color color, float width, float duration)
    {
        CreateLine(from, to, color, width, duration);
    }

    /// <summary>
    /// 2点を結ぶ光の線を作り、一定時間後に消すメソッド
    /// </summary>
    private void CreateLine(Vector3 from, Vector3 to, Color color, float width, float duration)
    {
        GameObject lineObject = new GameObject("TaserLine");
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.widthMultiplier = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (_lineMaterial != null) line.sharedMaterial = _lineMaterial;

        SetColor(line, color);
        Destroy(lineObject, duration);
    }

    /// <summary>
    /// 即死攻撃の範囲を示す、地面の円を作るメソッド(普段は非表示)
    /// </summary>
    private void CreateRing()
    {
        GameObject ringObject = new GameObject("KillRing");
        ringObject.transform.SetParent(transform, false);

        _ring = ringObject.AddComponent<LineRenderer>();
        _ring.useWorldSpace = true;
        _ring.loop = true;
        _ring.positionCount = _ringSegments;
        _ring.widthMultiplier = 0.1f;
        _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (_lineMaterial != null) _ring.sharedMaterial = _lineMaterial;
        _ring.enabled = false;
    }

    /// <summary>
    /// 即死攻撃を溜めている間だけ、地面の円を出し、赤と橙で点滅させるメソッド(全員の画面)
    /// 円の中心は、ドローンの真下の地面(溜めている間、ドローンは動かない)
    /// </summary>
    private void UpdateRing()
    {
        bool isShown = _drone.state == CSE_SecurityDroneState.KillCharge;
        if (_ring.enabled != isShown) _ring.enabled = isShown;
        if (!isShown) return;

        Vector3 center = GetGroundPosition(_drone.transform.position) + Vector3.up * _ringLift;
        for (int i = 0; i < _ringSegments; i++)
        {
            float angle = (float)i / _ringSegments * Mathf.PI * 2.0f;
            _ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * _killRadius);
        }

        bool isAlternate = Mathf.FloorToInt(Time.time / _ringBlinkInterval) % 2 == 1;
        SetColor(_ring, isAlternate ? _ringAlternateColor : _warningColor);
    }

    // 光の色を、MaterialPropertyBlockで上書きする
    private void SetColor(Renderer target, Color color)
    {
        target.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_unlitColorId, color);
        target.SetPropertyBlock(_propertyBlock);
    }
}
