using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Properties
 Summary: Inspector panel (transform, light, camera, volume, audio, material list) and
          Material panel (the material picked in the Inspector list: ball, shader + every
          property; empty until one is picked). Every edit is undoable (Ctrl+Z).
          - Maya materials: shader and values are saved per material name (library).
          - Scene materials: edited for this run only; Reset restores the original values.
 History: 2026/10/11 New (Material split out of the Inspector, ball preview)
          2026/10/11 Material list only in the Inspector (one row per material)
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private Vector2 _inspectorScroll;
    private Vector2 _materialScroll;
    private string _activeMaterialKey;
    private bool _shaderPickerOpen;
    private bool _previewDragging;
    private CS_MayaPreviewMaterialPreview _preview;
    private readonly Dictionary<Material, Material> _sceneOriginals = new Dictionary<Material, Material>();
    private readonly Dictionary<string, Texture2D> _loadedTextures = new Dictionary<string, Texture2D>();


    // =========================================================
    // Inspector
    // =========================================================

    private void DrawInspector()
    {
        IReadOnlyList<GameObject> selected = _selection.Selected;
        string note = selected.Count > 1 ? selected.Count + " objects" : null;
        Rect content = DrawWindow(_inspectorRect, "Inspector", note);
        if (selected.Count == 0)
        {
            GUI.Label(new Rect(content.x + 8, content.y + 4, content.width - 16, RowHeight), "Nothing selected", _dim);
            _inspectorContentHeight = 0f;
            return;
        }
        _labelWidth = Mathf.Clamp(content.width * 0.34f, 96f, 150f);
        GameObject target = selected[0];
        CS_MayaPreviewKind kind = _selection.GroupLabel != null ? CS_MayaPreviewKind.MayaGroup : CS_MayaPreviewIcons.KindOf(target.transform);

        GUILayout.BeginArea(content);
        // Header: icon, active toggle, name, type
        GUILayout.BeginHorizontal(GUILayout.Height(24));
        GUILayout.Space(6);
        Rect icon = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
        icon.y += 3;
        DrawTinted(icon, CS_MayaPreviewIcons.Get(kind), CS_MayaPreviewIcons.Tint(kind));
        GUILayout.Space(4);
        if (selected.Count == 1 && target != _view.gameObject)
        {
            Rect box = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
            box.y += 5;
            if (GUI.Button(box, GUIContent.none, GUIStyle.none))
            {
                CS_MayaPreviewUndo.RecordActive(target, target.activeSelf ? "Hide" : "Show");
                target.SetActive(!target.activeSelf);
                CS_MayaPreviewUndo.Close();
            }
            Fill(box, BorderColor);
            Fill(new Rect(box.x + 1, box.y + 1, 12, 12), FieldColor);
            if (target.activeSelf) Fill(new Rect(box.x + 3, box.y + 3, 8, 8), AccentColor);
            if (box.Contains(Event.current.mousePosition)) _tooltip = "Active";
            GUILayout.Space(4);
        }
        string title = _selection.GroupLabel ?? (selected.Count == 1 ? DisplayName(target) : DisplayName(target) + "  +" + (selected.Count - 1));
        Rect titleRect = GUILayoutUtility.GetRect(10, 22, GUILayout.ExpandWidth(true));
        GUI.Label(titleRect, Fit(title, _bold, titleRect.width - 90), _bold);
        GUI.Label(titleRect, CS_MayaPreviewIcons.Label(kind), _smallRight);
        GUILayout.Space(6);
        GUILayout.EndHorizontal();

        _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll, false, false, GUIStyle.none, _skin.verticalScrollbar, GUIStyle.none);
        _fieldScope = "Inspector.";
        DrawTransform();
        if (selected.Count == 1)
        {
            if (target.TryGetComponent(out Light light) && Section("Light  (" + light.type + ")", CS_MayaPreviewIcons.Get(kind), CS_MayaPreviewIcons.Tint(kind)))
                CS_MayaPreviewUndo.WatchLight(light, "Light", () => DrawLight(target, light));
            if (target.TryGetComponent(out Camera cameraComponent) && Section("Camera", CS_MayaPreviewIcons.Get(CS_MayaPreviewKind.Camera), CS_MayaPreviewIcons.Tint(CS_MayaPreviewKind.Camera)))
                CS_MayaPreviewUndo.WatchCamera(cameraComponent, "Camera", () => DrawCamera(cameraComponent));
            if (target.TryGetComponent(out Volume volume) && Section("Volume", CS_MayaPreviewIcons.Get(CS_MayaPreviewKind.Volume), CS_MayaPreviewIcons.Tint(CS_MayaPreviewKind.Volume)))
                CS_MayaPreviewUndo.WatchVolume(volume, "Volume", () => DrawVolume(volume));
            if (target.TryGetComponent(out AudioSource audio) && Section("Audio Source", CS_MayaPreviewIcons.Get(CS_MayaPreviewKind.Audio), CS_MayaPreviewIcons.Tint(CS_MayaPreviewKind.Audio)))
                CS_MayaPreviewUndo.WatchAudio(audio, "Audio", () => DrawAudio(audio));
        }
        DrawMaterialList();
        GUILayout.Space(4);
        if (Event.current.type == EventType.Repaint) _inspectorContentHeight = GUILayoutUtility.GetLastRect().yMax + 2f;
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static string DisplayName(GameObject o)
    {
        if (!CS_MayaPreviewExport.IsLive(o)) return o.name;
        return CS_MayaPreviewExport.LastSegment(CS_MayaPreviewExport.TransformPath(CS_MayaPreviewExport.ShapePath(o)));
    }

    private List<Transform> TransformTargets()
    {
        var list = new List<Transform>();
        foreach (GameObject o in _selection.Selected)
        {
            if (o == null) continue;
            bool nested = false;
            foreach (GameObject other in _selection.Selected)
                if (other != null && other != o && o.transform.IsChildOf(other.transform)) { nested = true; break; }
            if (!nested) list.Add(o.transform);
        }
        return list;
    }

    private void DrawTransform()
    {
        List<Transform> targets = TransformTargets();
        if (targets.Count == 0 || !Section("Transform")) return;
        Transform t = targets[0];
        if (CS_MayaPreviewExport.IsLive(t.gameObject))
            Note("Offset from the Maya position (Maya transforms are frozen into the mesh).");
        if (targets.Count > 1) Note(targets.Count + " objects: a changed value is set on all of them.");
        CS_MayaPreviewUndo.WatchTransforms(targets, "Transform", () =>
        {
            Vector3 position = VectorField("Position", t.localPosition);
            Vector3 rotation = VectorField("Rotation", t.localEulerAngles);
            Vector3 scale = VectorField("Scale", t.localScale);
            Vector3 oldPosition = t.localPosition, oldRotation = t.localEulerAngles, oldScale = t.localScale;
            foreach (Transform target in targets)
            {
                if (position != oldPosition) target.localPosition = Merge(target.localPosition, oldPosition, position);
                if (rotation != oldRotation) target.localEulerAngles = Merge(target.localEulerAngles, oldRotation, rotation);
                if (scale != oldScale) target.localScale = Merge(target.localScale, oldScale, scale);
                if (position != oldPosition || rotation != oldRotation)
                {
                    var controller = target.GetComponent<CS_MayaPreviewCameraController>();
                    if (controller != null) controller.SynchronizeView();
                }
            }
        });
    }

    // Only the components that were edited change on every object.
    private static Vector3 Merge(Vector3 current, Vector3 before, Vector3 after)
    {
        for (int i = 0; i < 3; i++) if (before[i] != after[i]) current[i] = after[i];
        return current;
    }

    private void DrawLight(GameObject target, Light light)
    {
        light.enabled = Toggle("Enabled", light.enabled);
        light.intensity = Mathf.Max(0, Number("Intensity", light.intensity));
        light.color = ColorField("Light color", light.color, false, "lightColor");
        light.useColorTemperature = Toggle("Use temperature", light.useColorTemperature);
        if (light.useColorTemperature)
            light.colorTemperature = Mathf.Clamp(Number("Kelvin", light.colorTemperature), 1000, 20000);
        light.bounceIntensity = Mathf.Max(0, Number("Bounce light", light.bounceIntensity));
        if (light.type != LightType.Directional)
            light.range = Mathf.Max(0.01f, Number("Range", light.range));
        if (light.type == LightType.Spot)
        {
            light.spotAngle = Mathf.Clamp(Number("Spot angle", light.spotAngle), 1, 179);
            light.innerSpotAngle = Mathf.Clamp(Number("Inner angle", light.innerSpotAngle), 0, light.spotAngle);
        }
        bool shadows = Toggle("Cast shadows", light.shadows != LightShadows.None);
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        var hd = target.GetComponent<HDAdditionalLightData>();
        if (hd == null) return;
        SubHeader("HDRP");
        hd.lightDimmer = Mathf.Clamp01(Number("Light multiplier", hd.lightDimmer));
        hd.affectDiffuse = Toggle("Affect diffuse", hd.affectDiffuse);
        hd.affectSpecular = Toggle("Affect reflections", hd.affectSpecular);
        hd.volumetricDimmer = Mathf.Clamp01(Number("Fog lighting", hd.volumetricDimmer));
        if (light.type == LightType.Directional)
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

    private void DrawCamera(Camera camera)
    {
        if (camera.orthographic)
            camera.orthographicSize = Mathf.Max(0.001f, Number("Ortho size", camera.orthographicSize));
        else camera.fieldOfView = Mathf.Clamp(Number("Field of view", camera.fieldOfView), 1, 179);
        camera.nearClipPlane = Mathf.Max(0.0001f, Number("Near clip", camera.nearClipPlane));
        camera.farClipPlane = Mathf.Max(camera.nearClipPlane + 0.01f, Number("Far clip", camera.farClipPlane));
    }

    private void DrawVolume(Volume volume)
    {
        volume.enabled = Toggle("Enabled", volume.enabled);
        BeginRow("Mode"); GUILayout.Label(volume.isGlobal ? "Global" : "Local", _dim); EndRow();
        BeginRow("Profile"); GUILayout.Label(volume.sharedProfile != null ? volume.sharedProfile.name : "None", _dim); EndRow();
        volume.weight = Slider("Weight", volume.weight, 0, 1);
        volume.priority = Number("Priority", volume.priority);
        if (!volume.isGlobal) volume.blendDistance = Mathf.Max(0, Number("Blend distance", volume.blendDistance));
        if (volume.sharedProfile != null && volume.sharedProfile.components.Count > 0)
        {
            var names = new List<string>();
            foreach (var component in volume.sharedProfile.components) if (component != null) names.Add(component.GetType().Name);
            Note("Overrides: " + string.Join(", ", names));
        }
    }

    private void DrawAudio(AudioSource source)
    {
        BeginRow("Clip"); GUILayout.Label(source.clip != null ? source.clip.name : "None", _dim); EndRow();
        source.mute = Toggle("Mute", source.mute);
        source.volume = Slider("Volume", source.volume, 0, 1);
        source.pitch = Slider("Pitch", source.pitch, -3, 3);
        source.spatialBlend = Slider("Spatial blend", source.spatialBlend, 0, 1);
    }

    // Materials used by the selection, one row each: name, shader, Edit.
    // Edit (or a click on the row) shows that material in the Material panel.
    private void DrawMaterialList()
    {
        List<MaterialSlot> slots = CollectMaterials();
        if (slots.Count == 0) return;
        if (!Section("Materials  (" + slots.Count + ")")) return;
        float nameWidth = 60f;
        foreach (MaterialSlot slot in slots) nameWidth = Mathf.Max(nameWidth, _row.CalcSize(new GUIContent(slot.name)).x);
        Event e = Event.current;
        foreach (MaterialSlot slot in slots)
        {
            bool active = slot.key == _activeMaterialKey && _materialOpen;
            Rect row = GUILayoutUtility.GetRect(10, RowHeight + 2, GUILayout.ExpandWidth(true));
            Rect edit = new Rect(row.xMax - 52, row.y + 1, 44, row.height - 2);
            if (active) Fill(row, SelectColor);
            else if (row.Contains(e.mousePosition)) Fill(row, HoverColor);

            float x = row.x + 10f;
            DrawTinted(new Rect(x, row.y + 4, 11, 11), CS_MayaPreviewIcons.Disc,
                slot.maya ? CS_MayaPreviewIcons.Tint(CS_MayaPreviewKind.MayaMesh) : (active ? Color.white : DimColor));
            x += 17f;
            float space = Mathf.Max(20f, edit.x - 8f - x);
            float nameColumn = Mathf.Min(nameWidth + 14f, space * 0.62f);
            string shader = slot.material.shader != null ? slot.material.shader.name : "";
            GUI.Label(new Rect(x, row.y, nameColumn, row.height), Fit(slot.name, _row, nameColumn - 6f), _row);
            GUI.Label(new Rect(x + nameColumn, row.y, space - nameColumn, row.height),
                Fit(ShortShader(shader), active ? _row : _dim, space - nameColumn - 4f), active ? _row : _dim);

            if (GUI.Button(edit, "Edit", active ? _buttonOn : _button)) EditMaterial(slot);
            else if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
            {
                EditMaterial(slot);
                e.Use();
            }
            if (row.Contains(e.mousePosition)) _tooltip = slot.name + "  (" + shader + ")";
            GUILayout.Space(1);
        }
    }

    private void EditMaterial(MaterialSlot slot)
    {
        if (slot.key != _activeMaterialKey) { _shaderPickerOpen = false; _materialScroll = Vector2.zero; }
        _activeMaterialKey = slot.key;
        _materialOpen = true;
        GUI.FocusControl(null);
        _typing = false;
    }

    // Keeps the picked material only while the selection still uses it.
    private void ValidateActiveMaterial()
    {
        if (_activeMaterialKey == null) return;
        if (_selection.Count > 0 && CollectMaterials().Exists(slot => slot.key == _activeMaterialKey)) return;
        _activeMaterialKey = null;
        _shaderPickerOpen = false;
    }


    // =========================================================
    // Material panel
    // =========================================================

    private class MaterialSlot
    {
        public string key;          // Maya material name, or "#<id>" for a scene material
        public string name;
        public Material material;
        public bool maya;
        public int users;
    }

    private List<MaterialSlot> CollectMaterials()
    {
        var slots = new List<MaterialSlot>();
        var library = CS_MayaPreviewMaterialLibrary.Current;
        foreach (Renderer renderer in _selection.Renderers())
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                MaterialSlot slot = slots.Find(s => s.material == material);
                if (slot == null)
                {
                    slot = new MaterialSlot { material = material };
                    if (library != null && library.Owns(material, out string mayaName))
                    { slot.maya = true; slot.key = mayaName; slot.name = mayaName; }
                    else { slot.key = "#" + material.GetInstanceID(); slot.name = material.name.Replace(" (Instance)", ""); }
                    slots.Add(slot);
                }
                slot.users++;
            }
        }
        return slots;
    }

    private static string ShortShader(string shader)
    {
        if (shader.Length <= 22) return shader;
        int slash = shader.LastIndexOf('/');
        return slash >= 0 ? shader.Substring(slash + 1) : shader;
    }

    private void DrawMaterialPanel()
    {
        // Empty until a material is picked in the Inspector's material list.
        MaterialSlot active = null;
        if (_activeMaterialKey != null && _selection.Count > 0)
            active = CollectMaterials().Find(slot => slot.key == _activeMaterialKey);
        if (active == null) { _activeMaterialKey = null; _shaderPickerOpen = false; }
        Rect content = DrawWindow(_materialRect, "Material", active != null ? ShortShader(active.material.shader.name) : null);
        if (active == null)
        {
            GUI.Label(new Rect(content.x + 8, content.y + 4, content.width - 16, RowHeight), "Inspector > Materials > Edit", _dim);
            return;
        }
        _labelWidth = Mathf.Clamp(content.width * 0.40f, 110f, 175f);
        var library = CS_MayaPreviewMaterialLibrary.Current;

        GUILayout.BeginArea(new Rect(content.x + 4, content.y + 4, content.width - 8, content.height - 6));

        // Ball + name / shader
        GUILayout.BeginHorizontal();
        const float ball = 120f;
        GUILayout.BeginVertical(GUILayout.Width(ball));
        Rect previewRect = GUILayoutUtility.GetRect(ball, ball, GUILayout.Width(ball), GUILayout.Height(ball));
        DrawBall(previewRect, active.material);
        GUILayout.BeginHorizontal();
        bool cube = _preview != null && _preview.Cube;
        if (GUILayout.Button("Sphere", !cube ? _buttonOn : _button, GUILayout.Height(16)) && _preview != null) _preview.Cube = false;
        if (GUILayout.Button("Cube", cube ? _buttonOn : _button, GUILayout.Height(16)) && _preview != null) _preview.Cube = true;
        GUILayout.EndHorizontal();
        if (_preview != null)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(16));
            GUILayout.Label("Light", _small, GUILayout.Width(32));
            _preview.KeyLight = GUILayout.HorizontalSlider(_preview.KeyLight, 0f, 4f, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
        GUILayout.Space(8);
        GUILayout.BeginVertical();
        Rect nameRect = GUILayoutUtility.GetRect(10, 22, GUILayout.ExpandWidth(true));
        GUI.Label(nameRect, Fit(active.name, _bold, nameRect.width), _bold);
        GUILayout.Label(active.maya ? "Maya material  - saved per name" : "Scene material  - this run only", _dim);
        GUILayout.Label("Used by " + active.users + (active.users == 1 ? " slot" : " slots") + " in the selection", _dim);
        GUILayout.Space(4);
        if (active.maya && library != null)
        {
            string shader = active.material.shader.name;
            if (GUILayout.Button(new GUIContent((_shaderPickerOpen ? "▼  " : "►  ") + ShortShader(shader), shader), _button, GUILayout.Height(RowHeight + 2)))
                _shaderPickerOpen = !_shaderPickerOpen;
            if (GUILayout.Button("Reset (HDRP/Lit + Maya)", _button, GUILayout.Height(RowHeight + 2)))
            {
                CS_MayaPreviewUndo.RecordMayaMaterial(library, active.key, "Reset material", false);
                ClearFields();
                library.ResetMaterial(active.key);
                CS_MayaPreviewUndo.Close();
                _shaderPickerOpen = false;
            }
        }
        else
        {
            GUILayout.Label(new GUIContent("Shader: " + ShortShader(active.material.shader.name), active.material.shader.name), _row);
            if (GUILayout.Button("Reset to original values", _button, GUILayout.Height(RowHeight + 2))
                && _sceneOriginals.TryGetValue(active.material, out Material original) && original != null)
            {
                CS_MayaPreviewUndo.WatchMaterialInstance(active.material, "Reset material", () => active.material.CopyPropertiesFromMaterial(original));
                ClearFields();
            }
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        string tip = GUI.tooltip;
        if (!string.IsNullOrEmpty(tip)) _tooltip = tip;

        if (_shaderPickerOpen && active.maya && library != null)
        {
            GUILayout.Space(4);
            DrawShaderPicker(library, active);
            GUILayout.EndArea();
            return;   // the material is replaced when a shader is picked; values next frame
        }

        GUILayout.Space(6);
        Fill(GUILayoutUtility.GetRect(10, 1, GUILayout.ExpandWidth(true)), BorderColor);
        _materialScroll = GUILayout.BeginScrollView(_materialScroll, false, false, GUIStyle.none, _skin.verticalScrollbar, GUIStyle.none);
        _fieldScope = "Material." + active.key + ".";
        if (active.maya && library != null)
            CS_MayaPreviewUndo.WatchMayaMaterial(library, active.key, "Material " + active.name, () => DrawProperties(active, library));
        else
            CS_MayaPreviewUndo.WatchMaterialInstance(active.material, "Material " + active.name, () => DrawProperties(active, null));
        GUILayout.Space(8);
        if (library != null && library.Folder != null && active.maya) Note("Saved in: " + library.Folder);
        GUILayout.Space(6);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawBall(Rect rect, Material material)
    {
        if (_preview == null) _preview = CS_MayaPreviewMaterialPreview.Create();
        Fill(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), BorderColor);
        Fill(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
        Texture picture = _preview.Render(material);
        if (Event.current.type == EventType.Repaint && picture != null)
            GUI.DrawTexture(rect, picture, ScaleMode.ScaleToFit, false);
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition)) { _previewDragging = true; e.Use(); }
        else if (e.type == EventType.MouseDrag && _previewDragging) { _preview.Orbit(e.delta); e.Use(); }
        else if (e.type == EventType.MouseUp && _previewDragging) { _previewDragging = false; e.Use(); }
        if (rect.Contains(e.mousePosition) && !_previewDragging) _tooltip = "Drag to turn";
    }

    private void DrawShaderPicker(CS_MayaPreviewMaterialLibrary library, MaterialSlot active)
    {
        string current = active.material.shader.name;
        foreach (string name in library.ShaderNames)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            if (GUILayout.Button(new GUIContent(name), name == current ? _buttonOn : _button, GUILayout.Height(RowHeight + 2)))
            {
                _shaderPickerOpen = false;
                if (name != current)
                {
                    CS_MayaPreviewUndo.RecordMayaMaterial(library, active.key, "Shader " + ShortShader(name), false);
                    ClearFields();
                    library.SetShader(active.key, name);
                    CS_MayaPreviewUndo.Close();
                }
            }
            GUILayout.Space(4);
            GUILayout.EndHorizontal();
        }
    }

    // Every visible property of the shader, one line each.
    private void DrawProperties(MaterialSlot slot, CS_MayaPreviewMaterialLibrary library)
    {
        Material material = slot.material;
        Shader shader = material.shader;
        bool maya = library != null;
        if (!maya && !_sceneOriginals.ContainsKey(material))
            _sceneOriginals[material] = new Material(material) { hideFlags = HideFlags.DontSave };

        int count = shader.GetPropertyCount();
        for (int p = 0; p < count; p++)
        {
            ShaderPropertyFlags flags = shader.GetPropertyFlags(p);
            if ((flags & (ShaderPropertyFlags.HideInInspector | ShaderPropertyFlags.PerRendererData)) != 0) continue;
            string prop = shader.GetPropertyName(p);
            string label = shader.GetPropertyDescription(p);
            if (string.IsNullOrEmpty(label)) label = prop;
            if (maya && library.IsOverridden(slot.key, prop)) label = "* " + label;

            switch (shader.GetPropertyType(p))
            {
                case ShaderPropertyType.Range:
                {
                    Vector2 limits = shader.GetPropertyRangeLimits(p);
                    float value = material.GetFloat(prop);
                    float edited = Slider(label, value, limits.x, limits.y, prop, prop);
                    if (edited != value) SetFloat(slot, library, prop, edited);
                    break;
                }
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Int:
                {
                    bool isInt = shader.GetPropertyType(p) == ShaderPropertyType.Int;
                    float value = isInt ? material.GetInteger(prop) : material.GetFloat(prop);
                    if (IsToggle(shader, p))
                    {
                        bool on = value > 0.5f;
                        if (Toggle(label, on, prop) != on) SetFloat(slot, library, prop, on ? 0f : 1f);
                    }
                    else
                    {
                        float edited = Number(label, value, prop, prop);
                        if (edited != value) SetFloat(slot, library, prop, edited);
                    }
                    break;
                }
                case ShaderPropertyType.Color:
                {
                    Color value = material.GetColor(prop);
                    Color edited = ColorField(label, value, (flags & ShaderPropertyFlags.HDR) != 0, prop, prop);
                    if (edited != value)
                    {
                        if (maya) library.SetColor(slot.key, prop, edited);
                        else { material.SetColor(prop, edited); Validate(material); }
                    }
                    break;
                }
                case ShaderPropertyType.Vector:
                {
                    Vector4 value = material.GetVector(prop);
                    Vector4 edited = Vector4Field(label, value, prop, prop);
                    if (edited != value)
                    {
                        if (maya) library.SetVector(slot.key, prop, edited);
                        else { material.SetVector(prop, edited); Validate(material); }
                    }
                    break;
                }
                case ShaderPropertyType.Texture:
                {
                    if (shader.GetPropertyTextureDimension(p) != TextureDimension.Tex2D) break;
                    Texture texture = material.GetTexture(prop);
                    string current = maya ? library.TexturePath(slot.key, prop) : "";
                    string edited = PathField(label, current, texture, prop, prop);
                    if (edited == null) break;
                    if (maya) library.SetTexture(slot.key, prop, edited);
                    else
                    {
                        Texture2D loaded = edited.Length > 0 ? LoadTextureFile(edited, (flags & ShaderPropertyFlags.Normal) != 0) : null;
                        if (edited.Length == 0 || loaded != null) { material.SetTexture(prop, loaded); Validate(material); }
                    }
                    break;
                }
            }
        }
    }

    private static void SetFloat(MaterialSlot slot, CS_MayaPreviewMaterialLibrary library, string prop, float value)
    {
        if (library != null) { library.SetFloat(slot.key, prop, value); return; }
        Material material = slot.material;
        int index = material.shader.FindPropertyIndex(prop);
        if (index >= 0 && material.shader.GetPropertyType(index) == ShaderPropertyType.Int) material.SetInteger(prop, Mathf.RoundToInt(value));
        else material.SetFloat(prop, value);
        Validate(material);
    }

    private static void Validate(Material material)
    {
        try { HDMaterial.ValidateMaterial(material); }
        catch (Exception) { /* not an HDRP shader */ }
    }

    private static bool IsToggle(Shader shader, int index)
    {
        foreach (string attribute in shader.GetPropertyAttributes(index))
            if (attribute.StartsWith("Toggle") || attribute.StartsWith("MaterialToggle")) return true;
        return false;
    }

    private Texture2D LoadTextureFile(string path, bool linear)
    {
        string key = path + (linear ? "|linear" : "");
        if (_loadedTextures.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
        try
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { name = Path.GetFileName(path) };
            if (!texture.LoadImage(File.ReadAllBytes(path), false)) { Destroy(texture); return null; }
            _loadedTextures[key] = texture;
            _owned.Add(texture);
            return texture;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Maya Preview : " + path + "\n" + exception.Message);
            return null;
        }
    }
}
