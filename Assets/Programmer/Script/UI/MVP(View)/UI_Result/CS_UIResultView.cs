/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// リザルトシーンの View
/// ・ソロ / マルチ 両対応
/// ・点数は CS_ResultData.Score の加算済みスコアをそのまま表示する
/// </summary>
public class CS_UIResultView : CS_BaseView<CS_UIResultPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ソロ用 UI")]
    [SerializeField] private GameObject _soloPanel;
    [SerializeField] private TextMeshProUGUI _defeatVillainText;    // 悪人撃破
    [SerializeField] private TextMeshProUGUI _foundByPoliceText;    // 警察発見
    [SerializeField] private TextMeshProUGUI _crimeCompletedText;   // 犯罪完遂
    [SerializeField] private TextMeshProUGUI _defeatPlayerText;     // 他P撃破
    [SerializeField] private TextMeshProUGUI _totalScoreText;       // 総合スコア
    [SerializeField] private TextMeshProUGUI _rankText;             // 順位（仮）
    [SerializeField] private TextMeshProUGUI _titleText;            // 称号（仮）

    [Header("マルチ用 UI")]
    [SerializeField] private GameObject _multiPanel;
    [SerializeField] private Transform _playerResultsRoot;
    [SerializeField] private GameObject _playerResultPrefab;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private readonly List<GameObject> _playerResultItems = new();

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIResultPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // ソロ用：描画
    // =========================================================

    public void UpdateSoloResult(CS_ResultData data)
    {
        if (data == null) return;

        var s = data.score;

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

        if (_totalScoreText != null)
            _totalScoreText.text = $"総合スコア : {data.totalScore}点";

        if (_rankText != null)
            _rankText.text = "順位 : ---"; // 仮

        if (_titleText != null)
            _titleText.text = $"称号 : {data.title}";
    }

    // =========================================================
    // マルチ用：描画
    // =========================================================

    /// <summary>
    /// マルチ用：全プレイヤー分の結果を表示する
    /// リストの順番 = 順位（Model でソート済み）
    /// </summary>
    public void UpdateMultiResults(List<CS_ResultData> results)
    {
        if (results == null || results.Count == 0) return;

        ClearPlayerResultItems();

        for (int i = 0; i < results.Count; i++)
        {
            var item = Instantiate(_playerResultPrefab, _playerResultsRoot);
            var itemView = item.GetComponent<CS_UIPlayerResultItemView>();
            if (itemView != null)
                itemView.UpdateView(results[i], rank: i + 1);

            _playerResultItems.Add(item);
        }
    }

    // =========================================================
    // ソロ / マルチ 切り替え
    // =========================================================

    public void ShowSoloPanel()
    {
        if (_soloPanel != null) _soloPanel.SetActive(true);
        if (_multiPanel != null) _multiPanel.SetActive(false);
    }

    public void ShowMultiPanel()
    {
        if (_soloPanel != null) _soloPanel.SetActive(false);
        if (_multiPanel != null) _multiPanel.SetActive(true);
    }

    // =========================================================
    // クリーンアップ
    // =========================================================

    private void ClearPlayerResultItems()
    {
        foreach (var item in _playerResultItems)
        {
            if (item != null) Destroy(item);
        }
        _playerResultItems.Clear();
    }

    private void OnDestroy()
    {
        ClearPlayerResultItems();
    }
}
