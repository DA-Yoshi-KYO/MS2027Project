/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// リザルトシーンの Controller
/// ・リザルトシーンの GameControllerManager にアタッチする
/// ・ソロ / マルチ 両対応
/// </summary>
public class CS_ResultController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("UICanvas 配下の Result を直接セット")]
    [SerializeField] private CS_UIResultView _view;

    [Header("デバッグ設定")]
    [SerializeField] private bool _isMulti = false; // true = マルチ / false = ソロ
    [SerializeField] private int _debugPlayerCount = 4; // マルチのデバッグ人数

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIResultModel _model;
    private CS_UIResultPresenter _presenter;

    // =========================================================
    // Awake : Model 生成
    // =========================================================

    private void Awake()
    {
        _model = new CS_UIResultModel();
    }

    // =========================================================
    // Start : Presenter 取得 → MVP 組み立て → 仮データ表示
    // =========================================================

    private void Start()
    {
        if (_view == null)
        {
            Debug.LogError("[CS_ResultController] _view が未設定です！");
            return;
        }

        _presenter = _view.GetComponent<CS_UIResultPresenter>();

        if (_presenter == null)
        {
            Debug.LogError("[CS_ResultController] CS_UIResultPresenter が付いていません！");
            return;
        }

        _presenter.BindModel(_model);

        Debug.Log("[CS_ResultController] 初期化完了！");

        // ★ 仮データで表示確認
        ShowDebugResult();
    }

    // =========================================================
    // 仮データ表示（配置確認用・後で削除）
    // =========================================================

    private void ShowDebugResult()
    {
        if (_isMulti)
        {
            // ---- マルチ仮データ ----
            var allData = new List<ResultData>();
            for (int i = 0; i < _debugPlayerCount; i++)
            {
                allData.Add(new ResultData(
                    playerName: $"Player{i + 1}",
                    defeatVillainCount: Random.Range(0, 10),
                    foundByPoliceCount: Random.Range(0, 5),
                    crimeCompletedCount: Random.Range(0, 3),
                    defeatPlayerCount: Random.Range(0, 4)
                ));
            }
            SetMultiResult(allData);
        }
        else
        {
            // ---- ソロ仮データ ----
            var data = new ResultData(
                playerName: "Player1",
                defeatVillainCount: 5,
                foundByPoliceCount: 2,
                crimeCompletedCount: 1,
                defeatPlayerCount: 3
            );
            SetSoloResult(data);
        }
    }

    // =========================================================
    // 外部 API
    // =========================================================

    /// <summary>ソロ用：プレイヤーのスコアデータを受け取って表示する</summary>
    public void SetSoloResult(ResultData data)
    {
        if (data == null)
        {
            Debug.LogError("[CS_ResultController] ResultData が null です！");
            return;
        }
        _view.ShowSoloPanel();
        _model.SetResultData(data);
    }

    /// <summary>マルチ用：全プレイヤーのスコアデータを受け取って表示する</summary>
    public void SetMultiResult(List<ResultData> allPlayersData)
    {
        if (allPlayersData == null || allPlayersData.Count == 0)
        {
            Debug.LogError("[CS_ResultController] allPlayersData が null または空です！");
            return;
        }
        _view.ShowMultiPanel();
        _model.SetMultiResultData(allPlayersData);
    }

    /// <summary>ソロ用：順位をセットする（後から実装）</summary>
    public void SetRank(int rank) => _model.SetRank(rank);

    /// <summary>ソロ用：称号をセットする（後から実装）</summary>
    public void SetTitle(string title) => _model.SetTitle(title);

    /// <summary>マルチ用：特定プレイヤーの称号をセットする（後から実装）</summary>
    public void SetPlayerTitle(string playerName, string title)
        => _model.SetPlayerTitle(playerName, title);

    // =========================================================
    // OnDestroy : Model を破棄
    // =========================================================

    private void OnDestroy()
    {
        _model?.Dispose();
    }
}
