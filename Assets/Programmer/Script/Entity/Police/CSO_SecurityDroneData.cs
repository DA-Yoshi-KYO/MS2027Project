/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 巡回ドローンのデータ(データ表「防犯装置データ」)を管理するScriptableObject
/// </summary>
[CreateAssetMenu(menuName = "Police/Security Drone Data")]
public class CSO_SecurityDroneData : ScriptableObject
{
    [Header("＝＝＝ 移動速度(プレイヤーの通常移動速度に対する倍率) ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("巡回速度の倍率")]
    private float _patrolSpeedMultiplier = 0.3f;
    public float patrolSpeedMultiplier => _patrolSpeedMultiplier;

    [SerializeField, Min(0f)]
    [Tooltip("追跡速度の倍率(プレイヤーのダッシュより遅いので、逃げ切れる)")]
    private float _chaseSpeedMultiplier = 0.5f;
    public float chaseSpeedMultiplier => _chaseSpeedMultiplier;

    [Header("＝＝＝ 視界 ＝＝＝")]
    [SerializeField, Range(0f, 360f)]
    [Tooltip("視野角度(視野全体の幅)")]
    private float _viewAngle = 120.0f;
    public float viewAngle => _viewAngle;

    [SerializeField, Min(0f)]
    [Tooltip("視野距離(m)")]
    private float _viewDistance = 5.0f;
    public float viewDistance => _viewDistance;

    [SerializeField, Min(0f)]
    [Tooltip("視野の高さ(自分の目の高さから、上下にこの距離以内にいる標的だけが見える。空中にいるので、警察より広めにする)")]
    private float _viewHeight = 6.0f;
    public float viewHeight => _viewHeight;

    [Header("＝＝＝ 追跡 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("プレイヤーを見失ってから、しばらくついてくる時間(秒)")]
    private float _chaseDuration = 6.0f;
    public float chaseDuration => _chaseDuration;

    [Header("＝＝＝ 制圧モード(プレイヤーを攻撃する) ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("モード移行にかかる時間(秒)。プレイヤーを見ている時間がこの長さたまると、制圧モードに入る")]
    private float _modeTransitionTime = 15.0f;
    public float modeTransitionTime => _modeTransitionTime;

    [SerializeField, Min(0f)]
    [Tooltip("テーザーの攻撃の溜め時間(秒)。プレイヤーを追いながら溜める")]
    private float _taserChargeTime = 2.0f;
    public float taserChargeTime => _taserChargeTime;

    [SerializeField, Min(0f)]
    [Tooltip("テーザーが当たったプレイヤーが、スタン(動けない状態)になる時間(秒)")]
    private float _taserStunTime = 10.0f;
    public float taserStunTime => _taserStunTime;

    [SerializeField, Min(0f)]
    [Tooltip("即死攻撃の溜め時間(秒)。この間に範囲から逃げられなかったプレイヤーは即死する")]
    private float _killChargeTime = 5.0f;
    public float killChargeTime => _killChargeTime;

    [SerializeField, Min(0f)]
    [Tooltip("攻撃後の隙(秒)。この間、ドローンは動かず、攻撃もしない")]
    private float _recoveryTime = 5.0f;
    public float recoveryTime => _recoveryTime;

    [Header("＝＝＝ スタン ＝＝＝")]
    [SerializeField, Min(0.1f)]
    [Tooltip("背中から攻撃された時に、ドローンが動かなくなる時間(秒。停止によるスタン時間)")]
    private float _stunDuration = 5.0f;
    public float stunDuration => _stunDuration;

    [SerializeField, Range(0f, 360f)]
    [Tooltip("背中とみなす範囲の角度(真後ろを中心にした全体の幅。この範囲の方向からの攻撃だけがスタンさせる)")]
    private float _backAngle = 120.0f;
    public float backAngle => _backAngle;
}
