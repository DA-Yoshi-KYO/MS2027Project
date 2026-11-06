using UnityEngine;
using UnityEngine.InputSystem;

/*
+=====================================
 File   : CS_MayaPreviewFrameCache
 Summary: Draws the 3D scene only when it can have changed, and otherwise shows the last image.
          The preview camera renders into a screen-sized RenderTexture; the texture is drawn
          behind the GUI every frame (GUI.depth 1000), so the panels / gizmo stay responsive
          while HDRP does no work at all.
          The scene is drawn again (and kept drawn for SETTLE seconds so TAA / exposure settle) when:
            - the camera moves, zooms, or its viewport / the window size changes
            - a mouse button is held, the wheel turns or a key is pressed (editing, picking,
              dragging the gizmo, panel settings)
            - Maya sends an update (MarkDirty from the runtime server / idle throttle)
            - once every REFRESH seconds anyway (animations, objects created by Scan)
          Same resolution as the screen, so picking / WorldToScreenPoint are unchanged.
 History: 2026/11/05 New
=====================================+
*/
[DefaultExecutionOrder(10000)]   // after the camera controller / transform tool have moved things
[RequireComponent(typeof(Camera))]
public class CS_MayaPreviewFrameCache : MonoBehaviour
{
    private const float SETTLE = 0.5f;
    private const float REFRESH = 1.0f;

    private static float s_renderUntil;

    // Performance panel: how many times the 3D scene was really drawn.
    public static int SceneRenderCount { get; private set; }

    private Camera _camera;
    private RenderTexture _texture;
    private Matrix4x4 _lastView, _lastProjection;
    private Rect _lastRect;
    private float _nextRefresh;

    public static void MarkDirty(float hold = SETTLE)
    {
        s_renderUntil = Mathf.Max(s_renderUntil, Time.unscaledTime + hold);
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        MarkDirty();
    }

    private void OnDisable()
    {
        if (_camera != null)
        {
            _camera.targetTexture = null;
            _camera.enabled = true;
        }
        if (_texture != null)
        {
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }
    }

    private void LateUpdate()
    {
        EnsureTexture();

        float now = Time.unscaledTime;
        if (_camera.worldToCameraMatrix != _lastView || _camera.projectionMatrix != _lastProjection || _camera.rect != _lastRect)
        {
            _lastView = _camera.worldToCameraMatrix;
            _lastProjection = _camera.projectionMatrix;
            _lastRect = _camera.rect;
            MarkDirty();
        }

        if (Application.isFocused && HasInput()) MarkDirty();
        if (now >= _nextRefresh) { _nextRefresh = now + REFRESH; MarkDirty(0f); }

        bool render = now <= s_renderUntil;
        if (_camera.enabled != render) _camera.enabled = render;
        if (render) SceneRenderCount++;
    }

    private static bool HasInput()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed) return true;
            if (mouse.scroll.ReadValue().sqrMagnitude > 0f) return true;
        }
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.anyKey.isPressed;
    }

    private void EnsureTexture()
    {
        int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        if (_texture != null && _texture.width == width && _texture.height == height) return;
        if (_texture != null) { _camera.targetTexture = null; _texture.Release(); Destroy(_texture); }
        _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            name = "Maya Preview Frame", hideFlags = HideFlags.DontSave
        };
        _texture.Create();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = _texture;
        GL.Clear(true, true, Color.black);   // letterbox bars start black
        RenderTexture.active = previous;
        _camera.targetTexture = _texture;
        MarkDirty();
    }

    private void OnGUI()
    {
        GUI.depth = 1000;   // behind every other GUI (panels, gizmo, letterbox bars)
        if (Event.current.type != EventType.Repaint || _texture == null) return;
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texture, ScaleMode.StretchToFill, false);
        GUI.matrix = previous;
    }
}
