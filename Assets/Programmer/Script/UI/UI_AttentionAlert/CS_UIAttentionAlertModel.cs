/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-29 | 初回作成
 * ================================================ */

using System;
using R3;
using UnityEngine;

/// <summary>
/// AttentionAlert UIのModel。
/// ローカルプレイヤーが警戒状態かどうかを保持する。
/// Player側で生成してBindする。
/// </summary>
public sealed class CS_UIAttentionAlertModel : CS_BaseModel
{
    private static CS_UIAttentionAlertModel _current;

    public static event Action<CS_UIAttentionAlertModel> OnBound;
    public static event Action OnUnbound;

    private readonly ReactiveProperty<bool> _isAlerted = new(false);

    public ReadOnlyReactiveProperty<bool> isAlerted => _isAlerted;
    public bool currentIsAlerted => _isAlerted.Value;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
        OnBound = null;
        OnUnbound = null;
    }

    public static bool TryGet(out CS_UIAttentionAlertModel model)
    {
        model = _current;
        return model != null;
    }

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

    public void SetAlerted(bool isAlerted)
    {
        _isAlerted.Value = isAlerted;
    }

    public override void Dispose()
    {
        Unbind();
        _isAlerted.Dispose();
    }
}
