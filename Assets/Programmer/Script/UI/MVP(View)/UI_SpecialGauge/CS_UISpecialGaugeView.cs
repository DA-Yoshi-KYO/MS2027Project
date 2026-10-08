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
/// ・Presenterから渡された値を描画するだけ
/// ・自分だけ表示するので CanvasGroup / SetVisible は不要
/// </summary>
public class CS_UISpecialGaugeView : CS_BaseView<CS_UISpecialGaugePresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [SerializeField] private Image _gauge;

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UISpecialGaugePresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 描画（Presenter から呼ばれる）
    // =========================================================

    /// <summary>ゲージの表示更新（0〜1）</summary>
    public void UpdateGauge(float current, float max)
    {
        if (_gauge != null)
            _gauge.fillAmount = current / max;
    }
}
