/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * 2026-11-02 | 種類ごとのアイコン・距離・犯罪完遂度を出すようにした
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UICanvas上にスキャンの目印(悪人グループ・警察・巡回ドローンの頭上の印)を並べるView。
/// 見た目は仕様書(Confluence「UI」スキャンUI)の画像。画像はここの Inspector で設定する
///
/// ・_markerTemplate を必要な数だけ複製して使い回す(テンプレート自身は非表示のまま)
/// ・目印1つ分の表示は CS_UIScanMarkerItemView が行う(テンプレートに付いていなければ実行時に付ける)
/// ・悪人グループは、背景の三角に完遂度の分だけ赤い三角を下から溜め、悪人のマークを重ねる
/// ・警察・巡回ドローンは、それぞれのアイコンを1枚出す
/// </summary>
public class CS_UIScanMarkerView : CS_BaseView<CS_UIScanMarkerPresenter>
{
    [Header("目印")]
    [SerializeField] private RectTransform _markerTemplate;    // 複製元の目印(非表示にしておく)

    [Header("悪人グループ")]
    [SerializeField] private Sprite _villainBackgroundEmpty;   // 背景(灰色の三角)  Icon_Villain_BackGroundEmpty
    [SerializeField] private Sprite _villainBackgroundFull;    // 完遂度の分だけ溜まる赤い三角  Icon_Villain_BackGroundFull
    [SerializeField] private Sprite _villainMark;              // 背景に重ねる悪人のマーク  Icon_Villain

    [Header("警察・巡回ドローン")]
    [SerializeField] private Sprite _policeIcon;   // Icon_Police
    [SerializeField] private Sprite _droneIcon;    // Icon_Drone

    private readonly List<CS_UIScanMarkerItemView> _markers = new List<CS_UIScanMarkerItemView>();

    private void Awake()
    {
        if (_markerTemplate != null)
            _markerTemplate.gameObject.SetActive(false);
    }

    /// <summary>
    /// index番目の目印を、Canvas上の座標に出す(足りなければ作る)
    /// 0番から順に呼び、最後に HideFrom で残りを隠す
    /// </summary>
    public void ShowMarker(int index, Vector2 anchoredPosition, CS_UIScanMarkerData data)
    {
        CS_UIScanMarkerItemView marker = GetMarker(index);
        if (marker == null)
            return;

        ((RectTransform)marker.transform).anchoredPosition = anchoredPosition;

        switch (data.type)
        {
            case CSE_UIScanTargetType.Police:
                marker.ApplyIcon(_policeIcon, data.distance);
                break;
            case CSE_UIScanTargetType.Drone:
                marker.ApplyIcon(_droneIcon, data.distance);
                break;
            default:
                marker.ApplyVillain(
                    _villainBackgroundEmpty,
                    _villainBackgroundFull,
                    _villainMark,
                    data.distance,
                    data.crimeProgress);
                break;
        }

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

    private CS_UIScanMarkerItemView GetMarker(int index)
    {
        if (_markerTemplate == null)
            return null;

        while (_markers.Count <= index)
        {
            RectTransform marker = Instantiate(_markerTemplate, _markerTemplate.parent);
            marker.gameObject.SetActive(false);

            // テンプレートに付いていなければ付ける(仮の見た目はItemViewが作る)
            if (!marker.TryGetComponent(out CS_UIScanMarkerItemView item))
                item = marker.gameObject.AddComponent<CS_UIScanMarkerItemView>();

            _markers.Add(item);
        }

        return _markers[index];
    }
}
