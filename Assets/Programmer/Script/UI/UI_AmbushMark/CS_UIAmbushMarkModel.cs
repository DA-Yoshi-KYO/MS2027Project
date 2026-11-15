/* ================================================
 * 奇襲の星マーク(対象の悪人の位置に出る星)のModel
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-15 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

/// <summary>
/// 奇襲の星マークの対象を保持するModel(ローカルPlayerの画面だけに出す)。
/// Player側でnewし、Player番号を指定してBindする(CS_UILockOnModelと同じ形)。
/// ・SetTarget: 対象の悪人の位置に星を出す(パッと出て、縮みながら回って止まる)
/// ・Burst: 命中で星が弾けて消える
/// ・ClearTarget: 弾けずにすぐ消す(奇襲が中止された時など)
/// </summary>
public sealed class CS_UIAmbushMarkModel : CS_BaseModel
{
    private static readonly Dictionary<int, CS_UIAmbushMarkModel>
        _boundModels = new();

    private static int _localPlayerNumber = -1;

    private readonly ReactiveProperty<Transform> _target = new(null);
    private readonly Subject<Unit> _burst = new();
    private int _boundPlayerNumber = -1;
    private bool _isBound;

    public static int localPlayerNumber => _localPlayerNumber;

    public static event Action<int, CS_UIAmbushMarkModel> OnBound;
    public static event Action<int> OnUnbound;

    public ReadOnlyReactiveProperty<Transform> target => _target;
    public Observable<Unit> onBurst => _burst;
    public Transform currentTarget => _target.Value;
    public bool hasTarget => _target.Value != null;
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

    /// <summary>画面に出す番号(自分の番号)を登録する。Bindより先に呼ぶ。</summary>
    public static void SetLocalPlayerNumber(int playerNumber)
    {
        _localPlayerNumber = playerNumber;
    }

    public static bool TryGet(
        int playerNumber,
        out CS_UIAmbushMarkModel model)
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
            out CS_UIAmbushMarkModel oldModel) &&
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

        if (_boundModels.TryGetValue(playerNumber, out CS_UIAmbushMarkModel model) &&
            model == this)
        {
            _boundModels.Remove(playerNumber);
        }

        _boundPlayerNumber = -1;
        _isBound = false;
        _target.Value = null;

        OnUnbound?.Invoke(playerNumber);
    }

    /// <summary>対象の悪人を設定して星を出す(同じ対象なら何もしない)。</summary>
    public void SetTarget(Transform targetTransform)
    {
        _target.Value = targetTransform;
    }

    /// <summary>星を弾けさせて消す(対象がいない時は何もしない)。</summary>
    public void Burst()
    {
        if (_target.Value == null)
            return;

        _burst.OnNext(Unit.Default);
        _target.Value = null;
    }

    /// <summary>弾けずに、すぐ星を消す。</summary>
    public void ClearTarget()
    {
        _target.Value = null;
    }

    public override void Dispose()
    {
        Unbind();
        _target.Dispose();
        _burst.Dispose();
    }
}
