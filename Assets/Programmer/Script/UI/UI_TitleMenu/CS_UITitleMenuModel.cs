/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-20 | 初回作成
 * ================================================ */

using R3;
using System;
using UnityEngine;

/// <summary>
/// タイトルメニューの状態を管理するModel
/// ・選択中の項目番号を保持する(番号は CSE_TitleMenuItem の定義順)
/// ・選択の移動(MoveSelection)、直接選択(Select)、決定(Decide)を受け付ける
/// ・決定されたら OnDecided で通知する(シーン遷移などは Controller が行う)
/// ・一度決定したら、以降の操作は無視する(遷移中の二重入力を防ぐ)
/// ・Presenter / View のことは知らない
/// </summary>
public class CS_UITitleMenuModel : CS_BaseModel
{
    // =========================================================
    // 状態
    // =========================================================

    // 選択中の項目番号。値が変わった時に Presenter へ自動で知らせる
    private readonly ReactiveProperty<int> _selectedIndex = new ReactiveProperty<int>(0);

    // 項目の数(CSE_TitleMenuItem の数)
    private readonly int _itemCount = Enum.GetValues(typeof(CSE_TitleMenuItem)).Length;

    // true なら、端で移動した時に反対側へ回り込む(一番下の次は一番上)
    private readonly bool _isLoop;

    // 決定済みかどうか
    private bool _isDecided;

    /// <summary>選択中の項目番号(外には読み取り専用で公開)</summary>
    public ReadOnlyReactiveProperty<int> selectedIndex => _selectedIndex;

    /// <summary>選択中の項目</summary>
    public CSE_TitleMenuItem selectedItem => (CSE_TitleMenuItem)_selectedIndex.Value;

    /// <summary>項目の数</summary>
    public int itemCount => _itemCount;

    /// <summary>決定済みか(決定後は操作を受け付けない)</summary>
    public bool isDecided => _isDecided;

    /// <summary>決定された時の通知(決定された項目)</summary>
    public event Action<CSE_TitleMenuItem> OnDecided;

    // =========================================================
    // 生成
    // =========================================================

    /// <param name="isLoop">端で移動した時に反対側へ回り込むか</param>
    public CS_UITitleMenuModel(bool isLoop = true)
    {
        _isLoop = isLoop;
    }

    // =========================================================
    // 操作
    // =========================================================

    /// <summary>
    /// 選択を動かす
    /// direction: -1=前の項目(上)、+1=次の項目(下)
    /// </summary>
    public void MoveSelection(int direction)
    {
        if (_isDecided || direction == 0)
            return;

        int next = _selectedIndex.Value + direction;

        if (_isLoop)
            next = ((next % _itemCount) + _itemCount) % _itemCount;
        else
            next = Mathf.Clamp(next, 0, _itemCount - 1);

        _selectedIndex.Value = next;
    }

    /// <summary>項目番号を指定して選択する(マウスのホバー用)</summary>
    public void Select(int index)
    {
        if (_isDecided)
            return;

        if (index < 0 || index >= _itemCount)
            return;

        _selectedIndex.Value = index;
    }

    /// <summary>選択中の項目で決定する。決定は1回だけ有効</summary>
    public void Decide()
    {
        if (_isDecided)
            return;

        _isDecided = true;
        OnDecided?.Invoke(selectedItem);
    }

    // =========================================================
    // 破棄
    // =========================================================

    public override void Dispose()
    {
        OnDecided = null;
        _selectedIndex.Dispose();
    }
}