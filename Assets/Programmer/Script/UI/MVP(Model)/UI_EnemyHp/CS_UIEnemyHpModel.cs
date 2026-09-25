/* ================================================
 * 敵のHP状態を保持
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵のHP状態を保持
/// Bind(番号) で「何番の敵のHPか」を公開し、UI側がそれを拾って表示する
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public class CS_UIEnemyHpModel : CS_BaseModel
{
    private static readonly Dictionary<int, CS_UIEnemyHpModel> _boundModels = new();

    public static event Action<int, CS_UIEnemyHpModel> OnBound;
    public static event Action<int> OnUnbound;

    public static bool TryGet(int enemyNumber, out CS_UIEnemyHpModel model)
        => _boundModels.TryGetValue(enemyNumber, out model);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModels.Clear();
        OnBound = null;
        OnUnbound = null;
    }

    private readonly ReactiveProperty<int> _currentHp;
    private readonly ReactiveProperty<int> _maxHp;
    private int _enemyNumber = -1;

    public ReadOnlyReactiveProperty<int> currentHp => _currentHp;
    public ReadOnlyReactiveProperty<int> maxHp => _maxHp;

    public CS_UIEnemyHpModel(int maxHp) : this(maxHp, maxHp) { }

    public CS_UIEnemyHpModel(int maxHp, int currentHp)
    {
        _maxHp = new ReactiveProperty<int>(Mathf.Max(1, maxHp));
        _currentHp = new ReactiveProperty<int>(Mathf.Clamp(currentHp, 0, _maxHp.Value));
    }

    public void Bind(int enemyNumber)
    {
        Unbind();
        _enemyNumber = enemyNumber;
        _boundModels[enemyNumber] = this;
        OnBound?.Invoke(enemyNumber, this);
    }

    public void Unbind()
    {
        if (_enemyNumber < 0) return;

        if (_boundModels.TryGetValue(_enemyNumber, out var current) && current == this)
        {
            _boundModels.Remove(_enemyNumber);
            OnUnbound?.Invoke(_enemyNumber);
        }
        _enemyNumber = -1;
    }

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
