/* ================================================
 * 　Hpの変化をViewに通知する
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// Hpの変化をViewに通知する(Model → View の一方向)
/// HPバーにアタッチし、指定番号のHpModelがBindされたら購読を始める
/// Model・HPバーのどちらが先に生成されても紐づく
/// Modelの破棄は持ち主(使用者)が行うので、ここでは購読解除だけ行う
/// </summary>
public class CS_UIEnemyHpPresenter : CS_BasePresenter
{
    [Header("何番目の敵のHPを表示するか")]
    [SerializeField] private int _enemyNumber;

    private CS_UIEnemyHpView _view;
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    void Awake()
    {
        _view = GetComponent<CS_UIEnemyHpView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        CS_UIEnemyHpModel.OnBound += HandleBound;
        CS_UIEnemyHpModel.OnUnbound += HandleUnbound;

        if (CS_UIEnemyHpModel.TryGet(_enemyNumber, out var model))
        {
            BindModel(model);
        }
    }

    void OnDisable()
    {
        CS_UIEnemyHpModel.OnBound -= HandleBound;
        CS_UIEnemyHpModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    private void HandleBound(int enemyNumber, CS_UIEnemyHpModel model)
    {
        if (enemyNumber == _enemyNumber) BindModel(model);
    }

    private void HandleUnbound(int enemyNumber)
    {
        if (enemyNumber == _enemyNumber) UnbindModel();
    }

    private void BindModel(CS_UIEnemyHpModel model)
    {
        _modelSubscription.Disposable =
            model.currentHp
                .CombineLatest(model.maxHp, (hp, max) => (hp, max))
                .Subscribe(x => _view.UpdateHp(x.hp, x.max));
    }

    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
    }
}
