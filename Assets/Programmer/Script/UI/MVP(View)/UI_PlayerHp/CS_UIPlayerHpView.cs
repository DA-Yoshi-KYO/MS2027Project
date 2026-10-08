/* ================================================
 * Hpの描画の処理
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hpの描画の処理
/// </summary>
public class CS_UIPlayerHpView : CS_BaseView<CS_UIPlayerHpPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("Hpゲージの画像")]
    [SerializeField] private Image _hpGauge;

    [Header("Hpの数値")]
    [SerializeField] private TextMeshProUGUI _hpText;

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIPlayerHpPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 描画（Presenter から呼ばれる）
    // =========================================================

    public void UpdateHp(int hp, int max)
    {
        float fill = (float)hp / max;

        if (_hpGauge != null)
            _hpGauge.fillAmount = fill;

        if (_hpText != null)
            _hpText.text = hp.ToString();
    }
}
