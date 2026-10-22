/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using System;

/// <summary>
/// 警戒状態UIのModel。
/// 現在の警戒状態を保持し、変更時だけ通知する。
/// </summary>
public class CS_UIPlayerAlertModel : CS_BaseModel
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CSE_PlayerAlertState _alertState =
        CSE_PlayerAlertState.None;

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public CSE_PlayerAlertState alertState => _alertState;

    // =========================================================
    // イベント
    // =========================================================

    public event Action<CSE_PlayerAlertState> OnAlertStateChanged;

    // =========================================================
    // 状態変更
    // =========================================================

    public void SetAlertState(CSE_PlayerAlertState alertState)
    {
        if (_alertState == alertState)
            return;

        _alertState = alertState;
        OnAlertStateChanged?.Invoke(_alertState);
    }

    // =========================================================
    // Dispose
    // =========================================================

    public override void Dispose()
    {
        OnAlertStateChanged = null;
    }
}
