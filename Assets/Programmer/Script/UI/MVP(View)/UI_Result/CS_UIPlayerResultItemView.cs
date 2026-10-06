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
/// ・点数は CS_ResultData.Score の加算済みスコアをそのまま表示する
/// </summary>
public class CS_UIPlayerResultItemView : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("プレイヤー情報")]
    [SerializeField] private TextMeshProUGUI _rankText;
    [SerializeField] private TextMeshProUGUI _playerNumberText;
    [SerializeField] private TextMeshProUGUI _titleText;

    [Header("スコア詳細")]
    [SerializeField] private TextMeshProUGUI _defeatVillainText;
    [SerializeField] private TextMeshProUGUI _foundByPoliceText;
    [SerializeField] private TextMeshProUGUI _crimeCompletedText;
    [SerializeField] private TextMeshProUGUI _defeatPlayerText;
    [SerializeField] private TextMeshProUGUI _totalScoreText;

    // =========================================================
    // 描画（CS_UIResultView から呼ばれる）
    // =========================================================

    /// <summary>
    /// 1プレイヤー分の結果を表示する
    /// rank はリストの順番（Model でソート済み）から渡される
    /// </summary>
    public void UpdateView(CS_ResultData data, int rank)
    {
        if (data == null) return;

        var s = data.score;

        // 順位
        if (_rankText != null)
            _rankText.text = $"{rank}位";

        // プレイヤー番号
        if (_playerNumberText != null)
            _playerNumberText.text = $"Player{data.playerNumber}";

        // 称号
        if (_titleText != null)
            _titleText.text = $"称号 : {data.title}";

        // 点数は加算済みスコアをそのまま表示
        if (_defeatVillainText != null)
            _defeatVillainText.text =
                $"倒した悪人 : {s.defeatVillainCount}人 ({s.defeatVillainScore:+#;-#;0}点)";

        if (_foundByPoliceText != null)
            _foundByPoliceText.text =
                $"警察に発見 : {s.foundByPoliceCount}回 ({s.foundByPoliceScore:+#;-#;0}点)";

        if (_crimeCompletedText != null)
            _crimeCompletedText.text =
                $"犯罪完遂 : {s.crimeCompletedCount}回 ({s.crimeCompletedScore:+#;-#;0}点)";

        if (_defeatPlayerText != null)
            _defeatPlayerText.text =
                $"他P撃破 : {s.defeatPlayerCount}人 ({s.defeatPlayerScore:+#;-#;0}点)";

        // 総合スコア
        if (_totalScoreText != null)
            _totalScoreText.text = $"総合スコア : {data.totalScore}点";
    }
}
