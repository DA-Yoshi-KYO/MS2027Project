/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using System;
using R3;
using UnityEngine;

/// <summary>
/// ローカルプレイヤーのLIMITBREAK値を保持するModel。
/// Player側で生成してBindする。
/// </summary>
public sealed class CS_UILimitBreakModel : CS_BaseModel
{
    private static CS_UILimitBreakModel _current;

    public static event Action<CS_UILimitBreakModel> OnBound;
    public static event Action OnUnbound;

    private readonly ReactiveProperty<int> _bonusPercent = new(0);
    private readonly ReactiveProperty<Camera> _targetCamera = new(null);

    public ReadOnlyReactiveProperty<int> bonusPercent => _bonusPercent;
    public ReadOnlyReactiveProperty<Camera> targetCamera => _targetCamera;
    public int currentBonusPercent => _bonusPercent.Value;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
        OnBound = null;
        OnUnbound = null;
    }

    public static bool TryGet(out CS_UILimitBreakModel model)
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

    /// <summary>
    /// LIMITBREAK値を設定する。0未満は0へ補正する。
    /// 現仕様では20刻みだが、Modelは任意の整数値を受け取れる。
    /// </summary>
    public void SetBonusPercent(int bonusPercent)
    {
        _bonusPercent.Value = Mathf.Max(0, bonusPercent);
    }

    public void AddBonusPercent(int additionalPercent)
    {
        SetBonusPercent(
            _bonusPercent.Value + additionalPercent
        );
    }

    public void ClearBonus()
    {
        SetBonusPercent(0);
    }

    /// <summary>
    /// UIを3D空間に配置する際の基準となるカメラ(ローカルプレイヤーのカメラ)を設定する。
    /// </summary>
    public void SetTargetCamera(Camera camera)
    {
        _targetCamera.Value = camera;
    }

    public void ClearTargetCamera()
    {
        _targetCamera.Value = null;
    }

    public override void Dispose()
    {
        Unbind();
        _bonusPercent.Dispose();
        _targetCamera.Dispose();
    }
}
