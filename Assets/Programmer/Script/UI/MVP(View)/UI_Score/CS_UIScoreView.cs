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
/// ・全員分のスコア表示（リアルタイム更新）
/// </summary>
public class CS_UIScoreView : CS_BaseView<CS_UIScorePresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ランキング表示")]
    [SerializeField] private Transform _scoreRoot;       // スコアアイテムの親
    [SerializeField] private GameObject _scoreItemPrefab; // CS_UIScoreItemView を持つ Prefab

    // =========================================================
    // 内部フィールド
    // =========================================================

    private readonly List<CS_UIScoreItemView> _rankingItems = new();

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIScorePresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // スコア表示（リアルタイム更新）
    // =========================================================

    /// <summary>
    /// 全員分のスコアを更新する
    /// Presenter から渡されるリストはスコア降順にソート済み
    /// </summary>
    public void UpdateRanking(List<(int playerNumber, int score)> ranking)
    {
        // 人数分のアイテムを確保
        EnsureScoreItems(ranking.Count);

        for (int i = 0; i < ranking.Count; i++)
        {
            var (playerNumber, score) = ranking[i];
            _rankingItems[i].UpdateView(
                rank: i + 1,
                playerNumber: playerNumber,
                score: score
            );
            _rankingItems[i].gameObject.SetActive(true);
        }

        // 余分なアイテムを非表示
        for (int i = ranking.Count; i < _rankingItems.Count; i++)
            _rankingItems[i].gameObject.SetActive(false);
    }

    // =========================================================
    // スコアアイテム管理
    // =========================================================

    private void EnsureScoreItems(int count)
    {
        while (_rankingItems.Count < count)
        {
            var go = Instantiate(_scoreItemPrefab, _scoreRoot);
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
    }
}
