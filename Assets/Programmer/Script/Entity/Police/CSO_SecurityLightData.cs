/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// ストリートライトのデータ(データ表「防犯装置データ」)を管理するScriptableObject
/// ライトの大きさ・照らす高さは、置く場所ごとに変えられるよう、CS_SecurityLightのインスペクタで設定する
/// </summary>
[CreateAssetMenu(menuName = "Police/Security Light Data")]
public class CSO_SecurityLightData : ScriptableObject
{
    [SerializeField, Min(0f)]
    [Tooltip("移動速度(プレイヤーの通常移動速度に対する倍率)")]
    private float _speedMultiplier = 0.3f;
    public float speedMultiplier => _speedMultiplier;

    [SerializeField, Min(0.1f)]
    [Tooltip("プレイヤーが照らされてから、赤と青で点滅する時間(秒)。同じプレイヤーは、この間は照らされ直さない")]
    private float _flashDuration = 2.0f;
    public float flashDuration => _flashDuration;
}
