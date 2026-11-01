/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using R3;

/// <summary>
/// 倒した悪人の数の Presenter
/// ・CS_UIDefeatVillainCountView と同じ GameObject にアタッチする
/// ・Model の値変化を購読して View に伝える
/// </summary>
public class CS_UIDefeatVillainCountPresenter : CS_BasePresenter
{
    // =========================================================
    // バッキングフィールド
    // =========================================================

    private CS_UIDefeatVillainCountView _view;

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public CS_UIDefeatVillainCountView view => _view;

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        _view = GetComponent<CS_UIDefeatVillainCountView>();
        _view.SetPresenter(this);
    }

    // =========================================================
    // Controller から呼ばれる更新エントリポイント
    // =========================================================

    /// <summary>
    /// Controller が Model をセットするときに呼ぶ。
    /// Model を購読して View に反映する。
    /// </summary>
    public void BindModel(CS_UIDefeatVillainCountModel model)
    {
        // currentCount・maxCount のどちらが変わっても View を更新する
        // （購読した瞬間に現在値が流れるので初期表示もここで行われる）
        model.currentCount
            .CombineLatest(model.maxCount, (count, max) => (count, max))
            .Subscribe(x => _view.UpdateCount(x.count, x.max))
            .AddTo(_disposables);
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}
