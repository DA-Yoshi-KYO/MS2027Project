/* ================================================
 * 　Hpの変化をViewに通知する
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * 2026-09-25 | 番号付きBindを廃止し、親(Villain)のTransformでModelを拾う形に変更
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// Hpの変化をViewに通知する(Model → View の一方向)
/// HPバーにアタッチし、Villainの直下に置く
/// 親のTransformでBindされたHpModelを拾って購読する
/// Model・HPバーのどちらが先に生成されても紐づく
/// Modelの破棄は持ち主(使用者)が行うので、ここでは購読解除だけ行う
/// </summary>
public class CS_UIVillainHpPresenter : CS_BasePresenter
{
    private CS_UIVillainHpView _view;

    //Modelを探すキー(直下に置かれている親のTransform)
    private Transform _parentTransform;

    //今紐づいているModelの購読(新しいModelを入れると前の購読は自動で解除される)
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    void Awake()
    {
        _view = GetComponent<CS_UIVillainHpView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        _parentTransform = transform.parent;

        CS_UIVillainHpModel.OnBound += HandleBound;
        CS_UIVillainHpModel.OnUnbound += HandleUnbound;

        if (_parentTransform != null && CS_UIVillainHpModel.TryGet(_parentTransform, out var model))
        {
            BindModel(model);
        }
    }

    void OnDisable()
    {
        CS_UIVillainHpModel.OnBound -= HandleBound;
        CS_UIVillainHpModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    private void HandleBound(Transform parentTransform, CS_UIVillainHpModel model)
    {
        if (parentTransform == _parentTransform) BindModel(model);
    }

    private void HandleUnbound(Transform parentTransform)
    {
        if (parentTransform == _parentTransform) UnbindModel();
    }

    //Modelを購読してViewに反映する
    private void BindModel(CS_UIVillainHpModel model)
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
