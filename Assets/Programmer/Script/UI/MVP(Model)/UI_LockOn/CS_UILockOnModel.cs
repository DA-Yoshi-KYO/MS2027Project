/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

/// <summary>
/// ローカルPlayerのロックオン対象を保持するModel。
/// Player側でnewし、Player番号を指定してBindする。
/// </summary>
public sealed class CS_UILockOnModel : CS_BaseModel
{
    private static readonly Dictionary<int, CS_UILockOnModel>
        _boundModels = new();

    private static int _localPlayerNumber = -1;

    private readonly ReactiveProperty<Transform> _target = new(null);
    private int _boundPlayerNumber = -1;
    private bool _isBound;

    public static int localPlayerNumber => _localPlayerNumber;

    public static event Action<int, CS_UILockOnModel> OnBound;
    public static event Action<int> OnUnbound;

    public ReadOnlyReactiveProperty<Transform> target => _target;
    public Transform currentTarget => _target.Value;
    public bool isLockedOn => _target.Value != null;
    public bool isBound => _isBound;
    public int boundPlayerNumber => _boundPlayerNumber;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModels.Clear();
        _localPlayerNumber = -1;
        OnBound = null;
        OnUnbound = null;
    }

    public static void SetLocalPlayerNumber(int playerNumber)
    {
        _localPlayerNumber = playerNumber;
    }

    public static bool TryGet(
        int playerNumber,
        out CS_UILockOnModel model)
    {
        return _boundModels.TryGetValue(playerNumber, out model);
    }

    public void Bind(int playerNumber)
    {
        if (_isBound && _boundPlayerNumber == playerNumber)
            return;

        Unbind();

        if (_boundModels.TryGetValue(
            playerNumber,
            out CS_UILockOnModel oldModel) &&
            oldModel != this)
        {
            oldModel.Unbind();
        }

        _boundPlayerNumber = playerNumber;
        _isBound = true;
        _boundModels[playerNumber] = this;

        OnBound?.Invoke(playerNumber, this);
    }

    public void Unbind()
    {
        if (!_isBound)
            return;

        int playerNumber = _boundPlayerNumber;

        if (_boundModels.TryGetValue(playerNumber, out CS_UILockOnModel model) &&
            model == this)
        {
            _boundModels.Remove(playerNumber);
        }

        _boundPlayerNumber = -1;
        _isBound = false;
        _target.Value = null;

        OnUnbound?.Invoke(playerNumber);
    }

    public void SetTarget(Transform targetTransform)
    {
        _target.Value = targetTransform;
    }

    public void ClearTarget()
    {
        _target.Value = null;
    }

    public override void Dispose()
    {
        Unbind();
        _target.Dispose();
    }
}
