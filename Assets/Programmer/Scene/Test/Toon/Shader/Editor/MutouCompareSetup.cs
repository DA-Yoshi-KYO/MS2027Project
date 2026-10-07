/* ================================================
 * HDRP Toon - mutou ベースモデル 比較セットアップ
 * ------------------------------------------------
 * Tools > Toon > Setup Mutou Compare (HDRP Lit vs Toon Unlit)
 *  Toon.unity 内の 2 体を自動で見つけてマテリアルを割り当てる。
 *   - Chara_Test (Chara_Test_HDRP.fbx)   : HDRP 標準 Lit マテリアル（ビフォー）
 *   - Chara_Test_UNLIT (Chara_Test_UNLIT.fbx) : 自作 SHG_NTE_* シェーダー（アフター）
 *       + アウトライン用 Renderer を追加 / CS_ToonLightDirection を追加
 *  何度実行しても同じ結果になる（既存マテリアルは上書き更新）。
 * ================================================ */
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class MutouCompareSetup
{
    const string ModelDir = "Assets/Teto_GameReady_UnityHDRP_v5/mutou";
    const string HdrpFbx = ModelDir + "/Chara_Test_HDRP.fbx";
    const string UnlitFbx = ModelDir + "/Chara_Test_UNLIT.fbx";
    const string MatDir = ModelDir + "/Materials";
    const string ToonRoot = "Assets/Programmer/Scene/Test/Toon";
    const string GraphDir = ToonRoot + "/Shader/Graph";
    const string OutlineTplDir = ToonRoot + "/Materiar";

    enum Part { Skin, Hair, Eye, Clothes, Gun }

    [MenuItem("Tools/Toon/Setup Mutou Compare (HDRP Lit vs Toon Unlit)")]
    static void Run()
    {
        var log = new System.Text.StringBuilder();
        EnsureFolder(ModelDir, "Materials");
        EnsureFolder(MatDir, "HDRP");
        EnsureFolder(MatDir, "Unlit");

        var hdrpRoots = FindInstances(HdrpFbx);
        var unlitRoots = FindInstances(UnlitFbx);
        if (hdrpRoots.Length == 0) hdrpRoots = new[] { Spawn(HdrpFbx, new Vector3(-0.8f, 0, 0)) };
        if (unlitRoots.Length == 0) unlitRoots = new[] { Spawn(UnlitFbx, new Vector3(0.8f, 0, 0)) };

        // ---------- HDRP Lit (before) ----------
        foreach (var root in hdrpRoots)
        {
            foreach (var r in MainRenderers(root))
            {
                Undo.RecordObject(r, "Mutou HDRP materials");
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var part = Classify(mats[i] != null ? mats[i].name : r.name);
                    mats[i] = GetLitMaterial(part);
                }
                r.sharedMaterials = mats;
                log.AppendLine("HDRP Lit  : " + root.name + "/" + r.name + " (" + mats.Length + " slots)");
            }
        }

        // ---------- Toon Unlit (after) ----------
        var light = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .FirstOrDefault(l => l.type == LightType.Directional && l.enabled);
        foreach (var root in unlitRoots)
        {
            var mains = MainRenderers(root).ToArray();
            foreach (var r in mains)
            {
                Undo.RecordObject(r, "Mutou toon materials");
                var mats = r.sharedMaterials;
                var parts = new Part[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    parts[i] = Classify(mats[i] != null ? mats[i].name : r.name);
                    mats[i] = GetToonMaterial(parts[i]);
                }
                r.sharedMaterials = mats;
                SetupOutline(root, r, parts);
                log.AppendLine("Toon Unlit: " + root.name + "/" + r.name + " (" + mats.Length + " slots) + outline");
            }

            // Light direction for the toon shaders
            var ld = root.GetComponent<CS_ToonLightDirection>();
            if (ld == null) ld = Undo.AddComponent<CS_ToonLightDirection>(root);
            var so = new SerializedObject(ld);
            so.FindProperty("characterRoot").objectReferenceValue = root.transform;
            if (light != null) so.FindProperty("mainLight").objectReferenceValue = light;
            so.ApplyModifiedProperties();
            log.AppendLine("CS_ToonLightDirection: " + root.name + " (light: " + (light ? light.name : "none") + ")");
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[MutouCompareSetup]\n" + log);
    }

    // ================= Materials =================
    static Material GetLitMaterial(Part p)
    {
        var m = LoadOrCreate(MatDir + "/HDRP/MT_Mutou_HDRP_" + p + ".mat", Shader.Find("HDRP/Lit"));
        m.SetTexture("_BaseColorMap", BaseTex(p));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", p == Part.Gun ? 0.6f : 0f);
        m.SetFloat("_Smoothness", p == Part.Hair ? 0.45f : p == Part.Gun ? 0.5f : p == Part.Eye ? 0.7f : 0.3f);
        m.SetFloat("_DoubleSidedEnable", 1f);
        HDMaterial.ValidateMaterial(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material GetToonMaterial(Part p)
    {
        string graph = p switch
        {
            Part.Skin => "SHG_NTE_Skin",
            Part.Hair => "SHG_NTE_Hair",
            Part.Eye => "SHG_NTE_Eye",
            Part.Gun => "SHG_NTE_Metal",
            _ => "SHG_NTE_Cloth",
        };
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphDir + "/" + graph + ".shadergraph");
        var m = LoadOrCreate(MatDir + "/Unlit/MT_Mutou_Toon_" + p + ".mat", shader);
        m.SetTexture("_BaseMap", BaseTex(p));
        m.SetColor("_BaseColor", Color.white);
        if (p == Part.Eye)
        {
            // mutou には Teto 用ハイライトマスク(35_EyeHighlightMask)が無い。
            // マスク未設定(白)だと瞳全体が白飛びするため、マスク依存の効果は切る。
            m.SetFloat("_EyeHighlightStrength", 0f);
            m.SetFloat("_EyeIrisGradient", 0f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material GetOutlineMaterial(Part p)
    {
        string tpl = p switch
        {
            Part.Hair => "MT_Outline Hair",
            Part.Eye => "MT_Outline_Eye",
            Part.Skin => "MT_Outline_Body",
            _ => "MT_Outline_Body",
        };
        var template = AssetDatabase.LoadAssetAtPath<Material>(OutlineTplDir + "/" + tpl + ".mat");
        if (template == null) return null;
        string path = MatDir + "/Unlit/MT_Mutou_Outline_" + p + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(template); AssetDatabase.CreateAsset(m, path); }
        else m.CopyPropertiesFromMaterial(template);
        m.SetTexture("_BaseMap", BaseTex(p));
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material LoadOrCreate(string path, Shader shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else if (m.shader != shader) m.shader = shader;
        return m;
    }

    static Texture2D BaseTex(Part p)
    {
        string n = p switch
        {
            Part.Skin => "Skin_tex",
            Part.Hair => "Hair_tex",
            Part.Eye => "Eye_tex",
            Part.Gun => "M_Gun_Base_color",
            _ => "Clothes_tex",
        };
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ModelDir + "/" + n + ".png");
    }

    static Part Classify(string name)
    {
        if (name.Contains("Skin")) return Part.Skin;
        if (name.Contains("Hair")) return Part.Hair;
        if (name.Contains("Eye")) return Part.Eye;
        if (name.Contains("Gun")) return Part.Gun;
        return Part.Clothes;
    }

    // ================= Outline =================
    static void SetupOutline(GameObject root, Renderer src, Part[] parts)
    {
        if (!(src is SkinnedMeshRenderer smr)) return;
        var outlineRoot = root.transform.Find("Outline");
        if (outlineRoot == null)
        {
            var go = new GameObject("Outline");
            Undo.RegisterCreatedObjectUndo(go, "Mutou outline");
            go.transform.SetParent(root.transform, false);
            outlineRoot = go.transform;
        }
        var t = outlineRoot.Find(src.name);
        if (t == null)
        {
            var go = new GameObject(src.name);
            Undo.RegisterCreatedObjectUndo(go, "Mutou outline");
            go.transform.SetParent(outlineRoot, false);
            t = go.transform;
        }
        var o = t.GetComponent<SkinnedMeshRenderer>();
        if (o == null) o = Undo.AddComponent<SkinnedMeshRenderer>(t.gameObject);
        Undo.RecordObject(o, "Mutou outline");
        o.sharedMesh = smr.sharedMesh;
        o.bones = smr.bones;
        o.rootBone = smr.rootBone;
        o.localBounds = smr.localBounds;
        o.updateWhenOffscreen = smr.updateWhenOffscreen;
        o.shadowCastingMode = ShadowCastingMode.Off;
        o.receiveShadows = false;
        o.sharedMaterials = parts.Select(GetOutlineMaterial).ToArray();
        // BlendShape (表情) を本体と揃える
        if (smr.sharedMesh != null)
            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                o.SetBlendShapeWeight(i, smr.GetBlendShapeWeight(i));
    }

    // ================= Scene helpers =================
    static GameObject[] FindInstances(string fbxPath)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(t => t.gameObject)
            .Where(g => PrefabUtility.IsOutermostPrefabInstanceRoot(g)
                        && PrefabUtility.GetCorrespondingObjectFromOriginalSource(g) == src)
            .ToArray();
    }

    static GameObject Spawn(string fbxPath, Vector3 pos)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
        g.transform.position = pos;
        Undo.RegisterCreatedObjectUndo(g, "Spawn mutou");
        return g;
    }

    static System.Collections.Generic.IEnumerable<Renderer> MainRenderers(GameObject root)
    {
        return root.GetComponentsInChildren<Renderer>(true).Where(r => !IsOutline(r.transform, root.transform));
    }

    static bool IsOutline(Transform t, Transform stop)
    {
        for (; t != null && t != stop; t = t.parent) if (t.name.Contains("Outline")) return true;
        return false;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
