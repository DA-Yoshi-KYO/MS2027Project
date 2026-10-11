using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/*
+=====================================
 File   : CSE_MayaPreviewExportImporter
 Summary: Builds Unity assets from a Maya preview export (CS_MayaPreviewExport).
          - Copy / drop the export folder anywhere under Assets: when export.mpx.json is
            imported, <folder>/Generated (meshes, materials) and <folder>/<name>.prefab are made.
          - Tools > Maya Preview > Import Export Folder... copies a folder from disk into
            Assets/MayaPreviewImports/<name> and builds it.
          - Rebuilding keeps the same assets (references in scenes stay valid).
 History: 2026/10/11 New
=====================================+
*/
public class CSE_MayaPreviewExportImporter : AssetPostprocessor
{
    private const string ImportRoot = "Assets/MayaPreviewImports";
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
        if (!path.EndsWith("/" + CS_MayaPreviewExport.ManifestFile, StringComparison.OrdinalIgnoreCase)) return;
        string folder = Path.GetDirectoryName(path).Replace('\\', '/');
        if (s_pending.Add(folder) && s_pending.Count == 1) EditorApplication.delayCall += Flush;
    }

    private static void Flush()
    {
        var folders = new List<string>(s_pending);
        s_pending.Clear();
        foreach (string folder in folders)
        {
            try { Build(folder); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }


    // =========================================================
    // Menus
    // =========================================================

    [MenuItem("Tools/Maya Preview/Import Export Folder...")]
    private static void ImportFolder()
    {
        string source = EditorUtility.OpenFolderPanel("Maya Preview export folder", "", "");
        if (string.IsNullOrEmpty(source)) return;
        if (!File.Exists(Path.Combine(source, CS_MayaPreviewExport.ManifestFile)))
        {
            EditorUtility.DisplayDialog("Maya Preview", CS_MayaPreviewExport.ManifestFile + " was not found in\n" + source, "OK");
            return;
        }
        string name = Path.GetFileName(source.TrimEnd('/', '\\'));
        string target = ImportRoot + "/" + name;
        string full = Path.GetFullPath(target);
        if (Directory.Exists(full) && !EditorUtility.DisplayDialog("Maya Preview", target + " exists. Update it?", "Update", "Cancel")) return;
        CopyFolder(source, full);
        AssetDatabase.Refresh();
        Build(target);
    }

    [MenuItem("Assets/Maya Preview/Rebuild Prefab From Export", true)]
    private static bool RebuildValidate()
    {
        string folder = SelectedFolder();
        return folder != null && File.Exists(Path.Combine(folder, CS_MayaPreviewExport.ManifestFile));
    }

    [MenuItem("Assets/Maya Preview/Rebuild Prefab From Export")]
    private static void Rebuild()
    {
        string folder = SelectedFolder();
        if (folder != null) Build(folder);
    }

    private static string SelectedFolder()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path)) return null;
        if (!AssetDatabase.IsValidFolder(path)) path = Path.GetDirectoryName(path).Replace('\\', '/');
        return path;
    }

    private static void CopyFolder(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (string directory in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(directory);
            if (name == "Generated") continue;
            CopyFolder(directory, Path.Combine(target, name));
        }
    }


    // =========================================================
    // Build
    // =========================================================

    public static void Build(string folder)
    {
        string manifestPath = folder + "/" + CS_MayaPreviewExport.ManifestFile;
        var manifest = JsonUtility.FromJson<CS_MayaPreviewExportManifest>(File.ReadAllText(manifestPath));
        if (manifest == null || manifest.nodes == null) { Debug.LogError("Maya Preview Import : invalid " + manifestPath); return; }

        string generated = folder + "/Generated";
        EnsureFolder(generated + "/Meshes");
        EnsureFolder(generated + "/Materials");

        // Textures: import settings first (normal maps / linear data).
        if (manifest.materials != null)
        {
            foreach (CS_MayaPreviewExportMaterial material in manifest.materials)
            {
                if (material?.textures == null) continue;
                foreach (CS_MayaPreviewExportTexture texture in material.textures)
                    if (!string.IsNullOrEmpty(texture.file)) SetupTexture(folder + "/" + texture.file, texture.normal, texture.linear);
            }
        }

        // Materials
        var materials = new List<Material>();
        if (manifest.materials != null)
            foreach (CS_MayaPreviewExportMaterial data in manifest.materials)
                materials.Add(data != null ? BuildMaterial(folder, generated, data) : null);

        // Hierarchy + meshes
        string prefabName = CS_MayaPreviewExport.SafeName(manifest.name);
        var root = new GameObject(prefabName);
        try
        {
            var objects = new GameObject[manifest.nodes.Length];
            var usedMeshNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < manifest.nodes.Length; i++)
            {
                CS_MayaPreviewExportNode node = manifest.nodes[i];
                var go = new GameObject(node.name);
                Transform parent = node.parent >= 0 && node.parent < i ? objects[node.parent].transform : root.transform;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = node.position;
                go.transform.localRotation = node.rotation;
                go.transform.localScale = node.scale;
                objects[i] = go;

                if (string.IsNullOrEmpty(node.mesh)) continue;
                string meshFile = folder + "/" + node.mesh;
                if (!File.Exists(meshFile)) { Debug.LogWarning("Maya Preview Import : missing " + meshFile); continue; }
                string meshName = CS_MayaPreviewExport.SafeName(Path.GetFileNameWithoutExtension(node.mesh));
                for (int n = 2; !usedMeshNames.Add(meshName); n++) meshName = meshName + "_" + n;
                Mesh mesh = SaveMesh(CS_MayaPreviewMeshIO.Read(meshFile), generated + "/Meshes/" + meshName + ".asset");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                var slots = new Material[node.materials != null ? node.materials.Length : 0];
                for (int s = 0; s < slots.Length; s++)
                    slots[s] = node.materials[s] >= 0 && node.materials[s] < materials.Count ? materials[node.materials[s]] : null;
                renderer.sharedMaterials = slots;
            }

            string prefabPath = folder + "/" + prefabName + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Maya Preview Import : built " + prefabPath + " (" + manifest.nodes.Length + " objects, " +
                      materials.Count + " materials)", prefab);
            EditorGUIUtility.PingObject(prefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
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

    private static Material BuildMaterial(string folder, string generated, CS_MayaPreviewExportMaterial data)
    {
        Shader shader = Shader.Find(data.shader);
        if (shader == null)
        {
            Debug.LogWarning("Maya Preview Import : shader \"" + data.shader + "\" not found, using HDRP/Lit (" + data.name + ")");
            shader = Shader.Find("HDRP/Lit");
        }
        var material = new Material(shader) { name = data.name };
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
                    ? AssetDatabase.LoadAssetAtPath<Texture>(folder + "/" + t.file)
                    : FindProjectTexture(t.textureName);
                if (texture == null) { Debug.LogWarning("Maya Preview Import : texture not found " + (t.file ?? t.textureName)); continue; }
                material.SetTexture(t.prop, texture);
                material.SetTextureScale(t.prop, t.scale);
                material.SetTextureOffset(t.prop, t.offset);
            }
        }
        if (data.keywords != null) material.shaderKeywords = data.keywords;
        try { HDMaterial.ValidateMaterial(material); } catch (Exception) { /* not an HDRP shader */ }

        string path = generated + "/Materials/" + CS_MayaPreviewExport.SafeName(data.name) + ".mat";
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
