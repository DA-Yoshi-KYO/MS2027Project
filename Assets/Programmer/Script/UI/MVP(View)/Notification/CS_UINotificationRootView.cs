/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-10 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 通知スロット3つを管理する RootView
/// ・3つの CS_UINotificationView を持つ
/// ・Presenter から呼ばれてスロットを更新する
/// </summary>
public class CS_UINotificationRootView : CS_BaseView<CS_UINotificationPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("通知スロット（上から順にセット）")]
    [SerializeField] private CS_UINotificationView[] _slots;

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UINotificationPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // スロット操作（Presenter から呼ばれる）
    // =========================================================

    /// <summary>スロットに通知を表示する</summary>
    public void ShowSlot(int slotIndex, CS_NotificationData data)
    {
        if (!IsValidIndex(slotIndex)) return;
        _slots[slotIndex].Show(data);
    }

    /// <summary>スロットを非表示にする</summary>
    public void HideSlot(int slotIndex)
    {
        if (!IsValidIndex(slotIndex)) return;
        _slots[slotIndex].Hide();
    }

    /// <summary>フェードアウトを更新する（毎フレーム）</summary>
    public void UpdateFade(int slotIndex, float remainTime)
    {
        if (!IsValidIndex(slotIndex)) return;
        _slots[slotIndex].UpdateFade(remainTime);
    }

    // =========================================================
    // 内部処理
    // =========================================================

    private bool IsValidIndex(int index)
        => _slots != null && index >= 0 && index < _slots.Length;
}
