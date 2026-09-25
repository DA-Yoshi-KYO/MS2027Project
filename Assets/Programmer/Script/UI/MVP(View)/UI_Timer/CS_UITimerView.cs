/* ================================================
 * Timerの描画の処理
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Timerの描画の処理
/// </summary>
public class CS_UITimerView : CS_BaseView<CS_UITimerPresenter>
{
    [Header("Timerの数値")][SerializeField] private TextMeshProUGUI _timerText;
    [Header("Timerのゲージ")][SerializeField] private Image _timerGauge;

    public void UpdateTimer(float remain, float max)
    {
        float fill = remain / max;
        _timerGauge.fillAmount = fill;

        int sec = Mathf.CeilToInt(remain);
        _timerText.text = sec.ToString();
    }
}
