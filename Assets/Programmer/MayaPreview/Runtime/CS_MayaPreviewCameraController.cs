using UnityEngine;
using UnityEngine.InputSystem;

/*
+=====================================
 File   : CS_MayaPreviewCameraController
 Summary: Maya camera controls for the preview view.
          Alt+LMB tumble / Alt+MMB track / Alt+RMB dolly / wheel dolly,
          F frame selection, A frame all, click select (Shift / Ctrl+click add or remove).
          Speeds follow Maya's defaults (measured in Maya 2024):
          - tumble : 40 degrees per 100 pixels (tumbleCtx tumbleScale 1.0)
          - track  : the point at the centre of interest follows the cursor
          - dolly  : drag right / down = in, left / up = out; x0.649 per wheel notch
 History: 2026/10/11 Maya speeds, multi-selection framing
=====================================+
*/
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Maya Preview/Camera Controls")]
public class CS_MayaPreviewCameraController : MonoBehaviour
{
    // Maya defaults (constants on purpose: values saved in the scene must not override them)
    // Measured in Maya 2024 (default preferences):
    //   tumble 100 px = 40 deg, dolly drag 132 px = x0.664 distance, one wheel notch = x0.649
    private const float TumbleDegreesPerPixel = 0.4f;
    private const float DollyPerPixel = 0.0031f;     // distance *= exp(-0.0031 * pixels)
    private const float WheelFactor = 0.649f;        // distance *= 0.649 per notch toward the view
    private const float WheelUnitsPerNotch = 120f;   // Windows: 120 units per notch

    private float _distance = 10f;
    private Camera _camera;
    private Vector3 _pivot;
    private int _dragButton = -1;
    private CS_MayaPreviewSelection _selection;
    private CS_MayaPreviewInspector _inspector;
    private CS_MayaPreviewTransformTool _transformTool;

    private float _defaultNear;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _defaultNear = _camera.nearClipPlane;
        _pivot = transform.position + transform.forward * _distance;
        _selection = gameObject.AddComponent<CS_MayaPreviewSelection>();
        _inspector = gameObject.AddComponent<CS_MayaPreviewInspector>();
        _transformTool = gameObject.AddComponent<CS_MayaPreviewTransformTool>();
    }

    private void OnDisable() => _dragButton = -1;

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) _dragButton = -1;
    }

    private void LateUpdate()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (!Application.isFocused || mouse == null || keyboard == null) return;
        if (_transformTool.IsDragging)
        {
            _transformTool.HandleInput(mouse, keyboard);
            return;
        }
        Vector2 position = mouse.position.ReadValue();
        if (_inspector != null && _inspector.BlocksInput(position))
        {
            _dragButton = -1;
            return;
        }

        bool alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
        bool typing = _inspector != null && _inspector.Typing;
        if (!alt && !typing && _transformTool.HandleInput(mouse, keyboard)) return;
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f && _camera.pixelRect.Contains(position))
        {
            // Some devices report 120 per notch, others 1 per notch.
            float notches = Mathf.Abs(scroll) >= 30f ? scroll / WheelUnitsPerNotch : Mathf.Sign(scroll);
            Dolly(Mathf.Pow(WheelFactor, Mathf.Clamp(notches, -10f, 10f)));
        }

        if (!alt)
        {
            _dragButton = -1;
            if (_camera.pixelRect.Contains(position))
            {
                bool additive = keyboard.shiftKey.isPressed || keyboard.ctrlKey.isPressed;
                if (mouse.leftButton.wasPressedThisFrame) _selection.Pick(_camera, position, additive);
                if (!typing && keyboard.fKey.wasPressedThisFrame && _selection.TryGetBounds(out Bounds bounds)) Focus(bounds);
                if (!typing && keyboard.aKey.wasPressedThisFrame && !keyboard.ctrlKey.isPressed && TryGetSceneBounds(out Bounds all)) Focus(all);
            }
            return;
        }

        // Start only on a new press inside this camera's viewport.
        if (_dragButton < 0)
        {
            if (!_camera.pixelRect.Contains(position)) return;
            if (mouse.leftButton.wasPressedThisFrame) _dragButton = 0;
            else if (mouse.middleButton.wasPressedThisFrame) _dragButton = 1;
            else if (mouse.rightButton.wasPressedThisFrame) _dragButton = 2;
            return;
        }

        bool held = _dragButton == 0 ? mouse.leftButton.isPressed
            : _dragButton == 1 ? mouse.middleButton.isPressed : mouse.rightButton.isPressed;
        if (!held)
        {
            _dragButton = -1;
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();
        if (_dragButton == 0)
        {
            if (_camera.orthographic) return;
            Quaternion rotation = Quaternion.AngleAxis(delta.x * TumbleDegreesPerPixel, Vector3.up)
                * transform.rotation * Quaternion.AngleAxis(-delta.y * TumbleDegreesPerPixel, Vector3.right);
            transform.SetPositionAndRotation(_pivot - rotation * Vector3.forward * _distance, rotation);
        }
        else if (_dragButton == 1)
        {
            float height = _camera.orthographic ? 2f * _camera.orthographicSize
                : 2f * _distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 movement = (-transform.right * delta.x - transform.up * delta.y)
                * (height / Mathf.Max(1f, _camera.pixelHeight));
            _pivot += movement;
            transform.position += movement;
        }
        else
        {
            // Right or down = in (screen y grows upward in the Input System).
            float amount = delta.x - delta.y;
            Dolly(Mathf.Exp(Mathf.Clamp(-amount * DollyPerPixel, -10f, 10f)));
        }
    }

    private void Dolly(float scale)
    {
        if (_camera.orthographic)
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize * scale, 0.001f, 100000f);
        else
        {
            _distance = Mathf.Clamp(_distance * scale, 0.002f, 100000f);
            transform.position = _pivot - transform.forward * _distance;
            UpdateNear();
        }
    }

    // Close-ups of small Maya parts (real size, centimetres) need a nearer clip plane.
    private void UpdateNear()
    {
        if (_camera.orthographic) return;
        _camera.nearClipPlane = Mathf.Clamp(_distance * 0.05f, 0.0005f, Mathf.Max(0.0005f, _defaultNear));
    }

    public void SynchronizeView()
    {
        _pivot = transform.position + transform.forward * _distance;
    }

    public void FocusSelection()
    {
        if (_selection != null && _selection.TryGetBounds(out Bounds bounds)) Focus(bounds);
    }

    private static bool TryGetSceneBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!CS_MayaPreviewSelection.Selectable(r)) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    private void Focus(Bounds bounds)
    {
        _pivot = bounds.center;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
        float aspect = Mathf.Max(_camera.aspect, 0.01f);
        if (_camera.orthographic)
        {
            _camera.orthographicSize = radius * 1.15f / Mathf.Min(1f, aspect);
            _distance = Mathf.Max(_distance, radius + _camera.nearClipPlane);
        }
        else
        {
            float halfAngle = Mathf.Atan(Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * Mathf.Min(1f, aspect));
            _distance = radius * 1.15f / Mathf.Sin(halfAngle);
            UpdateNear();
        }
        _camera.farClipPlane = Mathf.Max(_camera.farClipPlane, _distance + radius * 2f);
        transform.position = _pivot - transform.forward * _distance;
    }
}
