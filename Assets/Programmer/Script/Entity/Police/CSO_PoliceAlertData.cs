/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警戒状態(プレイヤーが警察陣営に見つかった状態)の設定を管理するScriptableObject
/// </summary>
[CreateAssetMenu(menuName = "Police/Alert Data")]
public class CSO_PoliceAlertData : ScriptableObject
{
    [SerializeField, Min(0f)]
    [Tooltip("付近の範囲(m)。プレイヤーが見つかった場所からこの距離以内にいる警察が、その場所へ向かう")]
    private float _alertRadius = 30.0f;
    public float alertRadius => _alertRadius;

    [SerializeField, Min(0.1f)]
    [Tooltip("警戒状態が続く時間(秒)。警戒状態になってから、この時間の間、新たに警察陣営に見つからなければ解除される(見つかるたびに数え直す)")]
    private float _alertDuration = 20.0f;
    public float alertDuration => _alertDuration;
}
