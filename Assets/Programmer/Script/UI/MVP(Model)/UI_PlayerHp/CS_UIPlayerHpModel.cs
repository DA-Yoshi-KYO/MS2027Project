/* ================================================
 * 　HPの状態を保持
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-09-25 | コンストラクタで初期化、Bindで番号付き公開に変更
 * ================================================ */

using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HPの状態を保持
/// ・Bind(playerNumber) で公開する
/// ・SetLocalPlayerNumber() でローカルプレイヤーの番号を登録する
/// ・Presenter はローカルプレイヤーの番号と一致したら表示する
/// </summary>
public class CS_UIPlayerHpModel : CS_BaseModel
{
    // =========================================================
    // 番号付きで公開された Model の一覧
    // =========================================================

    private static readonly Dictionary<int, CS_UIPlayerHpModel> _boundModels = new();

    // ローカルプレイヤーの番号（自分の番号）
    private static int _localPlayerNumber = -1;

    // Bind された時の通知（番号, Model）
    public static event Action<int, CS_UIPlayerHpModel> OnBound;

    // Bind が外れた時の通知（番号）
    public static event Action<int> OnUnbound;

    // 指定番号の Model を取得する
    public static bool TryGet(int playerNumber, out CS_UIPlayerHpModel model)
        => _boundModels.TryGetValue(playerNumber, out model);

    /// <summary>
    /// ローカルプレイヤーの番号を登録する
    /// IsOwner のプレイヤーが OnNetworkSpawn で呼ぶ
    /// </summary>
    public static void SetLocalPlayerNumber(int playerNumber)
    {
        _localPlayerNumber = playerNumber;
        Debug.Log($"[CS_UIPlayerHpModel] LocalPlayerNumber = {playerNumber}");
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

    private readonly ReactiveProperty<int> _currentHp;
    private readonly ReactiveProperty<int> _maxHp;
    private int _playerNumber = -1;

    public ReadOnlyReactiveProperty<int> currentHp => _currentHp;
    public ReadOnlyReactiveProperty<int> maxHp => _maxHp;

    public CS_UIPlayerHpModel(int maxHp) : this(maxHp, maxHp) { }

    public CS_UIPlayerHpModel(int maxHp, int currentHp)
    {
        _maxHp = new ReactiveProperty<int>(Mathf.Max(1, maxHp));
        _currentHp = new ReactiveProperty<int>(Mathf.Clamp(currentHp, 0, _maxHp.Value));
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

    public void SetHp(int hp)
    {
        _currentHp.Value = Mathf.Clamp(hp, 0, _maxHp.Value);
    }

    public void SetMaxHp(int maxHp)
    {
        _maxHp.Value = Mathf.Max(1, maxHp);
        SetHp(_currentHp.Value);
    }

    public override void Dispose()
    {
        Unbind();
        _currentHp.Dispose();
        _maxHp.Dispose();
    }
}
