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
        // ゲージ更新
        _timerGauge.fillAmount = remain / max;

        // 分と秒に変換
        int totalSec = Mathf.CeilToInt(remain);
        int minutes = totalSec / 60;
        int seconds = totalSec % 60;

        // 00:00 形式で表示
        _timerText.text = $"{minutes:D2}:{seconds:D2}";
    }

}
