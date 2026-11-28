/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// リザルトのタイム表示用(状態を持たない計算だけのクラス)
/// ・秒を「分:秒」の形にする。秒は2桁。例: 290秒 → "4:50"、65秒 → "1:05"
/// ・小数は切り上げる。ランクのしきい値が整数秒の時、表示とランクの判定が食い違わないようにするため
///   (例: Sのしきい値が60秒の時、60.4秒は "1:01" と表示され、Aになる)
/// ・計算で出る小さな誤差(0.0001秒未満)は無視する
/// </summary>
public static class CS_ResultTimeFormatter
{
    // 表示できる最大の秒数(99:59)。これを超える値は、この値にそろえる
    private const float maxSeconds = 5999f;

    public static string Format(float seconds)
    {
        float clamped = Mathf.Clamp(seconds, 0f, maxSeconds);

        int totalSeconds = Mathf.CeilToInt(clamped - 0.0001f);
        totalSeconds = Mathf.Max(0, totalSeconds);

        int minutes = totalSeconds / 60;
        int remainSeconds = totalSeconds % 60;

        return $"{minutes}:{remainSeconds:00}";
    }
}
