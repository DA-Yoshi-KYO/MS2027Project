/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ストリートライト(警察陣営の防犯装置)のクラス。プランナーが指定した場所に置いて使う
/// ・円形のライトが、メインストリートを、指定した向きに指定した距離(0〜50m)だけ往復して照らす(距離0なら、その場に留まる)
/// ・ライトは、ライトの位置から上へ「照らす高さ」の範囲の中で、一番高い床を照らす(二階の床などが範囲に入れば、そこを照らす)
///   床が変わる場所を動けば、その時の一番高い床に円が乗る。照らされるのは、その床の上にいるプレイヤー
///   屋根がある場所では、屋根が一番高い床になるので、屋根の下にいるプレイヤーは照らされない(屋根の上にいれば照らされる)
/// ・プレイヤーが照らされると、警察陣営を警戒状態にし、ライトが赤と青で点滅する(その後は元の色に戻る)
/// ・点灯・消灯を繰り返す設定にできる(消えている間は、照らされない。タイミングよく通り抜けるギミックに使う)
/// 位置と点灯は同期された時刻から計算するので、全員の画面で同じ動きになる(NetworkObjectは不要)
/// 照らされたかの判定と点滅は各マシンで行い、警戒状態にするのはサーバー(またはオフライン)だけで行う
/// </summary>
public class CS_SecurityLight : MonoBehaviour
{
    // 当たりを集める際に使い回すバッファ
    private readonly Collider[] _overlapBuffer = new Collider[16];

    // プレイヤーごとの、次に照らされ得る時刻(点滅している間は、照らされ直さない)
    private readonly Dictionary<CS_PlayerHealth, float> _nextHitTimes = new Dictionary<CS_PlayerHealth, float>();

    // 置かれた時の位置と向き(往復の基準)
    private Vector3 _originPosition = Vector3.zero;
    private Quaternion _originRotation = Quaternion.identity;

    // 1秒あたりに進む距離(m)
    private float _speed = 0.0f;

    // 今、ライトが照らしている床の高さ(ワールドのY)
    private float _floorY = 0.0f;

    // 床を探す時の、ライトの位置より下を調べる距離(m。ライトを地面にぴったり置いても、床を見つけられるようにする)
    private const float _floorSearchDepth = 1.0f;

    // 床に重ならないよう、ライトの円盤を床から浮かせる高さ(m)
    private const float _discHeightOffset = 0.05f;

    // 照らされるプレイヤーの足元が、一番高い床から、この距離(m)より下にいない・体の高さ(m)より上にいないこと
    private const float _floorTolerance = 0.5f;
    private const float _litBodyHeight = 2.5f;

    [Header("＝＝＝ データ ＝＝＝")]
    [SerializeField]
    [Tooltip("ストリートライトのデータ(速度の倍率・点滅の時間)")]
    private CSO_SecurityLightData _data = null;

    [SerializeField]
    [Tooltip("速度の基準にするプレイヤーのステータス(moveSpeedを基準速度にする)")]
    private CSO_PlayerStats _playerBaseStats = null;

    [Header("＝＝＝ 動き(置く場所ごとに設定) ＝＝＝")]
    [SerializeField]
    [Tooltip("動く向き(このオブジェクトの向き基準。X: 右 / Y: 上 / Z: 前)。左右・縦・斜めなど")]
    private Vector3 _moveDirection = Vector3.forward;

    [SerializeField, Range(0f, 100f)]
    [Tooltip("動く距離(m。置いた場所から、この距離まで往復する。0ならその場に留まる)")]
    private float _moveDistance = 5.0f;

    [Header("＝＝＝ ライトの大きさ(置く場所ごとに設定) ＝＝＝")]
    [SerializeField, Min(0.5f)]
    [Tooltip("ライトの大きさ(照らす円の直径。m。ライトごとに変えられる)")]
    private float _diameter = 10.0f;

    [SerializeField, Min(0.5f)]
    [Tooltip("ライトが照らす高さ(m。ライトの位置から上へ、この高さの範囲の中で、一番高い床を照らす。範囲の外の床(二階など)は照らさない)")]
    private float _lightHeight = 3.0f;

