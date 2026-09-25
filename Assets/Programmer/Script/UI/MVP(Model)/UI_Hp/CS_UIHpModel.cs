/* ================================================
 * 　HPの状態を保持
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-09-25 | コンストラクタで初期化、Bindで番号付き公開に変更
 * ================================================ */

using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

/// <summary>
/// HPの状態を保持
/// Bind(番号) で「何番のプレイヤーのHPか」を公開し、UI側がそれを拾って表示する
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public class CS_UIHpModel : CS_BaseModel
{
    // ---------------- 番号付きで公開されたModelの一覧 ----------------

    private static readonly Dictionary<int, CS_UIHpModel> _boundModels = new Dictionary<int, CS_UIHpModel>();

    //Bindされた時の通知(番号, Model)
    public static event Action<int, CS_UIHpModel> OnBound;

    //Bindが外れた時の通知(番号)
    public static event Action<int> OnUnbound;

    //指定番号のModelを取得する(UI側が後から生成された場合に使う)
    public static bool TryGet(int playerNumber, out CS_UIHpModel model)
    {
        return _boundModels.TryGetValue(playerNumber, out model);
    }

    //Domain Reloadを切っている場合に、前回の再生の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModels.Clear();
        OnBound = null;
        OnUnbound = null;
    }

    // ---------------- 状態 ----------------

    private readonly ReactiveProperty<int> _currentHp;
    private readonly ReactiveProperty<int> _maxHp;
    private int _playerNumber = -1;   //-1 = 未Bind

    //外部からは読み取り専用
    public ReadOnlyReactiveProperty<int> currentHp => _currentHp;
    public ReadOnlyReactiveProperty<int> maxHp => _maxHp;

    //満タンで始める
    public CS_UIHpModel(int maxHp) : this(maxHp, maxHp) { }

    public CS_UIHpModel(int maxHp, int currentHp)
    {
        _maxHp = new ReactiveProperty<int>(Mathf.Max(1, maxHp));
        _currentHp = new ReactiveProperty<int>(Mathf.Clamp(currentHp, 0, _maxHp.Value));
    }

    // ---------------- 公開 ----------------

    //このModelを何番のプレイヤーのHPとして公開するか
    public void Bind(int playerNumber)
    {
        Unbind();

        _playerNumber = playerNumber;
        _boundModels[playerNumber] = this;
        OnBound?.Invoke(playerNumber, this);
    }

    //公開をやめる
    public void Unbind()
    {
        if (_playerNumber < 0) return;

        //別のModelで上書きされていなければ外す
        if (_boundModels.TryGetValue(_playerNumber, out var current) && current == this)
        {
            _boundModels.Remove(_playerNumber);
            OnUnbound?.Invoke(_playerNumber);
        }
        _playerNumber = -1;
    }

    // ---------------- 値の変更 ----------------

    //Hpを設定する
    public void SetHp(int hp)
    {
        _currentHp.Value = Mathf.Clamp(hp, 0, _maxHp.Value);
    }

    //最大Hpを設定する(現在Hpが超えていたら切り詰める)
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
