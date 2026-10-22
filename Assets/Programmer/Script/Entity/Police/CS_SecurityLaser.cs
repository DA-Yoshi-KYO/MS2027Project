/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 防犯レーザー(警察陣営の防犯装置)のクラス。プランナーが指定した場所に置いて使う
/// ・棒状のレーザーが、指定した方向に指定した距離を往復する(距離0なら、その場に留まる)
/// ・プレイヤーが当たると、HPを減らし、警察陣営を警戒状態にし、赤と青で点滅する(その後は青に戻る)
/// 位置は同期された時刻から計算するので、全員の画面で同じ動きになる(NetworkObjectは不要)
/// 当たりの判定は各マシンで行い、点滅は各マシンの画面に出す
/// HPを減らす・警戒状態にするのは、サーバー(またはオフライン)だけで行う
/// </summary>
public class CS_SecurityLaser : MonoBehaviour
{
    // 当たりを集める際に使い回すバッファ
    private readonly Collider[] _overlapBuffer = new Collider[16];

    // プレイヤーごとの、次に当たれる時刻(点滅している間は、当たり直さない)
    private readonly Dictionary<CS_PlayerHealth, float> _nextHitTimes = new Dictionary<CS_PlayerHealth, float>();

    // 置かれた時の位置と向き(往復の基準)
    private Vector3 _originPosition = Vector3.zero;
    private Quaternion _originRotation = Quaternion.identity;

    // 1秒あたりに進む距離(m)
    private float _speed = 0.0f;

    [Header("＝＝＝ データ ＝＝＝")]
    [SerializeField]
    [Tooltip("防犯レーザーのデータ(速度の倍率・ダメージ・点滅の時間)")]
    private CSO_SecurityLaserData _data = null;

    [SerializeField]
    [Tooltip("速度の基準にするプレイヤーのステータス(moveSpeedを基準速度にする)")]
    private CSO_PlayerStats _playerBaseStats = null;

    [Header("＝＝＝ 動き(置く場所ごとに設定) ＝＝＝")]
    [SerializeField]
    [Tooltip("動く向き(このオブジェクトの向き基準。X: 右 / Y: 上 / Z: 前)。左右・縦・斜めなど")]
    private Vector3 _moveDirection = Vector3.forward;

    [SerializeField, Range(0f, 50f)]
    [Tooltip("動く距離(m。置いた場所から、この距離まで往復する。0ならその場に留まる)")]
    private float _moveDistance = 5.0f;

    [Header("＝＝＝ レーザーの形 ＝＝＝")]
    [SerializeField, Min(0.1f)]
    [Tooltip("レーザーの長さ(m。このオブジェクトの右向きに伸びる)")]
    private float _beamLength = 10.0f;

    [SerializeField, Min(0.05f)]
    [Tooltip("レーザーの太さ(m)")]
    private float _beamThickness = 0.3f;

    [Header("＝＝＝ 参照 ＝＝＝")]
    [SerializeField]
    [Tooltip("レーザーの見た目(長さ・太さに合わせて大きさを変える)")]
    private Transform _beam = null;

    [SerializeField]
    [Tooltip("レーザーの色を変えるクラス")]
    private CS_SecurityBlink _blink = null;

    private void Awake()
    {
        _originPosition = transform.position;
        _originRotation = transform.rotation;

        float baseSpeed = _playerBaseStats != null ? _playerBaseStats.moveSpeed : 0.0f;
        float multiplier = _data != null ? _data.speedMultiplier : 0.0f;
        _speed = baseSpeed * multiplier;

        ApplyBeamShape();
    }

    private void OnValidate()
    {
        ApplyBeamShape();
    }

    private void Update()
    {
        transform.position = GetPosition(GetTime());
    }

    private void FixedUpdate()
    {
        if (_data == null) return;

        // レーザーの箱に入っているプレイヤーを調べる
        Vector3 halfExtents = new Vector3(_beamLength * 0.5f, _beamThickness * 0.5f, _beamThickness * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(transform.position, halfExtents, _overlapBuffer, transform.rotation,
            CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            CS_PlayerHealth player = _overlapBuffer[i].GetComponentInParent<CS_PlayerHealth>();
            if (player == null || player.isDead || !CanHit(player)) continue;

            OnTouched(player);
        }
    }

    /// <summary>
    /// 時刻から、レーザーの位置を求めるメソッド(置いた場所から指定した向きへ、指定した距離まで往復する)
    /// </summary>
    private Vector3 GetPosition(double time)
    {
        Vector3 direction = _originRotation * _moveDirection.normalized;
        float offset = Mathf.PingPong((float)(time * _speed), _moveDistance);
        return _originPosition + direction * offset;
    }

    /// <summary>
    /// 往復の基準にする時刻を取得するメソッド(ネットワーク接続中は、全員で揃った時刻)
    /// </summary>
    private static double GetTime()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager != null && manager.IsListening ? manager.ServerTime.Time : Time.timeAsDouble;
    }

    /// <summary>
    /// プレイヤーに当たれるか(前に当たってから、点滅の時間が経っているか)を判定し、当たる時刻を更新するメソッド
    /// </summary>
    private bool CanHit(CS_PlayerHealth player)
    {
        if (_nextHitTimes.TryGetValue(player, out float nextTime) && Time.time < nextTime) return false;

        _nextHitTimes[player] = Time.time + _data.flashDuration;
        return true;
    }

    /// <summary>
    /// プレイヤーが当たった時の処理(点滅は各マシンで、HPを減らす・警戒状態にするのはサーバーだけ)
    /// </summary>
    private void OnTouched(CS_PlayerHealth player)
    {
        if (_blink != null) _blink.StartFlash(_data.flashDuration);

        if (CS_PoliceSquad.IsNetworkClientOnly()) return;

        player.TakeDamage(_data.damage, gameObject);
        CS_PoliceAlertManager.Report(player, player.transform.position);
    }

    /// <summary>
    /// レーザーの見た目を、長さ・太さに合わせるメソッド
    /// </summary>
    private void ApplyBeamShape()
    {
        if (_beam == null) return;

        _beam.localPosition = Vector3.zero;
        _beam.localRotation = Quaternion.identity;
        _beam.localScale = new Vector3(_beamLength, _beamThickness, _beamThickness);
    }

    private void OnDrawGizmosSelected()
    {
        // 動く範囲(両端のレーザーと、その間の線)をSceneビューに表示する
        Vector3 origin = Application.isPlaying ? _originPosition : transform.position;
        Quaternion rotation = Application.isPlaying ? _originRotation : transform.rotation;
        Vector3 end = origin + rotation * _moveDirection.normalized * _moveDistance;
        Vector3 size = new Vector3(_beamLength, _beamThickness, _beamThickness);

        Gizmos.color = new Color(1.0f, 0.3f, 0.3f, 0.9f);
        Gizmos.DrawLine(origin, end);
        Gizmos.matrix = Matrix4x4.TRS(origin, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.TRS(end, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
