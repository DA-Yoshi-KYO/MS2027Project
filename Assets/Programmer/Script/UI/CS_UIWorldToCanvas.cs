/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// ワールド座標を、Canvas上の座標(anchoredPosition)へ変換する処理をまとめたクラス。
/// ワールドの物の位置に目印を出すUIのPresenter(ハイドマーカー・スキャンの目印)が使う
/// </summary>
public static class CS_UIWorldToCanvas
{
    /// <summary>
    /// ワールド座標をCanvas上の座標へ変換する
    /// カメラの後ろ・画面の外にある時は false を返す
    /// </summary>
    public static bool TryConvert(
        Camera worldCamera,
        Canvas canvas,
        RectTransform canvasRect,
        Vector3 worldPosition,
        out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        if (worldCamera == null || canvasRect == null)
            return false;

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);

        // カメラの後ろには出さない
        if (screenPosition.z <= 0f)
            return false;

        bool isOnScreen =
            screenPosition.x >= 0f &&
            screenPosition.x <= Screen.width &&
            screenPosition.y >= 0f &&
            screenPosition.y <= Screen.height;
        if (!isOnScreen)
            return false;

        // Screen Space - Overlay の時はカメラを渡さない
        Camera uiCamera =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            uiCamera,
            out localPosition);
    }
}
