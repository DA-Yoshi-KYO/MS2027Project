using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
+=====================================
 File   : CS_MayaPreviewUndo
 Summary: Undo / redo (Ctrl+Z / Ctrl+Y) for edits made in the preview player.
          - Call a Record... helper BEFORE changing something. It stores the state before
            the change; the state after is taken when the edit ends.
          - Repeated edits of the same thing (slider drag, gizmo drag, typing) merge into
            one step while the mouse is held or within MergeSeconds.
 History: 2026/10/11 New
=====================================+
*/
public static class CS_MayaPreviewUndo
{
    private class Entry
    {
        public string key;
        public string label;
        public Func<string> capture;
        public Action<string> apply;
        public string before;
        public string after;
        public float time;
    }

    private const float MergeSeconds = 0.6f;
    private const int Limit = 200;
    private static readonly List<Entry> s_undo = new List<Entry>();
    private static readonly List<Entry> s_redo = new List<Entry>();
    private static Entry s_open;

    // Raised after an undo / redo was applied (text fields must re-read their values).
    public static event Action Applied;

    public static bool CanUndo => s_open != null || s_undo.Count > 0;
    public static bool CanRedo => s_redo.Count > 0;
    public static string NextUndoLabel => s_open != null ? s_open.label : s_undo.Count > 0 ? s_undo[s_undo.Count - 1].label : "";
    public static string NextRedoLabel => s_redo.Count > 0 ? s_redo[s_redo.Count - 1].label : "";


    // =========================================================
    // Core
    // =========================================================

    public static void Record(string key, string label, Func<string> capture, Action<string> apply, bool merge = true)
    {
        float now = Time.unscaledTime;
        if (merge && s_open != null && s_open.key == key && now - s_open.time < MergeSeconds)
        {
            s_open.time = now;
            return;
        }
        Close();
        string before;
        try { before = capture(); }
        catch (Exception exception) { Debug.LogWarning("Maya Preview Undo : " + exception.Message); return; }
        s_open = new Entry { key = key, label = label, capture = capture, apply = apply, before = before, time = now };
        s_redo.Clear();
    }

    // Keeps the open step alive (call every frame while a drag is going on).
    public static void Touch(string key)
    {
        if (s_open != null && s_open.key == key) s_open.time = Time.unscaledTime;
    }

    // For edits applied while drawing a GUI: run draw(), and if the state changed, store one step.
    public static void Watch(string key, string label, Func<string> capture, Action<string> apply, Action draw)
    {
        string before;
        try { before = capture(); }
        catch { draw(); return; }
        draw();
        string after;
        try { after = capture(); }
        catch { return; }
        if (after == before) return;
        float now = Time.unscaledTime;
        if (s_open != null && s_open.key == key && now - s_open.time < MergeSeconds)
        {
            s_open.time = now;
            return;
        }
        Close();
        s_open = new Entry { key = key, label = label, capture = capture, apply = apply, before = before, time = now };
        s_redo.Clear();
    }

    public static void Close()
    {
        if (s_open == null) return;
        Entry entry = s_open;
        s_open = null;
        try { entry.after = entry.capture(); }
        catch { return; }
        if (entry.after == entry.before) return;
        s_undo.Add(entry);
        if (s_undo.Count > Limit) s_undo.RemoveAt(0);
    }

    // Call once per frame.
    public static void Tick(bool mouseHeld)
    {
        if (s_open != null && !mouseHeld && Time.unscaledTime - s_open.time > MergeSeconds) Close();
    }

    public static bool Undo()
    {
        Close();
        if (s_undo.Count == 0) return false;
        Entry entry = s_undo[s_undo.Count - 1];
        s_undo.RemoveAt(s_undo.Count - 1);
        Apply(entry, entry.before);
        s_redo.Add(entry);
        return true;
    }

    public static bool Redo()
    {
        Close();
        if (s_redo.Count == 0) return false;
        Entry entry = s_redo[s_redo.Count - 1];
        s_redo.RemoveAt(s_redo.Count - 1);
        Apply(entry, entry.after);
        s_undo.Add(entry);
        return true;
    }

    public static void Clear()
    {
        s_open = null;
        s_undo.Clear();
        s_redo.Clear();
    }

    private static void Apply(Entry entry, string state)
    {
        try { entry.apply(state); }
        catch (Exception exception) { Debug.LogWarning("Maya Preview Undo : " + entry.label + "\n" + exception.Message); }
        Applied?.Invoke();
    }


    // =========================================================
    // Transforms
    // =========================================================

    public static void RecordTransforms(IList<Transform> targets, string label, bool merge = true)
    {
        var list = new List<Transform>();
        foreach (Transform t in targets) if (t != null) list.Add(t);
        if (list.Count == 0) return;
        var key = new StringBuilder("T");
        foreach (Transform t in list) key.Append(':').Append(t.GetInstanceID());
        Record(key.ToString(), label, () => CaptureTransforms(list), state => ApplyTransforms(list, state), merge);
    }

