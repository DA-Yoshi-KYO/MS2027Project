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
/// ・円形ゲージの中央にプレイヤーアイコンを表示する
/// ・ゲージが満タンになったらアイコンの色が黄色に変わる
/// ・アイコンは CS_PlayerIconData から取得する
/// </summary>
public class CS_UISpecialGaugeView : CS_BaseView<CS_UISpecialGaugePresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ゲージ画像（円形）")]
    [SerializeField] private Image _gauge;

    [Header("中央アイコン")]
    [SerializeField] private Image _iconImage;

    [Header("アイコンデータ（ScriptableObject）")]
    [SerializeField] private CSO_PlayerIconData _playerIconData;

    [Header("色設定")]
    [SerializeField] private Color _normalColor = Color.white;   // 通常時の色
    [SerializeField] private Color _fullColor = Color.yellow;  // 満タン時の色

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UISpecialGaugePresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 初期化
    // =========================================================

    /// <summary>
    /// アイコンをセットする
    /// Presenter から playerNumber を受け取って呼ぶ
    /// </summary>
    public void SetupIcon(int playerNumber)
    {
        if (_iconImage == null || _playerIconData == null) return;

        var icon = _playerIconData.GetIcon(playerNumber);
        if (icon != null)
            _iconImage.sprite = icon;

        // 初期色は通常色
        _iconImage.color = _normalColor;
    }

    // =========================================================
    // 描画（Presenter から呼ばれる）
    // =========================================================

    /// <summary>ゲージの表示更新（0〜1）</summary>
    public void UpdateGauge(float current, float max)
    {
        if (_gauge != null)
            _gauge.fillAmount = current / max;

        // 満タンかどうかでアイコンの色を切り替える
        if (_iconImage != null)
            _iconImage.color = current >= max ? _fullColor : _normalColor;
    }
}
