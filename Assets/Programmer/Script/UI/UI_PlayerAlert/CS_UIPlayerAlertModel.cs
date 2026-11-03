/* ================================================
 *
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * 2026-11-02 | Player側で生成してBindする形に変更
 * ================================================ */

using System;
using R3;
using UnityEngine;

/// <summary>
/// 警戒状態UI(「警戒状態!」「捜索中」)のModel。
/// ローカルプレイヤーの警戒状態を保持する。
/// Player側で生成してBindする。
/// </summary>
public sealed class CS_UIPlayerAlertModel : CS_BaseModel
{
    // =========================================================
    // Bindされている Model(ローカルプレイヤーの1つだけ)
    // =========================================================

    private static CS_UIPlayerAlertModel _current;

    public static event Action<CS_UIPlayerAlertModel> OnBound;
    public static event Action OnUnbound;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
        OnBound = null;
        OnUnbound = null;
    }

    public static bool TryGet(out CS_UIPlayerAlertModel model)
    {
        model = _current;
        return model != null;
    }

    // =========================================================
    // 状態
    // =========================================================

    private readonly ReactiveProperty<CSE_PlayerAlertState> _alertState =
        new(CSE_PlayerAlertState.None);

    public ReadOnlyReactiveProperty<CSE_PlayerAlertState> alertState => _alertState;
    public CSE_PlayerAlertState currentAlertState => _alertState.Value;

    // =========================================================
    // 公開
    // =========================================================

    public void Bind()
    {
        if (_current == this)
            return;

        if (_current != null)
            _current.Unbind();

        _current = this;
        OnBound?.Invoke(this);
    }

    public void Unbind()
    {
        if (_current != this)
            return;

        _current = null;
        OnUnbound?.Invoke();
    }

    // =========================================================
    // 状態変更
    // =========================================================

    public void SetAlertState(CSE_PlayerAlertState alertState)
    {
        _alertState.Value = alertState;
    }

    // =========================================================
    // Dispose
    // =========================================================

    public override void Dispose()
    {
        Unbind();
        _alertState.Dispose();
    }
}