    [Header("＝＝＝ 点灯・消灯(置く場所ごとに設定) ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("点灯している時間(秒)")]
    private float _onDuration = 2.0f;

    [SerializeField, Min(0f)]
    [Tooltip("消灯している時間(秒。0なら、ずっと点灯したまま。0より大きいと、点灯と消灯を繰り返す)")]
    private float _offDuration = 0.0f;

    [Header("＝＝＝ 参照 ＝＝＝")]
    [SerializeField]
    [Tooltip("ライトの見た目(床を照らす円。大きさをライトの大きさに合わせ、照らす床の高さに置く)")]
    private Transform _disc = null;

    [SerializeField]
    [Tooltip("ライトの色を変えるクラス")]
    private CS_SecurityBlink _blink = null;

    [SerializeField]
    [Tooltip("ライトの見た目のRenderer(消灯中は消す)")]
    private Renderer _discRenderer = null;

    private void Awake()
    {
        _originPosition = transform.position;
        _originRotation = transform.rotation;

        float baseSpeed = _playerBaseStats != null ? _playerBaseStats.moveSpeed : 0.0f;
        float multiplier = _data != null ? _data.speedMultiplier : 0.0f;
        _speed = baseSpeed * multiplier;

        ApplyDiscShape();
    }

    private void OnValidate()
    {
        ApplyDiscShape();
    }

    private void Update()
    {
        double time = GetTime();
        transform.position = GetPosition(time);
        UpdateFloorHeight();

        // 消灯中は、見た目を消す
        if (_discRenderer != null) _discRenderer.enabled = IsLit(time);
    }

    private void FixedUpdate()
    {
        if (_data == null || !IsLit(GetTime())) return;

        // ライトの円の上(床の探索範囲)にいるプレイヤーを集める
        float radius = _diameter * 0.5f;
        float bottom = transform.position.y - _floorSearchDepth - _floorTolerance;
        float top = transform.position.y + _lightHeight + _litBodyHeight;
        Vector3 center = new Vector3(transform.position.x, (bottom + top) * 0.5f, transform.position.z);
        Vector3 halfExtents = new Vector3(radius, (top - bottom) * 0.5f, radius);
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, Quaternion.identity,
            CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            CS_PlayerHealth player = _overlapBuffer[i].GetComponentInParent<CS_PlayerHealth>();
            if (player == null || player.isDead || !IsInsideCircle(player.transform.position, radius)) continue;
            if (!IsStandingOnLitFloor(_overlapBuffer[i].bounds.min.y, player.transform.position) || !CanHit(player)) continue;

            OnLit(player);
        }
    }

    /// <summary>
    /// プレイヤーが、ライトに照らされる床の上にいるかを判定するメソッド
    /// プレイヤーの真上から真下へ(ライトの位置から上へ「照らす高さ」の範囲を)調べ、最初に当たった面(=一番高い床)に足元が乗っていれば照らされる
    /// 屋根などがあれば、その屋根が一番高い床になるので、屋根の下にいるプレイヤーは照らされない(屋根の上にいれば照らされる)
    /// </summary>
    /// <param name="feetY">プレイヤーの足元の高さ(ワールドのY)</param>
    /// <param name="position">プレイヤーの位置</param>
    /// <returns>照らされるならtrue</returns>
    private bool IsStandingOnLitFloor(float feetY, Vector3 position)
    {
        float baseY = transform.position.y;
        Vector3 origin = new Vector3(position.x, baseY + _lightHeight, position.z);
        bool isHit = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _lightHeight + _floorSearchDepth,
            CS_PoliceLayers.obstacleLayers, QueryTriggerInteraction.Ignore);
        float floorY = isHit ? hit.point.y : baseY;

        return feetY >= floorY - _floorTolerance && feetY <= floorY + _litBodyHeight;
    }

    /// <summary>
    /// 時刻から、ライトの位置を求めるメソッド(置いた場所から指定した向きへ、指定した距離まで往復する)
    /// </summary>
    private Vector3 GetPosition(double time)
    {
        Vector3 direction = _originRotation * _moveDirection.normalized;
        float offset = Mathf.PingPong((float)(time * _speed), _moveDistance);
        return _originPosition + direction * offset;
    }

