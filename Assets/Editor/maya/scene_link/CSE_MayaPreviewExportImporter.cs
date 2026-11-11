using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/*
+=====================================
 File   : CSE_MayaPreviewExportImporter
 Summary: Finishes a Maya preview export inside the project by itself (no menu).
          MS2027.exe writes into Assets/Designer, one folder per type (Character ...):
          Mesh/<type>/<name>.fbx, Texture/<type>/*, Json/<type>/<name>.mpx.json.
          When the editor imports the .mpx.json:
          - Texture/<type>/* get their import settings (normal map / linear data),
          - Material/<type>/MT_*.mat are made or updated (shader + every value + textures),
          - the FBX gets those materials (material remap in its import settings),
          - Prefab/<type>/<name>.prefab is made once (a variant of the FBX: later exports
            update it through the FBX, changes made to the prefab are kept).
          Types without materials / prefab (Motion) only get the FBX.
          Old exports (<Folder>/MayaPreview/Exports, no type) still work.
          Right click a .mpx.json > Maya Preview > Rebuild From Export runs it again.
 History: 2026/10/11 New
          2026/10/11 FBX + Designer folders, automatic (no Import menu, no prefab)
          2026/11/09 Folders per export type, prefab
=====================================+
*/
public class CSE_MayaPreviewExportImporter : AssetPostprocessor
{
    private static readonly HashSet<string> s_pending = new HashSet<string>();


