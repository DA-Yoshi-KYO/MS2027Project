/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 悪人の犯罪完遂ゲージの View
/// ・ゲージ Prefab の子にアタッチする
/// ・Presenter から値を受け取り UI に反映する
/// ・ゲージ（fillAmount）とパーセント表示（%）を担当
/// </summary>
public class CS_UICrimeGaugeView : CS_BaseView<CS_UICrimeGaugePresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ゲージ")]
    [SerializeField] private Image _crimeGauge; // fillAmount で進捗を表示

    [Header("パーセント表示")]
    [SerializeField] private TextMeshProUGUI _percentText; // 例：「75%」

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UICrimeGaugePresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 描画（Presenter から呼ばれる）
    // =========================================================

    /// <summary>ゲージとパーセントを更新する</summary>
    public void UpdateGauge(float value, float max)
    {
        float fill = value / max;
        int percent = Mathf.RoundToInt(fill * 100f);

        // ゲージの進捗を反映
        if (_crimeGauge != null)
            _crimeGauge.fillAmount = fill;

        // パーセント表示を反映
        if (_percentText != null)
            _percentText.text = $"{percent}%";
    }
}
