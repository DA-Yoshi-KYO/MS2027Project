/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-29 | 初回作成(Issue #342)
 * 2026-10-30 | エリアごとの画面上の位置を持つようにした
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// エリア選択シーン(AreaSelect)の画面の Model。
///
/// ・残り秒数、エリアごとの選んだ人数、自分が選んだエリア、ボタンを押せるかを ReactiveProperty で保持する
/// ・エリアごとの画面上の位置(見下ろしカメラのビューポート座標。左下が(0, 0)、右上が(1, 1)。まだ分からなければ(-1, -1))も持つ
/// ・CS_AreaSelectController が new して持ち、Presenter に渡す
///
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public sealed class CS_UIAreaSelectModel : CS_BaseModel
{
    private readonly ReactiveProperty<int> _remainingSeconds = new(0);
    private readonly ReactiveProperty<int>[] _areaCounts;
    private readonly ReactiveProperty<Vector2>[] _areaScreenPositions;
    private readonly ReactiveProperty<int> _selectedArea = new(-1);
    private readonly ReactiveProperty<bool> _isInteractable = new(true);

    /// <summary>
    /// 画面上の位置がまだ分からない時の値
    /// </summary>
    public static readonly Vector2 unknownScreenPosition = new Vector2(-1f, -1f);

    public CS_UIAreaSelectModel(int areaCount)
    {
        _areaCounts = new ReactiveProperty<int>[areaCount];
        _areaScreenPositions = new ReactiveProperty<Vector2>[areaCount];
        for (int i = 0; i < areaCount; i++)
        {
            _areaCounts[i] = new ReactiveProperty<int>(0);
            _areaScreenPositions[i] = new ReactiveProperty<Vector2>(unknownScreenPosition);
        }
    }

    /// <summary>
    /// エリアの数
    /// </summary>
    public int areaCount => _areaCounts.Length;

    /// <summary>
    /// 選べる残り秒数
    /// </summary>
    public ReadOnlyReactiveProperty<int> remainingSeconds => _remainingSeconds;

    /// <summary>
    /// 自分が選んだエリア(まだ選んでいなければ-1)
    /// </summary>
    public ReadOnlyReactiveProperty<int> selectedArea => _selectedArea;

    /// <summary>
    /// ボタンを押せるか(本編へ移る時は押せない)
    /// </summary>
    public ReadOnlyReactiveProperty<bool> isInteractable => _isInteractable;

    /// <summary>
    /// そのエリアを選んだ人数
    /// </summary>
    public ReadOnlyReactiveProperty<int> GetAreaCount(int area)
    {
        return _areaCounts[area];
    }

    /// <summary>
    /// そのエリアの画面上の位置(0〜1。まだ分からなければunknownScreenPosition)
    /// </summary>
    public ReadOnlyReactiveProperty<Vector2> GetAreaScreenPosition(int area)
    {
        return _areaScreenPositions[area];
    }

    public void SetAreaScreenPosition(int area, Vector2 position)
    {
        if (area < 0 || area >= _areaScreenPositions.Length) return;

        _areaScreenPositions[area].Value = position;
    }

    public void SetRemainingSeconds(int seconds)
    {
        _remainingSeconds.Value = seconds;
    }

    public void SetAreaCount(int area, int count)
    {
        if (area < 0 || area >= _areaCounts.Length) return;

        _areaCounts[area].Value = count;
    }

    public void SetSelectedArea(int area)
    {
        _selectedArea.Value = area;
    }

    public void SetInteractable(bool value)
    {
        _isInteractable.Value = value;
    }

    public override void Dispose()
    {
        _remainingSeconds.Dispose();
        foreach (ReactiveProperty<int> count in _areaCounts)
        {
            count.Dispose();
        }
        foreach (ReactiveProperty<Vector2> position in _areaScreenPositions)
        {
            position.Dispose();
        }
        _selectedArea.Dispose();
        _isInteractable.Dispose();
    }
}
