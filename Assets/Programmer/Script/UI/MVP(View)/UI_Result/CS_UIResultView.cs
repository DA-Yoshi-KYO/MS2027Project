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
/// ・ソロ：1人分の詳細表示
/// ・マルチ：参加人数分の詳細表示（スコア順・Prefab で動的生成）
/// </summary>
public class CS_UIResultView : CS_BaseView<CS_UIResultPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ソロ用 UI")]
    [SerializeField] private GameObject _soloPanel; // ソロ用パネル

    [SerializeField] private TextMeshProUGUI _defeatVillainCountText;
    [SerializeField] private TextMeshProUGUI _foundByPoliceCountText;
    [SerializeField] private TextMeshProUGUI _crimeCompletedCountText;
    [SerializeField] private TextMeshProUGUI _defeatPlayerCountText;
    [SerializeField] private TextMeshProUGUI _totalScoreText;
    [SerializeField] private TextMeshProUGUI _rankText;
    [SerializeField] private TextMeshProUGUI _titleText;

    [Header("マルチ用 UI")]
    [SerializeField] private GameObject _multiPanel;           // マルチ用パネル
    [SerializeField] private Transform _playerResultsRoot;    // 各プレイヤー結果の親
    [SerializeField] private GameObject _playerResultPrefab;   // 1プレイヤー分の表示 Prefab

    // =========================================================
    // 内部フィールド
    // =========================================================

    // 生成したプレイヤー結果 UI のリスト
    private readonly List<GameObject> _playerResultItems = new List<GameObject>();

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIResultPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // ソロ用：描画（Presenter から呼ばれる）
    // =========================================================

    public void UpdateDefeatVillainCount(int count)
    {
        if (_defeatVillainCountText != null)
            _defeatVillainCountText.text = $"倒した悪人 : {count}人 (+{count * 100}点)";
    }

    public void UpdateFoundByPoliceCount(int count)
    {
        if (_foundByPoliceCountText != null)
            _foundByPoliceCountText.text = $"警察に発見 : {count}回 (-{count * 50}点)";
    }

    public void UpdateCrimeCompletedCount(int count)
    {
        if (_crimeCompletedCountText != null)
            _crimeCompletedCountText.text = $"犯罪完遂 : {count}回 (-{count * 80}点)";
    }

    public void UpdateDefeatPlayerCount(int count)
    {
        if (_defeatPlayerCountText != null)
            _defeatPlayerCountText.text = $"他P撃破 : {count}人 (+{count * 50}点)"; // 仮
    }

    public void UpdateTotalScore(int score)
    {
        if (_totalScoreText != null)
            _totalScoreText.text = $"総合スコア : {score}点";
    }

    public void UpdateRank(int rank)
    {
        if (_rankText != null)
            _rankText.text = rank <= 0 ? "順位 : ---" : $"順位 : {rank}位";
    }

    public void UpdateTitle(string title)
    {
        if (_titleText != null)
            _titleText.text = $"称号 : {title}";
    }

    // =========================================================
    // マルチ用：描画（Presenter から呼ばれる）
    // =========================================================

    /// <summary>
    /// マルチ用：全プレイヤー分の結果を表示する
    /// スコア順に並び替え済みのリストを受け取る
    /// </summary>
    public void UpdateMultiPlayerResults(List<PlayerResultData> results)
    {
        if (results == null || results.Count == 0) return;

        // 既存の UI をクリア
        ClearPlayerResultItems();

        // 各プレイヤー分の UI を生成
        foreach (var result in results)
        {
            var item = Instantiate(_playerResultPrefab, _playerResultsRoot);
            _playerResultItems.Add(item);

            // Prefab 内のテキストを更新
            var itemView = item.GetComponent<CS_UIPlayerResultItemView>();
            if (itemView != null)
                itemView.UpdateView(result);
        }
    }

    // =========================================================
    // ソロ / マルチ 切り替え
    // =========================================================

    /// <summary>ソロ表示に切り替える</summary>
    public void ShowSoloPanel()
    {
        if (_soloPanel != null) _soloPanel.SetActive(true);
        if (_multiPanel != null) _multiPanel.SetActive(false);
    }

    /// <summary>マルチ表示に切り替える</summary>
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
