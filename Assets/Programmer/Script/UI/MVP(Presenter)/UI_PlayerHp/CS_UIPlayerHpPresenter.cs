/* ================================================
 * 　Hpの変化をViewに通知する
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-09-25 | HPバーにアタッチし、指定番号のModelがBindされたら紐づける形に変更
 * ================================================ */

using R3;

/// <summary>
/// Hpの変化をViewに通知する
/// ・ローカルプレイヤーの番号と一致したら表示する
/// ・_playerNumber の指定不要（Model の localPlayerNumber を使う）
/// </summary>
public class CS_UIPlayerHpPresenter : CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIPlayerHpView _view;
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    // =========================================================
    // 初期化
    // =========================================================

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

        // すでに Bind 済みのローカルプレイヤーの Model があれば即購読
        int localNumber = CS_UIPlayerHpModel.localPlayerNumber;
        if (localNumber >= 0 && CS_UIPlayerHpModel.TryGet(localNumber, out var model))
            BindModel(model);
    }

    void OnDisable()
    {
        CS_UIPlayerHpModel.OnBound -= HandleBound;
        CS_UIPlayerHpModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    // =========================================================
    // Bind / Unbind ハンドラ
    // =========================================================

    private void HandleBound(int playerNumber, CS_UIPlayerHpModel model)
    {
        // ローカルプレイヤーの番号と一致したら表示
        if (playerNumber == CS_UIPlayerHpModel.localPlayerNumber)
            BindModel(model);
    }

    private void HandleUnbound(int playerNumber)
    {
        if (playerNumber == CS_UIPlayerHpModel.localPlayerNumber)
            UnbindModel();
    }

    // =========================================================
    // Model 購読
    // =========================================================

    private void BindModel(CS_UIPlayerHpModel model)
    {
        _modelSubscription.Disposable = model.currentHp
            .CombineLatest(model.maxHp, (hp, max) => (hp, max))
            .Subscribe(x => _view.UpdateHp(x.hp, x.max));
    }

    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}
