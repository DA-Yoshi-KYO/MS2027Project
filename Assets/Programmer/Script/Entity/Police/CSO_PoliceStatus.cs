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
    [Tooltip("視野角度")]
    private float _viewAngle;
    public float viewAngle => _viewAngle;

    [SerializeField, Min(0f)]
    [Tooltip("視野距離")]
    private float _viewDistance;
    public float viewDistance => _viewDistance;

    [Header("＝＝＝ 攻撃力 ＝＝＝")]
    [SerializeField, Min(10000f)/**/]
    [Tooltip("攻撃力(プレイヤーを一撃で倒すため、プレイヤーの最大体力より大きい値にする)")]
    private float _attackPower;
    public float attackPower => _attackPower;

}
