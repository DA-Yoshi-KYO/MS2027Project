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
/// ・ソロのランク・タイムは、エリアごとのクリアタイムから計算して持つ(計算は CS_ResultRankCalculator)
/// </summary>
public class CS_UIResultModel : CS_BaseModel
{
    // =========================================================
    // 設定(追加)
    // =========================================================

    // ランクのしきい値(エリアごとのクリアタイム)。ソロのランク計算に使う
    private readonly CSO_ResultRankSettings _rankSettings;

    // =========================================================
    // ソロ用データ（バッキングフィールド）
    // =========================================================

    private readonly ReactiveProperty<CS_ResultData> _soloResult
        = new ReactiveProperty<CS_ResultData>(null);

    // ソロ用：エリアの結果（無ければnull）
    private readonly ReactiveProperty<CS_SoloAreaResult?> _soloAreaResult
        = new ReactiveProperty<CS_SoloAreaResult?>(null);

    // 追加: ソロ用：ランクとタイムのまとめ（エリアごとのタイムが無ければnull）
    private readonly ReactiveProperty<CS_SoloRankSummary?> _soloRankSummary
        = new ReactiveProperty<CS_SoloRankSummary?>(null);

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
    public ReadOnlyReactiveProperty<CS_SoloAreaResult?> soloAreaResult => _soloAreaResult;

    /// <summary>追加: ソロ用のランクとタイムのまとめ</summary>
    public ReadOnlyReactiveProperty<CS_SoloRankSummary?> soloRankSummary => _soloRankSummary;

    // =========================================================
    // 生成
    // =========================================================

    /// <summary>ランクの設定なしで作る(ソロのランクは、すべて C になる)</summary>
    public CS_UIResultModel() : this(null)
    {
    }

    /// <summary>ランクの設定を渡して作る</summary>
    public CS_UIResultModel(CSO_ResultRankSettings rankSettings)
    {
        _rankSettings = rankSettings;
    }

    /// <summary>ソロ用：エリアの結果をセットする</summary>
    public void SetSoloAreaResult(CS_SoloAreaResult result)
    {
        _soloAreaResult.Value = result;

        // 追加: エリアごとのタイムから、ランクとタイムのまとめを作る
        _soloRankSummary.Value = BuildSoloRankSummary(result);
    }

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
    // ソロ用：ランクとタイムの計算(追加)
    // =========================================================

    /// <summary>
    /// エリアごとのクリアタイムから、ランクとタイムのまとめを作る
    /// ・エリアごとのランク : しきい値と比べて決める(未クリアは C)
    /// ・総合ランク         : エリアごとのランクの平均(未クリアのエリアも C として含める)
    /// ・総合タイム         : エリアごとの秒数の合計(未クリアのエリアも含める)
    /// エリアごとのタイムが無ければ null
    /// </summary>
    private CS_SoloRankSummary? BuildSoloRankSummary(CS_SoloAreaResult result)
    {
        IReadOnlyList<CS_SoloAreaTime> times = result.areaTimes;

        if (times == null || times.Count == 0)
            return null;

        var rows = new List<CS_SoloAreaRow>(times.Count);
        var ranks = new List<CSE_ResultRank>(times.Count);
        float totalSeconds = 0f;

        for (int i = 0; i < times.Count; i++)
        {
            CS_SoloAreaTime time = times[i];

            CSE_ResultRank rank = CS_ResultRankCalculator.EvaluateArea(
                _rankSettings,
                i,
                time.isCleared,
                time.seconds
            );

            rows.Add(new CS_SoloAreaRow(i + 1, time.isCleared, time.seconds, rank));
            ranks.Add(rank);
            totalSeconds += time.seconds;
        }

        return new CS_SoloRankSummary(
            CS_ResultRankCalculator.EvaluateOverall(ranks),
            totalSeconds,
            rows
        );
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
        _soloAreaResult.Dispose();
        _soloRankSummary.Dispose();
        _multiResults.Dispose();
    }
}
