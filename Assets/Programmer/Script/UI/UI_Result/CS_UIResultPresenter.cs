/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using R3;

/// <summary>
/// リザルトシーンの Presenter
/// ・CS_UIResultView と同じ GameObject にアタッチする
/// ・ソロ / マルチ 両対応
/// ・Model の値変化を購読して View に伝える
/// </summary>
public class CS_UIResultPresenter : CS_BasePresenter
{
    // =========================================================
    // バッキングフィールド
    // =========================================================

    private CS_UIResultView _view;

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public CS_UIResultView view => _view;

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        _view = GetComponent<CS_UIResultView>();
        _view.SetPresenter(this);
    }

    // =========================================================
    // Controller から呼ばれる更新エントリポイント
    // =========================================================

    public void BindModel(CS_UIResultModel model)
    {
        // ソロ用
        model.soloResult
            .Subscribe(_view.UpdateSoloResult)
            .AddTo(_disposables);

        // マルチ用
        model.multiResults
            .Subscribe(_view.UpdateMultiResults)
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
