/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// リザルトシーンの Controller
/// ・リザルトシーンの GameControllerManager にアタッチする
/// ・CS_ResultDataStore から実データを受け取って表示する
/// ・データがない場合はデバッグ用の仮データを表示する
/// ・数字のカウントアップは、カットシーンが開き終わってから始める(カットシーン未設定ならすぐ始める)
/// </summary>
public class CS_ResultController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("UICanvas 配下の Result を直接セット")]
    [SerializeField] private CS_UIResultView _view;

    [Header("ソロのランクのしきい値(DB_ResultRankSettings)")]
    [SerializeField] private CSO_ResultRankSettings _rankSettings;

    // 追加
    [Header("カットシーン(任意)。設定すると、開き終わってから数字のカウントアップを始める")]
    [SerializeField] private CS_ResultCutsceneController _cutscene;

    // 追加
    [Tooltip("カットシーンの開き終わりが来なかった時の保険(秒)。この時間がたったら、カウントアップを始める")]
    [Min(0.5f)]
    [SerializeField] private float _countUpFallbackSeconds = 5f;

    [Header("デバッグ設定（ResultScene 単体で再生するとき用）")]
    [SerializeField] private bool _isMultiDebug = false;
    [SerializeField] private int _debugPlayerCount = 4;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIResultModel _model;
    private CS_UIResultPresenter _presenter;

    // カウントアップを始めたか(開き終わりと保険の両方が来ても、1回だけ始める)
    private bool _hasStartedCountUps;

    // =========================================================
    // Awake : Model 生成
    // =========================================================

    private void Awake()
    {
        _model = new CS_UIResultModel(_rankSettings);
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

        // 追加: 数字のカウントアップを始めるタイミングを決める
        ScheduleCountUps();
    }

    // =========================================================
    // カウントアップを始めるタイミング(追加)
    // =========================================================

    /// <summary>
    /// ・カットシーンが設定されていれば、開き終わり(OnRevealed)を待つ
    ///   (開き終わりが来なくても、_countUpFallbackSeconds 秒後には始める)
    /// ・設定されていなければ、すぐ始める
    /// </summary>
    private void ScheduleCountUps()
    {
        if (_cutscene == null)
        {
            StartCountUps();
            return;
        }

        _cutscene.OnRevealed += HandleCutsceneRevealed;
        StartCoroutine(FallbackRoutine());
    }

    private void HandleCutsceneRevealed()
    {
        StartCountUps();
    }

    /// <summary>開き終わりが来なかった時でも、数字が見えないままにならないようにする保険</summary>
    private IEnumerator FallbackRoutine()
    {
        // WaitForSecondsRealtime = timeScale=0 でも進む待ち時間
        yield return new WaitForSecondsRealtime(_countUpFallbackSeconds);

        StartCountUps();
    }

    private void StartCountUps()
    {
        if (_hasStartedCountUps)
        {
            return;
        }

        _hasStartedCountUps = true;

        if (_cutscene != null)
        {
            _cutscene.OnRevealed -= HandleCutsceneRevealed;
        }

        if (_view != null)
        {
            _view.PlayIntro();
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

            // ソロのエリアの結果(エリアごとのクリアタイムなど)。読み取ったらクリア
            if (CS_SoloResultStore.hasData)
            {
                _model.SetSoloAreaResult(CS_SoloResultStore.result);
                CS_SoloResultStore.Clear();
            }
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
        // デバッグ用に、キャラをランダムに選ぶための一覧
        var characterTypes = (CSE_CharacterType[])System.Enum.GetValues(typeof(CSE_CharacterType));

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
                int surpriseCount = UnityEngine.Random.Range(0, 7);
                int saCount = UnityEngine.Random.Range(0, 7);

                data.SetScore(new CS_ResultData.Score(
                    defeatVillainScore: villainCount * 100, // 仮の点数（実際はボーナス込みで加算）
                    foundByPoliceScore: policeCount * -50,
                    crimeCompletedScore: crimeCount * -80,
                    defeatPlayerScore: playerCount * 50,
                    defeatVillainCount: villainCount,
                    foundByPoliceCount: policeCount,
                    crimeCompletedCount: crimeCount,
                    defeatPlayerCount: playerCount,
                    surpriseAttackCount: surpriseCount,
                    saUseCount: saCount
                ));

                data.SetCharacterType(characterTypes[UnityEngine.Random.Range(0, characterTypes.Length)]);

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
                defeatPlayerCount: 3,
                surpriseAttackCount: 4,
                saUseCount: 2
            ));

            data.SetCharacterType(characterTypes[0]);

            _view.ShowSoloPanel();
            _model.SetSoloResult(data);

            // エリアごとのクリアタイム(3エリア。3つ目は未クリアの例)
            _model.SetSoloAreaResult(new CS_SoloAreaResult(
                CSE_SoloEndReason.TimeUp,
                2,
                3,
                0.8f,
                new List<CS_SoloAreaTime>
                {
                    new CS_SoloAreaTime(true, 45f),
                    new CS_SoloAreaTime(true, 85f),
                    new CS_SoloAreaTime(false, 120f),
                }
            ));
        }
    }

    // =========================================================
    // OnDestroy : Model を破棄
    // =========================================================

    private void OnDestroy()
    {
        if (_cutscene != null)
        {
            _cutscene.OnRevealed -= HandleCutsceneRevealed;
        }

        _model?.Dispose();
    }
}
