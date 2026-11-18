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
///
/// ・スコア降順で一覧表示する
/// ・同じスコアのプレイヤーは同順位として表示する
/// ・各プレイヤーのキャラクターアイコンを表示する
/// ・ソロでは順位を非表示にする
/// ・必殺技ゲージ満タン時にランキングアイコンの色を変更する
/// </summary>
public sealed class CS_UIScoreView :
    CS_BaseView<CS_UIScorePresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ランキング表示")]

    [SerializeField]
    private Transform _rankingRoot;

    [SerializeField]
    private GameObject _rankingItemPrefab;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private readonly Dictionary<
        int,
        CS_UIScoreItemView
    > _rankingItemMap =
        new Dictionary<int, CS_UIScoreItemView>();

    private readonly List<
        CS_UIScoreItemView
    > _rankingItems =
        new List<CS_UIScoreItemView>();

    // ソロでは順位を表示しない。
    private bool _isRankVisible = true;

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(
        CS_UIScorePresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // ランキング表示
    // =========================================================

    /// <summary>
    /// 全プレイヤー分のランキングを更新する。
    ///
    /// rankingはスコア降順に並んでいることを前提とする。
    /// 同じスコアは同順位として表示する。
    /// </summary>
    public void UpdateRanking(
        List<CS_UIScoreModel.RankingData> ranking)
    {
        if (ranking == null)
        {
            return;
        }

        EnsureRankingItems(
            ranking.Count
        );

        _rankingItemMap.Clear();

        for (int i = 0;
             i < ranking.Count;
             i++)
        {
            if (i >= _rankingItems.Count)
            {
                break;
            }

            CS_UIScoreModel.RankingData data =
                ranking[i];

            CS_UIScoreItemView item =
                _rankingItems[i];

            if (item == null)
            {
                continue;
            }

            int rank =
                CalculateCompetitionRank(
                    ranking,
                    data.score
                );

            item.UpdateView(
                rank: rank,
                playerNumber: data.playerNumber,
                score: data.score,
                characterType: data.characterType,
                isFull:
                    CS_UISpecialGaugeModel.IsFull(
                        data.playerNumber
                    )
            );

            item.SetRankVisible(
                _isRankVisible
            );

            item.gameObject.SetActive(true);

            _rankingItemMap[
                data.playerNumber
            ] = item;
        }

        // 参加人数が減った場合、
        // 余った表示枠を非表示にする。
        for (int i = ranking.Count;
             i < _rankingItems.Count;
             i++)
        {
            CS_UIScoreItemView item =
                _rankingItems[i];

            if (item != null)
            {
                item.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 競技順位を計算する。
    ///
    /// 100、100、50、20点の場合:
    /// 1位、1位、3位、4位。
    /// </summary>
    private static int CalculateCompetitionRank(
        List<CS_UIScoreModel.RankingData> ranking,
        int targetScore)
    {
        int higherScorePlayerCount = 0;

        for (int i = 0;
             i < ranking.Count;
             i++)
        {
            if (ranking[i].score >
                targetScore)
            {
                higherScorePlayerCount++;
            }
        }

        return higherScorePlayerCount + 1;
    }

    // =========================================================
    // 順位表示
    // =========================================================

    /// <summary>
    /// 順位テキストを表示するか設定する。
    ///
    /// ソロ:
    /// 順位を非表示にしてアイコンとスコアだけ表示する。
    ///
    /// マルチ:
    /// 順位、アイコン、スコアを表示する。
    /// </summary>
    public void SetRankVisible(
        bool isVisible)
    {
        _isRankVisible = isVisible;

        for (int i = 0;
             i < _rankingItems.Count;
             i++)
        {
            CS_UIScoreItemView item =
                _rankingItems[i];

            if (item != null)
            {
                item.SetRankVisible(
                    isVisible
                );
            }
        }
    }

    // =========================================================
    // 必殺技ゲージ満タン表示
    // =========================================================

    /// <summary>
    /// 指定プレイヤーのランキングアイコン色を更新する。
    /// </summary>
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
    // ランキングアイテム生成
    // =========================================================

    private void EnsureRankingItems(int count)
    {
        if (_rankingRoot == null)
        {
            Debug.LogError(
                $"{nameof(CS_UIScoreView)}: " +
                "RankingRootが設定されていません。",
                this
            );

            return;
        }

        if (_rankingItemPrefab == null)
        {
            Debug.LogError(
                $"{nameof(CS_UIScoreView)}: " +
                "RankingItemPrefabが設定されていません。",
                this
            );

            return;
        }

        while (_rankingItems.Count < count)
        {
            GameObject itemObject =
                Instantiate(
                    _rankingItemPrefab,
                    _rankingRoot
                );

            CS_UIScoreItemView item =
                itemObject.GetComponent<
                    CS_UIScoreItemView
                >();

            if (item == null)
            {
                Debug.LogError(
                    $"{nameof(CS_UIScoreView)}: " +
                    "RankingItemPrefabに" +
                    $"{nameof(CS_UIScoreItemView)}が" +
                    "ありません。",
                    itemObject
                );

                Destroy(itemObject);

                return;
            }

            item.SetRankVisible(
                _isRankVisible
            );

            _rankingItems.Add(item);
        }
    }

    // =========================================================
    // クリーンアップ
    // =========================================================

    private void OnDestroy()
    {
        for (int i = 0;
             i < _rankingItems.Count;
             i++)
        {
            CS_UIScoreItemView item =
                _rankingItems[i];

            if (item != null)
            {
                Destroy(
                    item.gameObject
                );
            }
        }

        _rankingItems.Clear();
        _rankingItemMap.Clear();
    }
}