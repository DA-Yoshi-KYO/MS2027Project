/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// リザルトシーンのスコアデータを保持する Model
/// ・ソロ / マルチ 両対応
/// ・マルチの場合は参加人数分の ResultData をスコア順に並び替えて管理する
/// ・総合スコア・順位・称号は仮実装（後から拡張可能）
/// </summary>
public class CS_UIResultModel : CS_BaseModel
{
    // =========================================================
    // ソロ用データ（バッキングフィールド）
    // =========================================================

    private readonly ReactiveProperty<int> _defeatVillainCount;
    private readonly ReactiveProperty<int> _foundByPoliceCount;
    private readonly ReactiveProperty<int> _crimeCompletedCount;
    private readonly ReactiveProperty<int> _defeatPlayerCount;
    private readonly ReactiveProperty<int> _totalScore;
    private readonly ReactiveProperty<int> _rank;
    private readonly ReactiveProperty<string> _title;

    // =========================================================
    // マルチ用データ（バッキングフィールド）
    // =========================================================

    private readonly ReactiveProperty<List<PlayerResultData>> _playerResults;

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public ReadOnlyReactiveProperty<int> defeatVillainCount => _defeatVillainCount;
    public ReadOnlyReactiveProperty<int> foundByPoliceCount => _foundByPoliceCount;
    public ReadOnlyReactiveProperty<int> crimeCompletedCount => _crimeCompletedCount;
    public ReadOnlyReactiveProperty<int> defeatPlayerCount => _defeatPlayerCount;
    public ReadOnlyReactiveProperty<int> totalScore => _totalScore;
    public ReadOnlyReactiveProperty<int> rank => _rank;
    public ReadOnlyReactiveProperty<string> title => _title;
    public ReadOnlyReactiveProperty<List<PlayerResultData>> playerResults => _playerResults;

    // =========================================================
    // コンストラクタ
    // =========================================================

    public CS_UIResultModel()
    {
        _defeatVillainCount = new ReactiveProperty<int>(0);
        _foundByPoliceCount = new ReactiveProperty<int>(0);
        _crimeCompletedCount = new ReactiveProperty<int>(0);
        _defeatPlayerCount = new ReactiveProperty<int>(0);
        _totalScore = new ReactiveProperty<int>(0);
        _rank = new ReactiveProperty<int>(0);
        _title = new ReactiveProperty<string>("---");
        _playerResults = new ReactiveProperty<List<PlayerResultData>>(new List<PlayerResultData>());
    }

    // =========================================================
    // ソロ用：プレイヤーからデータを受け取る
    // =========================================================

    /// <summary>ソロ用：プレイヤーのスコアデータを一括セットする</summary>
    public void SetResultData(ResultData data)
    {
        if (data == null) return;

        _defeatVillainCount.Value = Mathf.Max(0, data.defeatVillainCount);
        _foundByPoliceCount.Value = Mathf.Max(0, data.foundByPoliceCount);
        _crimeCompletedCount.Value = Mathf.Max(0, data.crimeCompletedCount);
        _defeatPlayerCount.Value = Mathf.Max(0, data.defeatPlayerCount);
        _totalScore.Value = CalcTotalScore(data);
        _rank.Value = 0;
        _title.Value = "---"; // 仮
    }

    // =========================================================
    // マルチ用：全プレイヤーのデータを受け取る
    // =========================================================

    /// <summary>
    /// マルチ用：全プレイヤーのデータを受け取ってスコア順に並び替える
    /// </summary>
    public void SetMultiResultData(List<ResultData> allPlayersData)
    {
        if (allPlayersData == null || allPlayersData.Count == 0) return;

        // スコアを計算してリストを生成
        var results = new List<PlayerResultData>();
        foreach (var data in allPlayersData)
        {
            results.Add(new PlayerResultData(
                playerName: data.playerName,
                defeatVillainCount: data.defeatVillainCount,
                foundByPoliceCount: data.foundByPoliceCount,
                crimeCompletedCount: data.crimeCompletedCount,
                defeatPlayerCount: data.defeatPlayerCount,
                totalScore: CalcTotalScore(data),
                title: "---" // 仮（後から SetTitle で更新可能）
            ));
        }

        // スコア順に並び替え（降順）
        results.Sort((a, b) => b.totalScore.CompareTo(a.totalScore));

        // 順位をセット
        for (int i = 0; i < results.Count; i++)
            results[i].SetRank(i + 1);

        _playerResults.Value = results;
    }

    // =========================================================
    // 共通：順位・称号セット（後から実装）
    // =========================================================

