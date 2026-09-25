/* ================================================
 * 　Hpの変化をViewに通知する
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-09-25 | HPバーにアタッチし、指定番号のModelがBindされたら紐づける形に変更
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// Hpの変化をViewに通知する(Model → View の一方向)
/// HPバーにアタッチし、指定番号のHpModelがBindされたら購読を始める
/// Model・HPバーのどちらが先に生成されても紐づく
/// Modelの破棄は持ち主(使用者)が行うので、ここでは購読解除だけ行う
/// </summary>
public class CS_UIHpPresenter : CS_BasePresenter
{
    [Header("何番目のプレイヤーのHPを表示するか(0〜3)")][SerializeField] private int _playerNumber;
    private CS_UIHpView _view;

    //今紐づいているModelの購読(新しいModelを入れると前の購読は自動で解除される)
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    void Awake()
    {
        _view = GetComponent<CS_UIHpView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        CS_UIHpModel.OnBound += HandleBound;
        CS_UIHpModel.OnUnbound += HandleUnbound;

        //HPバーより先にModelがBindされていた場合
        if (CS_UIHpModel.TryGet(_playerNumber, out var model))
        {
            BindModel(model);
        }
    }

    void OnDisable()
    {
        CS_UIHpModel.OnBound -= HandleBound;
        CS_UIHpModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    private void HandleBound(int playerNumber, CS_UIHpModel model)
    {
        if (playerNumber == _playerNumber) BindModel(model);
    }

    private void HandleUnbound(int playerNumber)
    {
        if (playerNumber == _playerNumber) UnbindModel();
    }

    //Modelを購読してViewに反映する
    private void BindModel(CS_UIHpModel model)
    {
        //現在Hp・最大Hpのどちらが変わってもViewを更新する
        //(購読した瞬間に現在値が流れるので、初期表示もここで行われる)
        _modelSubscription.Disposable = model.currentHp
            .CombineLatest(model.maxHp, (hp, max) => (hp, max))
            .Subscribe(x => _view.UpdateHp(x.hp, x.max));
    }

    //Modelの購読をやめる
    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
    }
}
