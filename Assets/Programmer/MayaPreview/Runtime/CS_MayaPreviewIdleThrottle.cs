using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/*
+=====================================
 ファイル名 : CS_MayaPreviewIdleThrottle
 概要       : Maya Preview の exe を軽くする。
              画質 (HDRP の設定・解像度・エフェクト) には一切触らず、
              「1秒に何回 Update するか」「そのうち何回描画するか」だけを状況に合わせて変える。

              操作中          : Graphics パネルで 60 / 30 / 15fps (既定 30)
              Maya から更新中 : 30fps (exe が裏でも Maya の編集はすぐ映る)
              落ち着くまで    : 15fps (操作をやめてから SETTLE_TIME 秒。TAA や雲を収束させる)
              操作してない    : Update 10fps / 描画 2fps (画面は止まっているので描き直さない)
              Maya 作業中     : Update 10fps / 描画 0.5fps (exe のウィンドウが裏にある時)

              描画しないフレームは OnDemandRendering で GPU の仕事を丸ごと省く
              (画面には最後に描いた絵がそのまま残る)。
              マウス / キー操作、ウィンドウサイズ変更があると即座に「操作中」、
              Maya から Mesh 更新が届くと即座に「Maya から更新中」に戻る。
 履歴       : 2026/10/28 新規作成
              2026/11/05 操作中の上限を 60 / 30 / 15 で切り替え (既定 30)
              2026/11/05 描画間引き (OnDemandRendering)、Maya 更新中モード、Performance 表示用ラベル
=====================================+
*/
public class CS_MayaPreviewIdleThrottle : MonoBehaviour
{
    // 何秒操作が無ければ省電力にするか
    private const float IDLE_DELAY = 1.5f;

    // 省電力にしてから、さらに何秒は毎フレーム描画するか (TAA / 雲 / 露出を落ち着かせる)
    private const float SETTLE_TIME = 1.0f;

    // 操作中の上限 fps (Graphics パネルで 60 / 30 / 15 を選ぶ)
    private static float s_activeFps = 30f;
    private static bool s_dirty;

    public static float ActiveFps
    {
        get => s_activeFps;
        set { if (!Mathf.Approximately(s_activeFps, value)) { s_activeFps = value; s_dirty = true; } }
    }

    // Maya から更新が届いている間
    private const int MAYA_FPS = 30;

    // 落ち着くまでの間
    private const int SETTLE_FPS = 15;

    // 操作していない時の Update 回数 (Maya からの受信はこの間隔以内に拾う)
    private const int IDLE_LOOP_FPS = 10;

    // 操作していない時、何回の Update に 1 回描画するか
    private const int IDLE_RENDER_INTERVAL = 5;         // 10fps / 5  = 2fps
    private const int BACKGROUND_RENDER_INTERVAL = 20;  // 10fps / 20 = 0.5fps

    private enum Mode { None, Active, Maya, Settle, Idle, Background }

    private static float _lastActiveTime;
    private static float _lastMayaTime = -100f;

    private Mode _mode = Mode.None;
    private int _lastWidth;
    private int _lastHeight;

    // Performance パネル用: 今の上限
    public static string CurrentModeLabel { get; private set; } = "-";

    // 操作 (マウス / キー / ウィンドウ) があった時
    public static void Wake()
    {
        _lastActiveTime = Time.unscaledTime;
    }

    // Maya から Mesh などが届いた時
    public static void WakeFromMaya()
    {
        _lastMayaTime = Time.unscaledTime;
        CS_MayaPreviewFrameCache.MarkDirty();
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

        float now = Time.unscaledTime;
        float quiet = Mathf.Min(now - _lastActiveTime, now - _lastMayaTime);

        Mode mode;

        if (now - _lastActiveTime <= IDLE_DELAY)
        {
            mode = Mode.Active;
        }
        else if (now - _lastMayaTime <= IDLE_DELAY)
        {
            mode = Mode.Maya;
        }
        else if (quiet <= IDLE_DELAY + SETTLE_TIME)
        {
            mode = Mode.Settle;
        }
        else
        {
            mode = Application.isFocused ? Mode.Idle : Mode.Background;
        }

        if (mode != _mode || s_dirty)
        {
            s_dirty = false;
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
                int interval = hz > 0.0 ? Mathf.Clamp(Mathf.RoundToInt((float)(hz / s_activeFps)), 1, 4) : 1;
                if (hz > 0.0 && hz / interval <= s_activeFps * 1.25f)
                {
                    QualitySettings.vSyncCount = interval;
                    Application.targetFrameRate = -1;
                }
                else
                {
                    // VSync の間引きでは上限まで下げきれない (240Hz など): フレームレート指定で抑える
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = Mathf.RoundToInt(s_activeFps);
                }
                OnDemandRendering.renderFrameInterval = 1;
                CurrentModeLabel = "Active (~" + Mathf.RoundToInt(QualitySettings.vSyncCount > 0 ? (float)(hz / interval) : s_activeFps) + " fps)";
                break;

            case Mode.Maya:
                int mayaFps = Mathf.Min(MAYA_FPS, Mathf.RoundToInt(s_activeFps));
                Set(mayaFps, 1);
                CurrentModeLabel = "Maya update (" + mayaFps + " fps)";
                break;

            case Mode.Settle:
                int settleFps = Mathf.Min(SETTLE_FPS, Mathf.RoundToInt(s_activeFps));
                Set(settleFps, 1);
                CurrentModeLabel = "Settling (" + settleFps + " fps)";
                break;

            case Mode.Idle:
                Set(IDLE_LOOP_FPS, IDLE_RENDER_INTERVAL);
                CurrentModeLabel = "Idle (draw " + RenderRate(IDLE_RENDER_INTERVAL) + " fps)";
                break;

            case Mode.Background:
                Set(IDLE_LOOP_FPS, BACKGROUND_RENDER_INTERVAL);
                CurrentModeLabel = "Background (draw " + RenderRate(BACKGROUND_RENDER_INTERVAL) + " fps)";
                // Behind Maya: give memory back to Windows (and to Maya).
                CS_MayaPreviewMemory.Trim();
                break;
        }
    }

    private static void Set(int loopFps, int renderInterval)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = loopFps;
        OnDemandRendering.renderFrameInterval = renderInterval;
    }

    private static string RenderRate(int interval) => ((float)IDLE_LOOP_FPS / interval).ToString("0.#");

    private void OnDisable()
    {
        _mode = Mode.None;
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
        OnDemandRendering.renderFrameInterval = 1;
        CurrentModeLabel = "off (uncapped)";
    }
}
