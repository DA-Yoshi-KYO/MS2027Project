/* ================================================
 * スコアの表示だけを担当するView
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * 2026-11-02 | ソロの時は順位を出さないようにする
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スコアランキングの表示を担当するView。
/// ・スコア降順で一覧表示する
/// ・同じスコアのプレイヤーは同順位として表示する
/// ・必殺技ゲージ満タン時にアイコンの色を変更する
/// </summary>
public class CS_UIScoreView : CS_BaseView<CS_UIScorePresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ランキング表示")]
    [SerializeField] private Transform _rankingRoot;
    [SerializeField] private GameObject _rankingItemPrefab;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private readonly Dictionary<int, CS_UIScoreItemView>
        _rankingItemMap = new();

    private readonly List<CS_UIScoreItemView>
        _rankingItems = new();

    // 順位を出すか(ソロでは出さない)
    private bool _isRankVisible = true;

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIScorePresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // ランキング表示
    // =========================================================

    /// <summary>
    /// 全員分のランキングを更新する。
    /// rankingはスコア降順にソート済みであることを前提とする。
    /// 同じスコアは同順位として表示する。
    /// </summary>
    public void UpdateRanking(
        List<(int playerNumber, int score)> ranking)
    {
        if (ranking == null)
            return;

        EnsureRankingItems(ranking.Count);
        _rankingItemMap.Clear();

        for (int i = 0; i < ranking.Count; i++)
        {
            var (playerNumber, score) = ranking[i];
            CS_UIScoreItemView item = _rankingItems[i];

            int rank = CalculateCompetitionRank(
                ranking,
                score
            );

            item.UpdateView(
                rank: rank,
                playerNumber: playerNumber,
                score: score,
                isFull: CS_UISpecialGaugeModel.IsFull(playerNumber)
            );

            item.gameObject.SetActive(true);
            _rankingItemMap[playerNumber] = item;
        }

        // 参加人数が減った場合、余った表示枠を非表示にする。
        for (int i = ranking.Count; i < _rankingItems.Count; i++)
            _rankingItems[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// 競技順位を計算する。
    /// 順位 = 対象スコアより高いプレイヤー数 + 1。
    /// 例：100、100、50、20点の場合は1位、1位、3位、4位。
    /// </summary>
    private int CalculateCompetitionRank(
        List<(int playerNumber, int score)> ranking,
        int targetScore)
    {
        int higherScorePlayerCount = 0;

        foreach (var result in ranking)
        {
            if (result.score > targetScore)
                higherScorePlayerCount++;
        }

        return higherScorePlayerCount + 1;
    }

    // =========================================================
    // 順位の表示
    // =========================================================

    /// <summary>
    /// 順位の表示を出すかどうかを切り替える。
    /// ソロでは自分1人だけなので順位を出さず、アイコンと点数だけにする。
    /// </summary>
    public void SetRankVisible(bool isVisible)
    {
        _isRankVisible = isVisible;

        foreach (CS_UIScoreItemView item in _rankingItems)
            item.SetRankVisible(isVisible);
    }

    // =========================================================
    // 必殺技ゲージ満タン表示
    // =========================================================

    public void UpdateIconColor(
        int playerNumber,
        bool isFull)
    {
        if (_rankingItemMap.TryGetValue(
            playerNumber,
            out CS_UIScoreItemView item))
        {
            item.SetIconFull(isFull);
        }
    }

    // =========================================================
    // ランキングアイテム管理
    // =========================================================

    private void EnsureRankingItems(int count)
    {
        while (_rankingItems.Count < count)
        {
            GameObject itemObject = Instantiate(
                _rankingItemPrefab,
                _rankingRoot
            );

            CS_UIScoreItemView item =
                itemObject.GetComponent<CS_UIScoreItemView>();

            if (item == null)
            {
                Debug.LogError(
                    "[CS_UIScoreView] RankingItemPrefabに" +
                    "CS_UIRankingItemViewがありません。"
                );

                Destroy(itemObject);
                return;
            }

            item.SetRankVisible(_isRankVisible);
            _rankingItems.Add(item);
        }
    }

    // =========================================================
    // クリーンアップ
    // =========================================================

    private void OnDestroy()
    {
        foreach (CS_UIScoreItemView item in _rankingItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        _rankingItems.Clear();
        _rankingItemMap.Clear();
    }
}
