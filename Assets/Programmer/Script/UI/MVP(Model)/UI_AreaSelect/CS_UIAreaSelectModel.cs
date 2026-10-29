/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-29 | 初回作成(Issue #342)
 * ================================================ */

using R3;

/// <summary>
/// エリア選択シーン(AreaSelect)の画面の Model。
///
/// ・残り秒数、エリアごとの選んだ人数、自分が選んだエリア、ボタンを押せるかを ReactiveProperty で保持する
/// ・CS_AreaSelectController が new して持ち、Presenter に渡す
///
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public sealed class CS_UIAreaSelectModel : CS_BaseModel
{
    private readonly ReactiveProperty<int> _remainingSeconds = new(0);
    private readonly ReactiveProperty<int>[] _areaCounts;
    private readonly ReactiveProperty<int> _selectedArea = new(-1);
    private readonly ReactiveProperty<bool> _isInteractable = new(true);

    public CS_UIAreaSelectModel(int areaCount)
    {
        _areaCounts = new ReactiveProperty<int>[areaCount];
        for (int i = 0; i < areaCount; i++)
        {
            _areaCounts[i] = new ReactiveProperty<int>(0);
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
        _selectedArea.Dispose();
        _isInteractable.Dispose();
    }
}
