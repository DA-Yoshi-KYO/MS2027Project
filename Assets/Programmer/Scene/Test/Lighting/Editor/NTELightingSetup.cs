using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// NTELighting シーンの夜景用パラメータを Volume / Moon Light に一括適用するエディタツール
public static class NTELightingSetup
{
    private const string ProfilePath = "Assets/Programmer/Scene/Test/Lighting/NTELighting/NTEL_Volume.asset";

    [MenuItem("Tools/Lighting/Apply NTE Night Preset")]
    public static void Apply()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogError($"[NTELightingSetup] VolumeProfile が見つかりません: {ProfilePath}");
            return;
        }

        if (!profile.Has<ColorAdjustments>())
        {
            var added = profile.Add<ColorAdjustments>();
            added.name = nameof(ColorAdjustments);
            AssetDatabase.AddObjectToAsset(added, profile);
        }

        Edit<Exposure>(profile, so =>
        {
            SetInt(so, "mode", 0); // Fixed
            SetFloat(so, "fixedExposure", 1.8f);
        });

        Edit<PhysicallyBasedSky>(profile, so =>
        {
            SetFloat(so, "aerosolDensity", 0.005f);
            SetColor(so, "zenithTint", new Color(0.55f, 0.65f, 1f));
            SetColor(so, "horizonTint", new Color(0.75f, 0.85f, 1f));
            SetFloat(so, "colorSaturation", 1.2f);
        });

        Edit<VolumetricClouds>(profile, so =>
        {
            SetInt(so, "cloudControl", 0);   // Simple
            SetInt(so, "m_CloudPreset", 4);  // Custom
            SetFloat(so, "bottomAltitude", 1500f);
            SetFloat(so, "altitudeRange", 1500f);
            SetFloat(so, "densityMultiplier", 0.35f);
            SetFloat(so, "shapeFactor", 0.8f);
            SetFloat(so, "shapeScale", 3.5f);
            SetFloat(so, "erosionFactor", 0.7f);
            SetFloat(so, "erosionScale", 100f);
            SetBool(so, "microErosion", true);
            SetFloat(so, "microErosionFactor", 0.5f);
            SetFloat(so, "ambientLightProbeDimmer", 1f);
            SetFloat(so, "sunLightDimmer", 1f);
            SetBool(so, "shadows", false);
        });

        Edit<Fog>(profile, so =>
        {
            SetBool(so, "enabled", true);
            SetInt(so, "colorMode", 1); // Sky Color
            SetColor(so, "tint", new Color(0.7f, 0.8f, 1f));
            SetFloat(so, "meanFreePath", 1200f);
            SetFloat(so, "baseHeight", 0f);
            SetFloat(so, "maximumHeight", 80f);
            SetBool(so, "enableVolumetricFog", true);
            SetColor(so, "albedo", new Color(0.8f, 0.87f, 1f));
            SetFloat(so, "anisotropy", 0.6f);
            SetFloat(so, "depthExtent", 100f);
            SetFloat(so, "globalLightProbeDimmer", 0.5f);
            SetInt(so, "denoisingMode", 3); // Reprojection + Gaussian
        });

        Edit<ScreenSpaceReflection>(profile, so =>
        {
            SetFloat(so, "m_MinSmoothness", 0.6f);
            SetFloat(so, "m_SmoothnessFadeStart", 0.7f);
            SetInt(so, "m_RayMaxIterations", 64);
            SetBool(so, "reflectSky", true);
        });

        Edit<ScreenSpaceAmbientOcclusion>(profile, so =>
        {
            SetBool(so, "rayTracing", false);
            SetFloat(so, "intensity", 0.6f);
            SetFloat(so, "directLightingStrength", 0.2f);
            SetFloat(so, "radius", 1.5f);
        });

        Edit<IndirectLightingController>(profile, so =>
        {
            SetFloat(so, "indirectDiffuseLightingMultiplier", 1.3f);
            SetFloat(so, "reflectionLightingMultiplier", 1f);
        });

        Edit<Tonemapping>(profile, so => SetInt(so, "mode", 1)); // Neutral

        Edit<ColorAdjustments>(profile, so =>
        {
            SetFloat(so, "postExposure", 0f);
            SetFloat(so, "contrast", 12f);
            SetFloat(so, "saturation", 15f);
        });

        Edit<WhiteBalance>(profile, so =>
        {
            SetFloat(so, "temperature", -20f);
            SetFloat(so, "tint", 0f);
        });

        Edit<SplitToning>(profile, so =>
        {
            SetColor(so, "shadows", new Color(0.35f, 0.45f, 0.7f));
            SetColor(so, "highlights", new Color(0.65f, 0.55f, 0.45f));
            SetFloat(so, "balance", -20f);
        });

        Edit<Bloom>(profile, so =>
        {
            SetFloat(so, "threshold", 0f);
            SetFloat(so, "intensity", 0.2f);
            SetFloat(so, "scatter", 0.65f);
            SetBool(so, "m_HighQualityFiltering", true);
        });

        Edit<Vignette>(profile, so =>
        {
            SetFloat(so, "intensity", 0.2f);
            SetFloat(so, "smoothness", 0.35f);
        });

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        ApplyMoonLight();

        Debug.Log("[NTELightingSetup] NTE Night Preset を適用しました");
    }

    private const string MaterialDir = "Assets/Programmer/Scene/Test/Lighting/NTELighting/Materials";
    private const string StreetSetName = "NTE_StreetSet";

    // 街灯・ネオン・簡易ビルを配置する (再実行すると作り直す)
    [MenuItem("Tools/Lighting/Place NTE Street Lights")]
    public static void PlaceStreetLights()
    {
        var old = GameObject.Find(StreetSetName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject(StreetSetName);
        Undo.RegisterCreatedObjectUndo(root, "Place NTE Street Lights");

        var asphalt = GetMaterial("NTE_WetAsphalt", new Color(0.08f, 0.085f, 0.1f), 0.82f);
        var building = GetMaterial("NTE_Building", new Color(0.25f, 0.27f, 0.3f), 0.3f);
        var metal = GetMaterial("NTE_LampPole", new Color(0.2f, 0.21f, 0.23f), 0.5f);
        var lampHead = GetEmissiveMaterial("NTE_LampHead", new Color(1f, 0.85f, 0.65f), 11f);

        // 地面と両側のビル
        CreateBlock(root, "StreetGround", new Vector3(0f, -0.06f, 15f), new Vector3(40f, 0.1f, 60f), asphalt);
        CreateBlock(root, "Building_L", new Vector3(-11f, 8f, 10f), new Vector3(8f, 16f, 40f), building);
        CreateBlock(root, "Building_R", new Vector3(11f, 8f, 10f), new Vector3(8f, 16f, 40f), building);

        // 街灯: 道路の両側に 12m 間隔。手前の 2 本だけ影あり
        var lamps = new GameObject("StreetLamps");
        lamps.transform.SetParent(root.transform, false);
        foreach (var side in new[] { -1f, 1f })
        {
            foreach (var z in new[] { 0f, 12f, 24f })
            {
                CreateStreetLamp(lamps, new Vector3(6f * side, 0f, z), -side, z == 0f, metal, lampHead);
            }
        }

        // ネオン看板 + 壁への色被り用ポイントライト
        var neons = new GameObject("NeonSigns");
        neons.transform.SetParent(root.transform, false);
        CreateNeon(neons, "Neon_Pink", new Vector3(-6.9f, 6f, 6f), new Vector3(0.1f, 4f, 1.2f), 1f, new Color(1f, 0.2f, 0.6f));
        CreateNeon(neons, "Neon_Red", new Vector3(-6.9f, 9f, 18f), new Vector3(0.1f, 5f, 1.4f), 1f, new Color(1f, 0.08f, 0.08f));
        CreateNeon(neons, "Neon_Cyan", new Vector3(6.9f, 7f, 14f), new Vector3(0.1f, 4.5f, 1.2f), -1f, new Color(0.2f, 0.85f, 1f));
        CreateNeon(neons, "Neon_Green", new Vector3(6.9f, 3.5f, 4f), new Vector3(0.1f, 0.8f, 4f), -1f, new Color(0.2f, 1f, 0.35f));

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[NTELightingSetup] 街灯とネオンを配置しました");
    }

    private static void CreateStreetLamp(GameObject parent, Vector3 basePos, float toRoad, bool castShadow, Material pole, Material head)
    {
        var lamp = new GameObject($"StreetLamp_{(toRoad > 0 ? "L" : "R")}_{basePos.z:0}");
        lamp.transform.SetParent(parent.transform, false);
        lamp.transform.localPosition = basePos;

        CreateBlock(lamp, "Pole", new Vector3(0f, 3f, 0f), new Vector3(0.12f, 6f, 0.12f), pole);
        CreateBlock(lamp, "Arm", new Vector3(0.75f * toRoad, 6f, 0f), new Vector3(1.5f, 0.1f, 0.1f), pole);
        CreateBlock(lamp, "Head", new Vector3(1.4f * toRoad, 5.93f, 0f), new Vector3(0.5f, 0.1f, 0.25f), head);

        var lightGo = new GameObject("SpotLight");
        lightGo.transform.SetParent(lamp.transform, false);
        lightGo.transform.localPosition = new Vector3(1.4f * toRoad, 5.85f, 0f);
        lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var hd = lightGo.AddHDLight(LightType.Spot);
        var light = lightGo.GetComponent<Light>();
        light.lightUnit = LightUnit.Lumen;
        light.intensity = 4500f;
        light.color = Color.white;
        light.useColorTemperature = true;
        light.colorTemperature = 4000f;
        light.range = 18f;
        light.spotAngle = 110f;
        light.innerSpotAngle = 55f;
        light.enableSpotReflector = true;
        light.shadows = castShadow ? LightShadows.Soft : LightShadows.None;
        hd.volumetricDimmer = 1f;
    }

    private static void CreateNeon(GameObject parent, string name, Vector3 pos, Vector3 size, float toRoad, Color color)
    {
        var mat = GetEmissiveMaterial($"NTE_{name}", color, 9.5f);
        var sign = CreateBlock(parent, name, pos, size, mat);

        var lightGo = new GameObject();
        lightGo.transform.SetParent(sign.transform.parent, false);
        lightGo.name = $"{name}_SpillLight";
        lightGo.transform.localPosition = pos + new Vector3(0.8f * toRoad, 0f, 0f);

        var hd = lightGo.AddHDLight(LightType.Point);
        var light = lightGo.GetComponent<Light>();
        light.lightUnit = LightUnit.Lumen;
        light.intensity = 600f;
        light.color = color;
        light.useColorTemperature = false;
        light.range = 8f;
        light.shadows = LightShadows.None;
        hd.volumetricDimmer = 0.5f;
    }

    private static GameObject CreateBlock(GameObject parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static Material GetMaterial(string name, Color baseColor, float smoothness)
    {
        var mat = LoadOrCreateMaterial(name);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", smoothness);
        HDMaterial.ValidateMaterial(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetEmissiveMaterial(string name, Color color, float ev100)
    {
        var mat = GetMaterial(name, color * 0.2f, 0.5f);
        HDMaterial.SetUseEmissiveIntensity(mat, true);
        HDMaterial.SetEmissiveIntensity(mat, ev100, EmissiveIntensityUnit.EV100);
        HDMaterial.SetEmissiveColor(mat, color);
        HDMaterial.ValidateMaterial(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material LoadOrCreateMaterial(string name)
    {
        var path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        if (!AssetDatabase.IsValidFolder(MaterialDir))
            AssetDatabase.CreateFolder("Assets/Programmer/Scene/Test/Lighting/NTELighting", "Materials");

        mat = new Material(Shader.Find("HDRP/Lit"));
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void ApplyMoonLight()
    {
        var moon = GameObject.Find("Moon Light");
        if (moon == null)
        {
            Debug.LogWarning("[NTELightingSetup] Moon Light が見つからないためライト設定をスキップしました");
            return;
        }

        var light = moon.GetComponent<Light>();
        Undo.RecordObject(light, "NTE Moon Light");
        light.intensity = 3f; // Lux

        var hdLight = moon.GetComponent<HDAdditionalLightData>();
        if (hdLight != null)
        {
            var so = new SerializedObject(hdLight);
            so.FindProperty("m_VolumetricDimmer").floatValue = 0.3f;
            so.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(light);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(moon.scene);
    }

    private static void Edit<T>(VolumeProfile profile, System.Action<SerializedObject> edit) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var component))
        {
            Debug.LogWarning($"[NTELightingSetup] {typeof(T).Name} が Profile にありません");
            return;
        }

        component.active = true;
        var so = new SerializedObject(component);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(component);
    }

    private static SerializedProperty Param(SerializedObject so, string name)
    {
        var prop = so.FindProperty(name);
        if (prop == null)
        {
            Debug.LogWarning($"[NTELightingSetup] {so.targetObject.GetType().Name}.{name} が見つかりません");
            return null;
        }
        prop.FindPropertyRelative("m_OverrideState").boolValue = true;
        return prop.FindPropertyRelative("m_Value");
    }

    private static void SetFloat(SerializedObject so, string name, float v) { var p = Param(so, name); if (p != null) p.floatValue = v; }
    private static void SetInt(SerializedObject so, string name, int v) { var p = Param(so, name); if (p != null) p.intValue = v; }
    private static void SetBool(SerializedObject so, string name, bool v) { var p = Param(so, name); if (p != null) p.boolValue = v; }
    private static void SetColor(SerializedObject so, string name, Color v) { var p = Param(so, name); if (p != null) p.colorValue = v; }
}
