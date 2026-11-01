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
/// 倒した悪人の数の View
/// ・UICanvas 配下の GameObject にアタッチする
/// ・ゲージ（fillAmount）と数値（「5 / 10」形式）を表示する
/// ・評価表示は未定のため後から追加できる設計
/// </summary>
public class CS_UIDefeatVillainCountView : CS_BaseView<CS_UIDefeatVillainCountPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ゲージ")]
    [SerializeField] private Image _countGauge; // fillAmount で進捗を表示

    [Header("数値テキスト（例：5 / 10）")]
    [SerializeField] private TextMeshProUGUI _countText;

    // ※ 評価表示（★など）は未定のため後から追加する

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIDefeatVillainCountPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 描画（Presenter から呼ばれる）
    // =========================================================

    /// <summary>ゲージと数値テキストを更新する</summary>
    public void UpdateCount(int count, int max)
    {
        float fill = (float)count / max;

        // ゲージの進捗を反映
        if (_countGauge != null)
            _countGauge.fillAmount = fill;

        // 数値テキストを反映（例：「5 / 10」）
        if (_countText != null)
            _countText.text = $"{count} / {max}";
    }
}
