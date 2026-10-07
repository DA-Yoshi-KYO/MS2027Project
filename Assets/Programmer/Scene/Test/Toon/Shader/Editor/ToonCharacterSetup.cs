/* ================================================
 * HDRP Toon - Character look setup (one click)
 * ------------------------------------------------
 * Tools > Toon > Setup Character Look
 *  1. アウトライン用 Renderer の影を OFF（アウトラインが落とす汚い影を消す）
 *  2. アウトラインを各パーツのテクスチャ色で作り直す（黒線ではなく馴染む線）
 *  3. Directional Light の影解像度を上げる（512 → 2048）
 *  4. キャラ用の影距離 Volume を追加（影マップの解像度を近距離に集中）
 *  5. Main Camera を TAA にしてジャギーを消す
 * 背景用の NTE_Volume は変更しない。
 * ================================================ */
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class ToonCharacterSetup
{
    const string Root = "Assets/Programmer/Scene/Test/Toon";
    const string AutoOutlineDir = Root + "/Materiar/AutoOutline";
    const string ShadowProfilePath = Root + "/SD_CharacterShadow_Volume.asset";

    [MenuItem("Tools/Toon/Setup Character Look")]
    static void Run()
    {
        var log = new System.Text.StringBuilder();
        var renderers = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var outlineRenderers = renderers.Where(r => IsOutline(r.transform)).ToArray();
        var mainRenderers = renderers.Where(r => !IsOutline(r.transform)).ToArray();

        // 1 + 2. Outline renderers
        if (!AssetDatabase.IsValidFolder(AutoOutlineDir)) AssetDatabase.CreateFolder(Root + "/Materiar", "AutoOutline");
        foreach (var o in outlineRenderers)
        {
            Undo.RecordObject(o, "Toon outline setup");
            o.shadowCastingMode = ShadowCastingMode.Off;
            o.receiveShadows = false;
            var src = mainRenderers.FirstOrDefault(r => r.name == o.name && r.sharedMesh == o.sharedMesh);
            if (src == null) { log.AppendLine("No source renderer for outline " + o.name); continue; }
            var mats = o.sharedMaterials;
            for (int i = 0; i < mats.Length && i < src.sharedMaterials.Length; i++)
            {
                var template = mats[i];
                var baseMat = src.sharedMaterials[i];
                if (template == null || baseMat == null || !template.HasProperty("_OutlineTexBlend")) continue;
                // Already an auto material: re-derive from its template name is unnecessary, just refresh the texture.
                var tex = baseMat.HasProperty("_BaseMap") ? baseMat.GetTexture("_BaseMap") : null;
                string path = AutoOutlineDir + "/MT_Outline_" + Sanitize(baseMat.name) + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(template);
                    AssetDatabase.CreateAsset(m, path);
                }
                else if (!template.name.StartsWith("MT_Outline_") || AssetDatabase.GetAssetPath(template) != path)
                {
                    m.CopyPropertiesFromMaterial(template);
                }
                m.SetTexture("_BaseMap", tex);
                EditorUtility.SetDirty(m);
                mats[i] = m;
            }
            o.sharedMaterials = mats;
            log.AppendLine("Outline " + o.name + ": " + mats.Length + " materials, shadows off");
        }

        // 3. Directional light shadow resolution
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional && l.enabled))
        {
            var hd = l.GetComponent<HDAdditionalLightData>();
            if (hd == null) continue;
            Undo.RecordObject(hd, "Toon light setup");
            hd.SetShadowResolution(2048);
            EditorUtility.SetDirty(hd);
            log.AppendLine("Light " + l.name + ": shadow resolution 2048");
        }

        // 4. Character shadow distance volume (does not touch the background NTE_Volume)
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ShadowProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ShadowProfilePath);
        }
        if (!profile.TryGet(out HDShadowSettings shadow))
        {
            shadow = profile.Add<HDShadowSettings>(true);
            AssetDatabase.AddObjectToAsset(shadow, profile);
        }
        shadow.maxShadowDistance.Override(30f);
        EditorUtility.SetDirty(shadow);
        EditorUtility.SetDirty(profile);
        var volGo = GameObject.Find("CharacterShadowVolume");
        if (volGo == null)
        {
            volGo = new GameObject("CharacterShadowVolume");
            Undo.RegisterCreatedObjectUndo(volGo, "Toon shadow volume");
        }
        var vol = volGo.GetComponent<Volume>() ?? volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10;
        vol.sharedProfile = profile;
        log.AppendLine("Shadow volume: max distance 30m");

        // 5. Anti-aliasing on the main camera
        var cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetComponent<HDAdditionalCameraData>();
            if (data != null)
            {
                Undo.RecordObject(data, "Toon camera setup");
                data.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                data.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
                data.taaSharpenStrength = 0.4f;
                EditorUtility.SetDirty(data);
                log.AppendLine("Camera " + cam.name + ": TAA High");
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[ToonCharacterSetup]\n" + log);
    }

    static bool IsOutline(Transform t)
    {
        for (; t != null; t = t.parent) if (t.name.Contains("Outline")) return true;
        return false;
    }

    static string Sanitize(string n)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return n.Replace(" (Instance)", "").Replace(' ', '_');
    }
}
