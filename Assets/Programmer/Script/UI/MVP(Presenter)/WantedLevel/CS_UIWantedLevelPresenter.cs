/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-09 | 初回作成
 * ================================================ */

using R3;

/// <summary>
/// 手配度の変化を View に通知する
/// ・ローカルプレイヤーの番号と一致したら表示する
/// </summary>
public class CS_UIWantedLevelPresenter : CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIWantedLevelView _view;
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        _view = GetComponent<CS_UIWantedLevelView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        CS_UIWantedLevelModel.OnBound += HandleBound;
        CS_UIWantedLevelModel.OnUnbound += HandleUnbound;

        // すでに Bind 済みのローカルプレイヤーの Model があれば即購読
        int localNumber = CS_UIWantedLevelModel.localPlayerNumber;
        if (localNumber >= 0 && CS_UIWantedLevelModel.TryGet(localNumber, out var model))
            BindModel(model);
    }

    void OnDisable()
    {
        CS_UIWantedLevelModel.OnBound -= HandleBound;
        CS_UIWantedLevelModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    // =========================================================
    // Bind / Unbind ハンドラ
    // =========================================================

    private void HandleBound(int playerNumber, CS_UIWantedLevelModel model)
    {
        if (playerNumber == CS_UIWantedLevelModel.localPlayerNumber)
            BindModel(model);
    }

    private void HandleUnbound(int playerNumber)
    {
        if (playerNumber == CS_UIWantedLevelModel.localPlayerNumber)
            UnbindModel();
    }

    // =========================================================
    // Model 購読
    // =========================================================

    private void BindModel(CS_UIWantedLevelModel model)
    {
        _modelSubscription.Disposable = model.currentLevel
            .CombineLatest(model.maxLevel, (level, max) => (level, max))
            .Subscribe(x => _view.UpdateWantedLevel(x.level, x.max));
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
