/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-06 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 展示用の実行環境を整えるクラス(全画面・カーソル・終了操作)
/// </summary>
// ========================================
/*
 * メモ
 * ・起動時に自動で生成され、シーンをまたいで残る。シーンに置く必要はない
 * ・全画面: ビルドでは起動時にフルスクリーンウィンドウにする(エディタでは何もしない)
 * ・カーソル: 自分のプレイヤーがいる間は消して固定し、いない間は独自カーソルを出す
 *   カーソルを触るのはこのクラスだけにする(CS_Playerからは触らない)
 * ・終了: ESCを長押しすると終了する(エディタでは再生を止める)。秒数はDB_ExhibitionSettingsで変える
 */
// ========================================
public class CS_ExhibitionRuntime : MonoBehaviour
{
    private const float DefaultQuitHoldSeconds = 2.5f;     // 設定ファイルが無い時の長押し秒数

    private static CS_ExhibitionRuntime _instance;

    private CSO_ExhibitionSettings _settings;
    private float _escapeHeldSeconds;      // ESCを押し続けている秒数
    private bool _isQuitting;

    private float quitHoldSeconds => _settings != null ? _settings.quitHoldSeconds : DefaultQuitHoldSeconds;

    // 最初のシーンを読む前に自分を作る
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (_instance != null) return;

        GameObject runtimeObject = new GameObject(nameof(CS_ExhibitionRuntime));
        DontDestroyOnLoad(runtimeObject);
        _instance = runtimeObject.AddComponent<CS_ExhibitionRuntime>();
    }

    private void Awake()
    {
        _settings = Resources.Load<CSO_ExhibitionSettings>(CSO_ExhibitionSettings.resourcePath);
        if (_settings == null) Debug.LogWarning($"[CS_ExhibitionRuntime] {CSO_ExhibitionSettings.resourcePath} が見つからないので、既定の設定で動きます");

        ApplyFullScreen();
        ApplyCursor(CS_Player.localPlayer != null);
        CS_Player.onLocalPlayerSet += OnLocalPlayerSet;
    }

    private void OnDestroy()
    {
        CS_Player.onLocalPlayerSet -= OnLocalPlayerSet;
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        UpdateQuitHold();
    }

    // 別のウィンドウから戻ってきた時に、カーソルの状態をかけ直す
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) ApplyCursor(CS_Player.localPlayer != null);
    }

    // 起動時の画面モードを全画面にする
    // ※ 前回起動時の画面モードが保存されていても、ここで上書きする
    private void ApplyFullScreen()
    {
#if !UNITY_EDITOR
        Resolution native = Screen.currentResolution;
        Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
#endif
    }

    // 自分のプレイヤーが出た・消えた時にカーソルを切り替える
    private void OnLocalPlayerSet(CS_Player player)
    {
        ApplyCursor(player != null);
    }

    // プレイ中はカーソルを消して固定し、それ以外(メニューなど)は独自カーソルを出す
    private void ApplyCursor(bool isPlaying)
    {
        if (isPlaying)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Texture2D texture = _settings != null ? _settings.cursorTexture : null;
        Vector2 hotspot = _settings != null ? _settings.cursorHotspot : Vector2.zero;
        Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
    }

    // ESCを押し続けた秒数を数え、決めた秒数に届いたら終了する
    private void UpdateQuitHold()
    {
        if (_isQuitting) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.isPressed)
        {
            _escapeHeldSeconds = 0f;
            return;
        }

        // 時間を止めている場面でも数えられるよう、unscaledDeltaTimeを使う
        _escapeHeldSeconds += Time.unscaledDeltaTime;
        if (_escapeHeldSeconds < quitHoldSeconds) return;

        _isQuitting = true;
        QuitApplication();
    }

    // アプリを終了する(エディタでは再生を止める)
    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
