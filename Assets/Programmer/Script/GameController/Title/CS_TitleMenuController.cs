/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-20 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトルメニューの Controller
/// ・Model を生成して Presenter に渡す
/// ・決定された項目に応じて、シーン遷移 / ゲーム終了を行う
///     SoloMode  → _soloSceneName のシーンへ
///     MultiMode → _multiSceneName のシーンへ
///     End       → ゲームを終了(エディタでは再生を停止)
/// </summary>
public class CS_TitleMenuController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("タイトルメニューのPresenter(TitleMenuオブジェクト)")]
    [SerializeField]
    private CS_UITitleMenuPresenter _presenter;

    [Header("遷移先のシーン名(Build Settings への登録が必要)")]

    [Tooltip("SoloMode を決定した時に読み込むシーン")]
    [SerializeField]
    private string _soloSceneName;

    [Tooltip("MultiMode を決定した時に読み込むシーン")]
    [SerializeField]
    private string _multiSceneName;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UITitleMenuModel _model;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _model = new CS_UITitleMenuModel();
        _model.OnDecided += HandleDecided;
    }

    private void Start()
    {
        if (_presenter == null)
        {
            Debug.LogError(
                "[CS_TitleMenuController] _presenterが未設定です。"
            );
            return;
        }

        _presenter.BindModel(_model);
    }

    // =========================================================
    // 決定時の処理
    // =========================================================

    private void HandleDecided(CSE_TitleMenuItem item)
    {
        switch (item)
        {
            case CSE_TitleMenuItem.SoloMode:
                LoadScene(_soloSceneName);
                break;

            case CSE_TitleMenuItem.MultiMode:
                LoadScene(_multiSceneName);
                break;

            case CSE_TitleMenuItem.End:
                QuitGame();
                break;
        }
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError(
                "[CS_TitleMenuController] 遷移先のシーン名が未設定です。"
            );
            return;
        }

        // Build Settings に無いシーンは読み込めないので、先に確認する
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                "[CS_TitleMenuController] " +
                $"シーンを読み込めません(Build Settings に登録されているか確認): {sceneName}"
            );
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    private void OnDestroy()
    {
        _model?.Dispose();
    }
}