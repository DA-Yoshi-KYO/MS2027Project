using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
+=====================================
 File   : CS_MayaPreviewInspector
 Summary: Runtime Hierarchy / Inspector for the Maya preview player (Unity editor dark look).
          - Hierarchy button / panel at the top-left; Inspector button / panel at the top-right.
          - The whole GUI scales with the window (GUI.matrix), so a small PiP window
            gets a small GUI.
          - Tab hides / shows every panel and icon.
 History: 2026/10/10 Rebuilt layout and look (functions unchanged)
=====================================+
*/
public class CS_MayaPreviewInspector : MonoBehaviour
{
    // =========================================================
    // References / state
    // =========================================================

    private CS_MayaPreviewSelection _selection;
    private CS_MayaPreviewWindow _window;
    private bool _viewOpen;
    private Camera _view;
    private Light[] _lights;
    private Camera[] _cameras;
    private float _nextScan;
    private GameObject _editing;
    private readonly Dictionary<string, string> _fields = new Dictionary<string, string>();
    private readonly List<Rect> _iconRects = new List<Rect>();
    private bool _typing;
    private bool _inspectorOpen = true;
    private bool _hierarchyOpen = true;
    private bool _uiHidden;
    private Vector2 _scroll;
    private Vector2 _hierarchyScroll;
    private readonly HashSet<Transform> _visible = new HashSet<Transform>();
    private readonly List<Transform> _roots = new List<Transform>();
    private readonly HashSet<int> _collapsed = new HashSet<int>();
    private readonly HashSet<string> _foldedSections = new HashSet<string>();
    private readonly Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();
    private readonly List<Material> _ownedMaterials = new List<Material>();
    private string _fieldScope = "";
    private string _shaderPicker;   // material name whose shader list is open


    // =========================================================
    // Layout (virtual GUI units; GUI.matrix scales them to the window)
    // =========================================================

    private const float RefWidth = 1280f;
    private const float RefHeight = 720f;
    private const float MinScale = 0.6f;
    private const float MaxScale = 1.25f;
    private const float Margin = 6f;
    private const float ButtonWidth = 104f;
    private const float ButtonHeight = 22f;
    private const float ButtonGap = 1f;
    private const float PanelGap = 3f;
    private const float HeaderHeight = 22f;
    private const float RowHeight = 18f;
    private const float Indent = 14f;
    private const float LabelWidth = 108f;

    private float _scale = 1f;
    private float VW => Screen.width / _scale;
    private float VH => Screen.height / _scale;
    private float InspectorWidth => Mathf.Min(320f, VW * 0.5f);
    private float HierarchyWidth => Mathf.Min(250f, VW * 0.4f);
    private Rect HierarchyButton => new Rect(Margin, Margin, ButtonWidth, ButtonHeight);
    private Rect ViewButton => new Rect(HierarchyButton.xMax + ButtonGap, Margin, 72f, ButtonHeight);
    private Rect ViewPanel
    {
        get
        {
            float x = _hierarchyOpen ? HierarchyPanel.xMax + PanelGap : ViewButton.x;
            return new Rect(x, PanelTop, 214f, HeaderHeight + 128f);
        }
    }
    private Rect InspectorButton => new Rect(VW - Margin - ButtonWidth, Margin, ButtonWidth, ButtonHeight);
    private float PanelTop => Margin + ButtonHeight + PanelGap;
    private float PanelHeight => Mathf.Max(60f, VH - PanelTop - Margin);
    private Rect InspectorPanel => new Rect(VW - Margin - InspectorWidth, PanelTop, InspectorWidth,
        _selection != null && _selection.SelectedObject != null ? PanelHeight : HeaderHeight + 40f);
    private Rect HierarchyPanel => new Rect(Margin, Margin + ButtonHeight + PanelGap, HierarchyWidth,
        Mathf.Max(60f, VH - (Margin + ButtonHeight + PanelGap) - Margin));


    // =========================================================
    // Look (Unity editor dark theme)
    // =========================================================

    private static readonly Color PanelColor = new Color(0.22f, 0.22f, 0.22f, 0.97f);
    private static readonly Color HeaderColor = new Color(0.16f, 0.16f, 0.16f, 1f);
    private static readonly Color BorderColor = new Color(0.10f, 0.10f, 0.10f, 1f);
    private static readonly Color SectionColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    private static readonly Color HoverColor = new Color(0.27f, 0.27f, 0.27f, 1f);
    private static readonly Color SelectColor = new Color(0.17f, 0.36f, 0.53f, 1f);
    private static readonly Color TextColor = new Color(0.82f, 0.82f, 0.82f, 1f);
    private static readonly Color DimColor = new Color(0.56f, 0.56f, 0.56f, 1f);
    private static readonly Color FieldColor = new Color(0.165f, 0.165f, 0.165f, 1f);
    private static readonly Color AccentColor = new Color(0.23f, 0.47f, 0.73f, 1f);

