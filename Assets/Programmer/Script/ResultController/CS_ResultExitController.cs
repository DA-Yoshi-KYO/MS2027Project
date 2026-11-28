/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// リザルトから抜ける流れの Controller(ソロ・マルチ共通)
///
/// 流れ
///   1. リザルトの演出が全部終わる(CS_ResultController が BeginWaiting で時間を渡す)
///   2. _inputDelayAfterIntro 秒後から、ボタンを受け付ける(促す表示 _promptObject があれば出す)
///   3. ボタンが押されたら
///        ・リザルトのデータを消す(CS_ResultDataStore / CS_SoloResultStore)
///        ・カットシーンで画面を閉じる(開く動きの逆再生。G2は左から右へ、G3は右から左へ)
///   4. 閉じ終わったら、タイトルのシーンへ遷移する
///        ・オンライン接続中なら、先に切断してから遷移する
///
/// ボタン(CS_CustomInputActionManager の「UI/Submit」)
///   キーボード : Enter(ほかに F・Space・テンキーの Enter も効く)
///   コントローラー : A ボタン(下ボタン)
///
/// ・演出の途中でボタンが押されても、何も起きない(スキップは作っていない)
/// ・遷移先のシーン名は、Inspector で設定する(Build Settings への登録が必要)
/// ・カットシーン(_cutscene)が未設定なら、閉じる動きは飛ばして、すぐタイトルへ遷移する
/// ・リザルトのシーンの、常に有効なオブジェクト(例: GameControllerManager)にアタッチする
/// </summary>
public class CS_ResultExitController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("リザルトのカットシーン(閉じる動きに使う)")]
    [SerializeField]
    private CS_ResultCutsceneController _cutscene;

    [Header("戻る先のシーン名(Build Settings への登録が必要)")]
    [SerializeField]
    private string _titleSceneName;

    [Header("ボタンの受け付け")]

    [Tooltip("演出が全部終わってから、ボタンを受け付け始めるまでの待ち時間(秒)")]
    [Min(0f)]
    [SerializeField]
    private float _inputDelayAfterIntro = 0.3f;

    [Header("ボタンを促す表示(任意。例: Press Enter)")]

    [Tooltip("ボタンを受け付け始めた時に表示する。空なら何も出さない。最初は自動で非表示になる")]
    [SerializeField]
    private GameObject _promptObject;

    // =========================================================
    // 内部フィールド
    // =========================================================

    // 購読中の InputAction(購読解除のために覚えておく)
    private InputAction _submitAction;

    // ボタンを受け付けてよいか(演出が全部終わった後)
    private bool _isReady;

    // 抜ける流れを始めたか(二重に始めないため)
    private bool _hasStarted;

    // 受け付け開始を待っている処理(途中で止めるために覚えておく)
    private Coroutine _waitCoroutine;

    // =========================================================
    // Unityイベント
    // =========================================================

    private void Awake()
    {
        SetPromptActive(false);
    }

    private void OnEnable()
    {
        // 「UI」マップ。CS_CustomInputActionManager が、全マップを有効にしている
        _submitAction =
            CS_CustomInputActionManager.instance.customInputAction.ResultUI.Submit;

        _submitAction.performed += HandleSubmit;
    }

    private void OnDisable()
    {
        // Manager は static で残り続けるので、解除しないと
        // シーン遷移後も、壊れた後のこのオブジェクトへ入力が届いてしまう
        if (_submitAction != null)
        {
            _submitAction.performed -= HandleSubmit;
            _submitAction = null;
        }
    }

    private void OnDestroy()
    {
        if (_cutscene != null)
        {
            _cutscene.OnClosed -= HandleClosed;
        }
    }

    // =========================================================
    // 演出が終わるのを待つ(CS_ResultController から呼ばれる)
    // =========================================================

    /// <summary>
    /// リザルトの演出が終わるまでの時間(秒)を受け取って、その後にボタンを受け付け始める。
    /// 2回目以降の呼び出しは無視する。
    /// </summary>
    public void BeginWaiting(float introSeconds)
    {
        if (_waitCoroutine != null ||
            _isReady ||
            _hasStarted)
        {
            return;
        }

        _waitCoroutine =
            StartCoroutine(
                WaitRoutine(
                    Mathf.Max(0f, introSeconds) +
                    _inputDelayAfterIntro
                )
            );
    }

    private IEnumerator WaitRoutine(float seconds)
    {
        // WaitForSecondsRealtime = timeScale=0 でも進む待ち時間
        yield return new WaitForSecondsRealtime(seconds);

        _isReady = true;
        _waitCoroutine = null;

        SetPromptActive(true);
    }

    // =========================================================
    // ボタン
    // =========================================================

    private void HandleSubmit(InputAction.CallbackContext context)
    {
        // 演出が終わるまでと、始めた後は、何もしない
        if (!_isReady ||
            _hasStarted)
        {
            return;
        }

        StartExit();
    }

    // =========================================================
    // 抜ける流れ
    // =========================================================

    private void StartExit()
    {
        _hasStarted = true;

        SetPromptActive(false);

        // リザルトのデータを消す(次に遊ぶ時に、古い結果が残らないように)
        CS_ResultDataStore.Clear();
        CS_SoloResultStore.Clear();

        if (_cutscene == null)
        {
            GoToTitle();
            return;
        }

        _cutscene.OnClosed += HandleClosed;

        // 始められなかった時(開き終わっていない等)は、待たずにタイトルへ進む
        if (!_cutscene.PlayClose())
        {
            _cutscene.OnClosed -= HandleClosed;
            GoToTitle();
        }
    }

    /// <summary>画面を閉じ終わった時。タイトルへ遷移する</summary>
    private void HandleClosed()
    {
        _cutscene.OnClosed -= HandleClosed;
        GoToTitle();
    }

    private void GoToTitle()
    {
        if (string.IsNullOrEmpty(_titleSceneName))
        {
            Debug.LogError(
                "[CS_ResultExitController] 戻る先のシーン名が未設定です。"
            );
            return;
        }

        // Build Settings に無いシーンは読み込めないので、先に確認する
        if (!Application.CanStreamedLevelBeLoaded(_titleSceneName))
        {
            Debug.LogError(
                "[CS_ResultExitController] " +
                $"シーンを読み込めません(Build Settings に登録されているか確認): {_titleSceneName}"
            );
            return;
        }

        StartCoroutine(LoadTitleRoutine());
    }

    /// <summary>
    /// オンライン接続中なら、先に切断してから、タイトルを読み込む。
    /// (接続したままシーンを読み込むと、Netcode のシーン管理とぶつかるため)
    /// </summary>
    private IEnumerator LoadTitleRoutine()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager != null &&
            networkManager.IsListening)
        {
            networkManager.Shutdown();

            // 切断が終わるまで待つ
            while (networkManager != null &&
                   networkManager.ShutdownInProgress)
            {
                yield return null;
            }
        }

        SceneManager.LoadScene(_titleSceneName);
    }

    // =========================================================
    // 促す表示
    // =========================================================

    private void SetPromptActive(bool isActive)
    {
        if (_promptObject != null)
        {
            _promptObject.SetActive(isActive);
        }
    }
}
