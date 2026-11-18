/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * 2026-11-02 | ソロの時は順位を出さないようにする
 * ================================================ */

using UnityEngine;

/// <summary>
/// スコアランキングのPresenter。
///
/// ・ランキング更新通知を購読する
/// ・キャラクター情報付きランキングをViewへ渡す
/// ・必殺技ゲージ満タン状態をViewへ渡す
/// ・ソロでは順位テキストを非表示にする
///
/// CS_UIScoreModel.Bind()は呼ばない。
/// Modelを所有するゲーム側がBind()を呼ぶ。
/// </summary>
[RequireComponent(typeof(CS_UIScoreView))]
public sealed class CS_UIScorePresenter :
    CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIScoreView _view;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _view =
            GetComponent<CS_UIScoreView>();

        _view.SetPresenter(this);

        // ソロでは順位を非表示にする。
        // マルチでは順位を表示する。
        _view.SetRankVisible(
            !CS_GameModeStore.isSolo
        );
    }

    private void OnEnable()
    {
        // スコア、参加人数、キャラクター情報の
        // 変化によるランキング更新を受け取る。
        CS_UIScoreModel.OnRankingUpdated +=
            HandleRankingUpdated;

        // 必殺技ゲージの満タン状態が変化したら、
        // 対象プレイヤーのランキングアイコンを更新する。
        CS_UISpecialGaugeModel.OnGaugeFullChanged +=
            HandleGaugeFullChanged;

        // Presenterより先にScoreModelが公開済みの場合も、
        // 現在のランキングを即時反映する。
        HandleRankingUpdated();
    }

    private void OnDisable()
    {
        CS_UIScoreModel.OnRankingUpdated -=
            HandleRankingUpdated;

        CS_UISpecialGaugeModel.OnGaugeFullChanged -=
            HandleGaugeFullChanged;
    }

    // =========================================================
    // ランキング更新
    // =========================================================

    /// <summary>
    /// キャラクター情報付きランキングを取得して、
    /// Viewへ反映する。
    ///
    /// 既存コード用のGetRanking()ではなく、
    /// アイコン表示に必要なcharacterTypeを含む
    /// GetRankingData()を使用する。
    /// </summary>
    private void HandleRankingUpdated()
    {
        _view.UpdateRanking(
            CS_UIScoreModel.GetRankingData()
        );
    }

    // =========================================================
    // 必殺技ゲージ満タン状態
    // =========================================================

    private void HandleGaugeFullChanged(
        int playerNumber,
        bool isFull)
    {
        _view.UpdateIconColor(
            playerNumber,
            isFull
        );
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}