/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-08 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;

/// <summary>
/// ランキングの1人分の表示を担当する View
/// ・RankingItemPrefab にアタッチする
/// ・CS_UIScoreView から生成されて UpdateView() で表示を更新する
/// </summary>
public class CS_UIScoreItemView : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [SerializeField] private TextMeshProUGUI _rankText;       // 順位
    [SerializeField] private TextMeshProUGUI _playerNameText; // プレイヤー名
    [SerializeField] private TextMeshProUGUI _scoreText;      // スコア

    // =========================================================
    // 描画（CS_UIScoreView から呼ばれる）
    // =========================================================

    /// <summary>1人分のランキングを更新する</summary>
    public void UpdateView(int rank, string playerName, int score)
    {
        if (_rankText != null)
            _rankText.text = $"{rank}位";

        if (_playerNameText != null)
            _playerNameText.text = playerName;

        if (_scoreText != null)
            _scoreText.text = $"{score}点";
    }
}
