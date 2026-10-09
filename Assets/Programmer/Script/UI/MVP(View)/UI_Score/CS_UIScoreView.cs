/* ================================================
 * スコアの表示だけを担当するView
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スコアの表示を担当する View
/// ・全員分のランキング表示（リアルタイム更新）
/// ・必殺技ゲージ満タン時にアイコンの色を変える
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

    // playerNumber → RankingItemView のマップ
    private readonly Dictionary<int, CS_UIScoreItemView> _rankingItemMap = new();
    private readonly List<CS_UIScoreItemView> _rankingItems = new();

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIScorePresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // ランキング表示（リアルタイム更新）
    // =========================================================

    public void UpdateRanking(List<(int playerNumber, int score)> ranking)
    {
        EnsureRankingItems(ranking.Count);

        // playerNumber → RankingItemView のマップを更新
        _rankingItemMap.Clear();

        for (int i = 0; i < ranking.Count; i++)
        {
            var (playerNumber, score) = ranking[i];
            var item = _rankingItems[i];

            item.UpdateView(
                rank: i + 1,
                playerNumber: playerNumber,
                score: score,
                isFull: CS_UISpecialGaugeModel.IsFull(playerNumber) // ★ 満タン状態を渡す
            );
            item.gameObject.SetActive(true);

            // ★ playerNumber と item を紐づける
            _rankingItemMap[playerNumber] = item;
        }

        // 余分なアイテムを非表示
        for (int i = ranking.Count; i < _rankingItems.Count; i++)
            _rankingItems[i].gameObject.SetActive(false);
    }

    // =========================================================
    // ★ アイコンの色を更新（満タン状態が変わったとき）
    // =========================================================

    public void UpdateIconColor(int playerNumber, bool isFull)
    {
        if (_rankingItemMap.TryGetValue(playerNumber, out var item))
            item.SetIconFull(isFull);
    }

    // =========================================================
    // ランキングアイテム管理
    // =========================================================

    private void EnsureRankingItems(int count)
    {
        while (_rankingItems.Count < count)
        {
            var go = Instantiate(_rankingItemPrefab, _rankingRoot);
            var item = go.GetComponent<CS_UIScoreItemView>();
            _rankingItems.Add(item);
        }
    }

    // =========================================================
    // クリーンアップ
    // =========================================================

    private void OnDestroy()
    {
        foreach (var item in _rankingItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        _rankingItems.Clear();
        _rankingItemMap.Clear();
    }
}
