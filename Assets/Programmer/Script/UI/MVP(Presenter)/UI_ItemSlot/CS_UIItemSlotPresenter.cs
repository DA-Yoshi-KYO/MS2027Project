/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-27 | 初回作成
 * ================================================ */

/// <summary>
/// アイテムスロットの Presenter
/// Model（アイテム情報）と View（見た目）を仲介する役割。
/// 
/// ・Model が Bind されたらアイコンを表示
/// ・Model が Unbind されたら非表示
/// ・初期状態で Model が無ければ非表示
/// 
/// GameObject.SetActive(false) は使わず、CanvasGroup で見た目だけ消す。
/// </summary>
public class CS_UIItemSlotPresenter : CS_BasePresenter
{
    private CS_UIItemSlotView _view;   // View への参照
    private CS_UIItemSlotModel _model; // 現在バインドされている Model

    void Awake()
    {
        _view = GetComponent<CS_UIItemSlotView>();
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        CS_UIItemSlotModel.OnBound += HandleBound;
        CS_UIItemSlotModel.OnUnbound += HandleUnbound;

        // 初期状態：Model が存在しないなら非表示
        if (!CS_UIItemSlotModel.TryGet(out var model))
        {
            _view.SetVisible(false);
            return;
        }

        // Model が存在するなら通常通り Bind
        BindModel(model);
        _view.SetIcon(model.iconSprite);
        _view.SetVisible(true);
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
        _view.SetIcon(model.iconSprite);
        _view.SetVisible(true);
    }

    private void HandleUnbound()
    {
        UnbindModel();
        _view.SetVisible(false);
    }

    private void BindModel(CS_UIItemSlotModel model)
    {
        _model = model;
    }

    private void UnbindModel()
    {
        _model?.Dispose();
        _model = null;
    }
}
