/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 悪人の犯罪完遂ゲージの状態を保持
/// Bind(親Transform) で「どのグループのゲージか」を公開し、
/// その子のゲージバーが拾って表示する
/// ※ Model は UI のクラスを一切参照しない（UI → Model の一方向）
/// </summary>
public class CS_UICrimeGaugeModel : CS_BaseModel
{
    // =========================================================
    // グループごとに公開された Model の一覧
    // =========================================================

    private static readonly Dictionary<Transform, CS_UICrimeGaugeModel> _boundModels
        = new Dictionary<Transform, CS_UICrimeGaugeModel>();

    // Bind された時の通知（親Transform, Model）
    public static event Action<Transform, CS_UICrimeGaugeModel> OnBound;

    // Bind が外れた時の通知（親Transform）
    public static event Action<Transform> OnUnbound;

    // 指定したグループの Model を取得する（ゲージが後から有効になった場合に使う）
    public static bool TryGet(Transform parentTransform, out CS_UICrimeGaugeModel model)
    {
        return _boundModels.TryGetValue(parentTransform, out model);
    }

    // Domain Reload を切っている場合に、前回の再生の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModels.Clear();
        OnBound = null;
        OnUnbound = null;
    }

    // =========================================================
    // 状態
    // =========================================================

    private readonly ReactiveProperty<float> _currentValue; // 現在の犯罪完遂値
    private readonly ReactiveProperty<float> _maxValue;     // 最大値（可変）
    private Transform _parentTransform;                     // null = 未Bind

    // 外部からは読み取り専用
    public ReadOnlyReactiveProperty<float> currentValue => _currentValue;
    public ReadOnlyReactiveProperty<float> maxValue => _maxValue;

    // 0 からスタート
    public CS_UICrimeGaugeModel(float maxValue) : this(maxValue, 0f) { }

    public CS_UICrimeGaugeModel(float maxValue, float currentValue)
    {
        _maxValue = new ReactiveProperty<float>(Mathf.Max(1f, maxValue));
        _currentValue = new ReactiveProperty<float>(Mathf.Clamp(currentValue, 0f, _maxValue.Value));
    }

    // =========================================================
    // 公開
    // =========================================================

    /// <summary>この Model をどのグループのゲージとして公開するか</summary>
    public void Bind(Transform parentTransform)
    {
        Unbind();

        _parentTransform = parentTransform;
        _boundModels[parentTransform] = this;
        OnBound?.Invoke(parentTransform, this);
    }

    /// <summary>公開をやめる</summary>
    public void Unbind()
    {
        if (_parentTransform == null) return;

        // 別の Model で上書きされていなければ外す
        if (_boundModels.TryGetValue(_parentTransform, out var current) && current == this)
        {
            _boundModels.Remove(_parentTransform);
            OnUnbound?.Invoke(_parentTransform);
        }
        _parentTransform = null;
    }

    // =========================================================
    // 値の変更
    // =========================================================

    /// <summary>犯罪完遂値を設定する（増える方向）</summary>
    public void SetValue(float value)
    {
        _currentValue.Value = Mathf.Clamp(value, 0f, _maxValue.Value);
    }

    /// <summary>犯罪完遂値を加算する</summary>
    public void AddValue(float amount)
    {
        SetValue(_currentValue.Value + amount);
    }

    /// <summary>最大値を設定する（現在値が超えていたら切り詰める）</summary>
    public void SetMaxValue(float maxValue)
    {
        _maxValue.Value = Mathf.Max(1f, maxValue);
        SetValue(_currentValue.Value); // 現在値をクランプし直す
    }

    /// <summary>ゲージをリセットする（0に戻す）</summary>
    public void Reset()
    {
        _currentValue.Value = 0f;
    }

    /// <summary>犯罪が完遂したか（100%に達したか）</summary>
    public bool IsCompleted => _currentValue.Value >= _maxValue.Value;

    // =========================================================
    // IDisposable
    // =========================================================

    public override void Dispose()
    {
        Unbind();
        _currentValue.Dispose();
        _maxValue.Dispose();
    }
}