    public static void WatchTransforms(IList<Transform> targets, string label, Action draw)
    {
        var list = new List<Transform>();
        foreach (Transform t in targets) if (t != null) list.Add(t);
        var key = new StringBuilder("T");
        foreach (Transform t in list) key.Append(':').Append(t.GetInstanceID());
        Watch(key.ToString(), label, () => CaptureTransforms(list), state => ApplyTransforms(list, state), draw);
    }

    private static string CaptureTransforms(List<Transform> list)
    {
        var text = new StringBuilder();
        foreach (Transform t in list)
        {
            if (t == null) { text.Append('-').Append(';'); continue; }
            Vector3 p = t.localPosition, s = t.localScale;
            Quaternion r = t.localRotation;
            AppendFloats(text, p.x, p.y, p.z, r.x, r.y, r.z, r.w, s.x, s.y, s.z);
            text.Append(';');
        }
        return text.ToString();
    }

    private static void ApplyTransforms(List<Transform> list, string state)
    {
        string[] items = state.Split(';');
        for (int i = 0; i < list.Count && i < items.Length; i++)
        {
            Transform t = list[i];
            float[] v = ParseFloats(items[i]);
            if (t == null || v.Length < 10) continue;
            t.localPosition = new Vector3(v[0], v[1], v[2]);
            t.localRotation = new Quaternion(v[3], v[4], v[5], v[6]);
            t.localScale = new Vector3(v[7], v[8], v[9]);
            var controller = t.GetComponent<CS_MayaPreviewCameraController>();
            if (controller != null) controller.SynchronizeView();
        }
    }


    // =========================================================
    // Maya materials (CS_MayaPreviewMaterialLibrary)
    // =========================================================

    public static void RecordMayaMaterial(CS_MayaPreviewMaterialLibrary library, string materialName, string label, bool merge = true)
    {
        if (library == null || string.IsNullOrEmpty(materialName)) return;
        Record("M:" + materialName + (merge ? "" : ":" + Time.frameCount), label,
            () => library.Snapshot(materialName),
            state => library.Restore(materialName, state), merge);
    }


    public static void WatchMayaMaterial(CS_MayaPreviewMaterialLibrary library, string materialName, string label, Action draw)
    {
        if (library == null || string.IsNullOrEmpty(materialName)) { draw(); return; }
        Watch("M:" + materialName, label, () => library.Snapshot(materialName), state => library.Restore(materialName, state), draw);
    }


    // =========================================================
    // Scene material instances (runtime copies)
    // =========================================================

    private static readonly Dictionary<string, Material> s_materialStates = new Dictionary<string, Material>();

    public static void RecordMaterialInstance(Material material, string label)
    {
        if (material == null) return;
        Record("I:" + material.GetInstanceID(), label, () => CaptureMaterial(material), state =>
        {
            if (material != null && s_materialStates.TryGetValue(state, out Material copy) && copy != null)
                material.CopyPropertiesFromMaterial(copy);
        });
    }

    public static void WatchMaterialInstance(Material material, string label, Action draw)
    {
        if (material == null) { draw(); return; }
        Watch("I:" + material.GetInstanceID(), label, () => CaptureMaterial(material), state =>
        {
            if (material != null && s_materialStates.TryGetValue(state, out Material copy) && copy != null)
                material.CopyPropertiesFromMaterial(copy);
        }, draw);
    }

