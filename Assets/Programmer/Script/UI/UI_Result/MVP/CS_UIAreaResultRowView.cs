/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ソロ用: エリア1つ分の行の表示(エリア名・タイム・ランク画像)
/// ・CS_UIResultView の _areaRows に、エリアの並び順(上から)で割り当てる
/// ・エリア名の文字が画像に入っている場合は、_areaNameText は空でよい
/// ・タイムの TMP に CS_UICountUpText を付けると、カウントアップで登場する
/// ・ランク画像に CS_UIRankStamp(任意)を付けると、順番に合わせて登場する
/// </summary>
public class CS_UIAreaResultRowView : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("エリア名(任意。例: エリア1)")]
    [SerializeField] private TextMeshProUGUI _areaNameText;

    [Header("クリアタイム(例: 4:50)")]
    [SerializeField] private TextMeshProUGUI _timeText;

    [Header("そのエリアのランクの画像")]
    [SerializeField] private Image _rankImage;

    // =========================================================
    // 描画(CS_UIResultView から呼ばれる)
    // =========================================================

    /// <summary>この行を表示して、内容を設定する</summary>
    public void UpdateView(
        CS_SoloAreaRow row,
        CSO_ResultSpriteTable spriteTable)
    {
        gameObject.SetActive(true);

        if (_areaNameText != null)
            _areaNameText.text = $"エリア{row.areaNumber}";

        // カウントアップの準備(部品が付いていなければ、すぐ表示する)
        CS_ResultNumberUtility.SetTime(_timeText, row.seconds);

        // 変更: ランク画像。CS_UIRankStamp が付いていれば、登場まで見えない状態で待たせる
        CS_ResultImageUtility.SetRankSprite(
            _rankImage,
            spriteTable != null
                ? spriteTable.GetAreaRankSprite(row.rank)   // エリアごとのランクの画像
                : null
        );
    }

    /// <summary>この行を隠す(エリアの数が行の数より少ない時)</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // =========================================================
    // 登場の順番(CS_UIResultView が集めて、順番に始める)
    // =========================================================

    /// <summary>
    /// この行のカウントアップする数字(タイム)を、list に足す。
    /// 隠れている行(エリアの数が少ない時)は足さない。順番に出す時に、無駄な待ち時間を作らないため。
    /// </summary>
    public void CollectCountUps(List<CS_UICountUpText> list)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        CS_UICountUpText countUp = CS_ResultNumberUtility.GetCountUp(_timeText);

        if (countUp != null)
        {
            list.Add(countUp);
        }
    }

    /// <summary>
    /// この行のランク画像の登場の部品(CS_UIRankStamp)を、list に足す。
    /// 付いていなければ足さない(その時、ランク画像はすぐ表示されている)。
    /// 隠れている行は足さない。
    /// </summary>
    public void CollectRankStamps(List<CS_UIRankStamp> list)
    {
        if (!gameObject.activeInHierarchy ||
            _rankImage == null)
        {
            return;
        }

        CS_UIRankStamp stamp = _rankImage.GetComponent<CS_UIRankStamp>();

        if (stamp != null)
        {
            list.Add(stamp);
        }
    }
}
