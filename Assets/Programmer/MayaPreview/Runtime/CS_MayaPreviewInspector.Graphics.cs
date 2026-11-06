using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Graphics
 Summary: Graphics panel (right column, below Material).
          Lowers the GPU load of the preview camera only:
            - Max FPS      : 60 / 30 / 15 while operating (IdleThrottle)
            - Render scale : 100 / 75 / 50 % (HDRP dynamic resolution, then upscaled to the window)
            - Quality      : Unity quality level (switches the HDRP asset)
            - Light / Full : heavy features (ray tracing, clouds, volumetric fog, SSR, SSAO, SSGI,
                             planar reflection, contact shadows, DoF, motion blur) off + lowest
                             quality + half-size textures + 50 % + max 30 fps /
                             everything back as the project has it
            - Textures     : full / half / quarter size (memory)
            - Features     : each HDRP feature on / off (camera Custom Frame Settings)
          First run (and once after Light got lighter) applies Light; "Full" restores the look.
          The scene, Volume profiles and HDRP asset files are not modified. Render scale changes the
          HDRP asset only in memory and only in the built exe (never in the Editor).
          Shown state = what the camera really renders with. Remembered between runs.
 History: 2026/11/05 New
          2026/11/05 Drop-down under the button, render scale, max fps, light by default
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private const string GraphicsPrefsKey = "MayaPreview.Graphics";
    private const string RenderScalePrefsKey = "MayaPreview.RenderScale";
    private const string ActiveFpsPrefsKey = "MayaPreview.ActiveFps";
    // Raised when "Light" gets lighter: the new light setup is applied once on the next start.
    private const string GraphicsVersionPrefsKey = "MayaPreview.GraphicsVersion";
    private const int GraphicsVersion = 3;

    private static readonly float[] RenderScales = { 1f, 0.75f, 0.5f };
    private static readonly int[] FpsCaps = { 60, 30, 15 };
    private static readonly string[] TextureSizes = { "Full", "1/2", "1/4" };

    private struct GraphicsFeature
    {
        public FrameSettingsField field;
        public string label;
        public bool heavy;   // turned off by "Light"
    }

    private static readonly GraphicsFeature[] GraphicsFeatures =
    {
        // The scene's SSR is set to ray tracing and the HDRP assets support ray tracing: by far the most expensive.
        new GraphicsFeature { field = FrameSettingsField.RayTracing, label = "Ray tracing", heavy = true },
        new GraphicsFeature { field = FrameSettingsField.VolumetricClouds, label = "Volumetric clouds", heavy = true },
        new GraphicsFeature { field = FrameSettingsField.Volumetrics, label = "Volumetric fog", heavy = true },
        new GraphicsFeature { field = FrameSettingsField.AtmosphericScattering, label = "Fog" },
        new GraphicsFeature { field = FrameSettingsField.SSR, label = "Reflection (SSR)", heavy = true },
        new GraphicsFeature { field = FrameSettingsField.SSAO, label = "Ambient occlusion", heavy = true },
        new GraphicsFeature { field = FrameSettingsField.SSGI, label = "Global illumination", heavy = true },
        new GraphicsFeature { field = FrameSettingsField.ShadowMaps, label = "Shadows" },
        new GraphicsFeature { field = FrameSettingsField.ContactShadows, label = "Contact shadows", heavy = true },
        new GraphicsFeature { field = FrameSettingsField.PlanarProbe, label = "Planar reflection", heavy = true },
        new GraphicsFeature { field = FrameSettingsField.SubsurfaceScattering, label = "Subsurface scattering" },
        new GraphicsFeature { field = FrameSettingsField.Decals, label = "Decals" },
        new GraphicsFeature { field = FrameSettingsField.Postprocess, label = "Post process (all)" },
        new GraphicsFeature { field = FrameSettingsField.Bloom, label = "Bloom" },
        new GraphicsFeature { field = FrameSettingsField.DepthOfField, label = "Depth of field", heavy = true },
        new GraphicsFeature { field = FrameSettingsField.MotionBlur, label = "Motion blur", heavy = true },
        new GraphicsFeature { field = FrameSettingsField.Antialiasing, label = "Anti-aliasing" },
    };

    private bool _graphicsOpen;
    private Rect _graphicsRect;
    private Vector2 _graphicsScroll;

    private HDAdditionalCameraData _cameraData;
    private bool _graphicsCaptured;
    private bool _originalCustom;
    private FrameSettingsOverrideMask _originalMask;
    private FrameSettings _originalSettings;
    private readonly Dictionary<FrameSettingsField, bool> _graphicsOverrides = new Dictionary<FrameSettingsField, bool>();

    private float _renderScale = 0.75f;
    private HDRenderPipelineAsset _scaledAsset;
    private GlobalDynamicResolutionSettings _originalDrs;

    private bool EnsureCameraData()
    {
        if (_cameraData != null) return true;
        if (_view == null) return false;
        if (!_view.TryGetComponent(out _cameraData)) _cameraData = _view.gameObject.AddComponent<HDAdditionalCameraData>();
        if (!_graphicsCaptured)
        {
            _graphicsCaptured = true;
            _originalCustom = _cameraData.customRenderingSettings;
            _originalMask = _cameraData.renderingPathCustomFrameSettingsOverrideMask;
            _originalSettings = _cameraData.renderingPathCustomFrameSettings;
        }
        return true;
    }

    // Called from Awake. First run: the light setup.
    private void LoadGraphics()
    {
        bool firstRun = !PlayerPrefs.HasKey(GraphicsPrefsKey) || PlayerPrefs.GetInt(GraphicsVersionPrefsKey, 0) < GraphicsVersion;
        PlayerPrefs.SetInt(GraphicsVersionPrefsKey, GraphicsVersion);
        _renderScale = PlayerPrefs.GetFloat(RenderScalePrefsKey, 0.75f);
        CS_MayaPreviewIdleThrottle.ActiveFps = PlayerPrefs.GetInt(ActiveFpsPrefsKey, 30);
        ApplyRenderScale();

        if (firstRun) { LightGraphics(false); return; }
        string saved = PlayerPrefs.GetString(GraphicsPrefsKey, "");
        if (string.IsNullOrEmpty(saved) || !EnsureCameraData()) return;
        foreach (string entry in saved.Split(','))
        {
            string[] pair = entry.Split(':');
            if (pair.Length != 2 || !int.TryParse(pair[0], out int field)) continue;
            ApplyFeature((FrameSettingsField)field, pair[1] == "1");
        }
    }

    private void SaveGraphics()
    {
        var parts = new List<string>();
        foreach (var pair in _graphicsOverrides) parts.Add((int)pair.Key + ":" + (pair.Value ? "1" : "0"));
        PlayerPrefs.SetString(GraphicsPrefsKey, string.Join(",", parts));
        PlayerPrefs.SetFloat(RenderScalePrefsKey, _renderScale);
        PlayerPrefs.SetInt(ActiveFpsPrefsKey, Mathf.RoundToInt(CS_MayaPreviewIdleThrottle.ActiveFps));
        // Read back before the first scene loads next time (CS_MayaPreviewMemory).
        PlayerPrefs.SetInt(CS_MayaPreviewMemory.QualityPrefsKey, QualitySettings.GetQualityLevel());
        PlayerPrefs.SetInt(CS_MayaPreviewMemory.TextureLimitPrefsKey, QualitySettings.globalTextureMipmapLimit);
        PlayerPrefs.Save();
    }

    private void ApplyFeature(FrameSettingsField field, bool on)
    {
        if (!EnsureCameraData()) return;
        _cameraData.customRenderingSettings = true;
        FrameSettingsOverrideMask mask = _cameraData.renderingPathCustomFrameSettingsOverrideMask;
        mask.mask[(uint)field] = true;
        _cameraData.renderingPathCustomFrameSettingsOverrideMask = mask;
        _cameraData.renderingPathCustomFrameSettings.SetEnabled(field, on);
        _graphicsOverrides[field] = on;
    }

    private void SetFeature(FrameSettingsField field, bool on)
    {
        ApplyFeature(field, on);
        SaveGraphics();
        CS_MayaPreviewIdleThrottle.Wake();
    }

    // Everything as the camera / project had it (100 %, 60 fps).
    private void FullGraphics()
    {
        if (!EnsureCameraData()) return;
        _cameraData.customRenderingSettings = _originalCustom;
        _cameraData.renderingPathCustomFrameSettingsOverrideMask = _originalMask;
        _cameraData.renderingPathCustomFrameSettings = _originalSettings;
        _graphicsOverrides.Clear();
        if (RenderScaleAvailable)
        {
            if (QualitySettings.GetQualityLevel() != 0) QualitySettings.SetQualityLevel(0, true);
            QualitySettings.globalTextureMipmapLimit = 0;
        }
        _renderScale = 1f;
        ApplyRenderScale();
        CS_MayaPreviewIdleThrottle.ActiveFps = 60;
        SaveGraphics();
        CS_MayaPreviewIdleThrottle.Wake();
        Flash("Graphics: full");
    }

    private void LightGraphics(bool flash = true)
    {
        foreach (GraphicsFeature feature in GraphicsFeatures)
            if (feature.heavy) ApplyFeature(feature.field, false);
        // Lowest quality level (smaller shadow atlas, cheaper color buffer), then half resolution.
        // (built exe only: in the Editor these would change the project's quality settings)
        int low = QualitySettings.names.Length - 1;
        if (RenderScaleAvailable && low >= 0 && QualitySettings.GetQualityLevel() != low) QualitySettings.SetQualityLevel(low, true);
        // Half-size textures: half the texture memory (the bigger part of it next to HDRP's buffers).
        if (RenderScaleAvailable) QualitySettings.globalTextureMipmapLimit = Mathf.Max(QualitySettings.globalTextureMipmapLimit, 1);
        _renderScale = 0.5f;
        ApplyRenderScale();
        CS_MayaPreviewIdleThrottle.ActiveFps = Mathf.Min(CS_MayaPreviewIdleThrottle.ActiveFps, 30f);
        SaveGraphics();
        CS_MayaPreviewIdleThrottle.Wake();
        if (flash) Flash("Graphics: light");
    }

    // Render scale = HDRP dynamic resolution forced to a percentage. The HDRP assets have it
    // switched off, so it is switched on in memory (built exe only: in the Editor the change
    // would end up saved into the asset file).
    private bool RenderScaleAvailable => !Application.isEditor;

    private void ApplyRenderScale()
    {
        if (!RenderScaleAvailable || !EnsureCameraData()) return;
        var asset = QualitySettings.renderPipeline as HDRenderPipelineAsset;
        if (asset == null) asset = GraphicsSettings.defaultRenderPipeline as HDRenderPipelineAsset;
        if (asset == null) return;

        // Quality level changed: give the previous asset its settings back.
        if (_scaledAsset != null && _scaledAsset != asset) RestoreRenderScale();

        RenderPipelineSettings settings = asset.currentPlatformRenderPipelineSettings;
        if (_scaledAsset != asset) { _originalDrs = settings.dynamicResolutionSettings; _scaledAsset = asset; }

        bool scaled = _renderScale < 0.999f;
        GlobalDynamicResolutionSettings drs = _originalDrs;
        if (scaled)
        {
            float percent = _renderScale * 100f;
            drs.enabled = true;
            drs.dynResType = DynamicResolutionType.Software;
            drs.upsampleFilter = DynamicResUpscaleFilter.CatmullRom;
            drs.forceResolution = true;
            drs.forcedPercentage = percent;
            drs.minPercentage = Mathf.Min(drs.minPercentage > 0f ? drs.minPercentage : percent, percent);
            drs.maxPercentage = 100f;
        }
        settings.dynamicResolutionSettings = drs;
        asset.currentPlatformRenderPipelineSettings = settings;
        _view.allowDynamicResolution = scaled;
        _cameraData.allowDynamicResolution = scaled;
        CS_MayaPreviewIdleThrottle.Wake();
    }

    private void RestoreRenderScale()
    {
        if (_scaledAsset == null) return;
        RenderPipelineSettings settings = _scaledAsset.currentPlatformRenderPipelineSettings;
        settings.dynamicResolutionSettings = _originalDrs;
        _scaledAsset.currentPlatformRenderPipelineSettings = settings;
        _scaledAsset = null;
    }

    // What the camera really renders with (after the HDRP asset's support is applied).
    private bool EffectiveFeature(FrameSettingsField field)
    {
        HDCamera camera = HDCamera.GetOrCreate(_view);
        return camera != null && camera.frameSettings.IsEnabled(field);
    }

    private void DrawGraphics()
    {
        Rect content = DrawWindow(_graphicsRect, "Graphics", "preview camera");
        GUILayout.BeginArea(new Rect(content.x + 6, content.y + 4, content.width - 12, content.height - 6));
        _graphicsScroll = GUILayout.BeginScrollView(_graphicsScroll, false, false, GUIStyle.none, _skin.verticalScrollbar, GUIStyle.none);
        float h = RowHeight + 2;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Light", _button, GUILayout.Height(h))) LightGraphics();
        if (GUILayout.Button("Full", _graphicsOverrides.Count == 0 && _renderScale >= 0.999f ? _buttonOn : _button, GUILayout.Height(h))) FullGraphics();
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Max FPS", _dim, GUILayout.Width(78));
        foreach (int fps in FpsCaps)
            if (GUILayout.Button(fps.ToString(), Mathf.RoundToInt(CS_MayaPreviewIdleThrottle.ActiveFps) == fps ? _buttonOn : _button, GUILayout.Height(h)))
            { CS_MayaPreviewIdleThrottle.ActiveFps = fps; SaveGraphics(); }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(RenderScaleAvailable ? "Render scale" : "Scale (exe)", _dim, GUILayout.Width(78));
        GUI.enabled = RenderScaleAvailable;
        foreach (float scale in RenderScales)
            if (GUILayout.Button(Mathf.RoundToInt(scale * 100f) + "%", Mathf.Approximately(_renderScale, scale) ? _buttonOn : _button, GUILayout.Height(h)))
            { _renderScale = scale; ApplyRenderScale(); SaveGraphics(); }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Quality", _dim, GUILayout.Width(78));
        GUI.enabled = RenderScaleAvailable;
        string[] names = QualitySettings.names;
        int level = QualitySettings.GetQualityLevel();
        for (int i = 0; i < names.Length; i++)
            if (GUILayout.Button(ShortQuality(names[i]), level == i ? _buttonOn : _button, GUILayout.Height(h)) && level != i)
            {
                QualitySettings.SetQualityLevel(i, true);
                ApplyRenderScale();
                SaveGraphics();
                CS_MayaPreviewIdleThrottle.Wake();
                Flash("Quality: " + names[i]);
            }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Textures", _dim, GUILayout.Width(78));
        GUI.enabled = RenderScaleAvailable;
        int limit = QualitySettings.globalTextureMipmapLimit;
        for (int i = 0; i < TextureSizes.Length; i++)
            if (GUILayout.Button(TextureSizes[i], limit == i ? _buttonOn : _button, GUILayout.Height(h)) && limit != i)
            {
                QualitySettings.globalTextureMipmapLimit = i;
                SaveGraphics();
                CS_MayaPreviewIdleThrottle.Wake();
                Flash("Textures: " + TextureSizes[i]);
            }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        Fill(GUILayoutUtility.GetRect(10, 1, GUILayout.ExpandWidth(true)), BorderColor);
        GUILayout.Space(2);
        foreach (GraphicsFeature feature in GraphicsFeatures)
        {
            bool effective = EffectiveFeature(feature.field);
            bool overridden = _graphicsOverrides.TryGetValue(feature.field, out bool wanted);
            bool on = overridden ? wanted : effective;
            bool unsupported = on && !effective && overridden;   // asked for, but the HDRP asset does not support it
            Rect row = GUILayoutUtility.GetRect(10, RowHeight, GUILayout.ExpandWidth(true));
            if (GUI.Button(row, GUIContent.none, GUIStyle.none)) SetFeature(feature.field, !on);
            if (row.Contains(Event.current.mousePosition)) Fill(row, HoverColor);
            Rect box = new Rect(row.x + 4, row.y + 3, 12, 12);
            Fill(box, on ? OutlineYellow : DimColor);
            Fill(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), FieldColor);
            if (on) Fill(new Rect(box.x + 3, box.y + 3, box.width - 6, box.height - 6), unsupported ? DimColor : OutlineYellow);
            GUI.Label(new Rect(row.x + 22, row.y, row.width - 24, row.height), feature.label + (unsupported ? "  (n/a)" : ""), on && !unsupported ? _row : _rowDim);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static string ShortQuality(string name) =>
        name == "High Fidelity" ? "High" : name == "Performant" ? "Low" : name == "Balanced" ? "Mid" : name;
}
