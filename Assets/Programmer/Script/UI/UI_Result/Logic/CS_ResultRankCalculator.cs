/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// リザルトのランクを決める(状態を持たない計算だけのクラス)
/// ・エリアごとのランク : クリアタイムを、エリアごとのしきい値と比べて決める
/// ・総合のランク       : エリアごとのランクを数値(S=3、A=2、B=1、C=0)にして平均し、四捨五入する
///                        例: SSA → S、SAB → A
/// ・クリアできなかったエリアは C として扱う
/// ・ランクの決め方を変える時は、ここだけ直す
/// </summary>
public static class CS_ResultRankCalculator
{
    // =========================================================
    // エリアごとのランク
    // =========================================================

    /// <summary>
    /// 1エリアのランクを決める。
    /// ・クリアできていなければ C
    /// ・クリアタイムがしきい値「以下」なら、そのランク(S → A → B の順に調べる)
    /// ・どれにも届かなければ C
    /// </summary>
    public static CSE_ResultRank EvaluateArea(
        CSO_ResultRankSettings settings,
        int areaIndex,
        bool isCleared,
        float clearSeconds)
    {
        if (!isCleared)
        {
            return CSE_ResultRank.C;
        }

        if (settings == null)
        {
            Debug.LogError(
                $"{nameof(CS_ResultRankCalculator)}: " +
                "ランクの設定(CSO_ResultRankSettings)が未設定です。Cにします。"
            );

            return CSE_ResultRank.C;
        }

        CSO_ResultRankSettings.AreaThreshold threshold =
            settings.GetThreshold(areaIndex);

        if (clearSeconds <= threshold.sSeconds)
        {
            return CSE_ResultRank.S;
        }

        if (clearSeconds <= threshold.aSeconds)
        {
            return CSE_ResultRank.A;
        }

        if (clearSeconds <= threshold.bSeconds)
        {
            return CSE_ResultRank.B;
        }

        return CSE_ResultRank.C;
    }

    // =========================================================
    // 総合のランク
    // =========================================================

    /// <summary>
    /// エリアごとのランクの平均から、総合のランクを決める。
    /// ・S=3、A=2、B=1、C=0 の平均を、四捨五入する(ちょうど半分は切り上げ)
    ///   例: SSA → 平均2.67 → S。 SAB → 平均2.00 → A
    /// ・エリアが1つもなければ C
    /// ・どのエリアを含めるか(クリアできなかったエリアも含めるか)は、呼ぶ側が決める
    /// </summary>
    public static CSE_ResultRank EvaluateOverall(
        IReadOnlyList<CSE_ResultRank> areaRanks)
    {
        if (areaRanks == null ||
            areaRanks.Count == 0)
        {
            return CSE_ResultRank.C;
        }

        int sum = 0;

        for (int i = 0; i < areaRanks.Count; i++)
        {
            sum += (int)areaRanks[i];
        }

        int count = areaRanks.Count;

        // 小数を使わずに「平均を四捨五入(半分は切り上げ)」を求める
        // = floor(sum / count + 0.5) = (2 * sum + count) / (2 * count)
        int average = (2 * sum + count) / (2 * count);

        return (CSE_ResultRank)Mathf.Clamp(
            average,
            (int)CSE_ResultRank.C,
            (int)CSE_ResultRank.S
        );
    }
}
