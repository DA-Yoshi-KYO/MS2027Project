/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-26 | 初回作成
 * ================================================ */

using R3;

/// <summary>
/// 必殺技ゲージ（Special Gauge）のPresenter
/// ・ローカルプレイヤーの番号と一致したら表示する
/// ・Bind 時にアイコンをセットする
/// </summary>
public class CS_UISpecialGaugePresenter : CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UISpecialGaugeView _view;
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        _view = GetComponent<CS_UISpecialGaugeView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        CS_UISpecialGaugeModel.OnBound += HandleBound;
        CS_UISpecialGaugeModel.OnUnbound += HandleUnbound;

        // すでに Bind 済みのローカルプレイヤーの Model があれば即購読
        int localNumber = CS_UISpecialGaugeModel.localPlayerNumber;
        if (localNumber >= 0 && CS_UISpecialGaugeModel.TryGet(localNumber, out var model))
        {
            // ★ アイコンをセット
            _view.SetupIcon(localNumber);
            BindModel(model);
        }
    }

    void OnDisable()
    {
        CS_UISpecialGaugeModel.OnBound -= HandleBound;
        CS_UISpecialGaugeModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    // =========================================================
    // Bind / Unbind ハンドラ
    // =========================================================

    private void HandleBound(int playerNumber, CS_UISpecialGaugeModel model)
    {
        if (playerNumber != CS_UISpecialGaugeModel.localPlayerNumber) return;

        // ★ アイコンをセット
        _view.SetupIcon(playerNumber);
        BindModel(model);
    }

    private void HandleUnbound(int playerNumber)
    {
        if (playerNumber == CS_UISpecialGaugeModel.localPlayerNumber)
            UnbindModel();
    }

    // =========================================================
    // Model 購読
    // =========================================================

    private void BindModel(CS_UISpecialGaugeModel model)
    {
        _modelSubscription.Disposable =
            model.currentGauge.Subscribe(x => _view.UpdateGauge(x, model.maxGauge));
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
