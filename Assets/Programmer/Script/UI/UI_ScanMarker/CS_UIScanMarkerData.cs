/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-02 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// スキャンの目印1つ分の表示内容
/// </summary>
public readonly struct CS_UIScanMarkerData
{
    public readonly CSE_UIScanTargetType type;  // 相手の種類
    public readonly Vector3 worldPosition;      // 相手の足元の位置(悪人グループはメンバーの中心)
    public readonly float distance;             // 自分からの距離(m)
    public readonly float crimeProgress;        // 犯罪完遂度(0〜1)。悪人グループだけ使う

    public CS_UIScanMarkerData(
        CSE_UIScanTargetType type,
        Vector3 worldPosition,
        float distance,
        float crimeProgress = 0f)
    {
        this.type = type;
        this.worldPosition = worldPosition;
        this.distance = distance;
        this.crimeProgress = Mathf.Clamp01(crimeProgress);
    }
}