    // =========================================================
    // Automatic build when an export lands in Assets
    // =========================================================

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported) Check(path);
        foreach (string path in moved) Check(path);
    }

    private static void Check(string path)
    {
        if (!path.EndsWith(CS_MayaPreviewExport.ManifestExtension, StringComparison.OrdinalIgnoreCase)) return;
        if (s_pending.Add(path) && s_pending.Count == 1) EditorApplication.delayCall += Flush;
    }

    private static void Flush()
    {
        var paths = new List<string>(s_pending);
        s_pending.Clear();
        foreach (string path in paths)
        {
            try { Build(path); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }


    // =========================================================
    // Menu
    // =========================================================

    [MenuItem("Assets/Maya Preview/Rebuild From Export", true)]
    private static bool RebuildValidate()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) && path.EndsWith(CS_MayaPreviewExport.ManifestExtension, StringComparison.OrdinalIgnoreCase);
    }

    [MenuItem("Assets/Maya Preview/Rebuild From Export")]
    private static void Rebuild() => Build(AssetDatabase.GetAssetPath(Selection.activeObject));


    // =========================================================
    // Build
    // =========================================================

    // Assets/Designer/Json/<type>/<name>.mpx.json -> Assets/Designer
    // <Folder>/MayaPreview/Exports/<name>.mpx.json -> <Folder> (old exports)
    private static string RootOf(string manifestPath, CS_MayaPreviewExportManifest manifest)
    {
        string folder = Path.GetDirectoryName(manifestPath).Replace('\\', '/');
        if (manifest.version >= 3)
        {
            int json = folder.LastIndexOf("/" + CS_MayaPreviewExport.JsonFolder + "/", StringComparison.OrdinalIgnoreCase);
            if (json > 0) return folder.Substring(0, json);
        }
        string suffix = "/" + CS_MayaPreviewExport.ManifestFolder;
        if (folder.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return folder.Substring(0, folder.Length - suffix.Length);
        return Path.GetDirectoryName(folder).Replace('\\', '/');
    }

    public static void Build(string manifestPath)
    {
        CS_MayaPreviewExportManifest manifest;
        try { manifest = JsonUtility.FromJson<CS_MayaPreviewExportManifest>(File.ReadAllText(manifestPath)); }
        catch (Exception exception)
        {
            Debug.LogWarning("Maya Preview Import : could not read " + manifestPath + "\n" + exception.Message);
            return;
        }
        if (manifest == null || string.IsNullOrEmpty(manifest.fbx)) return;   // old export format (no FBX)

        string root = RootOf(manifestPath, manifest);
        string fbxPath = root + "/" + manifest.fbx;
        bool typed = manifest.version >= 3;
        bool buildMaterials = !typed || manifest.buildMaterials;
        string materialFolder = root + "/" + (typed && !string.IsNullOrEmpty(manifest.materialFolder) ? manifest.materialFolder : CS_MayaPreviewExport.MaterialFolder);
        if (buildMaterials) EnsureFolder(materialFolder);

        // Textures: import settings first (normal maps / linear data).
        if (buildMaterials && manifest.materials != null)
        {
            foreach (CS_MayaPreviewExportMaterial material in manifest.materials)
            {
                if (material?.textures == null) continue;
                foreach (CS_MayaPreviewExportTexture texture in material.textures)
                    if (!string.IsNullOrEmpty(texture.file)) SetupTexture(root + "/" + texture.file, texture.normal, texture.linear);
            }
        }

        var materials = new List<Material>();
        if (buildMaterials && manifest.materials != null)
            foreach (CS_MayaPreviewExportMaterial data in manifest.materials)
                materials.Add(data != null ? BuildMaterial(root, materialFolder, data) : null);
        AssetDatabase.SaveAssets();

        RemapModel(fbxPath, manifest.materials, materials, buildMaterials);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        UnityEngine.Object ping = model;
        bool prefab = typed && !string.IsNullOrEmpty(manifest.prefab);
        if (model != null && prefab) ping = MakePrefab(model, root + "/" + manifest.prefab) ?? ping;
        Debug.Log("Maya Preview Import : " + fbxPath +
                  (buildMaterials ? "  (" + materials.Count + " materials in " + materialFolder + ")" : "") +
                  (prefab ? "  prefab " + root + "/" + manifest.prefab : ""), ping);
        if (ping != null) EditorGUIUtility.PingObject(ping);
    }

    // Made once as a variant of the FBX model: re-exports update it through the FBX,
    // and what was added to the prefab in Unity is kept.
    private static UnityEngine.Object MakePrefab(GameObject model, string prefabPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;
        EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            instance.name = Path.GetFileNameWithoutExtension(prefabPath);
            return PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Maya Preview Import : could not make the prefab " + prefabPath + "\n" + exception.Message);
            return null;
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // The FBX uses the MT_ materials (same names as its own materials).
    private static void RemapModel(string fbxPath, CS_MayaPreviewExportMaterial[] data, List<Material> materials, bool staticModel)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("Maya Preview Import : FBX not found (" + fbxPath + ")");
            return;
        }
        bool changed = false;
        if (importer.importCameras) { importer.importCameras = false; changed = true; }
        if (importer.importLights) { importer.importLights = false; changed = true; }
        // Motion (no materials): the animation settings are left to the user.
        if (staticModel && importer.importAnimation) { importer.importAnimation = false; changed = true; }
        if (staticModel && importer.animationType != ModelImporterAnimationType.None) { importer.animationType = ModelImporterAnimationType.None; changed = true; }
        if (!importer.preserveHierarchy) { importer.preserveHierarchy = true; changed = true; }
        if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            changed = true;
        }

        Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> map = importer.GetExternalObjectMap();
        for (int i = 0; i < materials.Count && data != null && i < data.Length; i++)
        {
            if (materials[i] == null || data[i] == null || string.IsNullOrEmpty(data[i].asset)) continue;
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), data[i].asset);
            if (map.TryGetValue(id, out UnityEngine.Object current) && current == materials[i]) continue;
            importer.AddRemap(id, materials[i]);
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    private static void SetupTexture(string path, bool normal, bool linear)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool changed = false;
        if (normal && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; changed = true; }
        if (!normal && linear && importer.sRGBTexture) { importer.sRGBTexture = false; changed = true; }
        if (changed) importer.SaveAndReimport();
    }

    // <Folder>/Material/MT_xxx.mat: made, or updated in place (references stay valid).
    private static Material BuildMaterial(string root, string materialFolder, CS_MayaPreviewExportMaterial data)
    {
        Shader shader = Shader.Find(data.shader);
        if (shader == null)
        {
            Debug.LogWarning("Maya Preview Import : shader \"" + data.shader + "\" not found, using HDRP/Lit (" + data.name + ")");
            shader = Shader.Find("HDRP/Lit");
        }
        string assetName = !string.IsNullOrEmpty(data.asset) ? data.asset : CS_MayaPreviewExport.MaterialAssetName(data.name);
        var material = new Material(shader) { name = assetName };
        if (data.floats != null) foreach (var v in data.floats) if (material.HasProperty(v.prop)) material.SetFloat(v.prop, v.value);
        if (data.ints != null) foreach (var v in data.ints) if (material.HasProperty(v.prop)) material.SetInteger(v.prop, Mathf.RoundToInt(v.value));
        if (data.colors != null) foreach (var v in data.colors) if (material.HasProperty(v.prop)) material.SetColor(v.prop, v.value);
        if (data.vectors != null) foreach (var v in data.vectors) if (material.HasProperty(v.prop)) material.SetVector(v.prop, v.value);
        if (data.textures != null)
        {
            foreach (CS_MayaPreviewExportTexture t in data.textures)
            {
                if (!material.HasProperty(t.prop)) continue;
                Texture texture = !string.IsNullOrEmpty(t.file)
                    ? AssetDatabase.LoadAssetAtPath<Texture>(root + "/" + t.file)
                    : FindProjectTexture(t.textureName);
                if (texture == null)
                {
                    Debug.LogWarning("Maya Preview Import : texture not found " + (string.IsNullOrEmpty(t.file) ? t.textureName : t.file) + " (" + assetName + ")");
                    continue;
                }
                material.SetTexture(t.prop, texture);
                material.SetTextureScale(t.prop, t.scale);
                material.SetTextureOffset(t.prop, t.offset);
            }
        }
        if (data.keywords != null) material.shaderKeywords = data.keywords;
        try { HDMaterial.ValidateMaterial(material); } catch (Exception) { /* not an HDRP shader */ }

        string path = materialFolder + "/" + CS_MayaPreviewExport.SafeName(assetName) + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.shader = material.shader;
            existing.CopyPropertiesFromMaterial(material);
            existing.shaderKeywords = material.shaderKeywords;
            UnityEngine.Object.DestroyImmediate(material);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Texture FindProjectTexture(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Texture>(path);
        }
        return null;
    }
}
