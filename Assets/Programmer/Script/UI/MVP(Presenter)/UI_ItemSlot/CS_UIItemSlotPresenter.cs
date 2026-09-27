/* ================================================
 *
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-27 | 初回作成
 * 2026-09-28 | Modelのアイコン(ReactiveProperty)を購読してViewに反映する形に変更
 * ================================================ */

using R3;

/// <summary>
/// アイテムスロットの Presenter
/// Model（アイテム情報）と View（見た目）を仲介する役割。(Model → View の一方向)
///
/// ・Model が Bind されたらアイコンの購読を始める
/// ・アイコンが変わったら View を更新（null なら非表示）
/// ・Model が Unbind されたら非表示
/// ・Model・スロットUIのどちらが先に生成されても紐づく
///
/// Modelの破棄は持ち主(使用者)が行うので、ここでは購読解除だけ行う
/// </summary>
public class CS_UIItemSlotPresenter : CS_BasePresenter
{
    private CS_UIItemSlotView _view;   // View への参照

    //今紐づいているModelの購読(新しいModelを入れると前の購読は自動で解除される)
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    void Awake()
    {
        _view = GetComponent<CS_UIItemSlotView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        CS_UIItemSlotModel.OnBound += HandleBound;
        CS_UIItemSlotModel.OnUnbound += HandleUnbound;

        // スロットUIより先にModelがBindされていた場合
        if (CS_UIItemSlotModel.TryGet(out var model))
        {
            BindModel(model);
        }
        else
        {
            _view.SetVisible(false);
        }
    }

    void OnDisable()
    {
        CS_UIItemSlotModel.OnBound -= HandleBound;
        CS_UIItemSlotModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    private void HandleBound(CS_UIItemSlotModel model)
    {
        BindModel(model);
    }

    private void HandleUnbound()
    {
        UnbindModel();
        _view.SetVisible(false);
    }

    //Modelのアイコンを購読してViewに反映する
    //(購読した瞬間に現在値が流れるので、初期表示もここで行われる)
    private void BindModel(CS_UIItemSlotModel model)
    {
        _modelSubscription.Disposable = model.icon.Subscribe(sprite =>
        {
            _view.SetIcon(sprite);
            _view.SetVisible(sprite != null);
        });
    }

    //Modelの購読をやめる
    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
    }
}
