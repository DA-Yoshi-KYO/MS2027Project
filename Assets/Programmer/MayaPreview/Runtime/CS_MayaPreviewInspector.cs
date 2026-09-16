using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class CS_MayaPreviewInspector : MonoBehaviour
{
    private CS_MayaPreviewSelection _selection;
    private Camera _view;
    private Light[] _lights;
    private Camera[] _cameras;
    private float _nextScan;
    private GameObject _editing;
    private readonly Dictionary<string, string> _fields = new Dictionary<string, string>();
    private readonly List<Rect> _iconRects = new List<Rect>();
    private bool _typing;
    private bool _inspectorOpen = true;
    private Rect InspectorButton => new Rect(Mathf.Max(0, Screen.width - 330), 24, 140, 28);
    private Vector2 _scroll;
    private bool _hierarchyOpen = true;
    private Vector2 _hierarchyScroll;
    private readonly HashSet<Transform> _visible = new HashSet<Transform>();
    private readonly List<Transform> _roots = new List<Transform>();
    private readonly HashSet<int> _collapsed = new HashSet<int>();
    private readonly Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();
    private readonly List<Material> _ownedMaterials = new List<Material>();
    private string _fieldScope = "";
    private GUIStyle _hierarchyRowStyle;
    private const float HierarchyPadding = 12f;
    private const float HierarchyRowGap = 6f;
    private Rect HierarchyPanel => new Rect(12, 12, 250, Mathf.Max(100, Screen.height - 24));
    private Rect HierarchyButton => new Rect(24, 24, 130, 28);
    private Rect Panel => new Rect(Mathf.Max(0, Screen.width - 330), 66, 320, Mathf.Max(100, Screen.height - 76));

    private void Awake()
    {
        _selection = GetComponent<CS_MayaPreviewSelection>();
        _view = GetComponent<Camera>();
        Scan();
    }

    private void Scan()
    {
        _lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        _cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        _visible.Clear(); _roots.Clear();
        foreach (var light in _lights) AddToHierarchy(light.transform);
        foreach (var camera in _cameras) AddToHierarchy(camera.transform);
        foreach (var filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            if (filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null
                && filter.name != "Selection Edges" && filter.name != "Maya Preview Grid (100m)")
                AddToHierarchy(filter.transform);
        _nextScan = Time.unscaledTime + 1;
    }

    private void Update() { if (Time.unscaledTime >= _nextScan) Scan(); }

    public bool BlocksInput(Vector2 screenPosition)
    {
        Vector2 point = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        if (HierarchyButton.Contains(point) || (_hierarchyOpen && HierarchyPanel.Contains(point))) return true;
        if (InspectorButton.Contains(point) || _typing || (_inspectorOpen && _selection.SelectedObject != null && Panel.Contains(point))) return true;
        foreach (Rect rect in _iconRects) if (rect.Contains(point)) return true;
        return false;
    }

    private void OnGUI()
    {
        _iconRects.Clear();
        DrawHierarchy();
        if (GUI.Button(InspectorButton, _inspectorOpen ? "Inspector  -" : "Inspector  +"))
        { _inspectorOpen = !_inspectorOpen; GUI.FocusControl(null); _typing = false; }
        foreach (var light in _lights) if (light != null) WorldIcon(light.gameObject, true);
        foreach (var camera in _cameras) if (camera != null && camera != _view) WorldIcon(camera.gameObject, false);

        GameObject target = _selection.SelectedObject;
        if (_editing != target)
        {
            _editing = target; _fields.Clear(); _scroll = Vector2.zero;
            GUI.FocusControl(null); _typing = false;
        }
        if (target == null || !_inspectorOpen) return;
        DrawPanel(Panel);
        GUILayout.BeginArea(Panel);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Inspector - " + target.name);
        GUILayout.EndHorizontal();
        _scroll = GUILayout.BeginScrollView(_scroll);
        Transform t = target.transform;
        bool live = target.name.StartsWith("MayaLive_") && t.parent != null && t.parent.name == "Maya_Model_Root";
        GUILayout.Label(live ? "Maya + Unity offset" : "Transform");
        if (live) GUILayout.Label("Displayed position = Maya + Position below");
        Vector3 position = VectorField("Position", t.localPosition);
        Vector3 rotation = VectorField("Rotation", t.localEulerAngles);
        Vector3 scale = VectorField("Scale", t.localScale);
        bool moved = position != t.localPosition || rotation != t.localEulerAngles;
        if (position != t.localPosition) t.localPosition = position;
        if (rotation != t.localEulerAngles) t.localEulerAngles = rotation;
        if (scale != t.localScale) t.localScale = scale;
        if (moved)
        {
            var controller = target.GetComponent<CS_MayaPreviewCameraController>();
            if (controller != null) controller.SynchronizeView();
        }
        var lightComponent = target.GetComponent<Light>();
        if (lightComponent != null)
        {
            GUILayout.Space(10); GUILayout.Label("Light - " + lightComponent.type);
            lightComponent.enabled = GUILayout.Toggle(lightComponent.enabled, "Enabled");
            lightComponent.intensity = Mathf.Max(0, Number("Intensity", lightComponent.intensity));
            lightComponent.color = ColorField("Light color", lightComponent.color);
            lightComponent.useColorTemperature = GUILayout.Toggle(lightComponent.useColorTemperature, "Use color temperature");
            if (lightComponent.useColorTemperature)
                lightComponent.colorTemperature = Mathf.Clamp(Number("Kelvin", lightComponent.colorTemperature), 1000, 20000);
            lightComponent.bounceIntensity = Mathf.Max(0, Number("Bounce light", lightComponent.bounceIntensity));
            if (lightComponent.type != LightType.Directional)
                lightComponent.range = Mathf.Max(0.01f, Number("Range", lightComponent.range));
            if (lightComponent.type == LightType.Spot)
            {
                lightComponent.spotAngle = Mathf.Clamp(Number("Spot angle", lightComponent.spotAngle), 1, 179);
                lightComponent.innerSpotAngle = Mathf.Clamp(Number("Inner angle", lightComponent.innerSpotAngle), 0, lightComponent.spotAngle);
            }
            bool shadows = GUILayout.Toggle(lightComponent.shadows != LightShadows.None, "Cast shadows");
            lightComponent.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            var hd = target.GetComponent<HDAdditionalLightData>();
            if (hd != null)
            {
                hd.lightDimmer = Mathf.Clamp01(Number("Light multiplier", hd.lightDimmer));
                hd.affectDiffuse = GUILayout.Toggle(hd.affectDiffuse, "Affect diffuse");
                hd.affectSpecular = GUILayout.Toggle(hd.affectSpecular, "Affect reflections");
                hd.volumetricDimmer = Mathf.Clamp01(Number("Fog lighting", hd.volumetricDimmer));
                if (lightComponent.type == LightType.Directional)
                    hd.angularDiameter = Mathf.Clamp(Number("Sun diameter", hd.angularDiameter), 0, 90);
                else hd.shapeRadius = Mathf.Max(0, Number("Source radius", hd.shapeRadius));
                if (shadows)
                {
                    hd.shadowDimmer = Mathf.Clamp01(Number("Shadow strength", hd.shadowDimmer));
                    hd.normalBias = Mathf.Max(0, Number("Normal bias", hd.normalBias));
                    hd.slopeBias = Mathf.Max(0, Number("Slope bias", hd.slopeBias));
                    hd.shadowNearPlane = Mathf.Max(0.001f, Number("Shadow near", hd.shadowNearPlane));
                }
            }
        }
        var meshRenderer = target.GetComponent<MeshRenderer>();
        if (meshRenderer != null) DrawMaterials(meshRenderer);
        var cameraComponent = target.GetComponent<Camera>();
        if (cameraComponent != null)
        {
            GUILayout.Space(10); GUILayout.Label("Camera");
            if (cameraComponent.orthographic)
                cameraComponent.orthographicSize = Mathf.Max(0.01f, Number("Ortho size", cameraComponent.orthographicSize));
            else cameraComponent.fieldOfView = Mathf.Clamp(Number("Field of view", cameraComponent.fieldOfView), 1, 179);
            cameraComponent.nearClipPlane = Mathf.Max(0.001f, Number("Near clip", cameraComponent.nearClipPlane));
            cameraComponent.farClipPlane = Mathf.Max(cameraComponent.nearClipPlane + 0.01f, Number("Far clip", cameraComponent.farClipPlane));
        }
        GUILayout.Space(10);
        GUILayout.Label("Changes apply during this run.");
        GUILayout.EndScrollView(); GUILayout.EndArea();
        _typing = GUI.GetNameOfFocusedControl().StartsWith("Inspector.");
        if (Event.current.type == EventType.MouseDown && !Panel.Contains(Event.current.mousePosition))
        { GUI.FocusControl(null); _typing = false; }
    }

    private void AddToHierarchy(Transform node)
    {
        while (node != null)
        {
            if (!_visible.Add(node)) break;
            if (node.parent == null) _roots.Add(node);
            node = node.parent;
        }
    }

    private static void DrawPanel(Rect rect)
    {
        Color previous = GUI.color;
        GUI.color = new Color(0.08f, 0.1f, 0.13f, 0.78f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private void DrawHierarchy()
    {
        if (_hierarchyOpen) DrawPanel(HierarchyPanel);
        if (GUI.Button(HierarchyButton, _hierarchyOpen ? "Hierarchy  -" : "Hierarchy  +"))
        { _hierarchyOpen = !_hierarchyOpen; GUI.FocusControl(null); _typing = false; }
        if (!_hierarchyOpen) return;
        if (_hierarchyRowStyle == null)
            _hierarchyRowStyle = new GUIStyle(GUI.skin.button) { margin = new RectOffset(0, 0, 0, 0) };
        Rect panel = HierarchyPanel;
        float contentTop = HierarchyButton.yMax + HierarchyPadding;
        GUILayout.BeginArea(new Rect(panel.x + HierarchyPadding, contentTop,
            panel.width - HierarchyPadding * 2, Mathf.Max(1, panel.yMax - HierarchyPadding - contentTop)));
        _hierarchyScroll = GUILayout.BeginScrollView(_hierarchyScroll);
        foreach (var root in _roots) if (root != null) DrawNode(root, 0);
        GUILayout.EndScrollView(); GUILayout.EndArea();
    }

    private void DrawNode(Transform node, int depth)
    {
        bool children = false;
        foreach (Transform child in node) if (_visible.Contains(child)) { children = true; break; }
        int id = node.GetInstanceID();
        GUILayout.BeginHorizontal(); GUILayout.Space(depth * 12);
        if (children)
        {
            if (GUILayout.Button(_collapsed.Contains(id) ? ">" : "v", _hierarchyRowStyle, GUILayout.Width(22), GUILayout.Height(26)))
            { if (!_collapsed.Add(id)) _collapsed.Remove(id); }
            GUILayout.Space(HierarchyRowGap);
        }
        else GUILayout.Space(22 + HierarchyRowGap);
        Color previous = GUI.backgroundColor;
        if (_selection.SelectedObject == node.gameObject) GUI.backgroundColor = new Color(1, 0.8f, 0.2f);
        string kind = node.GetComponent<Light>() != null ? "Light" : node.GetComponent<Camera>() != null ? "Camera" : "";
        string name = node.name.StartsWith("MayaLive_") ? node.name.Substring(9) : node.name;
        if (GUILayout.Button(string.IsNullOrEmpty(kind) ? name : name + "  (" + kind + ")", _hierarchyRowStyle, GUILayout.Height(26)))
        { GUI.FocusControl(null); _typing = false; _selection.SelectObject(node.gameObject); }
        GUI.backgroundColor = previous;
        GUILayout.EndHorizontal();
        GUILayout.Space(HierarchyRowGap);
        if (children && !_collapsed.Contains(id))
            foreach (Transform child in node) if (_visible.Contains(child)) DrawNode(child, depth + 1);
    }

    private Color ColorField(string label, Color value)
    {
        GUILayout.Label(label);
        Rect swatch = GUILayoutUtility.GetRect(20, 14, GUILayout.ExpandWidth(true));
        Color previous = GUI.color;
        GUI.color = new Color(value.r, value.g, value.b, 1);
        GUI.DrawTexture(swatch, Texture2D.whiteTexture); GUI.color = previous;
        string scope = _fieldScope;
        _fieldScope += label + ".";
        value.r = Slider("Red", value.r, 0, 1);
        value.g = Slider("Green", value.g, 0, 1);
        value.b = Slider("Blue", value.b, 0, 1);
        _fieldScope = scope;
        return value;
    }

    private float Slider(string label, float value, float min, float max)
    {
        value = Mathf.Clamp(Number(label, value), min, max);
        return GUILayout.HorizontalSlider(value, min, max);
    }

    private void DrawMaterials(MeshRenderer renderer)
    {
        GUILayout.Space(12); GUILayout.Label("Material");
        // Clone only when a value changes; never edit the shared source material.
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material source = materials[i];
            if (source == null) continue;
            _fieldScope = "Material." + i + ".";
            GUILayout.Label((i + 1) + ": " + source.name.Replace(" (Instance)", ""));
            string colorProperty = source.HasProperty("_BaseColor") ? "_BaseColor" : source.HasProperty("_Color") ? "_Color" : null;
            Color oldColor = colorProperty != null ? source.GetColor(colorProperty) : Color.white;
            Color newColor = colorProperty != null ? ColorField("Base color", oldColor) : oldColor;
            float oldMetal = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0;
            float metal = source.HasProperty("_Metallic") ? Slider("Metallic", oldMetal, 0, 1) : oldMetal;
            float oldSmooth = source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : 0;
            float smooth = source.HasProperty("_Smoothness") ? Slider("Smoothness", oldSmooth, 0, 1) : oldSmooth;
            bool changed = newColor != oldColor || metal != oldMetal || smooth != oldSmooth;
            if (changed)
            {
                if (!_originalMaterials.ContainsKey(renderer))
                {
                    _originalMaterials[renderer] = (Material[])materials.Clone();
                    for (int j = 0; j < materials.Length; j++)
                        if (materials[j] != null)
                        {
                            materials[j] = new Material(materials[j]) { name = materials[j].name };
                            _ownedMaterials.Add(materials[j]);
                        }
                    renderer.sharedMaterials = materials;
                }
                Material material = materials[i];
                if (colorProperty != null) material.SetColor(colorProperty, newColor);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smooth);
            }
        }
        _fieldScope = "";
    }

    private void OnDestroy()
    {
        foreach (var entry in _originalMaterials) if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
        foreach (var material in _ownedMaterials) if (material != null) Destroy(material);
    }

    private Vector3 VectorField(string title, Vector3 value)
    {
        GUILayout.Space(6); GUILayout.Label(title);
        value.x = Number(title + " X", value.x);
        value.y = Number(title + " Y", value.y);
        value.z = Number(title + " Z", value.z);
        return value;
    }

    private float Number(string label, float value)
    {
        string id = "Inspector." + _fieldScope + label;
        if (!_fields.ContainsKey(id) || GUI.GetNameOfFocusedControl() != id)
            _fields[id] = value.ToString("G7", CultureInfo.InvariantCulture);
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(110));
        GUI.SetNextControlName(id);
        string text = GUILayout.TextField(_fields[id]);
        bool changed = text != _fields[id];
        _fields[id] = text;
        GUILayout.EndHorizontal();
        if (changed && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            && !float.IsNaN(parsed) && !float.IsInfinity(parsed)) return parsed;
        return value;
    }

    private void WorldIcon(GameObject target, bool sun)
    {
        Vector3 point = _view.WorldToScreenPoint(target.transform.position);
        if (point.z <= _view.nearClipPlane || !_view.pixelRect.Contains(point)) return;
        Rect rect = new Rect(point.x - 16, Screen.height - point.y - 16, 32, 32);
        if ((_inspectorOpen && _selection.SelectedObject != null && Panel.Overlaps(rect)) || InspectorButton.Overlaps(rect)) return;
        if ((_hierarchyOpen && HierarchyPanel.Overlaps(rect)) || HierarchyButton.Overlaps(rect)
            || (rect.x > Screen.width - 54 && rect.y < 54)) return;
        Icon(target, sun, rect);
    }

    private void Icon(GameObject target, bool sun, Rect rect)
    {
        _iconRects.Add(rect);
        Color previous = GUI.backgroundColor;
        if (_selection.SelectedObject == target) GUI.backgroundColor = Color.yellow;
        if (GUI.Button(rect, rect.width > 40 ? "       " + target.name : ""))
        { GUI.FocusControl(null); _typing = false; _selection.SelectObject(target); }
        GUI.backgroundColor = previous;
        if (Event.current.type != EventType.Repaint) return;
        Color color = GUI.color;
        GUI.color = sun ? new Color(1, 0.8f, 0.1f) : new Color(0.4f, 0.8f, 1);
        Vector2 centre = new Vector2(rect.x + 16, rect.y + 16);
        if (sun)
        {
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                Vector2 a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float next = (i + 1) * Mathf.PI / 8;
                Line(centre + a * 5, centre + new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * 5);
                if (i % 2 == 0) Line(centre + a * 8, centre + a * 12);
            }
        }
        else
        {
            GUI.DrawTexture(new Rect(centre.x - 10, centre.y - 6, 13, 12), Texture2D.whiteTexture);
            Line(centre + new Vector2(3, -3), centre + new Vector2(10, -7));
            Line(centre + new Vector2(10, -7), centre + new Vector2(10, 7));
            Line(centre + new Vector2(10, 7), centre + new Vector2(3, 3));
        }
        GUI.color = color;
    }

    private static void Line(Vector2 a, Vector2 b)
    {
        Matrix4x4 matrix = GUI.matrix;
        Vector2 delta = b - a;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
        GUI.DrawTexture(new Rect(a.x, a.y - 1, delta.magnitude, 2), Texture2D.whiteTexture);
        GUI.matrix = matrix;
    }
}
