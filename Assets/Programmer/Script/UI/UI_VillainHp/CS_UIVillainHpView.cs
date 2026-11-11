/* ================================================
 * Hpの描画の処理
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hpの描画の処理
/// </summary>
public class CS_UIVillainHpView : CS_BaseView<CS_UIVillainHpPresenter>
{
    [SerializeField] private Image _hpGauge;
    [SerializeField] private TextMeshProUGUI _hpText;

    public void UpdateHp(int hp, int max)
    {
        float fill = (float)hp / max;
        _hpGauge.fillAmount = fill;
        _hpText.text = hp.ToString();
    }
}
