/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-23 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警察のステータスを管理するScriptableObject
/// </summary>
[CreateAssetMenu(menuName = "Police/Status Data")]
public class CSO_PoliceStatus : ScriptableObject
{
    [Header("＝＝＝ 移動速度 ＝＝＝")]

    [SerializeField, Range(0.0f, 2.0f)]
    [Tooltip("巡回速度倍率")]
    private float _patrolSpeedMultiplier;
    public float patrolSpeedMultiplier => _patrolSpeedMultiplier;

    [SerializeField, Range(0.0f, 2.0f)]
    [Tooltip("追跡速度倍率")]
    private float _chaseSpeedMultiplier;
    public float chaseSpeedMultiplier => _chaseSpeedMultiplier;

    [Header("＝＝＝ 視野 ＝＝＝")]

    [SerializeField, Range(0.0f, 360.0f)]
    [Tooltip("発見視界の視野角度(実際に目視でき、見つけてしまう視界。警戒状態に移行する。ゲーム中に見える視界)")]
    private float _viewAngle;
    public float viewAngle => _viewAngle;

    [SerializeField, Min(0f)]
    [Tooltip("発見視界の視野距離(実際に目視でき、見つけてしまう視界。警戒状態に移行する。ゲーム中に見える視界)")]
    private float _viewDistance;
    public float viewDistance => _viewDistance;

    [SerializeField, Min(0f)]
    [Tooltip("視野の高さ(自分の目の高さから、上下にこの距離以内にいる標的だけが見える。二階など、高さの違う場所の標的を見つけないようにする)")]
    private float _viewHeight = 3.0f;
    public float viewHeight => _viewHeight;

    [SerializeField, Range(0.0f, 360.0f)]
    [Tooltip("巡回時の視界の視野角度(判定の大きい視界。プレイヤーが入ると、怪しんで近づいてくる。見つけたことにはならない。ゲーム中には見えない)")]
    private float _suspicionViewAngle = 120.0f;
    public float suspicionViewAngle => _suspicionViewAngle;

    [SerializeField, Min(0f)]
    [Tooltip("巡回時の視界の視野距離(発見視界より大きくすると使われる。0または発見視界以下なら、怪しんで近づく動きはしない)")]
    private float _suspicionViewDistance = 10.0f;
    public float suspicionViewDistance => _suspicionViewDistance;

    [Header("＝＝＝ 追いかけ方 ＝＝＝")]
    [SerializeField]
    [Tooltip("標的を追いかける方法(この警察は、常にこの方法で追いかける)。追尾: 標的の後ろを追う / 先回り: 標的の動く先へ回り込む")]
    private CSE_PoliceChaseStyle _chaseStyle = CSE_PoliceChaseStyle.Direct;
    public CSE_PoliceChaseStyle chaseStyle => _chaseStyle;

    [SerializeField, Min(0f)]
    [Tooltip("先回りで、標的の何秒先の位置を狙うかの上限(秒)。大きいほど、大きく回り込む")]
    private float _interceptMaxLeadTime = 2.0f;
    public float interceptMaxLeadTime => _interceptMaxLeadTime;

    [SerializeField, Min(0f)]
    [Tooltip("先回りで、標的にこの距離まで近づいたら、追尾に切り替える(m。標的を追い越さないようにする)")]
    private float _interceptCloseDistance = 5.0f;
    public float interceptCloseDistance => _interceptCloseDistance;

    [SerializeField, Min(0f)]
    [Tooltip("先回りで、標的の速さがこれより遅い(ほぼ止まっている)時は、追尾と同じ動きにする(m/秒)")]
    private float _interceptMinTargetSpeed = 0.5f;
    public float interceptMinTargetSpeed => _interceptMinTargetSpeed;

    [Header("＝＝＝ 攻撃力 ＝＝＝")]
    [SerializeField, Min(10000f)]
    [Tooltip("攻撃力(プレイヤーを一撃で倒すため、プレイヤーの最大体力より大きい値にする)")]
    private float _attackPower;
    public float attackPower => _attackPower;

}
