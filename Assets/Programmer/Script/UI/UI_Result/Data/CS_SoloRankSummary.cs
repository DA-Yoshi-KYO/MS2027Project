/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;

/// <summary>
/// ソロの、エリア1つ分の表示用データ(タイムとランク)
/// </summary>
public readonly struct CS_SoloAreaRow
{
    private readonly int _areaNumber;
    private readonly bool _isCleared;
    private readonly float _seconds;
    private readonly CSE_ResultRank _rank;

    /// <summary>エリアの番号(1から)</summary>
    public int areaNumber => _areaNumber;

    public bool isCleared => _isCleared;
    public float seconds => _seconds;

    /// <summary>そのエリアのランク(未クリアはC)</summary>
    public CSE_ResultRank rank => _rank;

    public CS_SoloAreaRow(int areaNumber, bool isCleared, float seconds, CSE_ResultRank rank)
    {
        _areaNumber = areaNumber;
        _isCleared = isCleared;
        _seconds = seconds;
        _rank = rank;
    }
}

/// <summary>
/// ソロのリザルトに出す、ランクとタイムのまとめ
/// ・Model が、エリアごとのクリアタイムから計算して作る
/// ・総合ランク : エリアごとのランクの平均(未クリアのエリアも C として含める)
/// ・総合タイム : エリアごとの秒数の合計(未クリアのエリアも含める)
/// </summary>
public readonly struct CS_SoloRankSummary
{
    private readonly CSE_ResultRank _overallRank;
    private readonly float _totalSeconds;
    private readonly IReadOnlyList<CS_SoloAreaRow> _rows;

    public CSE_ResultRank overallRank => _overallRank;
    public float totalSeconds => _totalSeconds;
    public IReadOnlyList<CS_SoloAreaRow> rows => _rows;

    public CS_SoloRankSummary(
        CSE_ResultRank overallRank,
        float totalSeconds,
        IReadOnlyList<CS_SoloAreaRow> rows)
    {
        _overallRank = overallRank;
        _totalSeconds = totalSeconds;
        _rows = rows;
    }
}
