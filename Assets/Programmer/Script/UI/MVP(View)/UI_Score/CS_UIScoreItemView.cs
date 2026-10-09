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
/// ・RankingItemPrefab にアタッチする
/// ・CS_UIScoreView から生成されて UpdateView() で表示を更新する
/// ・プレイヤー名の代わりにプレイヤー番号ごとのアイコンを表示する
/// </summary>
public class CS_UIScoreItemView : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("表示 UI")]
    [SerializeField] private TextMeshProUGUI _rankText;  // 順位
    [SerializeField] private Image _iconImage; // プレイヤーアイコン
    [SerializeField] private TextMeshProUGUI _scoreText; // スコア

    [Header("アイコンデータ（ScriptableObject）")]
    [SerializeField] private CSO_PlayerIconData _playerIconData;

    // =========================================================
    // 描画（CS_UIScoreView から呼ばれる）
    // =========================================================

    /// <summary>1人分のランキングを更新する</summary>
    public void UpdateView(int rank, int playerNumber, int score)
    {
        // 順位
        if (_rankText != null)
            _rankText.text = $"{rank}位";

        // アイコン（ScriptableObject から取得）
        if (_iconImage != null && _playerIconData != null)
        {
            var icon = _playerIconData.GetIcon(playerNumber);
            if (icon != null)
                _iconImage.sprite = icon;
        }

        // スコア
        if (_scoreText != null)
            _scoreText.text = $"{score}点";
    }
}
