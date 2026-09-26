/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-26 | 初回作成
 * ================================================ */

using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーごとの必殺技ゲージ（Special Gauge）を保持するModel
/// HPゲージと同じく、Bind(playerNumber) で公開してUI側が拾う仕組み
/// </summary>
public class CS_UISpecialGaugeModel : CS_BaseModel
{
    private static readonly Dictionary<int, CS_UISpecialGaugeModel> _boundModels = new();
    public static event Action<int, CS_UISpecialGaugeModel> OnBound;
    public static event Action<int> OnUnbound;

    public static bool TryGet(int playerNumber, out CS_UISpecialGaugeModel model)
        => _boundModels.TryGetValue(playerNumber, out model);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModels.Clear();
        OnBound = null;
        OnUnbound = null;
    }

    // ★ 現在ゲージ値（HPと同じ構造）
    private readonly ReactiveProperty<float> _currentGauge = new ReactiveProperty<float>(0f);
    public ReadOnlyReactiveProperty<float> currentGauge => _currentGauge;

    // ★ 最大ゲージ値（HPと同じ構造）
    public float maxGauge { get; private set; }

    private int _playerNumber = -1;

    public CS_UISpecialGaugeModel(float maxGauge, float initGauge)
    {
        this.maxGauge = maxGauge;
        SetGauge(initGauge);
    }

    /// <summary>
    /// プレイヤー番号で公開（HPと同じ）
    /// </summary>
    public void Bind(int playerNumber)
    {
        Unbind();
        _playerNumber = playerNumber;
        _boundModels[playerNumber] = this;
        OnBound?.Invoke(playerNumber, this);
    }

    public void Unbind()
    {
        if (_playerNumber < 0) return;

        if (_boundModels.TryGetValue(_playerNumber, out var current) && current == this)
        {
            _boundModels.Remove(_playerNumber);
            OnUnbound?.Invoke(_playerNumber);
        }
        _playerNumber = -1;
    }

    /// <summary>
    /// ゲージ値を設定（0〜maxGauge）
    /// </summary>
    public void SetGauge(float value)
    {
        _currentGauge.Value = Mathf.Clamp(value, 0f, maxGauge);
    }

    /// <summary>
    /// ゲージを加算
    /// </summary>
    public void AddGauge(float value)
    {
        SetGauge(_currentGauge.Value + value);
    }

    public override void Dispose()
    {
        Unbind();
        _currentGauge.Dispose();
    }
}