    /// <summary>ソロ用：順位をセットする</summary>
    public void SetRank(int rank) => _rank.Value = Mathf.Max(0, rank);

    /// <summary>ソロ用：称号をセットする</summary>
    public void SetTitle(string title) => _title.Value = title;

    /// <summary>マルチ用：特定プレイヤーの称号をセットする</summary>
    public void SetPlayerTitle(string playerName, string title)
    {
        var list = _playerResults.Value;
        foreach (var result in list)
        {
            if (result.playerName == playerName)
            {
                result.SetTitle(title);
                break;
            }
        }
        // 変更を通知するためリストを再セット
        _playerResults.Value = list;
    }

    // =========================================================
    // 総合スコア計算（仮：後から変更可能）
    // =========================================================

    private int CalcTotalScore(ResultData data)
    {
        int score = 0;
        score += data.defeatVillainCount * 100; // 悪人撃破  +100点
        score -= data.foundByPoliceCount * 50;  // 警察発見  -50点
        score -= data.crimeCompletedCount * 80;  // 犯罪完遂  -80点
        score += data.defeatPlayerCount * 50;  // 他P撃破   +50点（仮）
        return Mathf.Max(0, score);
    }

    // =========================================================
    // IDisposable
    // =========================================================

    public override void Dispose()
    {
        _defeatVillainCount.Dispose();
        _foundByPoliceCount.Dispose();
        _crimeCompletedCount.Dispose();
        _defeatPlayerCount.Dispose();
        _totalScore.Dispose();
        _rank.Dispose();
        _title.Dispose();
        _playerResults.Dispose();
    }
}

// =========================================================
// データ構造
// =========================================================

/// <summary>
/// プレイヤーが持つスコアデータ
/// ソロ / マルチ 共通で使う
/// </summary>
[Serializable]
public class ResultData
{
    // ---- バッキングフィールド ----
    private readonly string _playerName;
    private readonly int _defeatVillainCount;
    private readonly int _foundByPoliceCount;
    private readonly int _crimeCompletedCount;
    private readonly int _defeatPlayerCount;

    // ---- プロパティ公開（読み取り専用）----
    public string playerName => _playerName;
    public int defeatVillainCount => _defeatVillainCount;
    public int foundByPoliceCount => _foundByPoliceCount;
    public int crimeCompletedCount => _crimeCompletedCount;
    public int defeatPlayerCount => _defeatPlayerCount;

    // ---- コンストラクタ ----
    public ResultData(
        string playerName,
        int defeatVillainCount,
        int foundByPoliceCount,
        int crimeCompletedCount,
        int defeatPlayerCount)
    {
        _playerName = playerName;
        _defeatVillainCount = defeatVillainCount;
        _foundByPoliceCount = foundByPoliceCount;
        _crimeCompletedCount = crimeCompletedCount;
        _defeatPlayerCount = defeatPlayerCount;
    }
}

/// <summary>
/// マルチ用：1プレイヤー分の表示データ（順位・称号付き）
/// </summary>
public class PlayerResultData
{
    // ---- バッキングフィールド ----
    private readonly string _playerName;
    private readonly int _defeatVillainCount;
    private readonly int _foundByPoliceCount;
    private readonly int _crimeCompletedCount;
    private readonly int _defeatPlayerCount;
    private readonly int _totalScore;
    private int _rank;  // 順位は後からセット
    private string _title; // 称号は後からセット

    // ---- プロパティ公開（読み取り専用）----
    public string playerName => _playerName;
    public int defeatVillainCount => _defeatVillainCount;
    public int foundByPoliceCount => _foundByPoliceCount;
    public int crimeCompletedCount => _crimeCompletedCount;
    public int defeatPlayerCount => _defeatPlayerCount;
    public int totalScore => _totalScore;
    public int rank => _rank;
    public string title => _title;

    // ---- コンストラクタ ----
    public PlayerResultData(
        string playerName,
        int defeatVillainCount,
        int foundByPoliceCount,
        int crimeCompletedCount,
        int defeatPlayerCount,
        int totalScore,
        string title = "---")
    {
        _playerName = playerName;
        _defeatVillainCount = defeatVillainCount;
        _foundByPoliceCount = foundByPoliceCount;
        _crimeCompletedCount = crimeCompletedCount;
        _defeatPlayerCount = defeatPlayerCount;
        _totalScore = totalScore;
        _title = title;
    }

    public void SetRank(int rank) => _rank = rank;
    public void SetTitle(string title) => _title = title;
}
