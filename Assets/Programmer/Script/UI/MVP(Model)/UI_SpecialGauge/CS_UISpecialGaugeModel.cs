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
/// ・Bind(playerNumber) で公開する
/// ・SetLocalPlayerNumber() でローカルプレイヤーの番号を登録する
/// ・Presenter はローカルプレイヤーの番号と一致したら表示する
/// </summary>
public class CS_UISpecialGaugeModel : CS_BaseModel
{
    // =========================================================
    // 番号付きで公開された Model の一覧
    // =========================================================

    private static readonly Dictionary<int, CS_UISpecialGaugeModel> _boundModels = new();

    // ローカルプレイヤーの番号（自分の番号）
    private static int _localPlayerNumber = -1;

    // Bind された時の通知（番号, Model）
    public static event Action<int, CS_UISpecialGaugeModel> OnBound;

    // Bind が外れた時の通知（番号）
    public static event Action<int> OnUnbound;

    // 指定番号の Model を取得する
    public static bool TryGet(int playerNumber, out CS_UISpecialGaugeModel model)
        => _boundModels.TryGetValue(playerNumber, out model);

    /// <summary>
    /// ローカルプレイヤーの番号を登録する
    /// IsOwner のプレイヤーが OnNetworkSpawn で呼ぶ
    /// </summary>
    public static void SetLocalPlayerNumber(int playerNumber)
    {
        _localPlayerNumber = playerNumber;
        Debug.Log($"[CS_UISpecialGaugeModel] LocalPlayerNumber = {playerNumber}");
    }

    /// <summary>ローカルプレイヤーの番号を取得する</summary>
    public static int localPlayerNumber => _localPlayerNumber;

    // Domain Reload 対策
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModels.Clear();
        _localPlayerNumber = -1;
        OnBound = null;
        OnUnbound = null;
    }

    // =========================================================
    // 状態
    // =========================================================

    private readonly ReactiveProperty<float> _currentGauge = new ReactiveProperty<float>(0f);
    public ReadOnlyReactiveProperty<float> currentGauge => _currentGauge;
    public float maxGauge { get; private set; }

    private int _playerNumber = -1;

    public CS_UISpecialGaugeModel(float maxGauge, float initGauge)
    {
        this.maxGauge = maxGauge;
        SetGauge(initGauge);
    }

    // =========================================================
    // 公開
    // =========================================================

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

    // =========================================================
    // 値の変更
    // =========================================================

    public void SetGauge(float value)
    {
        _currentGauge.Value = Mathf.Clamp(value, 0f, maxGauge);
    }

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
