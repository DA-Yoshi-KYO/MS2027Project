/* ================================================
 * 敵のHP状態を保持
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * 2026-09-25 | 番号付きBindを廃止し、親(Villain)のTransformでBindする形に変更
 * ================================================ */

using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

/// <summary>
/// 敵のHP状態を保持
/// Bind(親Transform) で「どのVillainのHPか」を公開し、その子のHPバーが拾って表示する
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public class CS_UIVillainHpModel : CS_BaseModel
{
    // ---------------- Villainごとに公開されたModelの一覧 ----------------

    private static readonly Dictionary<Transform, CS_UIVillainHpModel> _boundModels = new Dictionary<Transform, CS_UIVillainHpModel>();

    //Bindされた時の通知(親Transform, Model)
    public static event Action<Transform, CS_UIVillainHpModel> OnBound;

    //Bindが外れた時の通知(親Transform)
    public static event Action<Transform> OnUnbound;

    //指定したVillainのModelを取得する(HPバーが後から有効になった場合に使う)
    public static bool TryGet(Transform parentTransform, out CS_UIVillainHpModel model)
    {
        return _boundModels.TryGetValue(parentTransform, out model);
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
    private Transform _parentTransform;   //null = 未Bind

    //外部からは読み取り専用
    public ReadOnlyReactiveProperty<int> currentHp => _currentHp;
    public ReadOnlyReactiveProperty<int> maxHp => _maxHp;

    //満タンで始める
    public CS_UIVillainHpModel(int maxHp) : this(maxHp, maxHp) { }

    public CS_UIVillainHpModel(int maxHp, int currentHp)
    {
        _maxHp = new ReactiveProperty<int>(Mathf.Max(1, maxHp));
        _currentHp = new ReactiveProperty<int>(Mathf.Clamp(currentHp, 0, _maxHp.Value));
    }

    // ---------------- 公開 ----------------

    //このModelをどのVillainのHPとして公開するか(HPバーの親のTransformを渡す)
    public void Bind(Transform parentTransform)
    {
        Unbind();

        _parentTransform = parentTransform;
        _boundModels[parentTransform] = this;
        OnBound?.Invoke(parentTransform, this);
    }

    //公開をやめる
    public void Unbind()
    {
        if (_parentTransform == null) return;

        //別のModelで上書きされていなければ外す
        if (_boundModels.TryGetValue(_parentTransform, out var current) && current == this)
        {
            _boundModels.Remove(_parentTransform);
            OnUnbound?.Invoke(_parentTransform);
        }
        _parentTransform = null;
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
