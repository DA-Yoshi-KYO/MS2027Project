/* ================================================
 * 　Hpの初期化を行う
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// Hpの初期化を行う
/// </summary>
public class CS_UIHpMVPInit : CS_BaseInitMVP<CS_UIHpModel, CS_UIHpPresenter, CS_UIHpView>
{
    [Header("初期HP設定")]
    [SerializeField] private int _initHP = 100;
    [SerializeField] private int _maxHP = 100;

    public CS_UIHpPresenter Presenter => _presenter;

    protected override void InitModel(CS_UIHpModel model)
    {
        model._maxHp.Value = _maxHP;
        model.SetHp(_initHP);
    }

    protected override CS_UIHpPresenter CreatePresenter(CS_UIHpModel model, CS_UIHpView view)
    {
        return new CS_UIHpPresenter(model, view);
    }
}
