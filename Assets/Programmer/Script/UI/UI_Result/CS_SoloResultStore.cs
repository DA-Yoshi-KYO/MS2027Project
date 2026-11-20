/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-20 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// ソロの終わり方
/// </summary>
public enum CSE_SoloEndReason
{
    AllCleared,     // 全エリアの悪人を倒しきった
    TimeUp,         // 時間切れ
    CrimeCompleted, // 1つもエリアを守れなかった(失敗)
}

/// <summary>
/// ソロのリザルトに出す、エリアの結果
/// </summary>
public readonly struct CS_SoloAreaResult
{
    public readonly CSE_SoloEndReason reason;
    public readonly int protectedAreaCount;   // 守れた(悪人を倒しきった)エリアの数
    public readonly int areaCount;            // エリアの数
    public readonly float stoppedRate;        // 食い止めた率(0〜1)。倒した悪人の合計 ÷ 出した悪人の合計

    public CS_SoloAreaResult(CSE_SoloEndReason reason, int protectedAreaCount, int areaCount, float stoppedRate)
    {
        this.reason = reason;
        this.protectedAreaCount = protectedAreaCount;
        this.areaCount = areaCount;
        this.stoppedRate = Mathf.Clamp01(stoppedRate);
    }

    public bool isCleared => reason == CSE_SoloEndReason.AllCleared;
}

/// <summary>
/// ソロのエリアの結果を、MainScene → ResultScene へ運ぶ static クラス
/// ・CS_SoloGameJudge が終了時に Save する
/// ・CS_ResultController が読み取ったら Clear する(マルチの時は保存されないので、hasDataがfalse)
/// </summary>
public static class CS_SoloResultStore
{
    private static CS_SoloAreaResult _result;
    private static bool _hasData;

    public static bool hasData => _hasData;
    public static CS_SoloAreaResult result => _result;

    public static void Save(CS_SoloAreaResult result)
    {
        _result = result;
        _hasData = true;
    }

    public static void Clear() => _hasData = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _hasData = false;
}
