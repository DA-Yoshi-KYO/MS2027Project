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
public class CS_UIPlayerHpPresenter : CS_BasePresenter
{
    [Header("何番目のプレイヤーのHPを表示するか(0〜3)")][SerializeField] private int _playerNumber;
    private CS_UIPlayerHpView _view;

    //今紐づいているModelの購読(新しいModelを入れると前の購読は自動で解除される)
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    void Awake()
    {
        _view = GetComponent<CS_UIPlayerHpView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        CS_UIPlayerHpModel.OnBound += HandleBound;
        CS_UIPlayerHpModel.OnUnbound += HandleUnbound;

        // Model が存在するなら通常通り Bind
        if (CS_UIPlayerHpModel.TryGet(_playerNumber, out var model))
        {
            BindModel(model);
        }
    }

    void OnDisable()
    {
        CS_UIPlayerHpModel.OnBound -= HandleBound;
        CS_UIPlayerHpModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    private void HandleBound(int playerNumber, CS_UIPlayerHpModel model)
    {
        if (playerNumber == _playerNumber)
        {
            _view.SetVisible(true);   // ← 見た目だけ表示
            BindModel(model);
        }
    }

    private void HandleUnbound(int playerNumber)
    {
        if (playerNumber == _playerNumber)
        {
            UnbindModel();
            _view.SetVisible(false);
        }
    }

    //Modelを購読してViewに反映する
    private void BindModel(CS_UIPlayerHpModel model)
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
