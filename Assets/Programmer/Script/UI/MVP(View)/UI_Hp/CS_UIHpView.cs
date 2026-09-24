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
///  Hpの描画の処理
/// </summary>
public class CS_UIHpView : CS_BaseView<CS_UIHpPresenter>
{
    [Header("Hpゲージの画像")][SerializeField] private Image _hpGauge;
    [Header("Hpの数値")][SerializeField] private TextMeshProUGUI _hpText;

    //PresenterからHp変化の通知を受けて描画を更新する
    public void UpdateHp(int hp, int max)
    {
        //画像の更新
        float fill = (float)hp / max;
        _hpGauge.fillAmount = fill;

        //数値の変更
        _hpText.text = hp.ToString();
    }
}