    /// <summary>
    /// 照らす床の高さを求め、ライトの見た目をその床の上に置くメソッド
    /// ライトの位置から上へ「照らす高さ」(と、足元の少し下)の範囲を、上から下へ調べ、最初に当たった面(=一番高い床)を照らす
    /// 何も当たらなければ、ライトの位置の高さを照らす
    /// </summary>
    private void UpdateFloorHeight()
    {
        Vector3 origin = transform.position + Vector3.up * _lightHeight;
        bool isHit = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _lightHeight + _floorSearchDepth,
            CS_PoliceLayers.obstacleLayers, QueryTriggerInteraction.Ignore);
        _floorY = isHit ? hit.point.y : transform.position.y;

        if (_disc == null) return;

        Vector3 discPosition = _disc.position;
        discPosition.y = _floorY + _discHeightOffset;
        _disc.position = discPosition;
    }

    /// <summary>
    /// 時刻から、点灯しているかを求めるメソッド(消灯の時間が0なら、常に点灯)
    /// </summary>
    private bool IsLit(double time)
    {
        float cycle = _onDuration + _offDuration;
        if (_offDuration <= 0.0f || cycle <= 0.0f) return true;

        return time % cycle < _onDuration;
    }

    /// <summary>
    /// 往復と点灯の基準にする時刻を取得するメソッド(ネットワーク接続中は、全員で揃った時刻)
    /// </summary>
    private static double GetTime()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager != null && manager.IsListening ? manager.ServerTime.Time : Time.timeAsDouble;
    }

    /// <summary>
    /// 位置が、ライトの円の中(水平方向の距離が半径以内)かを判定するメソッド
    /// </summary>
    private bool IsInsideCircle(Vector3 position, float radius)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0.0f;
        return offset.sqrMagnitude <= radius * radius;
    }

    /// <summary>
    /// プレイヤーを照らせるか(前に照らしてから、点滅の時間が経っているか)を判定し、次の時刻を更新するメソッド
    /// </summary>
    private bool CanHit(CS_PlayerHealth player)
    {
        if (_nextHitTimes.TryGetValue(player, out float nextTime) && Time.time < nextTime) return false;

        _nextHitTimes[player] = Time.time + _data.flashDuration;
        return true;
    }

    /// <summary>
    /// プレイヤーが照らされた時の処理(点滅は各マシンで、警戒状態にするのはサーバーだけ)
    /// </summary>
    private void OnLit(CS_PlayerHealth player)
    {
        if (_blink != null) _blink.StartFlash(_data.flashDuration);

        if (CS_PoliceSquad.IsNetworkClientOnly()) return;

        CS_PoliceAlertManager.Report(player, player.transform.position);
    }

    /// <summary>
    /// ライトの見た目を、ライトの大きさ(直径)に合わせるメソッド(薄い円盤)
    /// </summary>
    private void ApplyDiscShape()
    {
        if (_disc == null) return;

        _disc.localPosition = Vector3.up * _discHeightOffset;
        _disc.localRotation = Quaternion.identity;
        _disc.localScale = new Vector3(_diameter, 0.01f, _diameter);
    }

    private void OnDrawGizmosSelected()
    {
        // 動く範囲(両端のライトと、その間の線)をSceneビューに表示する
        Vector3 origin = Application.isPlaying ? _originPosition : transform.position;
        Quaternion rotation = Application.isPlaying ? _originRotation : transform.rotation;
        Vector3 end = origin + rotation * _moveDirection.normalized * _moveDistance;

        Gizmos.color = new Color(1.0f, 0.9f, 0.3f, 0.9f);
        Gizmos.DrawLine(origin, end);
        DrawCylinder(origin, _diameter * 0.5f, _lightHeight);
        DrawCylinder(end, _diameter * 0.5f, _lightHeight);
    }

    /// <summary>
    /// 照らす範囲(円柱)をGizmosで描くメソッド
    /// </summary>
    private static void DrawCylinder(Vector3 bottom, float radius, float height)
    {
        Vector3 top = bottom + Vector3.up * height;
        Gizmos.DrawWireSphere(bottom, 0.1f);
        DrawCircle(bottom, radius);
        DrawCircle(top, radius);
        foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            Gizmos.DrawLine(bottom + direction * radius, top + direction * radius);
        }
    }

    /// <summary>
    /// 水平な円をGizmosで描くメソッド
    /// </summary>
    private static void DrawCircle(Vector3 center, float radius)
    {
        const int segments = 32;
        Vector3 previous = center + Vector3.right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2.0f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
