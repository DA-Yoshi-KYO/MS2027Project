/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-18 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// SAヒント矢印全体を管理するView。
///
/// ・最大3つの矢印を管理する
/// ・Presenterから位置と角度を受け取る
/// ・矢印ごとの拡縮位相をずらす
///
/// ModelのBind()は呼ばない。
/// </summary>
public sealed class CS_UISAHintView :
    CS_BaseView<CS_UISAHintPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("SAヒント矢印。最大3個")]
    [SerializeField]
    private CS_UISAHintItemView[] _items;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        HideAll();
    }

    public override void SetPresenter(
        CS_UISAHintPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // セットアップ
    // =========================================================

    public void Setup(
        CSO_UISAHintData data)
    {
        if (_items == null)
        {
            return;
        }

        int itemCount =
            Mathf.Max(
                1,
                _items.Length
            );

        for (int i = 0;
             i < _items.Length;
             i++)
        {
            CS_UISAHintItemView item =
                _items[i];

            if (item == null)
            {
                continue;
            }

            float phaseOffset =
                (float)i /
                itemCount;

            item.Setup(
                data,
                phaseOffset
            );
        }
    }

    // =========================================================
    // 表示
    // =========================================================

    public void ShowIndicator(
        int index,
        Vector2 anchoredPosition,
        float angle)
    {
        if (!TryGetItem(
                index,
                out CS_UISAHintItemView item))
        {
            return;
        }

        item.Show(
            anchoredPosition,
            angle
        );
    }

    public void HideIndicator(int index)
    {
        if (!TryGetItem(
                index,
                out CS_UISAHintItemView item))
        {
            return;
        }

        item.Hide();
    }

    public void HideFromIndex(
        int firstHiddenIndex)
    {
        if (_items == null)
        {
            return;
        }

        int startIndex =
            Mathf.Max(
                0,
                firstHiddenIndex
            );

        for (int i = startIndex;
             i < _items.Length;
             i++)
        {
            if (_items[i] != null)
            {
                _items[i].Hide();
            }
        }
    }

    public void HideAll()
    {
        HideFromIndex(0);
    }

    public int GetItemCount()
    {
        return _items != null
            ? _items.Length
            : 0;
    }

    private bool TryGetItem(
        int index,
        out CS_UISAHintItemView item)
    {
        item = null;

        if (_items == null ||
            index < 0 ||
            index >= _items.Length)
        {
            return false;
        }

        item = _items[index];

        return item != null;
    }
}