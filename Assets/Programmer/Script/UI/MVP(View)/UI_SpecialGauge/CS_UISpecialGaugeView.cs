/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-26 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 必殺技ゲージ（Special Gauge）のUI表示
/// Presenterから渡された値を描画するだけ
/// </summary>
public class CS_UISpecialGaugeView : CS_BaseView<CS_UISpecialGaugePresenter>
{
    [SerializeField] private Image _gauge;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// ゲージの表示更新（0〜1）
    /// </summary>
    public void UpdateGauge(float current, float max)
    {
        _gauge.fillAmount = current / max;
    }

    public void SetVisible(bool visible)
    {
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }
}
