using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
[AddComponentMenu("Maya Preview/Camera Controls")]
public class CS_MayaPreviewCameraController : MonoBehaviour
{
    [Header("Maya Camera Controls")]
    [SerializeField] private float orbitSpeed = 0.25f;
    [SerializeField] private float dollySpeed = 0.01f;
    [Tooltip("Wheel sensitivity per notch. Movement scales with distance to the focus point.")]
    [SerializeField, Range(0.01f, 10f)] private float wheelSensitivity = 0.18f;
    [Tooltip("Windows wheel input units per notch. Standard mouse wheels use 120.")]
    [SerializeField, Min(1f)] private float wheelUnitsPerNotch = 120f;
    [SerializeField] private float distance = 10f;

    private Camera _camera;
    private Vector3 _pivot;
    private int _dragButton = -1;
    private CS_MayaPreviewSelection _selection;
    private CS_MayaPreviewInspector _inspector;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _pivot = transform.position + transform.forward * distance;
        _selection = gameObject.AddComponent<CS_MayaPreviewSelection>();
        _inspector = gameObject.AddComponent<CS_MayaPreviewInspector>();
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
        if (_inspector != null && _inspector.BlocksInput(mouse.position.ReadValue()))
        {
            _dragButton = -1;
            return;
        }

        bool alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f && _camera.pixelRect.Contains(mouse.position.ReadValue()))
        {
            float notches = scroll / Mathf.Max(1f, wheelUnitsPerNotch);
            Dolly(Mathf.Exp(Mathf.Clamp(-notches * wheelSensitivity, -2f, 2f)));
        }

        if (!alt)
        {
            _dragButton = -1;
            if (_camera.pixelRect.Contains(mouse.position.ReadValue()))
            {
                if (mouse.leftButton.wasPressedThisFrame) _selection.Pick(_camera, mouse.position.ReadValue());
                if (keyboard.fKey.wasPressedThisFrame && _selection.TryGetBounds(out Bounds bounds))
                    Focus(bounds);
            }
            return;
        }

        // Start only on a new press inside this camera's viewport.
        if (_dragButton < 0)
        {
            if (!_camera.pixelRect.Contains(mouse.position.ReadValue())) return;
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
            Quaternion rotation = Quaternion.AngleAxis(delta.x * orbitSpeed, Vector3.up)
                * transform.rotation * Quaternion.AngleAxis(-delta.y * orbitSpeed, Vector3.right);
            transform.SetPositionAndRotation(_pivot - rotation * Vector3.forward * distance, rotation);
        }
        else if (_dragButton == 1)
        {
            float height = _camera.orthographic ? 2f * _camera.orthographicSize
                : 2f * distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 movement = (-transform.right * delta.x - transform.up * delta.y)
                * (height / Mathf.Max(1f, _camera.pixelHeight));
            _pivot += movement;
            transform.position += movement;
        }
        else
        {
            float scale = Mathf.Exp(Mathf.Clamp(-(delta.x + delta.y) * dollySpeed, -10f, 10f));
            Dolly(scale);
        }
    }

    private void Dolly(float scale)
    {
        if (_camera.orthographic)
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize * scale, 0.01f, 100000f);
        else
        {
            distance = Mathf.Clamp(distance * scale, Mathf.Max(0.01f, _camera.nearClipPlane * 2f), 100000f);
            transform.position = _pivot - transform.forward * distance;
        }
    }

    public void SynchronizeView()
    {
        _pivot = transform.position + transform.forward * distance;
    }

    private void Focus(Bounds bounds)
    {
        _pivot = bounds.center;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
        float aspect = Mathf.Max(_camera.aspect, 0.01f);
        if (_camera.orthographic)
        {
            _camera.orthographicSize = radius * 1.15f / Mathf.Min(1f, aspect);
            distance = Mathf.Max(distance, radius + _camera.nearClipPlane);
        }
        else
        {
            float halfAngle = Mathf.Atan(Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * Mathf.Min(1f, aspect));
            distance = Mathf.Max(radius * 1.15f / Mathf.Sin(halfAngle), radius + _camera.nearClipPlane);
        }
        _camera.farClipPlane = Mathf.Max(_camera.farClipPlane, distance + radius * 2f);
        transform.position = _pivot - transform.forward * distance;
    }
}
