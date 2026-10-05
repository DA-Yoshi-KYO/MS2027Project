/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-06 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// シーンをまたいでリザルトデータを運ぶ static クラス
/// ・MainScene → ResultScene へのスコアの受け渡しに使う
/// ・タイマー終了時（シーン移動の直前）に Save() で保存する
/// ・ResultScene で読み取ったら Clear() で消す
/// ・Domain Reload オフ対策に RuntimeInitializeOnLoadMethod でクリア
/// </summary>
public static class CS_ResultDataStore
{
    // =========================================================
    // バッキングフィールド
    // =========================================================

    private static readonly List<CS_ResultData> _results = new();

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    /// <summary>保存データがあるか</summary>
    public static bool hasData => _results.Count > 0;

    /// <summary>保存された全プレイヤーのリザルトデータ</summary>
    public static IReadOnlyList<CS_ResultData> results => _results;

    // =========================================================
    // 保存 / クリア
    // =========================================================

    /// <summary>
    /// 全プレイヤーの CS_ResultData を保存する
    /// タイマー終了時・シーン移動の直前に呼ぶ
    /// </summary>
    public static void Save(IEnumerable<CS_ResultData> results)
    {
        _results.Clear();
        _results.AddRange(results);
    }

    /// <summary>
    /// 保存データをクリアする
    /// ResultScene で読み取ったあとに呼ぶ
    /// </summary>
    public static void Clear() => _results.Clear();

    // =========================================================
    // Domain Reload オフ対策
    // =========================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _results.Clear();
}
