/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using R3;
using System.Collections.Generic;

/// <summary>
/// リザルトシーンのスコアデータを保持する Model
/// ・ソロ / マルチ 両対応
/// ・CS_ResultData を受け取って管理する
/// ・点数計算は CS_ResultData に委譲する
/// </summary>
public class CS_UIResultModel : CS_BaseModel
{
    // =========================================================
    // ソロ用データ（バッキングフィールド）
    // =========================================================

    private readonly ReactiveProperty<CS_ResultData> _soloResult
        = new ReactiveProperty<CS_ResultData>(null);

    // =========================================================
    // マルチ用データ（スコア順に並び替えたリスト）
    // =========================================================

    private readonly ReactiveProperty<List<CS_ResultData>> _multiResults
        = new ReactiveProperty<List<CS_ResultData>>(new List<CS_ResultData>());

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public ReadOnlyReactiveProperty<CS_ResultData> soloResult => _soloResult;
    public ReadOnlyReactiveProperty<List<CS_ResultData>> multiResults => _multiResults;

    // =========================================================
    // ソロ用：データをセットする
    // =========================================================

    /// <summary>ソロ用：CS_ResultData を受け取って表示する</summary>
    public void SetSoloResult(CS_ResultData data)
    {
        if (data == null) return;
        _soloResult.Value = data;
    }

    // =========================================================
    // マルチ用：全プレイヤーのデータを受け取る
    // =========================================================

    /// <summary>
    /// マルチ用：全プレイヤーの CS_ResultData を受け取って
    /// totalScore 順に並び替えて管理する
    /// </summary>
    public void SetMultiResults(List<CS_ResultData> allResults)
    {
        if (allResults == null || allResults.Count == 0) return;

        // totalScore 降順に並び替え
        var sorted = new List<CS_ResultData>(allResults);
        sorted.Sort((a, b) => b.totalScore.CompareTo(a.totalScore));

        _multiResults.Value = sorted;
    }

    // =========================================================
    // IDisposable
    // =========================================================

    public override void Dispose()
    {
        _soloResult.Dispose();
        _multiResults.Dispose();
    }
}
