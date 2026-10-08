/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

/// <summary>
/// スコアの変化を View に通知する
/// ・全員分のランキング表示（リアルタイム更新）
/// ・自分のスコア表示は削除済み
/// </summary>
public class CS_UIScorePresenter : CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIScoreView _view;

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        _view = GetComponent<CS_UIScoreView>();
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        CS_UIScoreModel.OnRankingUpdated += HandleRankingUpdated;

        // 初期表示
        HandleRankingUpdated();
    }

    void OnDisable()
    {
        CS_UIScoreModel.OnRankingUpdated -= HandleRankingUpdated;
    }

    // =========================================================
    // ランキング更新
    // =========================================================

    private void HandleRankingUpdated()
    {
        _view.UpdateRanking(CS_UIScoreModel.GetRanking());
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}