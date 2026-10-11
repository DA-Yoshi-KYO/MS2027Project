using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/*
+=====================================
 File   : CS_MayaPreviewTransformTool
 Summary: Maya-style manipulators for the selection (Q select / W move / E rotate / R scale).
          - Works on every selected object at once around the selection centre.
          - Every drag is one undo step (Ctrl+Z); Esc cancels the drag.
 History: 2026/10/11 Multi-selection and undo
=====================================+
*/
public class CS_MayaPreviewTransformTool : MonoBehaviour
{
    private Camera _camera;
    private CS_MayaPreviewSelection _selection;
    private CS_MayaPreviewInspector _inspector;
    private bool _scaleMode;
    private bool _rotateMode, _selectMode;
    private Plane _rotationPlane;
    private Vector3 _lastRotationDirection;
    private bool _planeRotation;
    private float _rotationAngle;
    private int _axis = -1;
    private readonly List<Transform> _targets = new List<Transform>();
    private readonly List<Vector3> _startPositions = new List<Vector3>();
    private readonly List<Quaternion> _startRotations = new List<Quaternion>();
    private readonly List<Vector3> _startScales = new List<Vector3>();
    private Vector2 _startMouse, _screenDirection;
    private Vector3 _worldAxis, _pivot;
    private Quaternion _frame = Quaternion.identity;
    private float _unitsPerPixel;
    private readonly Vector3[] _axes = { Vector3.right, Vector3.up, Vector3.forward };
    private readonly Color[] _colors = { Color.red, Color.green, new Color(0.2f, 0.55f, 1f) };
    public bool IsDragging => _axis >= 0;
    public string Mode => _selectMode ? "Select" : _rotateMode ? "Rotate" : _scaleMode ? "Scale" : "Move";

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _selection = GetComponent<CS_MayaPreviewSelection>();
        _inspector = GetComponent<CS_MayaPreviewInspector>();
    }

    // Objects the manipulator moves (the view camera itself is never one of them).
    private List<Transform> Targets()
    {
        var list = new List<Transform>();
        foreach (GameObject o in _selection.Selected)
        {
            if (o == null || o.transform == _camera.transform) continue;
            // A child of another selected object moves with its parent.
            bool nested = false;
            foreach (GameObject other in _selection.Selected)
                if (other != null && other != o && o.transform.IsChildOf(other.transform)) { nested = true; break; }
            if (!nested) list.Add(o.transform);
        }
        return list;
    }

    // Single object: its own axes. Several objects: world axes.
    private Quaternion Frame(List<Transform> targets) => targets.Count == 1 ? targets[0].rotation : Quaternion.identity;

    public bool HandleInput(Mouse mouse, Keyboard keyboard)
    {
        if (IsDragging)
        {
            if (_targets.Exists(t => t == null)) { EndDrag(); return true; }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                for (int i = 0; i < _targets.Count; i++)
                {
                    _targets[i].position = _startPositions[i];
                    _targets[i].rotation = _startRotations[i];
                    _targets[i].localScale = _startScales[i];
                }
                SyncCamera(); EndDrag(); return true;
            }
            if (!mouse.leftButton.isPressed) { EndDrag(); return true; }
            CS_MayaPreviewUndo.Touch(UndoKey());
            Vector2 delta = mouse.position.ReadValue() - _startMouse;
            float amount = Vector2.Dot(delta, _screenDirection);
            if (_rotateMode)
            {
                if (_planeRotation)
                {
                    Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
                    if (_rotationPlane.Raycast(ray, out float enter))
                    {
                        Vector3 direction = (ray.GetPoint(enter) - _pivot).normalized;
                        if (direction.sqrMagnitude > 0.5f)
                        {
                            _rotationAngle += Vector3.SignedAngle(_lastRotationDirection, direction, _worldAxis);
                            _lastRotationDirection = direction;
                        }
                    }
                }
                else _rotationAngle = amount;
                Quaternion rotation = Quaternion.AngleAxis(_rotationAngle, _worldAxis);
                for (int i = 0; i < _targets.Count; i++)
                {
                    _targets[i].rotation = rotation * _startRotations[i];
                    _targets[i].position = _pivot + rotation * (_startPositions[i] - _pivot);
                }
            }
            else if (_scaleMode)
            {
                float factor = Mathf.Exp(Mathf.Clamp(amount / 90f, -5f, 5f));
                for (int i = 0; i < _targets.Count; i++)
                {
                    Transform t = _targets[i];
                    Vector3 value = _startScales[i];
                    Vector3 offset = _startPositions[i] - _pivot;
                    if (_axis == 3)
                    {
                        value *= factor;
                        offset *= factor;
                    }
                    else
                    {
                        Vector3 axis = _frame * _axes[_axis];
                        // the object's local axis closest to the dragged axis
                        int local = ClosestAxis(_startRotations[i], axis);
                        value[local] *= factor;
                        offset += axis * (Vector3.Dot(offset, axis) * (factor - 1f));
                    }
                    t.localScale = value;
                    t.position = _pivot + offset;
                }
            }
            else
            {
                Vector3 move = _axis == 3
                    ? (_camera.transform.right * delta.x + _camera.transform.up * delta.y) * _unitsPerPixel
                    : _worldAxis * amount * _unitsPerPixel;
                for (int i = 0; i < _targets.Count; i++) _targets[i].position = _startPositions[i] + move;
            }
            SyncCamera();
            return true;
        }
        if (keyboard.qKey.wasPressedThisFrame) { _selectMode = true; _rotateMode = _scaleMode = false; }
        if (keyboard.wKey.wasPressedThisFrame) { _selectMode = _rotateMode = _scaleMode = false; }
        if (keyboard.eKey.wasPressedThisFrame) { _rotateMode = true; _selectMode = _scaleMode = false; }
        if (keyboard.rKey.wasPressedThisFrame) { _scaleMode = true; _selectMode = _rotateMode = false; }
        if (_selectMode) return false;
        if (!mouse.leftButton.wasPressedThisFrame) return false;
        List<Transform> targets = Targets();
        if (targets.Count == 0) return false;
        Vector3 pivot = Pivot();
        Quaternion frame = Frame(targets);
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
                    Vector3 a = RingPoint(frame, pivot, centre3.z, i, segmentIndex);
                    Vector3 b = RingPoint(frame, pivot, centre3.z, i, segmentIndex + 1);
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
            Vector3 axis = _scaleMode ? frame * _axes[i] : _axes[i];
            Vector2 end = _camera.WorldToScreenPoint(pivot + axis * HandleLength(centre3.z));
            Vector2 segment = end - centre;
            if (segment.magnitude < 12) continue;
            float u = Mathf.Clamp01(Vector2.Dot(point - centre, segment) / segment.sqrMagnitude);
            float separation = Vector2.Distance(point, centre + segment * u);
            if (separation < best) { best = separation; hit = i; }
        }
        if (hit < 0) return false;

        _targets.Clear(); _targets.AddRange(targets);
        _startPositions.Clear(); _startRotations.Clear(); _startScales.Clear();
        foreach (Transform t in targets)
        {
            _startPositions.Add(t.position); _startRotations.Add(t.rotation); _startScales.Add(t.localScale);
        }
        CS_MayaPreviewUndo.RecordTransforms(_targets, _rotateMode ? "Rotate" : _scaleMode ? "Scale" : "Move", false);
        _axis = hit;
        _frame = frame;
        _startMouse = point;
        _pivot = pivot;
        _unitsPerPixel = HandleLength(centre3.z) / 90f;
        if (_rotateMode)
        {
            _worldAxis = frame * _axes[hit];
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
            _worldAxis = _scaleMode ? frame * _axes[hit] : _axes[hit];
            Vector2 projected = (Vector2)_camera.WorldToScreenPoint(pivot + _worldAxis * HandleLength(centre3.z)) - centre;
            _screenDirection = projected.normalized;
            _unitsPerPixel = HandleLength(centre3.z) / Mathf.Max(12, projected.magnitude);
        }
        return true;
    }

    private string UndoKey()
    {
        var key = new System.Text.StringBuilder("T");
        foreach (Transform t in _targets) key.Append(':').Append(t != null ? t.GetInstanceID() : 0);
        return key.ToString();
    }

    private void EndDrag()
    {
        _axis = -1;
        CS_MayaPreviewUndo.Close();
        _targets.Clear();
    }

    private static int ClosestAxis(Quaternion rotation, Vector3 axis)
    {
        int best = 0;
        float score = -1;
        for (int i = 0; i < 3; i++)
        {
            Vector3 local = rotation * (i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward);
            float s = Mathf.Abs(Vector3.Dot(local, axis));
            if (s > score) { score = s; best = i; }
        }
        return best;
    }

    private float HandleLength(float depth) => (_camera.orthographic ? 2 * _camera.orthographicSize
        : 2 * depth * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f)) * 90 / Mathf.Max(1, _camera.pixelHeight);

    private Vector3 RingPoint(Quaternion frame, Vector3 pivot, float depth, int axis, int segment)
    {
        float angle = segment * (Mathf.PI * 2 / 64);
        Quaternion orientation = IsDragging && _rotateMode ? _frame : frame;
        Vector3 offset = orientation * (_axes[(axis + 1) % 3] * Mathf.Cos(angle)
            + _axes[(axis + 2) % 3] * Mathf.Sin(angle)) * HandleLength(depth);
        return _camera.WorldToScreenPoint(pivot + offset);
    }

    // Centre of the selection (bounds of everything selected, or the object position).
    private Vector3 Pivot()
    {
        if (IsDragging && !_rotateMode && !_scaleMode && _targets.Count > 0)
            return _pivot + (_targets[0].position - _startPositions[0]);
        if (IsDragging) return _pivot;
        List<Transform> targets = Targets();
        if (targets.Count == 1)
        {
            var renderer = targets[0].GetComponent<Renderer>();
            if (renderer != null && CS_MayaPreviewSelection.Selectable(renderer)) return renderer.bounds.center;
            if (CS_MayaPreviewIcons.IsMarker(CS_MayaPreviewIcons.KindOf(targets[0]))) return targets[0].position;
            return _selection.TryGetBounds(out Bounds b) ? b.center : targets[0].position;
        }
        return _selection.TryGetBounds(out Bounds bounds) ? bounds.center : Vector3.zero;
    }

    private void SyncCamera()
    {
        foreach (Transform t in _targets)
        {
            if (t == null) continue;
            var controller = t.GetComponent<CS_MayaPreviewCameraController>();
            if (controller != null) controller.SynchronizeView();
        }
    }

    private void OnApplicationFocus(bool focus) { if (!focus && IsDragging) EndDrag(); }
    private void OnDisable() { if (IsDragging) EndDrag(); }

    private void OnGUI()
    {
        if (_selectMode || Event.current.type != EventType.Repaint || _inspector == null || _inspector.UiHidden) return;
        List<Transform> targets = IsDragging ? _targets : Targets();
        if (targets.Count == 0) return;
        Vector3 pivot = Pivot(), centre = _camera.WorldToScreenPoint(pivot);
        if (centre.z <= _camera.nearClipPlane || !_camera.pixelRect.Contains(centre)) return;
        if (_inspector.BlocksInput(centre)) return;
        Quaternion frame = IsDragging ? _frame : Frame(targets);
        Vector2 start = new Vector2(centre.x, Screen.height - centre.y);
        Color previous = GUI.color;
        if (_rotateMode)
        {
            for (int i = 0; i < 3; i++)
            {
                GUI.color = _axis == i ? Color.yellow : _colors[i];
                for (int j = 0; j < 64; j++)
                {
                    Vector3 a = RingPoint(frame, pivot, centre.z, i, j);
                    Vector3 b = RingPoint(frame, pivot, centre.z, i, j + 1);
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
            Vector3 axis = _scaleMode ? frame * _axes[i] : _axes[i];
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

    // Called with GUI.matrix = identity only (rotation around a pivot breaks under a scaled matrix).
    private static void Line(Vector2 a, Vector2 b)
    {
        Matrix4x4 matrix = GUI.matrix;
        Vector2 delta = b - a;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
        GUI.DrawTexture(new Rect(a.x, a.y - 1.5f, delta.magnitude, 3), Texture2D.whiteTexture);
        GUI.matrix = matrix;
    }
}
