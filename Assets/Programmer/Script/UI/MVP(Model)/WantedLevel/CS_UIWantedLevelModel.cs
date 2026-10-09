/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-09 | 初回作成
 * ================================================ */

using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 手配度の状態を保持する Model
/// ・Bind(playerNumber) で公開する
/// ・SetLocalPlayerNumber() でローカルプレイヤーの番号を登録する
/// ・Presenter はローカルプレイヤーの番号と一致したら表示する
/// ・初期値は 0（空スタート）
/// </summary>
public class CS_UIWantedLevelModel : CS_BaseModel
{
    // =========================================================
    // 番号付きで公開された Model の一覧
    // =========================================================

    private static readonly Dictionary<int, CS_UIWantedLevelModel> _boundModels = new();

    // ローカルプレイヤーの番号
    private static int _localPlayerNumber = -1;

    // Bind された時の通知（番号, Model）
    public static event Action<int, CS_UIWantedLevelModel> OnBound;

    // Bind が外れた時の通知（番号）
    public static event Action<int> OnUnbound;

    // 指定番号の Model を取得する
    public static bool TryGet(int playerNumber, out CS_UIWantedLevelModel model)
        => _boundModels.TryGetValue(playerNumber, out model);

    /// <summary>ローカルプレイヤーの番号を登録する</summary>
    public static void SetLocalPlayerNumber(int playerNumber)
    {
        _localPlayerNumber = playerNumber;
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

    private readonly ReactiveProperty<int> _currentLevel;
    private readonly ReactiveProperty<int> _maxLevel;
    private int _playerNumber = -1;

    public ReadOnlyReactiveProperty<int> currentLevel => _currentLevel;
    public ReadOnlyReactiveProperty<int> maxLevel => _maxLevel;

    /// <summary>空スタート（初期値 0）</summary>
    public CS_UIWantedLevelModel(int maxLevel)
    {
        _maxLevel = new ReactiveProperty<int>(Mathf.Max(1, maxLevel));
        _currentLevel = new ReactiveProperty<int>(0); // 空スタート
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

    /// <summary>手配度を設定する（0〜maxLevel）</summary>
    public void SetLevel(int level)
    {
        _currentLevel.Value = Mathf.Clamp(level, 0, _maxLevel.Value);
    }

    /// <summary>手配度を加算する</summary>
    public void AddLevel(int value)
    {
        SetLevel(_currentLevel.Value + value);
    }

    /// <summary>最大手配度を設定する</summary>
    public void SetMaxLevel(int maxLevel)
    {
        _maxLevel.Value = Mathf.Max(1, maxLevel);
        SetLevel(_currentLevel.Value);
    }

    public override void Dispose()
    {
        Unbind();
        _currentLevel.Dispose();
        _maxLevel.Dispose();
    }
}
