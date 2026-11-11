/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-29 | 初回作成(Issue #342)
 * 2026-10-30 | 固定のボタンをやめ、エリアの数だけ印(CS_UIAreaSelectMarker)を生成してエリアの上に置くようにした
 * ================================================ */

using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;

/// <summary>
/// エリア選択シーン(AreaSelect)の画面の View。
/// エリアの印・残り時間の表示を持ち、印が押されたことを Observable で外へ知らせるだけ(処理は持たない)
/// </summary>
// ========================================
/*
 * メモ
 * ・背景は見下ろしカメラで映したフィールド。その上に、エリアごとの印を_markerRootの子として生成する
 *   _markerRootは画面いっぱいのRectTransformにしておく(見下ろしカメラのビューポート座標をそのまま使うため)
 * ・印の番号がエリアの番号(0〜)。印の名前は「Area 番号」(仮の見た目)
 * ・印はCreateMarkersで作る(Presenterがエリアの数を渡す)
 */
// ========================================
public class CS_UIAreaSelectView : CS_BaseView<CS_UIAreaSelectPresenter>
{
    [SerializeField][Tooltip("印を並べる親(画面いっぱいのRectTransform)")] private RectTransform _markerRoot;
    [SerializeField][Tooltip("エリアの印のプレハブ")] private CS_UIAreaSelectMarker _markerPrefab;
    [SerializeField][Tooltip("残り時間の表示")] private TMP_Text _remainingTimeText;

    private readonly List<CS_UIAreaSelectMarker> _markers = new List<CS_UIAreaSelectMarker>();
    private readonly Subject<int> _onAreaClicked = new Subject<int>();

    public Observable<int> onAreaClicked => _onAreaClicked;

    private void OnDestroy()
    {
        _onAreaClicked.Dispose();
    }

    /// <summary>
    /// エリアの数だけ印を作る(位置はSetAreaScreenPositionで決まるまで親の中央)
    /// </summary>
    public void CreateMarkers(int areaCount)
    {
        for (int i = 0; i < areaCount; i++)
        {
            int area = i;   // ラムダで使うので、ループ変数をコピーしておく
            CS_UIAreaSelectMarker marker = Instantiate(_markerPrefab, _markerRoot);
            marker.name = $"AreaMarker{area}";
            marker.SetName($"Area {area}");
            marker.SetSelected(false);
            marker.button.onClick.AddListener(() => _onAreaClicked.OnNext(area));
            _markers.Add(marker);
        }
    }

    public void SetRemainingSeconds(int seconds)
    {
        _remainingTimeText.text = $"{seconds}";
    }

    public void SetAreaCount(int area, int count)
    {
        if (area < 0 || area >= _markers.Count) return;

        _markers[area].SetCount(count);
    }

    public void SetAreaScreenPosition(int area, Vector2 position)
    {
        if (area < 0 || area >= _markers.Count) return;

        _markers[area].SetScreenPosition(position);
    }

    public void SetSelectedArea(int area)
    {
        for (int i = 0; i < _markers.Count; i++)
        {
            _markers[i].SetSelected(i == area);
        }
    }

    public void SetInteractable(bool value)
    {
        foreach (CS_UIAreaSelectMarker marker in _markers)
        {
            marker.button.interactable = value;
        }
    }
}
