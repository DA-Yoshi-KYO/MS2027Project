/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-10 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 通知の Presenter
/// ・Model の更新を受け取って View に伝える
/// ・毎フレーム Model.Tick() を呼んでタイマーを更新する
/// ・フェードアウトの更新も毎フレーム行う
/// </summary>
public class CS_UINotificationPresenter : CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UINotificationModel _model;
    private CS_UINotificationRootView _rootView;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _rootView = GetComponent<CS_UINotificationRootView>();
        _rootView.SetPresenter(this);
    }

    /// <summary>Controller から呼ばれる</summary>
    public void BindModel(CS_UINotificationModel model)
    {
        _model = model;

        // スロット更新イベントを購読
        _model.OnSlotUpdated += HandleSlotUpdated;
        _model.OnSlotCleared += HandleSlotCleared;
    }

    // =========================================================
    // Update（毎フレーム）
    // =========================================================

    private void Update()
    {
        if (_model == null) return;

        // Model のタイマーを更新
        _model.Tick(Time.deltaTime);

        // フェードアウトを更新
        for (int i = 0; i < CS_UINotificationModel.slotCount; i++)
        {
            _rootView.UpdateFade(i, _model.GetSlotTimer(i));
        }
    }

    // =========================================================
    // Model イベントハンドラ
    // =========================================================

    private void HandleSlotUpdated(int slotIndex, CS_NotificationData data)
    {
        _rootView.ShowSlot(slotIndex, data);
    }

    private void HandleSlotCleared(int slotIndex)
    {
        _rootView.HideSlot(slotIndex);
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        if (_model != null)
        {
            _model.OnSlotUpdated -= HandleSlotUpdated;
            _model.OnSlotCleared -= HandleSlotCleared;
        }
        base.OnDestroy();
    }
}
