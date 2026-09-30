/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;

/// <summary>
/// マルチ用：1プレイヤー分の結果表示 View
/// ・PlayerResultPrefab にアタッチする
/// ・CS_UIResultView から生成されて UpdateView() で表示を更新する
/// </summary>
public class CS_UIPlayerResultItemView : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("プレイヤー情報")]
    [SerializeField] private TextMeshProUGUI _rankText;       // 順位
    [SerializeField] private TextMeshProUGUI _playerNameText; // プレイヤー名
    [SerializeField] private TextMeshProUGUI _titleText;      // 称号（仮）

    [Header("スコア詳細")]
    [SerializeField] private TextMeshProUGUI _defeatVillainCountText;
    [SerializeField] private TextMeshProUGUI _foundByPoliceCountText;
    [SerializeField] private TextMeshProUGUI _crimeCompletedCountText;
    [SerializeField] private TextMeshProUGUI _defeatPlayerCountText;
    [SerializeField] private TextMeshProUGUI _totalScoreText;

    // =========================================================
    // 描画（CS_UIResultView から呼ばれる）
    // =========================================================

    /// <summary>1プレイヤー分の結果を表示する</summary>
    public void UpdateView(PlayerResultData data)
    {
        if (data == null) return;

        // 順位
        if (_rankText != null)
            _rankText.text = $"{data.rank}位";

        // プレイヤー名
        if (_playerNameText != null)
            _playerNameText.text = data.playerName;

        // 称号（仮）
        if (_titleText != null)
            _titleText.text = $"称号 : {data.title}";

        // 各項目
        if (_defeatVillainCountText != null)
            _defeatVillainCountText.text = $"倒した悪人 : {data.defeatVillainCount}人 (+{data.defeatVillainCount * 100}点)";

        if (_foundByPoliceCountText != null)
            _foundByPoliceCountText.text = $"警察に発見 : {data.foundByPoliceCount}回 (-{data.foundByPoliceCount * 50}点)";

        if (_crimeCompletedCountText != null)
            _crimeCompletedCountText.text = $"犯罪完遂 : {data.crimeCompletedCount}回 (-{data.crimeCompletedCount * 80}点)";

        if (_defeatPlayerCountText != null)
            _defeatPlayerCountText.text = $"他P撃破 : {data.defeatPlayerCount}人 (+{data.defeatPlayerCount * 50}点)"; // 仮

        // 総合スコア
        if (_totalScoreText != null)
            _totalScoreText.text = $"総合スコア : {data.totalScore}点";
    }
}
