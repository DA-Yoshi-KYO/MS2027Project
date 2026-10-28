/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UICanvas上にスキャンの目印(悪人の頭上の印)を並べるView。
/// 見た目は仮(赤い菱形)。目印の見た目は _markerTemplate を差し替える
///
/// ・_markerTemplate を必要な数だけ複製して使い回す(テンプレート自身は非表示のまま)
/// </summary>
public class CS_UIScanMarkerView : CS_BaseView<CS_UIScanMarkerPresenter>
{
    [Header("目印")]
    [SerializeField] private RectTransform _markerTemplate;    // 複製元の目印(非表示にしておく)

    private readonly List<RectTransform> _markers = new List<RectTransform>();

    private void Awake()
    {
        if (_markerTemplate != null)
            _markerTemplate.gameObject.SetActive(false);
    }

    /// <summary>
    /// index番目の目印を、Canvas上の座標に出す(足りなければ作る)
    /// 0番から順に呼び、最後に HideFrom で残りを隠す
    /// </summary>
    public void ShowMarker(int index, Vector2 anchoredPosition)
    {
        RectTransform marker = GetMarker(index);
        if (marker == null)
            return;

        marker.anchoredPosition = anchoredPosition;
        if (!marker.gameObject.activeSelf)
            marker.gameObject.SetActive(true);
    }

    /// <summary>
    /// index番目以降の目印を隠す(HideFrom(0) ですべて隠す)
    /// </summary>
    public void HideFrom(int index)
    {
        for (int i = index; i < _markers.Count; i++)
        {
            if (_markers[i].gameObject.activeSelf)
                _markers[i].gameObject.SetActive(false);
        }
    }

    private RectTransform GetMarker(int index)
    {
        if (_markerTemplate == null)
            return null;

        while (_markers.Count <= index)
        {
            RectTransform marker = Instantiate(_markerTemplate, _markerTemplate.parent);
            marker.gameObject.SetActive(false);
            _markers.Add(marker);
        }

        return _markers[index];
    }
}
