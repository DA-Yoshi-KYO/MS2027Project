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
/// リザルトの称号を決める(状態を持たない計算だけのクラス)
/// ・称号は「条件を満たしたものを全部」返す(複数)
/// ・条件の回数は CSO_ResultTitleSettings(Inspector)から受け取る。
///   settings が null の時は、下の初期値(3回 / 50回 / 50回)で判定する
///
/// 条件と称号
///   警察に見つかった数が「上限」以下      → 警察いらずの暗躍者      (PoliceFree)
///   奇襲の成功回数が「必要回数」以上      → ワンショットワンダウン  (OneShotOneDown)
///   SAの使用回数が「必要回数」以上        → ステルスアクションの名手 (StealthMaster)
///   上の3つをすべて満たした               → 稀代のダークヒーロー    (DarkHero)
///
/// 決め方
///   1. 満たした称号を、全部返す。並びは
///        稀代のダークヒーロー → 警察いらず → ワンショット → ステルス の順
///      3つすべて満たした時は、ダークヒーローに加えて、3つの称号も返す
///      (ダークヒーローだけにしたい時は、settings の showOnlyDarkHeroWhenAll を ON にする)
///   2. どれも満たしていない時だけ、「条件達成までの割合」が一番高い称号を1つ返す
///      割合が同じ時は、警察いらず → ワンショット → ステルス の順に優先する
///
/// 条件達成までの割合(0〜1)
///   奇襲・SA   : 回数 ÷ 必要回数
///   警察いらず : 上限 ÷ 見つかった数(上限を超えた時。回数が増えるほど小さくなる)
///
/// ・条件の考え方を変える/称号を増やす時は、ここを直す(回数だけなら Inspector で足りる)
/// </summary>
public static class CS_ResultTitleDecider
{
    // =========================================================
    // 初期値(settings が無い時に使う。Inspector の初期値と同じにしておく)
    // =========================================================

    private const int defaultPoliceFreeMaxCount = 3;
    private const int defaultOneShotOneDownMinCount = 50;
    private const int defaultStealthMasterMinCount = 50;
    private const bool defaultShowOnlyDarkHeroWhenAll = false;

    // =========================================================
    // 決める
    // =========================================================

    /// <summary>
    /// 称号の一覧を返す。必ず1つ以上入っている。
    /// settings が null なら、初期値で判定する。
    /// </summary>
    public static IReadOnlyList<CSE_ResultTitle> DecideAll(
        CSO_ResultTitleSettings settings,
        int foundByPoliceCount,
        int surpriseAttackCount,
        int saUseCount)
    {
        // 条件の回数(settings が無ければ初期値)。必要回数は0で割らないように、最低1にする
        int policeFreeMax =
            settings != null
                ? Mathf.Max(0, settings.policeFreeMaxCount)
                : defaultPoliceFreeMaxCount;

        int oneShotMin =
            settings != null
                ? Mathf.Max(1, settings.oneShotOneDownMinCount)
                : defaultOneShotOneDownMinCount;

        int stealthMin =
            settings != null
                ? Mathf.Max(1, settings.stealthMasterMinCount)
                : defaultStealthMasterMinCount;

        bool showOnlyDarkHero =
            settings != null
                ? settings.showOnlyDarkHeroWhenAll
                : defaultShowOnlyDarkHeroWhenAll;

        // 負の値は0として扱う
        int found = Mathf.Max(0, foundByPoliceCount);
        int surprise = Mathf.Max(0, surpriseAttackCount);
        int sa = Mathf.Max(0, saUseCount);

        bool isPoliceFree = found <= policeFreeMax;
        bool isOneShotOneDown = surprise >= oneShotMin;
        bool isStealthMaster = sa >= stealthMin;

        var titles = new List<CSE_ResultTitle>(4);

        // 3つすべて満たした → 稀代のダークヒーロー(先頭に出す)
        if (isPoliceFree &&
            isOneShotOneDown &&
            isStealthMaster)
        {
            titles.Add(CSE_ResultTitle.DarkHero);

            if (showOnlyDarkHero)
            {
                return titles;
            }
        }

        // 満たした称号を、全部入れる
        if (isPoliceFree)
        {
            titles.Add(CSE_ResultTitle.PoliceFree);
        }

        if (isOneShotOneDown)
        {
            titles.Add(CSE_ResultTitle.OneShotOneDown);
        }

        if (isStealthMaster)
        {
            titles.Add(CSE_ResultTitle.StealthMaster);
        }

        if (titles.Count > 0)
        {
            return titles;
        }

        // どれも満たしていない → 条件達成までの割合が一番高い称号を1つだけ
        titles.Add(
            DecideClosest(
                found,
                surprise,
                sa,
                policeFreeMax,
                oneShotMin,
                stealthMin
            )
        );

        return titles;
    }

    // =========================================================
    // どれも満たしていない時の称号
    // =========================================================

    /// <summary>
    /// 条件達成までの割合が一番高い称号を返す。
    /// 割合が同じ時は、警察いらず → ワンショット → ステルス の順に優先する。
    /// </summary>
    private static CSE_ResultTitle DecideClosest(
        int found,
        int surprise,
        int sa,
        int policeFreeMax,
        int oneShotMin,
        int stealthMin)
    {
        CSE_ResultTitle bestTitle = CSE_ResultTitle.PoliceFree;
        float bestRate = GetPoliceFreeRate(found, policeFreeMax);

        float oneShotRate = (float)surprise / oneShotMin;

        if (oneShotRate > bestRate)
        {
            bestTitle = CSE_ResultTitle.OneShotOneDown;
            bestRate = oneShotRate;
        }

        float stealthRate = (float)sa / stealthMin;

        if (stealthRate > bestRate)
        {
            bestTitle = CSE_ResultTitle.StealthMaster;
        }

        return bestTitle;
    }

    // =========================================================
    // 条件達成までの割合
    // =========================================================

    /// <summary>
    /// 「警察いらずの暗躍者」までの割合(0〜1)。
    /// 「上限以下」という条件なので、少ないほど近い。
    /// 条件を満たしていれば1。満たしていなければ 上限 ÷ 見つかった数(例: 上限3で4回→0.75、6回→0.5)。
    /// </summary>
    private static float GetPoliceFreeRate(
        int found,
        int policeFreeMax)
    {
        if (found <= policeFreeMax)
        {
            return 1f;
        }

        return (float)policeFreeMax / found;
    }
}
