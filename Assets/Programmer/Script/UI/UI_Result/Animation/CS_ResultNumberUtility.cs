/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;

/// <summary>
/// リザルトの数字表示用の小さな共通処理
/// ・数字を表示する時は、CS_ResultNumberUtility を通す
///     - その文字(TMP)に CS_UICountUpText が付いていれば、カウントアップの準備をする(すぐには見せない)
///     - 付いていなければ、これまでどおり、すぐ文字を入れる
///   → View は、カウントアップを使う/使わないを気にしなくてよい。Inspector の割り当ても変わらない
/// ・始める時は Play() を呼ぶ(付いていなければ、何もしない)
/// ・順番に出す時は、GetCountUp() で部品を集めて、View が順番に Play する
/// </summary>
public static class CS_ResultNumberUtility
{
    // =========================================================
    // 数字をセットする
    // =========================================================

    /// <summary>クリアタイム(秒)をセットする。表示は「分:秒」(例: 5:00)</summary>
    public static void SetTime(
        TMP_Text text,
        float seconds)
    {
        Set(
            text,
            seconds,
            CSE_CountUpFormat.MinutesSeconds,
            string.Empty
        );
    }

    /// <summary>回数などをセットする。suffix は数字の後ろに付ける文字(例: "回")</summary>
    public static void SetCount(
        TMP_Text text,
        int count,
        string suffix)
    {
        Set(
            text,
            count,
            CSE_CountUpFormat.Number,
            suffix
        );
    }

    private static void Set(
        TMP_Text text,
        float value,
        CSE_CountUpFormat format,
        string suffix)
    {
        if (text == null)
        {
            return;
        }

        CS_UICountUpText countUp =
            text.GetComponent<CS_UICountUpText>();

        if (countUp != null)
        {
            // カウントアップの準備。Play() が呼ばれるまで、見えない状態で待つ
            countUp.SetValue(value, format, suffix);
            return;
        }

        // 部品が付いていなければ、これまでどおり、すぐ表示する
        text.text = Format(value, format, suffix);
    }

    // =========================================================
    // 取得 / 始める / すぐ最後まで見せる
    // =========================================================

    /// <summary>
    /// その文字(TMP)に付いている CS_UICountUpText を返す。付いていなければ null。
    /// 順番に出すために、View が部品を集める時に使う。
    /// </summary>
    public static CS_UICountUpText GetCountUp(TMP_Text text)
    {
        if (text == null)
        {
            return null;
        }

        return text.GetComponent<CS_UICountUpText>();
    }

    /// <summary>カウントアップを始める(部品が付いていなければ、何もしない)</summary>
    public static void Play(TMP_Text text)
    {
        CS_UICountUpText countUp = GetCountUp(text);

        if (countUp != null)
        {
            countUp.Play();
        }
    }

    /// <summary>アニメーションを飛ばして、最後の状態をすぐ見せる(スキップ用)</summary>
    public static void ShowImmediately(TMP_Text text)
    {
        CS_UICountUpText countUp = GetCountUp(text);

        if (countUp != null)
        {
            countUp.ShowImmediately();
        }
    }

    // =========================================================
    // 文字にする
    // =========================================================

    /// <summary>
    /// 数字を、指定した形式の文字にする。
    /// ・Number         : 四捨五入した整数
    /// ・MinutesSeconds : 分:秒(秒は切り上げ)
    /// 後ろに suffix を付ける(空なら付けない)。
    /// </summary>
    public static string Format(
        float value,
        CSE_CountUpFormat format,
        string suffix)
    {
        string body =
            format == CSE_CountUpFormat.MinutesSeconds
                ? CS_ResultTimeFormatter.Format(value)
                : Mathf.RoundToInt(value).ToString();

        return
            string.IsNullOrEmpty(suffix)
                ? body
                : body + suffix;
    }
}
