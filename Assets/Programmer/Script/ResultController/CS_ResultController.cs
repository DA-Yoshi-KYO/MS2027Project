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
/// ・CS_ResultDataStore から実データを受け取って表示する
/// ・データがない場合はデバッグ用の仮データを表示する
/// </summary>
public class CS_ResultController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("UICanvas 配下の Result を直接セット")]
    [SerializeField] private CS_UIResultView _view;

    [Header("デバッグ設定（ResultScene 単体で再生するとき用）")]
    [SerializeField] private bool _isMultiDebug = false;
    [SerializeField] private int _debugPlayerCount = 4;

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
    // Start : Presenter 取得 → MVP 組み立て → データ表示
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

        // ---- 実データがあれば表示・なければデバッグ表示 ----
        if (CS_ResultDataStore.hasData)
        {
            ShowRealResult();
        }
        else
        {
            ShowDebugResult();
        }
    }

    // =========================================================
    // 実データ表示（MainScene から受け取ったデータ）
    // =========================================================

    private void ShowRealResult()
    {
        var results = new List<CS_ResultData>(CS_ResultDataStore.results);

        // 読み取ったらクリア
        CS_ResultDataStore.Clear();

        if (results.Count == 1)
        {
            _view.ShowSoloPanel();
            _model.SetSoloResult(results[0]);
        }
        else
        {
            _view.ShowMultiPanel();
            _model.SetMultiResults(results);
        }
    }

    // =========================================================
    // デバッグ表示（ResultScene 単体で再生するとき用）
    // =========================================================

    private void ShowDebugResult()
    {
        if (_isMultiDebug)
        {
            // ---- マルチ仮データ ----
            var allData = new List<CS_ResultData>();
            for (int i = 0; i < _debugPlayerCount; i++)
            {
                var data = new CS_ResultData(i + 1);

                // 点数はゲーム中に計算済みの値を入れる想定
                // デバッグ用にランダムな値を入れる
                int villainCount = UnityEngine.Random.Range(0, 10);
                int policeCount = UnityEngine.Random.Range(0, 5);
                int crimeCount = UnityEngine.Random.Range(0, 3);
                int playerCount = UnityEngine.Random.Range(0, 4);

                data.SetScore(new CS_ResultData.Score(
                    defeatVillainScore: villainCount * 100, // 仮の点数（実際はボーナス込みで加算）
                    foundByPoliceScore: policeCount * -50,
                    crimeCompletedScore: crimeCount * -80,
                    defeatPlayerScore: playerCount * 50,
                    defeatVillainCount: villainCount,
                    foundByPoliceCount: policeCount,
                    crimeCompletedCount: crimeCount,
                    defeatPlayerCount: playerCount
                ));
                allData.Add(data);
            }
            _view.ShowMultiPanel();
            _model.SetMultiResults(allData);
        }
        else
        {
            // ---- ソロ仮データ ----
            var data = new CS_ResultData(1);
            data.SetScore(new CS_ResultData.Score(
                defeatVillainScore: 500, // 5人 × 100点（仮）
                foundByPoliceScore: -100, // 2回 × -50点
                crimeCompletedScore: -80, // 1回 × -80点
                defeatPlayerScore: 150, // 3人 × 50点（仮）
                defeatVillainCount: 5,
                foundByPoliceCount: 2,
                crimeCompletedCount: 1,
                defeatPlayerCount: 3
            ));
            _view.ShowSoloPanel();
            _model.SetSoloResult(data);
        }
    }

    // =========================================================
    // OnDestroy : Model を破棄
    // =========================================================

    private void OnDestroy()
    {
        _model?.Dispose();
    }
}
