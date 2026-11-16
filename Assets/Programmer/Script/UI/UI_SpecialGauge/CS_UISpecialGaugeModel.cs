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
/// プレイヤーごとの必殺技ゲージを保持するModel。
///
/// ・Bind(playerNumber, characterType)で公開する
/// ・SetLocalPlayerNumber()でローカルプレイヤー番号を登録する
/// ・キャラクターの種類を保持する
/// ・全員分の満タン状態をstaticで管理する
/// </summary>
public class CS_UISpecialGaugeModel : CS_BaseModel
{
    // =========================================================
    // 番号付きで公開されたModelの一覧
    // =========================================================

    private static readonly Dictionary<
        int,
        CS_UISpecialGaugeModel
    > _boundModels = new();

    private static int _localPlayerNumber = -1;

    public static event Action<
        int,
        CS_UISpecialGaugeModel
    > OnBound;

    public static event Action<int> OnUnbound;

    public static event Action<int, bool>
        OnGaugeFullChanged;

    // =========================================================
    // staticプロパティ
    // =========================================================

    public static int localPlayerNumber =>
        _localPlayerNumber;

    // =========================================================
    // 状態
    // =========================================================

    private readonly ReactiveProperty<float>
        _currentGauge;

    private readonly float _maxGauge;

    private int _playerNumber = -1;

    private CSE_CharacterType _characterType;

    private bool _wasFullPrev;

    // =========================================================
    // 公開プロパティ
    // =========================================================

    public ReadOnlyReactiveProperty<float>
        currentGauge =>
        _currentGauge;

    public float maxGauge =>
        _maxGauge;

    public CSE_CharacterType characterType =>
        _characterType;

    public bool isFull =>
        _currentGauge.Value >= _maxGauge;

    // =========================================================
    // コンストラクター
    // =========================================================

    public CS_UISpecialGaugeModel(
        float maxGauge,
        float initGauge)
    {
        _maxGauge =
            Mathf.Max(
                0.01f,
                maxGauge
            );

        float clampedGauge =
            Mathf.Clamp(
                initGauge,
                0f,
                _maxGauge
            );

        _currentGauge =
            new ReactiveProperty<float>(
                clampedGauge
            );

        _wasFullPrev =
            clampedGauge >= _maxGauge;
    }

    // =========================================================
    // static操作
    // =========================================================

    public static bool TryGet(
        int playerNumber,
        out CS_UISpecialGaugeModel model)
    {
        return _boundModels.TryGetValue(
            playerNumber,
            out model
        );
    }

    /// <summary>
    /// ローカルプレイヤーの番号を登録する。
    /// </summary>
    public static void SetLocalPlayerNumber(
        int playerNumber)
    {
        _localPlayerNumber = playerNumber;
    }

    /// <summary>
    /// 指定プレイヤーのゲージが満タンか取得する。
    /// </summary>
    public static bool IsFull(
        int playerNumber)
    {
        if (_boundModels.TryGetValue(
                playerNumber,
                out CS_UISpecialGaugeModel model))
        {
            return model.isFull;
        }

        return false;
    }

    // =========================================================
    // Domain Reload対策
    // =========================================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModels.Clear();

        _localPlayerNumber = -1;

        OnBound = null;
        OnUnbound = null;
        OnGaugeFullChanged = null;
    }

    // =========================================================
    // Bind / Unbind
    // =========================================================

    /// <summary>
    /// プレイヤー番号とキャラクター種類を登録して公開する。
    /// </summary>
    public void Bind(
        int playerNumber,
        CSE_CharacterType characterType)
    {
        Unbind();

        _playerNumber = playerNumber;
        _characterType = characterType;

        _boundModels[playerNumber] = this;

        OnBound?.Invoke(
            playerNumber,
            this
        );
    }

    public void Unbind()
    {
        if (_playerNumber < 0)
        {
            return;
        }

        if (_boundModels.TryGetValue(
                _playerNumber,
                out CS_UISpecialGaugeModel current) &&
            current == this)
        {
            _boundModels.Remove(
                _playerNumber
            );

            OnUnbound?.Invoke(
                _playerNumber
            );
        }

        _playerNumber = -1;
    }

    // =========================================================
    // ゲージ変更
    // =========================================================

    public void SetGauge(float value)
    {
        bool wasFull =
            _currentGauge.Value >= _maxGauge;

        _currentGauge.Value =
            Mathf.Clamp(
                value,
                0f,
                _maxGauge
            );

        bool currentIsFull =
            _currentGauge.Value >= _maxGauge;

        _wasFullPrev = currentIsFull;

        if (currentIsFull == wasFull)
        {
            return;
        }

        if (_playerNumber >= 0)
        {
            OnGaugeFullChanged?.Invoke(
                _playerNumber,
                currentIsFull
            );
        }
    }

    public void AddGauge(float value)
    {
        SetGauge(
            _currentGauge.Value + value
        );
    }

    // =========================================================
    // Dispose
    // =========================================================

    public override void Dispose()
    {
        Unbind();

        _currentGauge.Dispose();
    }
}
