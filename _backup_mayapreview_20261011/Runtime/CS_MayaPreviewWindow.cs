using System;
using System.Runtime.InteropServices;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewWindow
 Summary: Keeps the preview window at the game's aspect ratio so the image always matches
          the real game view.
          - Player window: only the corners resize (edges are fixed) and the ratio is kept
            while dragging (Win32 WM_NCHITTEST / WM_SIZING on Unity's own window).
          - Whenever the window ratio still differs (maximised, resized from outside),
            the camera is letter / pillar boxed and the bars are drawn black.
          - Aspect presets, "reset ratio" and window-size presets for the View menu.
 History: 2026/10/10 New
=====================================+
*/
[RequireComponent(typeof(Camera))]
public class CS_MayaPreviewWindow : MonoBehaviour
{
    // =========================================================
    // Presets
    // =========================================================

    public static readonly string[] AspectNames = { "16:9", "16:10", "4:3", "21:9", "1:1" };
    public static readonly float[] AspectValues = { 16f / 9f, 16f / 10f, 4f / 3f, 21f / 9f, 1f };
    public static readonly int[] WidthPresets = { 960, 1280, 1600, 1920 };

    private const string PrefsKey = "MayaPreview.Aspect";

    public int AspectIndex { get; private set; }
    public float Aspect => AspectValues[AspectIndex];

    private Camera _camera;
    private int _lastWidth, _lastHeight, _lastAspect = -1;
    private Rect _barA, _barB;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        AspectIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, 0), 0, AspectValues.Length - 1);
        s_aspect = Aspect;
        InstallHook();
    }

    private void OnDestroy() => RemoveHook();
    private void OnApplicationQuit() => RemoveHook();

    private void Update()
    {
        if (Screen.width == _lastWidth && Screen.height == _lastHeight && AspectIndex == _lastAspect) return;
        _lastWidth = Screen.width; _lastHeight = Screen.height; _lastAspect = AspectIndex;
        UpdateLetterbox();
    }

    // Black bars behind every other GUI (higher depth = drawn first / underneath).
    private void OnGUI()
    {
        GUI.depth = 1000;
        if (Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        GUI.color = Color.black;
        if (_barA.width > 0 && _barA.height > 0) GUI.DrawTexture(_barA, Texture2D.whiteTexture);
        if (_barB.width > 0 && _barB.height > 0) GUI.DrawTexture(_barB, Texture2D.whiteTexture);
        GUI.color = previous;
    }


    // =========================================================
    // Public (View menu)
    // =========================================================

    public void SetAspect(int index)
    {
        AspectIndex = Mathf.Clamp(index, 0, AspectValues.Length - 1);
        s_aspect = Aspect;
        PlayerPrefs.SetInt(PrefsKey, AspectIndex);
        ResetRatio();
    }

    // Resize the window so its client area matches the ratio again (keeps the width).
    public void ResetRatio()
    {
        SetClientWidth(Screen.width);
    }

    public void SetClientWidth(int width)
    {
        width = Mathf.Max(320, width);
        int height = Mathf.Max(180, Mathf.RoundToInt(width / Aspect));
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
    }


    // =========================================================
    // Letterbox
    // =========================================================

    private void UpdateLetterbox()
    {
        float windowAspect = Screen.width / (float)Mathf.Max(1, Screen.height);
        Rect rect = new Rect(0, 0, 1, 1);
        if (windowAspect > Aspect + 0.001f)
        {
            float width = Aspect / windowAspect;
            rect = new Rect((1 - width) * 0.5f, 0, width, 1);
        }
        else if (windowAspect < Aspect - 0.001f)
        {
            float height = windowAspect / Aspect;
            rect = new Rect(0, (1 - height) * 0.5f, 1, height);
        }
        _camera.rect = rect;

        // GUI rects (top-left origin, pixels)
        float w = Screen.width, h = Screen.height;
        if (rect.x > 0)
        {
            _barA = new Rect(0, 0, rect.x * w, h);
            _barB = new Rect(rect.xMax * w, 0, w - rect.xMax * w, h);
        }
        else if (rect.y > 0)
        {
            _barA = new Rect(0, 0, w, rect.y * h);
            _barB = new Rect(0, rect.yMax * h, w, h - rect.yMax * h);
        }
        else
        {
            _barA = _barB = Rect.zero;
        }
    }


    // =========================================================
    // Win32: corners-only resize with a fixed ratio (player build on Windows only)
    // =========================================================

    private static float s_aspect = 16f / 9f;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private const int GWLP_WNDPROC = -4;
    private const uint WM_NCHITTEST = 0x0084;
    private const uint WM_SIZING = 0x0214;
    private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTBOTTOM = 15, HTBORDER = 18;
    private const int WMSZ_LEFT = 1, WMSZ_RIGHT = 2, WMSZ_TOP = 3, WMSZ_TOPLEFT = 4, WMSZ_TOPRIGHT = 5,
                      WMSZ_BOTTOM = 6, WMSZ_BOTTOMLEFT = 7, WMSZ_BOTTOMRIGHT = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProcW(IntPtr previous, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hWnd, System.Text.StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();

    private static IntPtr s_window, s_previous;
    private static WndProcDelegate s_hook;   // kept alive for the lifetime of the hook

    private static void InstallHook()
    {
        if (s_previous != IntPtr.Zero) return;
        uint pid = GetCurrentProcessId();
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out uint owner);
            if (owner != pid) return true;
            var name = new System.Text.StringBuilder(64);
            GetClassNameW(hWnd, name, 64);
            if (name.ToString() != "UnityWndClass") return true;
            found = hWnd;
            return false;
        }, IntPtr.Zero);
        if (found == IntPtr.Zero) return;
        s_window = found;
        s_hook = Hook;
        s_previous = SetWindowLongPtr(s_window, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(s_hook));
    }

    private static void RemoveHook()
    {
        if (s_previous == IntPtr.Zero) return;
        SetWindowLongPtr(s_window, GWLP_WNDPROC, s_previous);
        s_previous = IntPtr.Zero;
    }

    [AOT.MonoPInvokeCallback(typeof(WndProcDelegate))]
    private static IntPtr Hook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_NCHITTEST)
        {
            IntPtr hit = CallWindowProcW(s_previous, hWnd, msg, wParam, lParam);
            int code = hit.ToInt32();
            // Edges do not resize; only the four corners do.
            if (code == HTLEFT || code == HTRIGHT || code == HTTOP || code == HTBOTTOM) return (IntPtr)HTBORDER;
            return hit;
        }
        if (msg == WM_SIZING && lParam != IntPtr.Zero)
        {
            KeepRatio(hWnd, wParam.ToInt32(), lParam);
            return (IntPtr)1;
        }
        return CallWindowProcW(s_previous, hWnd, msg, wParam, lParam);
    }

    private static void KeepRatio(IntPtr hWnd, int edge, IntPtr lParam)
    {
        GetWindowRect(hWnd, out RECT window);
        GetClientRect(hWnd, out RECT client);
        int frameWidth = (window.Right - window.Left) - (client.Right - client.Left);
        int frameHeight = (window.Bottom - window.Top) - (client.Bottom - client.Top);

        RECT rect = Marshal.PtrToStructure<RECT>(lParam);
        int clientWidth = Math.Max(320, rect.Right - rect.Left - frameWidth);
        int clientHeight = Math.Max(180, rect.Bottom - rect.Top - frameHeight);
        if (edge == WMSZ_TOP || edge == WMSZ_BOTTOM)
            clientWidth = (int)Math.Round(clientHeight * s_aspect);
        else
            clientHeight = (int)Math.Round(clientWidth / s_aspect);

        int width = clientWidth + frameWidth, height = clientHeight + frameHeight;
        bool fromLeft = edge == WMSZ_LEFT || edge == WMSZ_TOPLEFT || edge == WMSZ_BOTTOMLEFT;
        bool fromTop = edge == WMSZ_TOP || edge == WMSZ_TOPLEFT || edge == WMSZ_TOPRIGHT;
        if (fromLeft) rect.Left = rect.Right - width; else rect.Right = rect.Left + width;
        if (fromTop) rect.Top = rect.Bottom - height; else rect.Bottom = rect.Top + height;
        Marshal.StructureToPtr(rect, lParam, false);
    }
#else
    private static void InstallHook() { }
    private static void RemoveHook() { }
#endif
}
