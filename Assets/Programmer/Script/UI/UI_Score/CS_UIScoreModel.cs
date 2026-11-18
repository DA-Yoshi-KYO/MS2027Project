/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーごとのスコア状態を保持するModel。
///
/// ・Bind(playerNumber, characterType)で公開する
/// ・プレイヤー番号とキャラクター種類を保持する
/// ・全員分のランキングをリアルタイムで公開する
/// ・従来形式とキャラクター情報付き形式の両方を取得できる
///
/// Bind()は、このModelを所有するゲーム側が呼ぶ。
/// PresenterやViewからは呼ばない。
/// </summary>
public sealed class CS_UIScoreModel :
    CS_BaseModel
{
    // =========================================================
    // ランキング1件分の詳細データ
    // =========================================================

    /// <summary>
    /// キャラクター情報を含むランキングデータ。
    /// 新しいランキングUIで使用する。
    /// </summary>
    public readonly struct RankingData
    {
        // -----------------------------------------------------
        // バッキングフィールド
        // -----------------------------------------------------

        private readonly int _playerNumber;
        private readonly int _score;

        private readonly CSE_CharacterType
            _characterType;

        // -----------------------------------------------------
        // 公開プロパティ
        // -----------------------------------------------------

        public int playerNumber =>
            _playerNumber;

        public int score =>
            _score;

        public CSE_CharacterType characterType =>
            _characterType;

        // -----------------------------------------------------
        // コンストラクター
        // -----------------------------------------------------

        public RankingData(
            int playerNumber,
            int score,
            CSE_CharacterType characterType)
        {
            _playerNumber =
                playerNumber;

            _score =
                score;

            _characterType =
                characterType;
        }
    }

    // =========================================================
    // 番号付きで公開されたModelの一覧
    // =========================================================

    private static readonly Dictionary<
        int,
        CS_UIScoreModel
    > _boundModels =
        new Dictionary<
            int,
            CS_UIScoreModel
        >();

    /// <summary>
    /// ランキング内容が変更されたときの通知。
    /// </summary>
    public static event Action OnRankingUpdated;

    // =========================================================
    // 状態
    // =========================================================

    private readonly ReactiveProperty<int>
        _score;

    private int _playerNumber = -1;

    private CSE_CharacterType
        _characterType;

    // =========================================================
    // 公開プロパティ
    // =========================================================

    /// <summary>
    /// 現在のスコア。
    /// </summary>
    public ReadOnlyReactiveProperty<int> score =>
        _score;

    /// <summary>
    /// Bindされたプレイヤー番号。
    /// 未Bindの場合は-1。
    /// </summary>
    public int playerNumber =>
        _playerNumber;

    /// <summary>
    /// Bindされたキャラクター種類。
    /// </summary>
    public CSE_CharacterType characterType =>
        _characterType;

    // =========================================================
    // コンストラクター
    // =========================================================

    public CS_UIScoreModel(
        int initialScore = 0)
    {
        _score =
            new ReactiveProperty<int>(
                initialScore
            );
    }

    // =========================================================
    // 従来形式のランキング取得
    // =========================================================

    /// <summary>
    /// 従来形式でランキングを取得する。
    ///
    /// 既存コードとの互換性を維持するため、
    /// playerNumberとscoreのタプルを返す。
    /// </summary>
    public static List<(
        int playerNumber,
        int score
    )> GetRanking()
    {
        List<(
            int playerNumber,
            int score
        )> ranking =
            new List<(
                int playerNumber,
                int score
            )>();

        foreach (
            KeyValuePair<
                int,
                CS_UIScoreModel
            > pair
            in _boundModels)
        {
            CS_UIScoreModel model =
                pair.Value;

            if (model == null)
            {
                continue;
            }

            ranking.Add(
                (
                    playerNumber:
                        pair.Key,

                    score:
                        model._score.Value
                )
            );
        }

        SortLegacyRanking(ranking);

        return ranking;
    }

    /// <summary>
    /// 従来形式のランキングを、
    /// スコア降順で並べ替える。
    ///
    /// 同点の場合はプレイヤー番号順にする。
    /// </summary>
    private static void SortLegacyRanking(
        List<(
            int playerNumber,
            int score
        )> ranking)
    {
        ranking.Sort(
            (left, right) =>
            {
                int scoreComparison =
                    right.score.CompareTo(
                        left.score
                    );

                if (scoreComparison != 0)
                {
                    return scoreComparison;
                }

                return
                    left.playerNumber.CompareTo(
                        right.playerNumber
                    );
            }
        );
    }

    // =========================================================
    // キャラクター情報付きランキング取得
    // =========================================================

    /// <summary>
    /// キャラクター情報を含むランキングを取得する。
    ///
    /// 新しいスコアランキングUIでは、
    /// こちらを使用する。
    /// </summary>
    public static List<RankingData>
        GetRankingData()
    {
        List<RankingData> ranking =
            new List<RankingData>();

        foreach (
            KeyValuePair<
                int,
                CS_UIScoreModel
            > pair
            in _boundModels)
        {
            CS_UIScoreModel model =
                pair.Value;

            if (model == null)
            {
                continue;
            }

            ranking.Add(
                new RankingData(
                    playerNumber:
                        pair.Key,

                    score:
                        model._score.Value,

                    characterType:
                        model._characterType
                )
            );
        }

        SortRankingData(ranking);

        return ranking;
    }

    /// <summary>
    /// キャラクター情報付きランキングを、
    /// スコア降順で並べ替える。
    ///
    /// 同点の場合はプレイヤー番号順にする。
    /// </summary>
    private static void SortRankingData(
        List<RankingData> ranking)
    {
        ranking.Sort(
            (left, right) =>
            {
                int scoreComparison =
                    right.score.CompareTo(
                        left.score
                    );

                if (scoreComparison != 0)
                {
                    return scoreComparison;
                }

                return
                    left.playerNumber.CompareTo(
                        right.playerNumber
                    );
            }
        );
    }

    // =========================================================
    // Domain Reload対策
    // =========================================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType
            .SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModels.Clear();

        OnRankingUpdated = null;
    }

    // =========================================================
    // Bind / Unbind
    // =========================================================

    /// <summary>
    /// このModelを指定プレイヤーのスコアとして公開する。
    ///
    /// Modelを所有するゲーム側が呼ぶ。
    /// PresenterやViewからは呼ばない。
    /// </summary>
    public void Bind(
        int playerNumber,
        CSE_CharacterType characterType)
    {
        Unbind();

        _playerNumber =
            playerNumber;

        _characterType =
            characterType;

        _boundModels[playerNumber] =
            this;

        OnRankingUpdated?.Invoke();
    }

    /// <summary>
    /// このModelの公開を解除する。
    /// </summary>
    public void Unbind()
    {
        if (_playerNumber < 0)
        {
            return;
        }

        if (_boundModels.TryGetValue(
                _playerNumber,
                out CS_UIScoreModel current) &&
            current == this)
        {
            _boundModels.Remove(
                _playerNumber
            );

            OnRankingUpdated?.Invoke();
        }

        _playerNumber = -1;
    }

    // =========================================================
    // 値の変更
    // =========================================================

    /// <summary>
    /// スコアを加算する。
    /// </summary>
    public void AddScore(int value)
    {
        if (value == 0)
        {
            return;
        }

        _score.Value += value;

        OnRankingUpdated?.Invoke();
    }

    /// <summary>
    /// スコアを設定する。
    /// </summary>
    public void SetScore(int value)
    {
        if (_score.Value == value)
        {
            return;
        }

        _score.Value = value;

        OnRankingUpdated?.Invoke();
    }

    // =========================================================
    // Dispose
    // =========================================================

    public override void Dispose()
    {
        Unbind();

        _score.Dispose();
    }
}