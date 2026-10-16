using UnityEngine;
using UnityEngine.InputSystem;

public class CS_MayaPreviewTransformTool : MonoBehaviour
{
    private Camera _camera;
    private CS_MayaPreviewSelection _selection;
    private CS_MayaPreviewInspector _inspector;
    private bool _scaleMode;
    private bool _rotateMode, _selectMode;
    private Quaternion _startRotation;
    private Plane _rotationPlane;
    private Vector3 _lastRotationDirection;
    private bool _planeRotation;
    private float _rotationAngle;
    private int _axis = -1;
    private Transform _target;
    private Vector2 _startMouse, _screenDirection;
    private Vector3 _startPosition, _startScale, _worldAxis;
    private Vector3 _scalePivotLocal, _scalePivotWorld;
    private float _unitsPerPixel;
    private readonly Vector3[] _axes = { Vector3.right, Vector3.up, Vector3.forward };
    private readonly Color[] _colors = { Color.red, Color.green, new Color(0.2f, 0.55f, 1f) };
    public bool IsDragging => _axis >= 0;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _selection = GetComponent<CS_MayaPreviewSelection>();
        _inspector = GetComponent<CS_MayaPreviewInspector>();
    }

    public bool HandleInput(Mouse mouse, Keyboard keyboard)
    {
        if (IsDragging)
        {
            if (_target == null || _selection.SelectedObject != _target.gameObject) { _axis = -1; return true; }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _target.position = _startPosition; _target.localScale = _startScale; _target.rotation = _startRotation;
                SyncCamera(); _axis = -1; return true;
            }
            if (!mouse.leftButton.isPressed) { _axis = -1; return true; }
            Vector2 delta = mouse.position.ReadValue() - _startMouse;
            float amount = Vector2.Dot(delta, _screenDirection);
            if (_rotateMode)
            {
                if (_planeRotation)
                {
                    Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
                    if (_rotationPlane.Raycast(ray, out float enter))
                    {
                        Vector3 direction = (ray.GetPoint(enter) - _scalePivotWorld).normalized;
                        if (direction.sqrMagnitude > 0.5f)
                        {
                            _rotationAngle += Vector3.SignedAngle(_lastRotationDirection, direction, _worldAxis);
                            _lastRotationDirection = direction;
                        }
                    }
                }
                else _rotationAngle = amount;
                Quaternion rotation = Quaternion.AngleAxis(_rotationAngle, _worldAxis);
                _target.rotation = rotation * _startRotation;
                _target.position = _scalePivotWorld + rotation * (_startPosition - _scalePivotWorld);
            }
            else if (_scaleMode)
            {
                float factor = Mathf.Exp(Mathf.Clamp(amount / 90f, -5f, 5f));
                Vector3 value = _startScale;
                if (_axis == 3) value *= factor;
                else value[_axis] *= factor;
                _target.localScale = value;
                _target.position += _scalePivotWorld - _target.TransformPoint(_scalePivotLocal);
            }
            else if (_axis == 3)
                _target.position = _startPosition + (_camera.transform.right * delta.x + _camera.transform.up * delta.y) * _unitsPerPixel;
            else _target.position = _startPosition + _worldAxis * amount * _unitsPerPixel;
            SyncCamera();
            return true;
        }
        if (keyboard.qKey.wasPressedThisFrame) { _selectMode = true; _rotateMode = _scaleMode = false; }
        if (keyboard.wKey.wasPressedThisFrame) { _selectMode = _rotateMode = _scaleMode = false; }
        if (keyboard.eKey.wasPressedThisFrame) { _rotateMode = true; _selectMode = _scaleMode = false; }
        if (keyboard.rKey.wasPressedThisFrame) { _scaleMode = true; _selectMode = _rotateMode = false; }
        if (_selectMode) return false;
        if (!mouse.leftButton.wasPressedThisFrame || _selection.SelectedObject == null) return false;
        Transform target = _selection.SelectedObject.transform;
        if (target == _camera.transform) return false;
        Vector3 pivot = Pivot(target);
        Vector3 centre3 = _camera.WorldToScreenPoint(pivot);
        if (centre3.z <= _camera.nearClipPlane) return false;
        Vector2 centre = centre3, point = mouse.position.ReadValue();
        int hit = -1;
        float best = 9;
        if (_rotateMode)
        {
            for (int i = 0; i < 3; i++)
                for (int segmentIndex = 0; segmentIndex < 64; segmentIndex++)
                {
                    Vector3 a = RingPoint(target, pivot, centre3.z, i, segmentIndex);
                    Vector3 b = RingPoint(target, pivot, centre3.z, i, segmentIndex + 1);
                    if (a.z <= _camera.nearClipPlane || b.z <= _camera.nearClipPlane) continue;
                    Vector2 segment = (Vector2)(b - a);
                    float u = Mathf.Clamp01(Vector2.Dot(point - (Vector2)a, segment) / Mathf.Max(segment.sqrMagnitude, 0.001f));
                    float separation = Vector2.Distance(point, (Vector2)a + segment * u);
                    if (separation < best)
                    { best = separation; hit = i; _screenDirection = segment.normalized; }
                }
        }
        else if (Vector2.Distance(point, centre) < 9) hit = 3;
        else for (int i = 0; i < 3; i++)
        {
            Vector3 axis = _scaleMode ? target.rotation * _axes[i] : _axes[i];
            Vector2 end = _camera.WorldToScreenPoint(pivot + axis * HandleLength(centre3.z));
            Vector2 segment = end - centre;
            if (segment.magnitude < 12) continue;
            float u = Mathf.Clamp01(Vector2.Dot(point - centre, segment) / segment.sqrMagnitude);
            float separation = Vector2.Distance(point, centre + segment * u);
            if (separation < best) { best = separation; hit = i; }
        }
        if (hit < 0) return false;
        _axis = hit; _target = target;
        _startMouse = point; _startPosition = target.position; _startScale = target.localScale;
        _scalePivotWorld = pivot; _scalePivotLocal = target.InverseTransformPoint(pivot);
        _startRotation = target.rotation;
        _unitsPerPixel = HandleLength(centre3.z) / 90f;
        if (_rotateMode)
        {
            _worldAxis = target.rotation * _axes[hit];
            _rotationPlane = new Plane(_worldAxis, pivot);
            Ray ray = _camera.ScreenPointToRay(point);
            _planeRotation = Mathf.Abs(Vector3.Dot(ray.direction, _worldAxis)) > 0.1f;
            _planeRotation &= _rotationPlane.Raycast(ray, out float enter);
            _lastRotationDirection = (ray.GetPoint(enter) - pivot).normalized;
            _planeRotation &= _lastRotationDirection.sqrMagnitude > 0.5f;
            _rotationAngle = 0;
        }
        else if (hit == 3) _screenDirection = new Vector2(1, 1).normalized;
        else
        {
            _worldAxis = _scaleMode ? target.rotation * _axes[hit] : _axes[hit];
            Vector2 projected = (Vector2)_camera.WorldToScreenPoint(pivot + _worldAxis * HandleLength(centre3.z)) - centre;
            _screenDirection = projected.normalized;
            _unitsPerPixel = HandleLength(centre3.z) / Mathf.Max(12, projected.magnitude);
        }
        return true;
    }

    private float HandleLength(float depth) => (_camera.orthographic ? 2 * _camera.orthographicSize
        : 2 * depth * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f)) * 90 / Mathf.Max(1, _camera.pixelHeight);

    private Vector3 RingPoint(Transform target, Vector3 pivot, float depth, int axis, int segment)
    {
        float angle = segment * (Mathf.PI * 2 / 64);
        Quaternion orientation = IsDragging && _rotateMode ? _startRotation : target.rotation;
        Vector3 offset = orientation * (_axes[(axis + 1) % 3] * Mathf.Cos(angle)
            + _axes[(axis + 2) % 3] * Mathf.Sin(angle)) * HandleLength(depth);
        return _camera.WorldToScreenPoint(pivot + offset);
    }

    private static Vector3 Pivot(Transform target)
    {
        var renderer = target.GetComponent<Renderer>();
        return renderer != null ? renderer.bounds.center : target.position;
    }

    private void SyncCamera()
    {
        var controller = _target.GetComponent<CS_MayaPreviewCameraController>();
        if (controller != null) controller.SynchronizeView();
    }

    private void OnApplicationFocus(bool focus) { if (!focus) _axis = -1; }
    private void OnDisable() => _axis = -1;

    private void OnGUI()
    {
        if (_selectMode || Event.current.type != EventType.Repaint || _selection.SelectedObject == null) return;
        Transform target = _selection.SelectedObject.transform;
        if (target == _camera.transform) return;
        Vector3 pivot = Pivot(target), centre = _camera.WorldToScreenPoint(pivot);
        if (centre.z <= _camera.nearClipPlane || !_camera.pixelRect.Contains(centre)) return;
        if (_inspector.BlocksInput(centre)) return;
        Vector2 start = new Vector2(centre.x, Screen.height - centre.y);
        Color previous = GUI.color;
        if (_rotateMode)
        {
            for (int i = 0; i < 3; i++)
            {
                GUI.color = _axis == i ? Color.yellow : _colors[i];
                for (int j = 0; j < 64; j++)
                {
                    Vector3 a = RingPoint(target, pivot, centre.z, i, j);
                    Vector3 b = RingPoint(target, pivot, centre.z, i, j + 1);
                    if (a.z <= _camera.nearClipPlane || b.z <= _camera.nearClipPlane
                        || _inspector.BlocksInput(a) || _inspector.BlocksInput(b)) continue;
                    Line(new Vector2(a.x, Screen.height - a.y), new Vector2(b.x, Screen.height - b.y));
                }
            }
            GUI.color = previous;
            GUI.Label(new Rect(start.x + 12, start.y + 12, 170, 24), "E : Rotate");
            return;
        }
        for (int i = 0; i < 3; i++)
        {
            Vector3 axis = _scaleMode ? target.rotation * _axes[i] : _axes[i];
            Vector3 projected = _camera.WorldToScreenPoint(pivot + axis * HandleLength(centre.z));
            if (projected.z <= _camera.nearClipPlane || _inspector.BlocksInput(projected)) continue;
            Vector2 end = new Vector2(projected.x, Screen.height - projected.y);
            if ((end - start).magnitude < 12) continue;
            GUI.color = _axis == i ? Color.yellow : _colors[i];
            Line(start, end);
            if (_scaleMode) GUI.DrawTexture(new Rect(end.x - 4, end.y - 4, 8, 8), Texture2D.whiteTexture);
            else
            {
                Vector2 direction = (end - start).normalized;
                Vector2 side = new Vector2(-direction.y, direction.x);
                Line(end, end - direction * 10 + side * 5);
                Line(end, end - direction * 10 - side * 5);
            }
        }
        GUI.color = Color.yellow;
        GUI.DrawTexture(new Rect(start.x - 4, start.y - 4, 8, 8), Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.Label(new Rect(start.x + 12, start.y + 12, 170, 24), _scaleMode ? "R : Scale" : "W : Move");
    }

    private static void Line(Vector2 a, Vector2 b)
    {
        Matrix4x4 matrix = GUI.matrix;
        Vector2 delta = b - a;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
        GUI.DrawTexture(new Rect(a.x, a.y - 1.5f, delta.magnitude, 3), Texture2D.whiteTexture);
        GUI.matrix = matrix;
    }
}
