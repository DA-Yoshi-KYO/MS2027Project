/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 防犯レーザーのデータ(データ表「防犯装置データ」)を管理するScriptableObject
/// </summary>
[CreateAssetMenu(menuName = "Police/Security Laser Data")]
public class CSO_SecurityLaserData : ScriptableObject
{
    [SerializeField, Min(0f)]
    [Tooltip("移動速度(プレイヤーの通常移動速度に対する倍率)")]
    private float _speedMultiplier = 0.6f;
    public float speedMultiplier => _speedMultiplier;

    [SerializeField, Min(0f)]
    [Tooltip("プレイヤーが当たった時に減らすHP")]
    private float _damage = 1.0f;
    public float damage => _damage;

    [SerializeField, Min(0.1f)]
    [Tooltip("プレイヤーが当たってから、赤と青で点滅する時間(秒)。同じプレイヤーには、この間は当たり直さない")]
    private float _flashDuration = 2.0f;
    public float flashDuration => _flashDuration;
}
