/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-08 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ランキングの1人分の表示を担当する View
/// ・必殺技ゲージが満タンになったらアイコンを黄色に変える
/// </summary>
public class CS_UIScoreItemView : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("表示 UI")]
    [SerializeField] private TextMeshProUGUI _rankText;
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _scoreText;

    [Header("アイコンデータ（ScriptableObject）")]
    [SerializeField] private CSO_PlayerIconData _playerIconData;

    [Header("色設定")]
    [SerializeField] private Color _normalColor = Color.white;  // 通常時
    [SerializeField] private Color _fullColor = Color.yellow; // 満タン時

    // =========================================================
    // 描画
    // =========================================================

    /// <summary>1人分のランキングを更新する</summary>
    public void UpdateView(int rank, int playerNumber, int score, bool isFull)
    {
        // 順位
        if (_rankText != null)
            _rankText.text = $"{rank}位";

        // アイコン
        if (_iconImage != null && _playerIconData != null)
        {
            var icon = _playerIconData.GetIcon(playerNumber);
            if (icon != null)
                _iconImage.sprite = icon;
        }

        // スコア
        if (_scoreText != null)
            _scoreText.text = $"{score}点";

        // ★ 満タン状態でアイコンの色を切り替える
        SetIconFull(isFull);
    }

    /// <summary>満タン状態でアイコンの色を切り替える</summary>
    public void SetIconFull(bool isFull)
    {
        if (_iconImage != null)
            _iconImage.color = isFull ? _fullColor : _normalColor;
    }
}
