/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-10 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 通知スロット3つを管理するRootView。
///
/// ・新規通知ではShow()を呼び、表示アニメーションを再生する
/// ・表示中の通知更新ではUpdateMessage()を呼ぶ
/// ・更新時はBackImageと文字だけを更新し、
///   表示アニメーションを最初から再生しない
/// ・Modelからスロット解除通知を受けたら非表示にする
/// </summary>
public sealed class CS_UINotificationRootView :
    CS_BaseView<CS_UINotificationPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("通知スロット（上から順に設定）")]
    [SerializeField]
    private CS_UINotificationView[] _slots;

    // =========================================================
    // 内部フィールド
    // =========================================================

    /// <summary>
    /// 各スロットが現在使用中かどうか。
    ///
    /// falseから通知が来た場合は新規表示、
    /// trueの状態で再度通知が来た場合は内容更新として扱う。
    /// </summary>
    private bool[] _isSlotActive;

    // =========================================================
    // Unityイベント
    // =========================================================

    private void Awake()
    {
        int slotLength =
            _slots != null
                ? _slots.Length
                : 0;

        _isSlotActive =
            new bool[slotLength];

        // 初期状態はすべて非表示。
        for (int i = 0;
             i < slotLength;
             i++)
        {
            CS_UINotificationView slot =
                _slots[i];

            _isSlotActive[i] = false;

            if (slot != null)
            {
                slot.Hide();
            }
        }
    }

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(
        CS_UINotificationPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // スロット表示
    // =========================================================

    /// <summary>
    /// 指定スロットへ通知を表示または更新する。
    ///
    /// 新規通知:
    /// Show()を呼んで、BackImageの展開と
    /// 文字のスケールアニメーションを再生する。
    ///
    /// 表示中の通知更新:
    /// UpdateMessage()を呼び、アニメーションを再生せず
    /// BackImageと文字を更新する。
    /// </summary>
    public void ShowSlot(
        int slotIndex,
        CS_NotificationData data)
    {
        if (!TryGetSlot(
                slotIndex,
                out CS_UINotificationView slot))
        {
            return;
        }

        if (data == null)
        {
            HideSlot(slotIndex);
            return;
        }

        bool isAlreadyActive =
            _isSlotActive[slotIndex];

        _isSlotActive[slotIndex] = true;

        if (isAlreadyActive)
        {
            // キル通知の結合や
            // カウントダウンの文章更新。
            slot.UpdateMessage(data);
        }
        else
        {
            // 新規通知なので表示アニメーションを再生。
            slot.Show(data);
        }
    }

    // =========================================================
    // スロット非表示
    // =========================================================

    /// <summary>
    /// 指定スロットを非表示にする。
    /// 次に通知が入ったときは新規通知として扱う。
    /// </summary>
    public void HideSlot(int slotIndex)
    {
        if (!TryGetSlot(
                slotIndex,
                out CS_UINotificationView slot))
        {
            return;
        }

        _isSlotActive[slotIndex] = false;

        slot.Hide();
    }

    /// <summary>
    /// 全通知スロットを非表示にする。
    /// </summary>
    public void HideAllSlots()
    {
        if (_slots == null ||
            _isSlotActive == null)
        {
            return;
        }

        for (int i = 0;
             i < _slots.Length;
             i++)
        {
            _isSlotActive[i] = false;

            CS_UINotificationView slot =
                _slots[i];

            if (slot != null)
            {
                slot.Hide();
            }
        }
    }

    // =========================================================
    // フェードアウト
    // =========================================================

    /// <summary>
    /// 指定スロットのフェードアウトを更新する。
    /// Presenterから毎フレーム呼び出される。
    /// </summary>
    public void UpdateFade(
        int slotIndex,
        float remainTime)
    {
        if (!TryGetSlot(
                slotIndex,
                out CS_UINotificationView slot))
        {
            return;
        }

        if (!_isSlotActive[slotIndex])
        {
            return;
        }

        slot.UpdateFade(remainTime);
    }

    // =========================================================
    // スロット取得
    // =========================================================

    /// <summary>
    /// 有効な番号から通知スロットを取得する。
    /// </summary>
    private bool TryGetSlot(
        int slotIndex,
        out CS_UINotificationView slot)
    {
        slot = null;

        if (_slots == null ||
            _isSlotActive == null)
        {
            return false;
        }

        if (slotIndex < 0 ||
            slotIndex >= _slots.Length ||
            slotIndex >= _isSlotActive.Length)
        {
            Debug.LogWarning(
                $"{nameof(CS_UINotificationRootView)}: " +
                $"無効な通知スロット番号です: {slotIndex}",
                this
            );

            return false;
        }

        slot = _slots[slotIndex];

        if (slot == null)
        {
            Debug.LogWarning(
                $"{nameof(CS_UINotificationRootView)}: " +
                $"通知スロットが設定されていません: {slotIndex}",
                this
            );

            return false;
        }

        return true;
    }

    // =========================================================
    // Inspector確認
    // =========================================================

    private void OnValidate()
    {
        if (_slots == null)
        {
            return;
        }

        if (_slots.Length !=
            CS_UINotificationModel.slotCount)
        {
            Debug.LogWarning(
                $"{nameof(CS_UINotificationRootView)}: " +
                $"通知スロットは" +
                $"{CS_UINotificationModel.slotCount}個必要です。 " +
                $"現在: {_slots.Length}個",
                this
            );
        }
    }
}
