/* ================================================
 * HDRP Toon - 旧 SHG_NTE_* マテリアルを汎用シェーダーへ自動変換
 * ------------------------------------------------
 *  SHG_NTE_Skin / Cloth / Hair / Leather / Metal -> SHG_Toon
 *  SHG_NTE_Eye                                   -> SHG_Toon_Eye
 *  見た目が変わらないように値を移す:
 *   - リム / ハイライトの「色 x 強さ」を 1 つの HDR 色に
 *   - 肌: 光の吸収 -> 肌の柔らかさ、落ち影の赤みを影の色へ
 *   - 髪: 髪のツヤ 1 / 金属: 金属感 1 / 布: 布の起毛感
 *  Unity 起動 (スクリプト読み込み) 時に 1 度だけ自動で実行。
 *  Tools > Toon > Upgrade Old Toon Materials で手動実行も可。
 * ================================================ */
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[InitializeOnLoad]
public static class ToonMaterialUpgrade
{
    const string GraphDir = "Assets/Programmer/Scene/Test/Toon/Shader/Graph/";
    const string ToonGraph = GraphDir + "SHG_Toon.shadergraph";
    const string EyeGraph = GraphDir + "SHG_Toon_Eye.shadergraph";
    const string DoneMarker = "Library/ToonMaterialUpgrade.done";

    enum Kind { Skin, Cloth, Hair, Leather, Metal, Eye }

    static readonly Dictionary<string, Kind> OldGraphs = new Dictionary<string, Kind>
    {
        { GraphDir + "SHG_NTE_Skin.shadergraph", Kind.Skin },
        { GraphDir + "SHG_NTE_Cloth.shadergraph", Kind.Cloth },
        { GraphDir + "SHG_NTE_Hair.shadergraph", Kind.Hair },
        { GraphDir + "SHG_NTE_Leather.shadergraph", Kind.Leather },
        { GraphDir + "SHG_NTE_Metal.shadergraph", Kind.Metal },
        { GraphDir + "SHG_NTE_Eye.shadergraph", Kind.Eye },
    };

    static ToonMaterialUpgrade()
    {
        if (!File.Exists(DoneMarker)) EditorApplication.delayCall += RunOnce;
    }

    static void RunOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += RunOnce;
            return;
        }
        if (Upgrade()) File.WriteAllText(DoneMarker, System.DateTime.Now.ToString("s"));
    }

    [MenuItem("Tools/Toon/Upgrade Old Toon Materials")]
    static void Menu() => Upgrade();

    // Returns false when the new shaders are not ready yet (nothing is changed then).
    static bool Upgrade()
    {
        var toon = AssetDatabase.LoadAssetAtPath<Shader>(ToonGraph);
        var eye = AssetDatabase.LoadAssetAtPath<Shader>(EyeGraph);
        if (toon == null || eye == null || ShaderUtil.ShaderHasError(toon) || ShaderUtil.ShaderHasError(eye))
        {
            Debug.LogWarning("[ToonMaterialUpgrade] SHG_Toon / SHG_Toon_Eye are not ready (missing or with errors). Materials were not changed.");
            return false;
        }

        var log = new System.Text.StringBuilder();
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Kind kind = Kind.Skin;
            bool old = false;
            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                if (OldGraphs.TryGetValue(dependency, out kind)) { old = true; break; }
            if (!old) continue;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null) continue;
            if (!OldGraphs.TryGetValue(AssetDatabase.GetAssetPath(material.shader), out kind)) continue;

            if (kind == Kind.Eye) ConvertEye(material, eye);
            else Convert(material, kind, toon);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            log.AppendLine("  " + kind + "  " + path);
            count++;
        }
        if (count > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[ToonMaterialUpgrade] " + count + " materials -> SHG_Toon / SHG_Toon_Eye\n" + log);
        }
        return true;
    }

    static void Convert(Material m, Kind kind, Shader toon)
    {
        // Read everything before the shader changes.
        float F(string name, float fallback) => m.HasProperty(name) ? m.GetFloat(name) : fallback;
        Color C(string name, Color fallback) => m.HasProperty(name) ? m.GetColor(name) : fallback;
        Color rim = C("_CelRimColor", new Color(1f, 0.97f, 0.94f)) * F("_CelRimStrength", 0.1f);
        Color spec = C("_CelSpecColor", Color.white) * F("_CelSpecStrength", 0f);
        Color shadow = C("_ShadowColor", new Color(0.7f, 0.7f, 0.7f));
        float absorb = F("_DetailStrength", 0f);
        float fabric = F("_FabricSheen", 0f);
        rim.a = spec.a = 1f;

        float subsurface = 0f, hair = 0f, metal = 0f, sheen = 0f;
        switch (kind)
        {
            case Kind.Skin:
                subsurface = absorb;
                spec = Color.black;                                   // the old skin had no highlight
                // The old skin coloured its cast shadows red-orange by itself: keep that in the shadow colour.
                shadow = new Color(Mathf.Min(1f, shadow.r * 1.05f), shadow.g * 0.90f, shadow.b * 0.86f, 1f);
                break;
            case Kind.Cloth:
                sheen = fabric;
                spec = Color.black;                                   // the old cloth had no highlight
                break;
            case Kind.Hair:
                hair = 1f;
                break;
            case Kind.Metal:
                metal = 1f;
                break;
        }

        m.shader = toon;
        m.SetColor("_CelRimColor", rim);
        m.SetColor("_CelSpecColor", spec);
        m.SetColor("_ShadowColor", shadow);
        m.SetFloat("_Subsurface", subsurface);
        m.SetFloat("_HairShine", hair);
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Sheen", sheen);
    }

    static void ConvertEye(Material m, Shader eye)
    {
        float brightness = m.HasProperty("_EyeBrightness") ? m.GetFloat("_EyeBrightness") : 1.25f;
        m.shader = eye;
        m.SetFloat("_EyeBrightness", brightness);
        m.SetFloat("_EyeReflection", 0.5f);
    }
}
