/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UICanvas上のハイドマーカー(飛べるハイドポイントの目印)を表示するView。
/// 見た目は仮(水色の四角)。本番のアイコンができたら _markerImage の Sprite を差し替える
/// </summary>
public class CS_UIHideMarkerView : CS_BaseView<CS_UIHideMarkerPresenter>
{
    [Header("ハイドマーカー")]
    [SerializeField] private RectTransform _markerRect;
    [SerializeField] private Image _markerImage;

    private void Awake()
    {
        if (_markerImage != null)
            _markerImage.raycastTarget = false;

        SetVisible(false);
    }

    /// <summary>
    /// 目印の表示・非表示を切り替える
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        if (_markerRect == null)
            return;

        if (_markerRect.gameObject.activeSelf != isVisible)
            _markerRect.gameObject.SetActive(isVisible);
    }

    /// <summary>
    /// 目印の位置(Canvas上の座標)を設定する
    /// </summary>
    public void SetPosition(Vector2 anchoredPosition)
    {
        if (_markerRect != null)
            _markerRect.anchoredPosition = anchoredPosition;
    }
}
