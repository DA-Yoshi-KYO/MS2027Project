using UnityEngine;
using UnityEngine.InputSystem;

/*
+=====================================
 ファイル名 : CS_MayaPreviewIdleThrottle
 概要       : Maya Preview の exe を軽くする。
              画質 (HDRP の設定・解像度・エフェクト) には一切触らず、
              「1秒に何回描画するか」だけを状況に合わせて変える。

              操作中        : 最大 約60fps (144Hz モニターでも 144 回は描かない)
              操作してない  : 15fps   (Maya からの更新はこの間隔以内に拾う)
              Maya 作業中   : 10fps   (exe のウィンドウが裏にある時)

              マウス / キー操作、ウィンドウサイズ変更、Maya からの
              Mesh 更新があると即座に「操作中」に戻る。
 履歴       : 2026/10/28 新規作成
=====================================+
*/
public class CS_MayaPreviewIdleThrottle : MonoBehaviour
{
    // 何秒操作が無ければ省電力にするか
    private const float IDLE_DELAY = 1.5f;

    // 操作中の上限 fps
    private const float ACTIVE_FPS = 60f;

    // 操作していない時 (exe が手前)
    private const int IDLE_FPS = 15;

    // exe が裏にある時 (Maya で作業中)
    private const int BACKGROUND_FPS = 10;

    private enum Mode { None, Active, Idle, Background }

    private static float _lastActiveTime;

    private Mode _mode = Mode.None;
    private int _lastWidth;
    private int _lastHeight;

    // Maya から Mesh が届いた時などに呼ぶ
    public static void Wake()
    {
        _lastActiveTime = Time.unscaledTime;
    }

    private void Awake()
    {
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        // ビルドでは通常ログのスタックトレースを書かない
        // (Mesh 更新のたびにログファイルへ長いトレースを書くのは無駄)
        // エラーのトレースはそのまま残す
        if (!Application.isEditor)
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        }

        Wake();
    }

    private void Update()
    {
        if (HasActivity())
        {
            Wake();
        }

        Mode mode;

        if (Time.unscaledTime - _lastActiveTime <= IDLE_DELAY)
        {
            mode = Mode.Active;
        }
        else
        {
            mode = Application.isFocused ? Mode.Idle : Mode.Background;
        }

        if (mode != _mode)
        {
            _mode = mode;
            Apply();
        }
    }

    private bool HasActivity()
    {
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            return true;
        }

        if (!Application.isFocused)
        {
            return false;
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.delta.ReadValue().sqrMagnitude > 0f) return true;
            if (mouse.scroll.ReadValue().sqrMagnitude > 0f) return true;
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed) return true;
        }

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.isPressed)
        {
            return true;
        }

        return false;
    }

    private void Apply()
    {
        switch (_mode)
        {
            case Mode.Active:
                // VSync は残したまま (画面のちらつき・裂けを出さない)、
                // 高リフレッシュレートのモニターでは 2〜4 フレームに 1 回の描画にして約 60fps に抑える
                double hz = Screen.currentResolution.refreshRateRatio.value;
                int interval = hz > 0.0 ? Mathf.Clamp(Mathf.RoundToInt((float)(hz / ACTIVE_FPS)), 1, 4) : 1;
                QualitySettings.vSyncCount = interval;
                Application.targetFrameRate = -1;
                break;

            case Mode.Idle:
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = IDLE_FPS;
                break;

            case Mode.Background:
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = BACKGROUND_FPS;
                break;
        }
    }

    private void OnDisable()
    {
        _mode = Mode.None;
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
    }
}
