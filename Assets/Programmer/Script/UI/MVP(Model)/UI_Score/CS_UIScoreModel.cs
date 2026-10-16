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
/// プレイヤーごとのスコア状態を保持
/// ・全員分のランキング表示（リアルタイム更新）
/// ・自分のスコア表示は削除済み
/// </summary>
public class CS_UIScoreModel : CS_BaseModel
{
    // =========================================================
    // 番号付きで公開された Model の一覧
    // =========================================================

    private static readonly Dictionary<int, CS_UIScoreModel> _boundModels = new();

    // ランキングが更新された時の通知
    public static event Action OnRankingUpdated;

    // 全員分のスコアをランキング順で取得する
    public static List<(int playerNumber, int score)> GetRanking()
    {
        var list = new List<(int, int)>();
        foreach (var kv in _boundModels)
            list.Add((kv.Key, kv.Value.score.CurrentValue));

        // スコア降順に並び替え
        list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return list;
    }

    // Domain Reload 対策
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModels.Clear();
        OnRankingUpdated = null;
    }

    // =========================================================
    // 状態
    // =========================================================

    private readonly ReactiveProperty<int> _score;
    private int _playerNumber = -1; // -1 = 未Bind

    // 外部からは読み取り専用
    public ReadOnlyReactiveProperty<int> score => _score;

    public CS_UIScoreModel(int initialScore = 0)
    {
        _score = new ReactiveProperty<int>(initialScore);
    }

    // =========================================================
    // 公開
    // =========================================================

    /// <summary>このModelを何番のプレイヤーのスコアとして公開するか</summary>
    public void Bind(int playerNumber)
    {
        Unbind();
        _playerNumber = playerNumber;
        _boundModels[playerNumber] = this;
        OnRankingUpdated?.Invoke();
    }

    /// <summary>公開をやめる</summary>
    public void Unbind()
    {
        if (_playerNumber < 0) return;

        if (_boundModels.TryGetValue(_playerNumber, out var current) && current == this)
        {
            _boundModels.Remove(_playerNumber);
            OnRankingUpdated?.Invoke();
        }
        _playerNumber = -1;
    }

    // =========================================================
    // 値の変更
    // =========================================================

    /// <summary>スコアを加算する</summary>
    public void AddScore(int value)
    {
        _score.Value += value;
        OnRankingUpdated?.Invoke();
    }

    /// <summary>スコアを設定する</summary>
    public void SetScore(int value)
    {
        _score.Value = value;
        OnRankingUpdated?.Invoke();
    }

    public override void Dispose()
    {
        Unbind();
        _score.Dispose();
    }
}