    private GUISkin _skin;
    private GUIStyle _title, _dim, _bold, _row, _foldout, _axis, _toolbar, _toolbarOn, _section;
    private readonly List<Object> _owned = new List<Object>();


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        _selection = GetComponent<CS_MayaPreviewSelection>();
        _view = GetComponent<Camera>();
        _window = GetComponent<CS_MayaPreviewWindow>();
        if (_window == null) _window = gameObject.AddComponent<CS_MayaPreviewWindow>();
        Scan();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextScan) Scan();
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && !_typing)
            _uiHidden = !_uiHidden;
    }

    private void OnDestroy()
    {
        foreach (var entry in _originalMaterials) if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
        foreach (var material in _ownedMaterials) if (material != null) Destroy(material);
        foreach (var asset in _owned) if (asset != null) Destroy(asset);
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

    private void AddToHierarchy(Transform node)
    {
        while (node != null)
        {
            if (!_visible.Add(node)) break;
            if (node.parent == null) _roots.Add(node);
            node = node.parent;
        }
    }

    // Camera / transform tools ask this before using the mouse.
    public bool BlocksInput(Vector2 screenPosition)
    {
        if (_uiHidden) return false;
        Vector2 point = new Vector2(screenPosition.x, Screen.height - screenPosition.y) / _scale;
        if (HierarchyButton.Contains(point) || InspectorButton.Contains(point) || ViewButton.Contains(point)) return true;
        if (_viewOpen && ViewPanel.Contains(point)) return true;
        if (_hierarchyOpen && HierarchyPanel.Contains(point)) return true;
        if (_typing || (_inspectorOpen && InspectorPanel.Contains(point))) return true;
        foreach (Rect rect in _iconRects) if (rect.Contains(point)) return true;
        return false;
    }


    // =========================================================
    // OnGUI
    // =========================================================

    private void OnGUI()
    {
        if (Event.current.type == EventType.Layout || Event.current.type == EventType.Repaint)
            _iconRects.Clear();
        if (_uiHidden) return;

        EnsureSkin();
        GUISkin previousSkin = GUI.skin;
        Matrix4x4 previousMatrix = GUI.matrix;
        _scale = Mathf.Clamp(Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight), MinScale, MaxScale);
        GUI.skin = _skin;
        GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));

        foreach (var light in _lights) if (light != null) WorldIcon(light.gameObject, true);
        foreach (var camera in _cameras) if (camera != null && camera != _view) WorldIcon(camera.gameObject, false);

        DrawToolbar();
        if (_hierarchyOpen) DrawHierarchy();
        if (_inspectorOpen) DrawInspector();
        if (_viewOpen) DrawView();

        _typing = GUI.GetNameOfFocusedControl().StartsWith("Inspector.");
        if (Event.current.type == EventType.MouseDown
            && !(_inspectorOpen && InspectorPanel.Contains(Event.current.mousePosition)))
        { GUI.FocusControl(null); _typing = false; }

        GUI.matrix = previousMatrix;
        GUI.skin = previousSkin;
    }

    private void DrawToolbar()
    {
        if (GUI.Button(HierarchyButton, (_hierarchyOpen ? "▼  " : "►  ") + "Hierarchy", _hierarchyOpen ? _toolbarOn : _toolbar))
        { _hierarchyOpen = !_hierarchyOpen; GUI.FocusControl(null); }
        if (GUI.Button(ViewButton, (_viewOpen ? "\u25BC  " : "\u25BA  ") + "View", _viewOpen ? _toolbarOn : _toolbar))
        { _viewOpen = !_viewOpen; GUI.FocusControl(null); }
        if (GUI.Button(InspectorButton, (_inspectorOpen ? "▼  " : "►  ") + "Inspector", _inspectorOpen ? _toolbarOn : _toolbar))
        { _inspectorOpen = !_inspectorOpen; GUI.FocusControl(null); }
    }

    // Panel background + title bar. Returns the content rect.
    private Rect DrawWindow(Rect rect, string title)
    {
        Fill(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), BorderColor);
        Fill(rect, PanelColor);
        Rect header = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
        Fill(header, HeaderColor);
        Fill(new Rect(rect.x, header.yMax - 1, rect.width, 1), BorderColor);
        GUI.Label(new Rect(header.x + 8, header.y, header.width - 16, header.height), title, _title);
        return new Rect(rect.x + 2, header.yMax + 2, rect.width - 4, rect.height - HeaderHeight - 4);
    }


    // =========================================================
    // View (aspect ratio / window size)
    // =========================================================

    private void DrawView()
    {
        Rect content = DrawWindow(ViewPanel, "View");
        GUILayout.BeginArea(new Rect(content.x + 6, content.y + 2, content.width - 12, content.height - 4));
        GUILayout.Label("Aspect ratio", _dim);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < CS_MayaPreviewWindow.AspectNames.Length; i++)
            if (GUILayout.Button(CS_MayaPreviewWindow.AspectNames[i], _window.AspectIndex == i ? _toolbarOn : _toolbar, GUILayout.Height(RowHeight + 2)))
                _window.SetAspect(i);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        if (GUILayout.Button("Reset ratio", _toolbar, GUILayout.Height(RowHeight + 2))) _window.ResetRatio();
        GUILayout.Space(6);
        GUILayout.Label("Window width", _dim);
        GUILayout.BeginHorizontal();
        foreach (int width in CS_MayaPreviewWindow.WidthPresets)
            if (GUILayout.Button(width.ToString(), Screen.width == width ? _toolbarOn : _toolbar, GUILayout.Height(RowHeight + 2)))
                _window.SetClientWidth(width);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }


    // =========================================================
    // Hierarchy
    // =========================================================

    private void DrawHierarchy()
    {
        Rect content = DrawWindow(HierarchyPanel, "Hierarchy");
        GUILayout.BeginArea(content);
        _hierarchyScroll = GUILayout.BeginScrollView(_hierarchyScroll, false, false, GUIStyle.none, _skin.verticalScrollbar, GUIStyle.none);
        foreach (var root in _roots) if (root != null) DrawNode(root, 0);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // "MayaLive_|Chibi_GRP|Chibi_Head|Chibi_HeadShape" -> "Chibi_Head" (Outliner name, one line)
    private static string DisplayName(string raw)
    {
        string name = raw.StartsWith("MayaLive_") ? raw.Substring(9) : raw;
        int bar = name.LastIndexOf('|');
        if (bar >= 0 && bar < name.Length - 1) name = name.Substring(bar + 1);
        if (name.Length > 5 && name.EndsWith("Shape")) name = name.Substring(0, name.Length - 5);
        return name;
    }

    private void DrawNode(Transform node, int depth)
    {
        bool children = false;
        foreach (Transform child in node) if (_visible.Contains(child)) { children = true; break; }
        int id = node.GetInstanceID();
        Rect row = GUILayoutUtility.GetRect(10, RowHeight, GUILayout.ExpandWidth(true));
        bool selected = _selection.SelectedObject == node.gameObject;
        if (Event.current.type == EventType.Repaint)
        {
            if (selected) Fill(row, SelectColor);
            else if (row.Contains(Event.current.mousePosition)) Fill(row, HoverColor);
        }

        float x = row.x + 4 + depth * Indent;
        Rect fold = new Rect(x, row.y, Indent, row.height);
        if (children && GUI.Button(fold, _collapsed.Contains(id) ? "►" : "▼", _foldout))
        { if (!_collapsed.Add(id)) _collapsed.Remove(id); }

        Rect label = new Rect(fold.xMax + 2, row.y, row.xMax - fold.xMax - 2, row.height);
        string name = DisplayName(node.name);
        string kind = node.GetComponent<Light>() != null ? "Light" : node.GetComponent<Camera>() != null ? "Camera" : "";
        if (GUI.Button(label, GUIContent.none, GUIStyle.none))
        { GUI.FocusControl(null); _typing = false; _selection.SelectObject(node.gameObject); }
        GUI.Label(label, name, _row);
        if (kind.Length > 0)
        {
            float width = _row.CalcSize(new GUIContent(name)).x;
            GUI.Label(new Rect(label.x + width + 6, label.y, label.width - width - 6, label.height), kind, _dim);
        }

        if (children && !_collapsed.Contains(id))
            foreach (Transform child in node) if (_visible.Contains(child)) DrawNode(child, depth + 1);
    }


    // =========================================================
    // Inspector
    // =========================================================

    private void DrawInspector()
    {
        GameObject target = _selection.SelectedObject;
        if (_editing != target)
        {
            _editing = target; _fields.Clear(); _scroll = Vector2.zero;
            GUI.FocusControl(null); _typing = false;
        }
        Rect content = DrawWindow(InspectorPanel, "Inspector");
        if (target == null)
        {
            GUI.Label(new Rect(content.x + 8, content.y + 6, content.width - 16, RowHeight), "Nothing selected", _dim);
            return;
        }

        GUILayout.BeginArea(content);
        GUILayout.BeginHorizontal();
        GUILayout.Space(6);
        GUILayout.Label(DisplayName(target.name), _bold);
        GUILayout.EndHorizontal();
        GUILayout.Space(2);
        _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, _skin.verticalScrollbar, GUIStyle.none);

        Transform t = target.transform;
        bool live = target.name.StartsWith("MayaLive_") && t.parent != null && t.parent.name == "Maya_Model_Root";
        if (Section("Transform"))
        {
            if (live) Note("Displayed position = Maya + offset below");
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
        }

        var lightComponent = target.GetComponent<Light>();
        if (lightComponent != null && Section("Light  (" + lightComponent.type + ")"))
            DrawLight(target, lightComponent);

        var meshRenderer = target.GetComponent<MeshRenderer>();
        if (meshRenderer != null) DrawMaterials(meshRenderer);

        var cameraComponent = target.GetComponent<Camera>();
        if (cameraComponent != null && Section("Camera"))
        {
            if (cameraComponent.orthographic)
                cameraComponent.orthographicSize = Mathf.Max(0.01f, Number("Ortho size", cameraComponent.orthographicSize));
            else cameraComponent.fieldOfView = Mathf.Clamp(Number("Field of view", cameraComponent.fieldOfView), 1, 179);
            cameraComponent.nearClipPlane = Mathf.Max(0.001f, Number("Near clip", cameraComponent.nearClipPlane));
            cameraComponent.farClipPlane = Mathf.Max(cameraComponent.nearClipPlane + 0.01f, Number("Far clip", cameraComponent.farClipPlane));
        }

        GUILayout.Space(8);
        var library = CS_MayaPreviewMaterialLibrary.Current;
        Note(library != null && library.Folder != null
            ? "Maya materials are saved per name in:"
            : "Changes apply during this run.");
        if (library != null && library.Folder != null) Note(library.Folder);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawLight(GameObject target, Light lightComponent)
    {
        lightComponent.enabled = Toggle("Enabled", lightComponent.enabled);
        lightComponent.intensity = Mathf.Max(0, Number("Intensity", lightComponent.intensity));
        lightComponent.color = ColorField("Light color", lightComponent.color);
        lightComponent.useColorTemperature = Toggle("Use temperature", lightComponent.useColorTemperature);
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
        bool shadows = Toggle("Cast shadows", lightComponent.shadows != LightShadows.None);
        lightComponent.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        var hd = target.GetComponent<HDAdditionalLightData>();
        if (hd == null) return;
        SubHeader("HDRP");
        hd.lightDimmer = Mathf.Clamp01(Number("Light multiplier", hd.lightDimmer));
        hd.affectDiffuse = Toggle("Affect diffuse", hd.affectDiffuse);
        hd.affectSpecular = Toggle("Affect reflections", hd.affectSpecular);
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

    private void DrawMaterials(MeshRenderer renderer)
    {
        var library = CS_MayaPreviewMaterialLibrary.Current;
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material source = materials[i];
            if (source == null) continue;
            if (library != null && library.Owns(source, out string materialName))
            {
                // Maya material: pick the shader and edit every value; saved per material name
                if (Section("Material " + (i + 1) + "  " + materialName))
                    DrawMayaMaterial(library, materialName, source);
                continue;
            }
            if (Section("Material " + (i + 1) + "  " + source.name.Replace(" (Instance)", "")))
                DrawSceneMaterial(renderer, materials, i);
        }
        _fieldScope = "";
    }

    // Material created for a Maya material name (CS_MayaPreviewMaterialLibrary).
    private void DrawMayaMaterial(CS_MayaPreviewMaterialLibrary library, string materialName, Material material)
    {
        _fieldScope = "Maya." + materialName + ".";
        Shader shader = material.shader;

        BeginRow("Shader");
        if (GUILayout.Button((_shaderPicker == materialName ? "\u25BC  " : "\u25BA  ") + shader.name, _toolbar, GUILayout.Height(RowHeight)))
            _shaderPicker = _shaderPicker == materialName ? null : materialName;
        EndRow();
        if (_shaderPicker == materialName)
        {
            foreach (string name in library.ShaderNames)
            {
                BeginRow("");
                if (GUILayout.Button(name, name == shader.name ? _toolbarOn : _toolbar, GUILayout.Height(RowHeight)))
                {
                    _shaderPicker = null;
                    if (name != shader.name) { ClearFields(); library.SetShader(materialName, name); }
                }
                EndRow();
            }
            return;   // the material is replaced; draw its values next frame
        }

        BeginRow("");
        bool reset = GUILayout.Button("Reset (HDRP/Lit + Maya)", _toolbar, GUILayout.Height(RowHeight));
        EndRow();
        if (reset) { ClearFields(); library.ResetMaterial(materialName); return; }

        int count = shader.GetPropertyCount();
        for (int p = 0; p < count; p++)
        {
            ShaderPropertyFlags flags = shader.GetPropertyFlags(p);
            if ((flags & (ShaderPropertyFlags.HideInInspector | ShaderPropertyFlags.PerRendererData)) != 0) continue;
            string prop = shader.GetPropertyName(p);
            string label = shader.GetPropertyDescription(p);
            if (string.IsNullOrEmpty(label)) label = prop;
            if (library.IsOverridden(materialName, prop)) label = "* " + label;

            switch (shader.GetPropertyType(p))
            {
                case ShaderPropertyType.Range:
                {
                    Vector2 limits = shader.GetPropertyRangeLimits(p);
                    float value = material.GetFloat(prop);
                    float edited = Slider(label, value, limits.x, limits.y, prop);
                    if (edited != value) library.SetFloat(materialName, prop, edited);
                    break;
                }
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Int:
                {
                    float value = shader.GetPropertyType(p) == ShaderPropertyType.Int ? material.GetInteger(prop) : material.GetFloat(prop);
                    if (IsToggle(shader, p))
                    {
                        bool on = value > 0.5f;
                        if (Toggle(label, on) != on) library.SetFloat(materialName, prop, on ? 0f : 1f);
                    }
                    else
                    {
                        float edited = Number(label, value, prop);
                        if (edited != value) library.SetFloat(materialName, prop, edited);
                    }
                    break;
                }
                case ShaderPropertyType.Color:
                {
                    Color value = material.GetColor(prop);
                    Color edited = ColorField(label, value, (flags & ShaderPropertyFlags.HDR) != 0, prop);
                    if (edited != value) library.SetColor(materialName, prop, edited);
                    break;
                }
                case ShaderPropertyType.Vector:
                {
                    Vector4 value = material.GetVector(prop);
                    Vector4 edited = Vector4Field(label, value, prop);
                    if (edited != value) library.SetVector(materialName, prop, edited);
                    break;
                }
                case ShaderPropertyType.Texture:
                {
                    if (shader.GetPropertyTextureDimension(p) != TextureDimension.Tex2D) break;
                    Texture texture = material.GetTexture(prop);
                    string current = library.TexturePath(materialName, prop);
                    string edited = PathField(label, current, texture != null ? texture.name : "None", prop);
                    if (edited != null) library.SetTexture(materialName, prop, edited);
                    break;
                }
            }
        }
    }

    private static bool IsToggle(Shader shader, int index)
    {
        foreach (string attribute in shader.GetPropertyAttributes(index))
            if (attribute.StartsWith("Toggle") || attribute.StartsWith("MaterialToggle")) return true;
        return false;
    }

    private void ClearFields()
    {
        _fields.Clear();
        GUI.FocusControl(null);
        _typing = false;
    }

    // Scene material (not from Maya): quick look tweaks, cloned so the asset is never changed.
    private void DrawSceneMaterial(MeshRenderer renderer, Material[] materials, int i)
    {
        Material source = materials[i];
        _fieldScope = "Material." + i + ".";
        string colorProperty = source.HasProperty("_BaseColor") ? "_BaseColor" : source.HasProperty("_Color") ? "_Color" : null;
        Color oldColor = colorProperty != null ? source.GetColor(colorProperty) : Color.white;
        Color newColor = colorProperty != null ? ColorField("Base color", oldColor) : oldColor;
        float oldMetal = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0;
        float metal = source.HasProperty("_Metallic") ? Slider("Metallic", oldMetal, 0, 1) : oldMetal;
        float oldSmooth = source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : 0;
        float smooth = source.HasProperty("_Smoothness") ? Slider("Smoothness", oldSmooth, 0, 1) : oldSmooth;
        if (newColor == oldColor && metal == oldMetal && smooth == oldSmooth) return;
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


    // =========================================================
    // Fields (Unity inspector style: label column + value column)
    // =========================================================

    private bool Section(string title)
    {
        GUILayout.Space(2);
        Rect bar = GUILayoutUtility.GetRect(10, 20, GUILayout.ExpandWidth(true));
        Fill(bar, SectionColor);
        Fill(new Rect(bar.x, bar.y, bar.width, 1), BorderColor);
        bool open = !_foldedSections.Contains(title);
        if (GUI.Button(bar, (open ? "▼  " : "►  ") + title, _section))
        { if (open) _foldedSections.Add(title); else _foldedSections.Remove(title); open = !open; }
        if (open) GUILayout.Space(3);
        return open;
    }

    private void SubHeader(string title)
    {
        GUILayout.Space(4);
        GUILayout.BeginHorizontal(); GUILayout.Space(6); GUILayout.Label(title, _bold); GUILayout.EndHorizontal();
    }

    private void Note(string text)
    {
        GUILayout.BeginHorizontal(); GUILayout.Space(6); GUILayout.Label(text, _dim); GUILayout.EndHorizontal();
    }

    private void BeginRow(string label)
    {
        GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
        GUILayout.Space(10);
        GUILayout.Label(label, GUILayout.Width(LabelWidth));
    }

    private void EndRow()
    {
        GUILayout.Space(4);
        GUILayout.EndHorizontal();
        GUILayout.Space(2);
    }

    private Vector3 VectorField(string title, Vector3 value)
    {
        BeginRow(title);
        GUILayout.Label("X", _axis, GUILayout.Width(10)); value.x = NumberBox(title + " X", value.x);
        GUILayout.Label("Y", _axis, GUILayout.Width(10)); value.y = NumberBox(title + " Y", value.y);
        GUILayout.Label("Z", _axis, GUILayout.Width(10)); value.z = NumberBox(title + " Z", value.z);
        EndRow();
        return value;
    }

    // key: unique id inside the current _fieldScope (defaults to the label)
    private float Number(string label, float value, string key = null)
    {
        BeginRow(label);
        value = NumberBox(key ?? label, value);
        EndRow();
        return value;
    }

    private float Slider(string label, float value, float min, float max, string key = null)
    {
        key = key ?? label;
        BeginRow(label);
        float slid = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
        if (slid != value) { value = slid; _fields.Remove("Inspector." + _fieldScope + key); }
        value = Mathf.Clamp(NumberBox(key, value, 48), min, max);
        EndRow();
        return value;
    }

    private Vector4 Vector4Field(string label, Vector4 value, string key)
    {
        BeginRow(label);
        value.x = NumberBox(key + ".x", value.x);
        value.y = NumberBox(key + ".y", value.y);
        value.z = NumberBox(key + ".z", value.z);
        value.w = NumberBox(key + ".w", value.w);
        EndRow();
        return value;
    }

    // Colour with RGBA; HDR colours accept values above 1.
    private Color ColorField(string label, Color value, bool hdr, string key)
    {
        BeginRow(label);
        Rect swatch = GUILayoutUtility.GetRect(20, 14, GUILayout.ExpandWidth(true));
        swatch.y += 2;
        if (Event.current.type == EventType.Repaint)
        {
            Fill(swatch, BorderColor);
            Fill(new Rect(swatch.x + 1, swatch.y + 1, swatch.width - 2, swatch.height - 2), new Color(value.r, value.g, value.b, 1));
        }
        EndRow();
        if (hdr)
        {
            value.r = Mathf.Max(0, Number("    R", value.r, key + ".r"));
            value.g = Mathf.Max(0, Number("    G", value.g, key + ".g"));
            value.b = Mathf.Max(0, Number("    B", value.b, key + ".b"));
        }
        else
        {
            value.r = Slider("    R", value.r, 0, 1, key + ".r");
            value.g = Slider("    G", value.g, 0, 1, key + ".g");
            value.b = Slider("    B", value.b, 0, 1, key + ".b");
        }
        value.a = Slider("    A", value.a, 0, 1, key + ".a");
        return value;
    }

    // Texture slot: shows the file path. Returns the new path when a valid file (or empty) is entered, else null.
    private string PathField(string label, string path, string textureName, string key)
    {
        BeginRow(label);
        string id = "Inspector." + _fieldScope + key + ".path";
        if (!_fields.ContainsKey(id) || GUI.GetNameOfFocusedControl() != id) _fields[id] = path;
        GUI.SetNextControlName(id);
        string text = GUILayout.TextField(_fields[id], GUILayout.MinWidth(30), GUILayout.ExpandWidth(true));
        bool changed = text != _fields[id];
        _fields[id] = text;
        EndRow();
        if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(text)) Note("            " + textureName);
        if (!changed) return null;
        string trimmed = text.Trim().Trim('"');
        if (trimmed.Length == 0) return path.Length > 0 ? "" : null;
        string extension = Path.GetExtension(trimmed).ToLowerInvariant();
        bool image = extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        return image && trimmed != path && File.Exists(trimmed) ? trimmed : null;
    }

    private bool Toggle(string label, bool value)
    {
        BeginRow(label);
        Rect box = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
        box.y += 2;
        if (GUI.Button(box, GUIContent.none, GUIStyle.none)) { value = !value; GUI.FocusControl(null); }
        if (Event.current.type == EventType.Repaint)
        {
            Fill(box, BorderColor);
            Fill(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), FieldColor);
            if (value) Fill(new Rect(box.x + 3, box.y + 3, box.width - 6, box.height - 6), AccentColor);
        }
        GUILayout.FlexibleSpace();
        EndRow();
        return value;
    }

    private Color ColorField(string label, Color value)
    {
        BeginRow(label);
        Rect swatch = GUILayoutUtility.GetRect(20, 14, GUILayout.ExpandWidth(true));
        swatch.y += 2;
        if (Event.current.type == EventType.Repaint)
        {
            Fill(swatch, BorderColor);
            Fill(new Rect(swatch.x + 1, swatch.y + 1, swatch.width - 2, swatch.height - 2), new Color(value.r, value.g, value.b, 1));
        }
        EndRow();
        string scope = _fieldScope;
        _fieldScope += label + ".";
        value.r = Slider("    R", value.r, 0, 1);
        value.g = Slider("    G", value.g, 0, 1);
        value.b = Slider("    B", value.b, 0, 1);
        _fieldScope = scope;
        return value;
    }

    private float NumberBox(string label, float value, float width = 0)
    {
        string id = "Inspector." + _fieldScope + label;
        if (!_fields.ContainsKey(id) || GUI.GetNameOfFocusedControl() != id)
            _fields[id] = value.ToString("G6", CultureInfo.InvariantCulture);
        GUI.SetNextControlName(id);
        string text = width > 0
            ? GUILayout.TextField(_fields[id], GUILayout.Width(width))
            : GUILayout.TextField(_fields[id], GUILayout.MinWidth(30), GUILayout.ExpandWidth(true));
        bool changed = text != _fields[id];
        _fields[id] = text;
        if (changed && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            && !float.IsNaN(parsed) && !float.IsInfinity(parsed)) return parsed;
        return value;
    }


    // =========================================================
    // Light / camera icons in the viewport
    // =========================================================

    private void WorldIcon(GameObject target, bool sun)
    {
        Vector3 point = _view.WorldToScreenPoint(target.transform.position);
        if (point.z <= _view.nearClipPlane || !_view.pixelRect.Contains(point)) return;
        Vector2 gui = new Vector2(point.x, Screen.height - point.y) / _scale;
        Rect rect = new Rect(gui.x - 14, gui.y - 14, 28, 28);
        if (HierarchyButton.Overlaps(rect) || InspectorButton.Overlaps(rect)) return;
        if (_inspectorOpen && InspectorPanel.Overlaps(rect)) return;
        if (_hierarchyOpen && HierarchyPanel.Overlaps(rect)) return;
        if (ViewButton.Overlaps(rect) || (_viewOpen && ViewPanel.Overlaps(rect))) return;
        Icon(target, sun, rect);
    }

    private void Icon(GameObject target, bool sun, Rect rect)
    {
        _iconRects.Add(rect);
        bool selected = _selection.SelectedObject == target;
        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        { GUI.FocusControl(null); _typing = false; _selection.SelectObject(target); }
        if (Event.current.type != EventType.Repaint) return;
        Fill(rect, selected ? new Color(0.17f, 0.36f, 0.53f, 0.9f) : new Color(0.12f, 0.12f, 0.12f, 0.75f));
        Color color = GUI.color;
        GUI.color = sun ? new Color(1, 0.8f, 0.1f) : new Color(0.4f, 0.8f, 1);
        Vector2 centre = rect.center;
        if (sun)
        {
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                Vector2 a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float next = (i + 1) * Mathf.PI / 8;
                Line(centre + a * 4, centre + new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * 4);
                if (i % 2 == 0) Line(centre + a * 7, centre + a * 10);
            }
        }
        else
        {
            GUI.DrawTexture(new Rect(centre.x - 9, centre.y - 5, 11, 10), Texture2D.whiteTexture);
            Line(centre + new Vector2(2, -2), centre + new Vector2(8, -6));
            Line(centre + new Vector2(8, -6), centre + new Vector2(8, 6));
            Line(centre + new Vector2(8, 6), centre + new Vector2(2, 2));
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


    // =========================================================
    // Skin
    // =========================================================

    private static void Fill(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private Texture2D Tex(Color fill, Color? border = null, int size = 4)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                texture.SetPixel(x, y, border.HasValue && edge ? border.Value : fill);
            }
        texture.Apply();
        _owned.Add(texture);
        return texture;
    }

    private static void SetBackground(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active)
    {
        style.normal.background = normal; style.hover.background = hover; style.active.background = active;
        style.focused.background = normal;
        style.onNormal.background = active; style.onHover.background = active; style.onActive.background = active;
    }

    private void EnsureSkin()
    {
        if (_skin != null) return;
        _skin = Instantiate(GUI.skin);
        _owned.Add(_skin);
        var one = new RectOffset(1, 1, 1, 1);

        _skin.label = new GUIStyle(GUI.skin.label) { fontSize = 12, padding = new RectOffset(2, 2, 1, 1), margin = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        _skin.label.normal.textColor = TextColor;
        _skin.label.hover.textColor = TextColor;

        _skin.textField = new GUIStyle(GUI.skin.textField) { fontSize = 12, border = one, padding = new RectOffset(4, 4, 1, 1), margin = new RectOffset(2, 2, 1, 1), fixedHeight = RowHeight - 2 };
        Texture2D field = Tex(FieldColor, BorderColor);
        Texture2D fieldHover = Tex(FieldColor, new Color(0.3f, 0.3f, 0.3f, 1f));
        Texture2D fieldFocus = Tex(FieldColor, AccentColor);
        _skin.textField.normal.background = field; _skin.textField.hover.background = fieldHover;
        _skin.textField.focused.background = fieldFocus; _skin.textField.active.background = fieldFocus;
        _skin.textField.onNormal.background = field; _skin.textField.onFocused.background = fieldFocus;
        _skin.textField.normal.textColor = _skin.textField.hover.textColor = _skin.textField.focused.textColor = TextColor;

        Texture2D track = new Texture2D(1, 12, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
        for (int y = 0; y < 12; y++) track.SetPixel(0, y, y == 5 || y == 6 ? new Color(0.37f, 0.37f, 0.37f, 1f) : Color.clear);
        track.Apply(); _owned.Add(track);
        _skin.horizontalSlider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 12, border = new RectOffset(0, 0, 0, 0), margin = new RectOffset(2, 4, 3, 0), padding = new RectOffset(0, 0, 0, 0), overflow = new RectOffset(0, 0, 0, 0) };
        _skin.horizontalSlider.normal.background = track;
        _skin.horizontalSliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 8, fixedHeight = 12, border = new RectOffset(0, 0, 0, 0), overflow = new RectOffset(0, 0, 0, 0), padding = new RectOffset(0, 0, 0, 0) };
        SetBackground(_skin.horizontalSliderThumb, Tex(new Color(0.6f, 0.6f, 0.6f, 1f)), Tex(new Color(0.75f, 0.75f, 0.75f, 1f)), Tex(new Color(0.85f, 0.85f, 0.85f, 1f)));

        _skin.verticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar) { fixedWidth = 8, border = new RectOffset(0, 0, 0, 0), margin = new RectOffset(1, 1, 1, 1), padding = new RectOffset(0, 0, 0, 0) };
        _skin.verticalScrollbar.normal.background = Tex(new Color(0.19f, 0.19f, 0.19f, 1f));
        _skin.verticalScrollbarThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb) { fixedWidth = 8, border = new RectOffset(0, 0, 0, 0), padding = new RectOffset(0, 0, 0, 0) };
        SetBackground(_skin.verticalScrollbarThumb, Tex(new Color(0.37f, 0.37f, 0.37f, 1f)), Tex(new Color(0.45f, 0.45f, 0.45f, 1f)), Tex(new Color(0.5f, 0.5f, 0.5f, 1f)));
        _skin.verticalScrollbarUpButton = new GUIStyle { name = "verticalscrollbarupbutton", fixedHeight = 0, fixedWidth = 0 };
        _skin.verticalScrollbarDownButton = new GUIStyle { name = "verticalscrollbardownbutton", fixedHeight = 0, fixedWidth = 0 };
        _skin.scrollView = new GUIStyle { name = "scrollview" };

        _title = new GUIStyle(_skin.label) { fontStyle = FontStyle.Bold };
        _bold = new GUIStyle(_skin.label) { fontStyle = FontStyle.Bold, fixedHeight = RowHeight + 2, wordWrap = false };
        _dim = new GUIStyle(_skin.label);
        _dim.normal.textColor = _dim.hover.textColor = DimColor;
        _row = new GUIStyle(_skin.label) { wordWrap = false };
        _axis = new GUIStyle(_skin.label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0) };
        _axis.normal.textColor = DimColor;
        _foldout = new GUIStyle(_skin.label) { fontSize = 8, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0) };
        _foldout.normal.textColor = _foldout.hover.textColor = DimColor;

        _section = new GUIStyle(_skin.label) { fontStyle = FontStyle.Bold, padding = new RectOffset(8, 4, 0, 0) };
        _section.hover.textColor = Color.white;

        _toolbar = new GUIStyle(_skin.label) { alignment = TextAnchor.MiddleCenter, border = one, padding = new RectOffset(6, 6, 0, 0) };
        SetBackground(_toolbar, Tex(new Color(0.24f, 0.24f, 0.24f, 0.95f), BorderColor), Tex(new Color(0.30f, 0.30f, 0.30f, 0.95f), BorderColor), Tex(new Color(0.20f, 0.20f, 0.20f, 1f), BorderColor));
        _toolbar.hover.textColor = Color.white;
        _toolbarOn = new GUIStyle(_toolbar) { fontStyle = FontStyle.Bold };
        SetBackground(_toolbarOn, Tex(new Color(0.16f, 0.16f, 0.16f, 0.97f), AccentColor), Tex(new Color(0.20f, 0.20f, 0.20f, 0.97f), AccentColor), Tex(new Color(0.14f, 0.14f, 0.14f, 1f), AccentColor));
        _toolbarOn.normal.textColor = _toolbarOn.hover.textColor = Color.white;
    }
}
