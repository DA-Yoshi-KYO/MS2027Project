/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * 2026-11-02 | ソロの時は順位を出さないようにする
 * ================================================ */

/// <summary>
/// スコアの変化を View に通知する
/// ・全員分のランキング表示（リアルタイム更新）
/// ・必殺技ゲージ満タン状態の変化も受け取る
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

        // ソロは自分1人だけなので、順位は出さずにアイコンと点数だけを出す
        _view.SetRankVisible(!CS_GameModeStore.isSolo);
    }

    void OnEnable()
    {
        CS_UIScoreModel.OnRankingUpdated += HandleRankingUpdated;
        CS_UISpecialGaugeModel.OnGaugeFullChanged += HandleGaugeFullChanged; // ★ 追加

        // 初期表示
        HandleRankingUpdated();
    }

    void OnDisable()
    {
        CS_UIScoreModel.OnRankingUpdated -= HandleRankingUpdated;
        CS_UISpecialGaugeModel.OnGaugeFullChanged -= HandleGaugeFullChanged; // ★ 追加
    }

    // =========================================================
    // ランキング更新
    // =========================================================

    private void HandleRankingUpdated()
    {
        _view.UpdateRanking(CS_UIScoreModel.GetRanking());
    }

    // =========================================================
    // ★ 必殺技ゲージ満タン状態の変化
    // =========================================================

    private void HandleGaugeFullChanged(int playerNumber, bool isFull)
    {
        _view.UpdateIconColor(playerNumber, isFull);
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}