    private static string CaptureMaterial(Material material)
    {
        var text = new StringBuilder();
        text.Append(material.GetInstanceID()).Append('|');
        Shader shader = material.shader;
        int count = shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
            string prop = shader.GetPropertyName(i);
            switch (shader.GetPropertyType(i))
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range: AppendFloats(text, material.GetFloat(prop)); break;
                case ShaderPropertyType.Int: text.Append(material.GetInteger(prop)).Append(','); break;
                case ShaderPropertyType.Color: Color c = material.GetColor(prop); AppendFloats(text, c.r, c.g, c.b, c.a); break;
                case ShaderPropertyType.Vector: Vector4 v = material.GetVector(prop); AppendFloats(text, v.x, v.y, v.z, v.w); break;
                case ShaderPropertyType.Texture:
                    Texture texture = material.GetTexture(prop);
                    text.Append(texture != null ? texture.GetInstanceID() : 0).Append(',');
                    break;
            }
        }
        string state = text.ToString();
        if (!s_materialStates.ContainsKey(state))
        {
            if (s_materialStates.Count > 400) s_materialStates.Clear();
            s_materialStates[state] = new Material(material) { hideFlags = HideFlags.DontSave };
        }
        return state;
    }


    // =========================================================
    // Lights / cameras / volumes
    // =========================================================

    [Serializable]
    private class LightState
    {
        public bool enabled, useTemperature;
        public float intensity, temperature, bounce, range, spot, innerSpot;
        public Color color;
        public int shadows;
        public float dimmer, volumetric, angular, shapeRadius, shadowDimmer, normalBias, slopeBias, shadowNear;
        public bool affectDiffuse, affectSpecular;
    }

    public static void WatchLight(Light light, string label, Action draw)
    {
        if (light == null) { draw(); return; }
        Watch("L:" + light.GetInstanceID(), label, () =>
        {
            var hd = light.GetComponent<HDAdditionalLightData>();
            var s = new LightState
            {
                enabled = light.enabled, intensity = light.intensity, color = light.color,
                useTemperature = light.useColorTemperature, temperature = light.colorTemperature,
                bounce = light.bounceIntensity, range = light.range, spot = light.spotAngle,
                innerSpot = light.innerSpotAngle, shadows = (int)light.shadows
            };
            if (hd != null)
            {
                s.dimmer = hd.lightDimmer; s.volumetric = hd.volumetricDimmer; s.angular = hd.angularDiameter;
                s.shapeRadius = hd.shapeRadius; s.shadowDimmer = hd.shadowDimmer; s.normalBias = hd.normalBias;
                s.slopeBias = hd.slopeBias; s.shadowNear = hd.shadowNearPlane;
                s.affectDiffuse = hd.affectDiffuse; s.affectSpecular = hd.affectSpecular;
            }
            return JsonUtility.ToJson(s);
        }, state =>
        {
            if (light == null) return;
            var s = JsonUtility.FromJson<LightState>(state);
            light.enabled = s.enabled; light.intensity = s.intensity; light.color = s.color;
            light.useColorTemperature = s.useTemperature; light.colorTemperature = s.temperature;
            light.bounceIntensity = s.bounce; light.range = s.range; light.spotAngle = s.spot;
            light.innerSpotAngle = s.innerSpot; light.shadows = (LightShadows)s.shadows;
            var hd = light.GetComponent<HDAdditionalLightData>();
            if (hd == null) return;
            hd.lightDimmer = s.dimmer; hd.volumetricDimmer = s.volumetric; hd.angularDiameter = s.angular;
            hd.shapeRadius = s.shapeRadius; hd.shadowDimmer = s.shadowDimmer; hd.normalBias = s.normalBias;
            hd.slopeBias = s.slopeBias; hd.shadowNearPlane = s.shadowNear;
            hd.affectDiffuse = s.affectDiffuse; hd.affectSpecular = s.affectSpecular;
        }, draw);
    }

    public static void WatchCamera(Camera camera, string label, Action draw)
    {
        if (camera == null) { draw(); return; }
        Watch("C:" + camera.GetInstanceID(), label, () =>
        {
            var text = new StringBuilder();
            AppendFloats(text, camera.fieldOfView, camera.orthographicSize, camera.nearClipPlane, camera.farClipPlane);
            return text.ToString();
        }, state =>
        {
            float[] v = ParseFloats(state);
            if (camera == null || v.Length < 4) return;
            camera.fieldOfView = v[0]; camera.orthographicSize = v[1];
            camera.nearClipPlane = v[2]; camera.farClipPlane = v[3];
        }, draw);
    }

    public static void WatchVolume(Volume volume, string label, Action draw)
    {
        if (volume == null) { draw(); return; }
        Watch("V:" + volume.GetInstanceID(), label, () =>
        {
            var text = new StringBuilder();
            text.Append(volume.enabled ? 1 : 0).Append(',');
            AppendFloats(text, volume.weight, volume.priority, volume.blendDistance);
            return text.ToString();
        }, state =>
        {
            float[] v = ParseFloats(state);
            if (volume == null || v.Length < 4) return;
            volume.enabled = v[0] > 0.5f; volume.weight = v[1]; volume.priority = v[2]; volume.blendDistance = v[3];
        }, draw);
    }

    public static void WatchAudio(AudioSource source, string label, Action draw)
    {
        if (source == null) { draw(); return; }
        Watch("A:" + source.GetInstanceID(), label, () =>
        {
            var text = new StringBuilder();
            text.Append(source.mute ? 1 : 0).Append(',');
            AppendFloats(text, source.volume, source.pitch, source.spatialBlend);
            return text.ToString();
        }, state =>
        {
            float[] v = ParseFloats(state);
            if (source == null || v.Length < 4) return;
            source.mute = v[0] > 0.5f; source.volume = v[1]; source.pitch = v[2]; source.spatialBlend = v[3];
        }, draw);
    }

    public static void RecordActive(GameObject target, string label)
    {
        if (target == null) return;
        Record("G:" + target.GetInstanceID(), label, () => target.activeSelf ? "1" : "0",
            state => { if (target != null) target.SetActive(state == "1"); }, false);
    }


    // =========================================================
    // Text helpers
    // =========================================================

    private static void AppendFloats(StringBuilder text, params float[] values)
    {
        foreach (float value in values) text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(',');
    }

    private static float[] ParseFloats(string text)
    {
        string[] parts = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var values = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]);
        return values;
    }
}
