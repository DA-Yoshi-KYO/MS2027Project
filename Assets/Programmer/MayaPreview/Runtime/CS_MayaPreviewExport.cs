using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

/*
+=====================================
 File   : CS_MayaPreviewExport
 Summary: Export from the preview player straight into the Unity project.
          <project>/Assets/Designer, one sub folder per export type (Character, Background ...):
            Mesh/<type>/<name>.fbx                hierarchy + meshes (FBX, Maya convention)
            Texture/<type>/*                      texture files of the Maya materials (copied)
            Material/<type>/MT_*.mat              made by the Unity editor when it gets focus
            Prefab/<type>/<name>.prefab           (CSE_MayaPreviewExportImporter)
            Json/<type>/<name>.mpx.json           materials (shader + every value), offsets, list,
                                                  type and the Maya scene (.mb) it came from
          - The types are read from Programmer/MayaPreview/Runtime/MayaPreviewExportTypes.json
            (folder, prefab yes/no, materials yes/no), so they change without a new build.
          - When Maya opens a scene that was exported before, its last export is restored
            (material settings, offsets, list, type and name) and Export asks to overwrite it.
          - Material names always start with "MT_" ("Skin" -> "MT_Skin").
          - Maya meshes are already frozen (world space vertices); the Unity side offset
            moved in the preview is kept as the object's transform.
          - Scene objects keep their Unity transforms; skinned meshes are baked (static).
          - Load() reads an export back: Maya material settings and offsets are restored,
            so shader work can continue from that export.
 History: 2026/10/11 New
          2026/10/11 FBX, written into the project (Assets/Designer), MT_ material names
          2026/11/09 Export types, folders per type, prefab, restore / overwrite per Maya scene
=====================================+
*/

[Serializable]
public class CS_MayaPreviewExportType
{
    public string id;            // folder name ("Character")
    public string label;         // shown in the panel
    public bool prefab = true;   // make Prefab/<id>/<name>.prefab
    public bool materials = true;// make materials + copy textures
    public string fbxFolder;     // "" = Mesh/<id>
}

[Serializable]
public class CS_MayaPreviewExportTypeList
{
    public CS_MayaPreviewExportNaming naming;
    public CS_MayaPreviewExportType[] types;
}

// File name rules. {type} = type id ("Character"), {name} = the object name in UpperCamel.
[Serializable]
public class CS_MayaPreviewExportNaming
{
    public string material = "MT_{type}_{name}";
    public string fbx = "{name}";
    public string prefab = "{name}";
}

// A name changed in the preview Inspector (Maya objects keep their Maya path).
[Serializable]
public class CS_MayaPreviewExportRename
{
    public string path;          // Maya transform / group path
    public string name;
}

[Serializable]
public class CS_MayaPreviewExportManifest
{
    public int version = 3;
    public string name;
    public string type;          // export type id ("Character"); "" for old exports
    public string scene;         // Maya scene (.mb) the export came from
    public string exportedAt;
    public string units = "FBX: centimetres, Y up (Maya). Transforms below: Unity metres";
    public string fbx;           // "Mesh/<type>/<name>.fbx", relative to Assets/Designer
    public string materialFolder;// "Material/<type>" ("" = old "Material")
    public string prefab;        // "Prefab/<type>/<name>.prefab" ("" = no prefab)
    public bool buildMaterials = true;
    public CS_MayaPreviewExportNode[] nodes;
    public CS_MayaPreviewExportMaterial[] materials;
    public CS_MayaPreviewExportEntry[] entries;
    public CS_MayaPreviewExportRename[] renames;
}

[Serializable]
public class CS_MayaPreviewExportNode
{
    public string name;
    public int parent = -1;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;
    public string mesh;          // mesh name in the FBX ("" = empty object)
    public int[] materials;      // indices into manifest.materials, one per submesh
    public string mayaPath;      // Maya shape path for Maya meshes ("" for scene objects)
}

[Serializable]
public class CS_MayaPreviewExportMaterial
{
    public string name;          // name in the preview (Maya material name for Maya materials)
    public string asset;         // material asset / FBX material name ("MT_...")
    public string shader;
    public bool maya;
    public string mayaSettings;  // preview settings (shader + changed values) for Load()
    public int renderQueue;
    public string[] keywords;
    public CS_MayaPreviewExportFloat[] floats;
    public CS_MayaPreviewExportFloat[] ints;
    public CS_MayaPreviewExportColor[] colors;
    public CS_MayaPreviewExportVector[] vectors;
    public CS_MayaPreviewExportTexture[] textures;
}

[Serializable] public class CS_MayaPreviewExportFloat { public string prop; public float value; }
[Serializable] public class CS_MayaPreviewExportColor { public string prop; public Color value; }
[Serializable] public class CS_MayaPreviewExportVector { public string prop; public Vector4 value; }

[Serializable]
public class CS_MayaPreviewExportTexture
{
    public string prop;
    public string file;          // "Texture/xxx.png" relative to the export folder ("" = use textureName)
    public string textureName;   // texture asset name (looked up in the project when not copied)
    public string source;        // original file path
    public bool normal;
    public bool linear;
    public Vector2 scale = Vector2.one;
    public Vector2 offset;
}

[Serializable]
public class CS_MayaPreviewExportEntry
{
    public string label;
    public string mayaGroup;     // Maya group path ("|Chibi_GRP|Chibi_Hat")
    public string mayaMesh;      // Maya shape path of a single Maya mesh
    public string scenePath;     // scene object path ("Chara_Test/Chara")
}


// =========================================================
// Export / load
// =========================================================

public static class CS_MayaPreviewExport
{
    public const string ManifestFolder = "MayaPreview/Exports";   // old exports (before types)
    public const string ManifestExtension = ".mpx.json";
    public const string MeshFolder = "Mesh";
    public const string MaterialFolder = "Material";
    public const string TextureFolder = "Texture";
    public const string PrefabFolder = "Prefab";
    public const string JsonFolder = "Json";
    public const string MaterialPrefix = "MT_";
    public const string ProjectFile = "MayaPreviewProject.txt";   // written next to the player by the build
    public const string TypesFile = "Programmer/MayaPreview/Runtime/MayaPreviewExportTypes.json";   // in Assets
    private const string LivePrefix = "MayaLive_";
    private const string ModelRootName = "Maya_Model_Root";

    // One row of the export list.
    public class Item
    {
        public string label;
        public string mayaGroup;   // Maya group path
        public GameObject target;  // Maya mesh or scene object
        public CS_MayaPreviewKind kind;

        public bool Same(Item other) =>
            other != null && (mayaGroup != null ? mayaGroup == other.mayaGroup : other.mayaGroup == null && target == other.target);
    }

    public struct Saved
    {
        public string file;        // manifest path
        public string name;
        public string type;        // "" for old exports
        public string scene;       // Maya scene it came from ("" = unknown)
        public DateTime time;
    }


    // =========================================================
    // Paths
    // =========================================================

    // The Unity project's Assets folder, in this order:
    //   1. the folder chosen in the Export panel (PlayerPrefs)
    //   2. MayaPreviewProject.txt written next to the player by the build
    //   3. a Unity project above the exe's folder (the exe was built into the project)
    private const string ProjectPrefsKey = "MayaPreview.ProjectAssets";
    private static string s_projectAssets;
    private static float s_projectCheckTime = -100f;

    public static string ProjectAssets()
    {
        if (Application.isEditor) return Normalize(Application.dataPath);
        // Checked at most every 2 s (the panel asks every frame).
        if (Time.unscaledTime - s_projectCheckTime < 2f) return s_projectAssets;
        s_projectCheckTime = Time.unscaledTime;
        s_projectAssets = FindProjectAssets();
        return s_projectAssets;
    }

    private static string FindProjectAssets()
    {
        try
        {
            string chosen = PlayerPrefs.GetString(ProjectPrefsKey, "");
            if (IsAssetsFolder(chosen)) return Normalize(chosen);

            string file = Path.Combine(Application.streamingAssetsPath, ProjectFile);
            if (File.Exists(file))
            {
                string path = File.ReadAllText(file).Trim();
                if (IsAssetsFolder(path)) return Normalize(path);
            }

            for (DirectoryInfo dir = new DirectoryInfo(Application.dataPath).Parent; dir != null; dir = dir.Parent)
            {
                string assets = Path.Combine(dir.FullName, "Assets");
                if (IsAssetsFolder(assets)) return Normalize(assets);
            }
        }
        catch (Exception exception) { Debug.LogWarning("Maya Preview Export : " + exception.Message); }
        return null;
    }

    // <project>/Assets of a Unity project (ProjectSettings next to it).
    private static bool IsAssetsFolder(string assets)
    {
        if (string.IsNullOrEmpty(assets) || !Directory.Exists(assets)) return false;
        string project = Path.GetDirectoryName(Normalize(assets));
        return project != null && Directory.Exists(Path.Combine(project, "ProjectSettings"));
    }

    // Export panel: the user points at the project (the project folder or its Assets folder).
    public static bool SetProjectFolder(string path)
    {
        path = Normalize((path ?? "").Trim().Trim('"'));
        if (string.IsNullOrEmpty(path)) return false;
        string assets = IsAssetsFolder(path) ? path : Normalize(Path.Combine(path, "Assets"));
        if (!IsAssetsFolder(assets)) return false;
        PlayerPrefs.SetString(ProjectPrefsKey, assets);
        PlayerPrefs.Save();
        s_projectCheckTime = -100f;
        return true;
    }

    // <project>/Assets/Designer
    public static string ProjectFolder()
    {
        string assets = ProjectAssets();
        return assets != null ? assets + "/Designer" : null;
    }

    public static string Normalize(string path) => (path ?? "").Trim().Replace('\\', '/').TrimEnd('/');


    // =========================================================
    // Export types (MayaPreviewExportTypes.json)
    // =========================================================

    private static CS_MayaPreviewExportType[] s_types;
    private static DateTime s_typesTime;

    private static CS_MayaPreviewExportType[] DefaultTypes() => new[]
    {
        new CS_MayaPreviewExportType { id = "Character",  label = "キャラクター", prefab = true,  materials = true,  fbxFolder = "" },
        new CS_MayaPreviewExportType { id = "Background", label = "背景",        prefab = true,  materials = true,  fbxFolder = "" },
        new CS_MayaPreviewExportType { id = "Effect",     label = "エフェクト",      prefab = true,  materials = true,  fbxFolder = "" },
        new CS_MayaPreviewExportType { id = "Motion",     label = "モーション",      prefab = false, materials = false, fbxFolder = "Motion" },
    };

    // Read again when the file changes (no rebuild needed).
    public static CS_MayaPreviewExportType[] Types()
    {
        string assets = ProjectAssets();
        string file = assets != null ? assets + "/" + TypesFile : null;
        try
        {
            if (file != null && File.Exists(file))
            {
                DateTime time = File.GetLastWriteTimeUtc(file);
                if (s_types != null && time == s_typesTime) return s_types;
                var list = JsonUtility.FromJson<CS_MayaPreviewExportTypeList>(File.ReadAllText(file));
                var valid = new List<CS_MayaPreviewExportType>();
                if (list?.types != null)
                    foreach (CS_MayaPreviewExportType t in list.types)
                        if (t != null && !string.IsNullOrWhiteSpace(t.id))
                        {
                            t.id = SafeName(t.id);
                            if (string.IsNullOrWhiteSpace(t.label)) t.label = t.id;
                            valid.Add(t);
                        }
                if (valid.Count > 0)
                {
                    s_naming = list.naming ?? new CS_MayaPreviewExportNaming();
                    s_types = valid.ToArray();
                    s_typesTime = time;
                    return s_types;
                }
            }
        }
        catch (Exception exception) { Debug.LogWarning("Maya Preview Export : " + file + "\n" + exception.Message); }
        if (s_types == null || s_typesTime != default) { s_types = DefaultTypes(); s_naming = new CS_MayaPreviewExportNaming(); s_typesTime = default; }
        return s_types;
    }

    private static CS_MayaPreviewExportNaming s_naming = new CS_MayaPreviewExportNaming();

    public static CS_MayaPreviewExportNaming Naming
    {
        get { Types(); return s_naming; }
    }

    // "chara_test" -> "CharaTest", "outline" -> "Outline" (project rule: UpperCamel)
    public static string UpperCamel(string name)
    {
        var result = new StringBuilder();
        foreach (string part in (name ?? "").Split(new[] { '_', '-', ' ', '.', '|', ':' }, StringSplitOptions.RemoveEmptyEntries))
            result.Append(char.ToUpperInvariant(part[0])).Append(part, 1, part.Length - 1);
        return result.Length > 0 ? SafeName(result.ToString()) : "Export";
    }

    public static string ApplyRule(string rule, CS_MayaPreviewExportType type, string name)
    {
        if (string.IsNullOrWhiteSpace(rule)) rule = "{name}";
        return SafeName(rule.Replace("{type}", type != null ? type.id : "").Replace("{name}", UpperCamel(name)).Replace("__", "_").Trim('_'));
    }

    // Export name (fbx / prefab / json) of the first object in the list.
    public static string ExportName(CS_MayaPreviewExportType type, string objectName) => ApplyRule(Naming.fbx, type, objectName);
    public static string PrefabName(CS_MayaPreviewExportType type, string objectName) => ApplyRule(Naming.prefab, type, objectName);

    // "Skin" / "MT_Skin" / "MT_Character_Skin" -> "MT_Character_Skin" (rule: MT_{type}_{name})
    public static string MaterialAssetName(string name, CS_MayaPreviewExportType type)
    {
        name = (name ?? "").Replace(" (Instance)", "").Trim();
        if (name.StartsWith(MaterialPrefix, StringComparison.OrdinalIgnoreCase)) name = name.Substring(MaterialPrefix.Length);
        if (type != null && name.StartsWith(type.id + "_", StringComparison.OrdinalIgnoreCase)) name = name.Substring(type.id.Length + 1);
        return ApplyRule(Naming.material, type, name);
    }


    // =========================================================
    // Names changed in the Inspector
    // =========================================================

    private static readonly Dictionary<string, string> s_renames = new Dictionary<string, string>();

    // Maya transform / group path -> shown name (the Maya name unless renamed)
    public static string NameOf(string path) =>
        path != null && s_renames.TryGetValue(path, out string name) ? name : LastSegment(path ?? "");

    public static void Rename(string path, string name)
    {
        if (string.IsNullOrEmpty(path)) return;
        name = (name ?? "").Trim();
        if (name.Length == 0 || name == LastSegment(path)) s_renames.Remove(path);
        else s_renames[path] = name;
    }

    private static CS_MayaPreviewExportRename[] RenameList()
    {
        var list = new List<CS_MayaPreviewExportRename>();
        foreach (KeyValuePair<string, string> pair in s_renames) list.Add(new CS_MayaPreviewExportRename { path = pair.Key, name = pair.Value });
        return list.ToArray();
    }

    public static CS_MayaPreviewExportType FindType(string id)
    {
        foreach (CS_MayaPreviewExportType t in Types())
            if (string.Equals(t.id, id, StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }

    public static string FbxFile(CS_MayaPreviewExportType type, string name) =>
        (string.IsNullOrWhiteSpace(type.fbxFolder) ? MeshFolder + "/" + type.id : Normalize(type.fbxFolder).Trim('/')) + "/" + SafeName(name) + ".fbx";

    public static string ManifestPath(string root, string type, string name) =>
        Normalize(root) + "/" + JsonFolder + "/" + SafeName(type) + "/" + SafeName(name) + ManifestExtension;


    // =========================================================
    // Maya scene (sent by Maya in SESSION): restore its last export
    // =========================================================

    public static string Scene { get; private set; } = "";
    public static string PendingRestore { get; private set; }   // manifest to restore once the meshes are in
    public static float SceneTime { get; private set; }

    // saved: Maya saved the scene (Save As): same objects, only the path changed.
    public static void SetScene(string scene, bool saved)
    {
        scene = Normalize(scene);
        if (string.Equals(scene, Scene, StringComparison.OrdinalIgnoreCase)) return;
        Scene = scene;
        if (saved) return;
        SceneTime = Time.unscaledTime;
        s_renames.Clear();   // another scene: its renames come back with its export
        string root = ProjectFolder();
        PendingRestore = root != null && scene.Length > 0 && FindByScene(FindExports(root), scene, out Saved previous) ? previous.file : null;
    }

    public static void ClearPendingRestore() => PendingRestore = null;

    public static bool SameScene(string a, string b) =>
        !string.IsNullOrEmpty(a) && string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    // The newest export made from this scene.
    public static bool FindByScene(List<Saved> exports, string scene, out Saved found)
    {
        foreach (Saved saved in exports)   // newest first
            if (SameScene(saved.scene, scene)) { found = saved; return true; }
        found = default;
        return false;
    }

    private static readonly Dictionary<string, (DateTime time, string type, string scene)> s_infoCache =
        new Dictionary<string, (DateTime, string, string)>(StringComparer.OrdinalIgnoreCase);

    // Assets/Designer/Json/<type>/*.mpx.json (and old MayaPreview/Exports), newest first.
    public static List<Saved> FindExports(string root)
    {
        var result = new List<Saved>();
        if (string.IsNullOrEmpty(root)) return result;
        foreach (string folder in new[] { Normalize(root) + "/" + JsonFolder, Normalize(root) + "/" + ManifestFolder })
        {
            try
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.GetFiles(folder, "*" + ManifestExtension, SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file);
                    var saved = new Saved
                    {
                        file = file.Replace('\\', '/'),
                        name = name.Substring(0, name.Length - ManifestExtension.Length),
                        type = "",
                        scene = "",
                        time = File.GetLastWriteTime(file)
                    };
                    // The panel scans every 2 s: read each manifest again only when it changed.
                    if (!s_infoCache.TryGetValue(saved.file, out (DateTime time, string type, string scene) info) || info.time != saved.time)
                    {
                        CS_MayaPreviewExportManifest manifest = ReadManifest(saved.file);
                        info = (saved.time, manifest?.type ?? "", manifest?.scene ?? "");
                        s_infoCache[saved.file] = info;
                    }
                    saved.type = info.type;
                    saved.scene = info.scene;
                    result.Add(saved);
                }
            }
            catch (Exception exception) { Debug.LogWarning("Maya Preview Export : " + exception.Message); }
        }
        result.Sort((a, b) => b.time.CompareTo(a.time));
        return result;
    }

    public static CS_MayaPreviewExportManifest ReadManifest(string file)
    {
        try { return JsonUtility.FromJson<CS_MayaPreviewExportManifest>(File.ReadAllText(file)); }
        catch (Exception) { return null; }
    }

    public static string SafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "Export";
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim().Replace(' ', '_');
    }

    // "Skin" -> "MT_Skin", "mt_Skin" -> "MT_Skin", "MT_Skin" stays.
    public static string MaterialAssetName(string name)
    {
        name = SafeName((name ?? "").Replace(" (Instance)", ""));
        if (name.StartsWith(MaterialPrefix, StringComparison.OrdinalIgnoreCase)) return MaterialPrefix + name.Substring(MaterialPrefix.Length);
        return MaterialPrefix + name;
    }


    // =========================================================
    // Maya objects
    // =========================================================

    public static Transform ModelRoot()
    {
        var root = GameObject.Find(ModelRootName);
        return root != null ? root.transform : null;
    }

    public static bool IsLive(GameObject o) => o != null && o.name.StartsWith(LivePrefix);

    // "MayaLive_|Chibi_GRP|Chibi_Head|Chibi_HeadShape" -> "|Chibi_GRP|Chibi_Head|Chibi_HeadShape"
    public static string ShapePath(GameObject o) => IsLive(o) ? o.name.Substring(LivePrefix.Length) : "";

    // Shape path -> transform path ("|Chibi_GRP|Chibi_Head")
    public static string TransformPath(string shapePath)
    {
        int bar = shapePath.LastIndexOf('|');
        return bar > 0 ? shapePath.Substring(0, bar) : shapePath;
    }

    public static string LastSegment(string path)
    {
        int bar = path.LastIndexOf('|');
        return bar >= 0 ? path.Substring(bar + 1) : path;
    }

    public static List<GameObject> LiveObjects()
    {
        var list = new List<GameObject>();
        Transform root = ModelRoot();
        if (root == null) return list;
        foreach (Transform child in root)
            if (IsLive(child.gameObject) && !CS_MayaPreviewSelection.IsDeleted(child.gameObject)) list.Add(child.gameObject);
        return list;
    }

    public static List<GameObject> LiveObjectsIn(string groupPath)
    {
        var list = new List<GameObject>();
        foreach (GameObject o in LiveObjects())
        {
            string path = TransformPath(ShapePath(o));
            if (path == groupPath || path.StartsWith(groupPath + "|")) list.Add(o);
        }
        return list;
    }

    public static string ScenePath(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent) parts.Insert(0, t.name);
        return string.Join("/", parts);
    }


    // =========================================================
    // Export
    // =========================================================

    private class Writer
    {
        public string root;
        public string textureFolder;   // "Texture/<type>"; null = do not copy textures
        public CS_MayaPreviewExportType type;
        public readonly List<CS_MayaPreviewExportNode> nodes = new List<CS_MayaPreviewExportNode>();
        public readonly List<CS_MayaPreviewFbxNode> fbx = new List<CS_MayaPreviewFbxNode>();
        public readonly List<CS_MayaPreviewExportMaterial> materials = new List<CS_MayaPreviewExportMaterial>();
        public readonly List<Color> materialColors = new List<Color>();
        public readonly Dictionary<Material, int> materialIndex = new Dictionary<Material, int>();
        public readonly HashSet<string> materialAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> renamed = new List<string>();
        public readonly Dictionary<string, string> copiedTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> usedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> warnings = new List<string>();
        public int meshCount;
    }

    // root: <project>/Assets/Designer. Files go to the folders of the type (see the file header).
    public static string Export(IList<Item> items, string name, string root, CS_MayaPreviewExportType type)
    {
        if (items == null || items.Count == 0) return "書き出すものがありません。Hierarchy からオブジェクトをドラッグしてください。";
        if (string.IsNullOrWhiteSpace(root)) return "Unity プロジェクトが見つかりません（MayaPreviewProject.txt）。プロジェクトから exe をビルドしてください。";
        if (type == null) return "種類（キャラクター、背景など）を選んでください。";
        name = SafeName(name);
        root = Normalize(root);
        var w = new Writer { root = root, textureFolder = type.materials ? TextureFolder + "/" + type.id : null };
        string fbxFile = FbxFile(type, name);
        w.type = type;
        try
        {
            var entries = new List<CS_MayaPreviewExportEntry>();
            foreach (Item item in items)
            {
                if (item.mayaGroup != null)
                {
                    AddMayaGroup(w, item.mayaGroup);
                    entries.Add(new CS_MayaPreviewExportEntry { label = item.label, mayaGroup = item.mayaGroup });
                }
                else if (item.target != null && IsLive(item.target))
                {
                    AddMesh(w, item.target, -1, NameOf(TransformPath(ShapePath(item.target))), true);
                    entries.Add(new CS_MayaPreviewExportEntry { label = item.label, mayaMesh = ShapePath(item.target) });
                }
                else if (item.target != null)
                {
                    AddScene(w, item.target.transform, -1, true);
                    entries.Add(new CS_MayaPreviewExportEntry { label = item.label, scenePath = ScenePath(item.target.transform) });
                }
            }
            if (w.meshCount == 0)
                return "書き出せるメッシュがありません。" + (w.warnings.Count > 0 ? "\n" + string.Join("\n", w.warnings) : "");

            var materialNames = new List<string>();
            foreach (CS_MayaPreviewExportMaterial material in w.materials) materialNames.Add(material.asset);
            CS_MayaPreviewFbx.Write(root + "/" + fbxFile, w.fbx, materialNames, w.materialColors, "MS2027 Maya Preview");

            var manifest = new CS_MayaPreviewExportManifest
            {
                name = name,
                type = type.id,
                scene = Scene,
                exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                fbx = fbxFile,
                materialFolder = type.materials ? MaterialFolder + "/" + type.id : "",
                prefab = type.prefab ? PrefabFolder + "/" + type.id + "/" + PrefabName(type, name) + ".prefab" : "",
                buildMaterials = type.materials,
                nodes = w.nodes.ToArray(),
                materials = w.materials.ToArray(),
                entries = entries.ToArray(),
                renames = RenameList()
            };
            string manifestPath = ManifestPath(root, type.id, name);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return "書き出しに失敗しました: " + exception.Message;
        }

        string message = "書き出しました（" + type.label + " / " + name + "）: メッシュ " + w.meshCount +
                         (type.materials ? "、マテリアル " + w.materials.Count + "、テクスチャ " + w.copiedTextures.Count : "") +
                         "\n" + fbxFile;
        if (type.materials && w.renamed.Count > 0) message += "\n名前をルールに合わせました: " + string.Join(", ", w.renamed);
        if (type.materials || type.prefab)
            message += "\nUnity エディタに戻ると " + (type.materials ? MaterialFolder + "/" + type.id : "") +
                       (type.prefab ? (type.materials ? " と " : "") + PrefabFolder + "/" + type.id + "/" + PrefabName(type, name) + ".prefab" : "") +
                       " ができます。";
        if (string.IsNullOrEmpty(Scene)) message += "\nMaya のシーンが未保存なので、mb とは紐付いていません。";
        if (w.warnings.Count > 0) message += "\n" + string.Join("\n", w.warnings);
        Debug.Log("Maya Preview Export : " + message);
        return message;
    }

    private static int AddNode(Writer w, string name, int parent, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        w.nodes.Add(new CS_MayaPreviewExportNode
        {
            name = name, parent = parent, position = position, rotation = rotation, scale = scale,
            mesh = "", materials = new int[0], mayaPath = ""
        });
        w.fbx.Add(new CS_MayaPreviewFbxNode { name = name, parent = parent, position = position, rotation = rotation, scale = scale });
        return w.nodes.Count - 1;
    }

    // Maya group: the group and every Maya mesh below it, with the Maya hierarchy as empty objects.
    private static void AddMayaGroup(Writer w, string groupPath)
    {
        List<GameObject> members = LiveObjectsIn(groupPath);
        var parents = new HashSet<string>();
        foreach (GameObject o in members)
        {
            string path = TransformPath(ShapePath(o));
            for (int bar = path.LastIndexOf('|'); bar > 0; bar = path.LastIndexOf('|', bar - 1))
                parents.Add(path.Substring(0, bar));
        }
        var groups = new Dictionary<string, int>();
        groups[groupPath] = AddNode(w, NameOf(groupPath), -1, Vector3.zero, Quaternion.identity, Vector3.one);

        int Group(string path)
        {
            if (groups.TryGetValue(path, out int index)) return index;
            int bar = path.LastIndexOf('|');
            int parent = bar > 0 && path.Length > groupPath.Length ? Group(path.Substring(0, bar)) : groups[groupPath];
            index = AddNode(w, NameOf(path), parent, Vector3.zero, Quaternion.identity, Vector3.one);
            groups[path] = index;
            return index;
        }

        members.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (GameObject o in members)
        {
            string path = TransformPath(ShapePath(o));
            int bar = path.LastIndexOf('|');
            // A transform that is also the parent of other members gets its own group.
            int parent = parents.Contains(path) ? Group(path)
                : path == groupPath ? groups[groupPath]
                : Group(path.Substring(0, bar));
            AddMesh(w, o, parent, NameOf(path), true);
        }
    }

    private static void AddMesh(Writer w, GameObject o, int parent, string name, bool live)
    {
        Transform t = o.transform;
        int index = AddNode(w, name, parent, t.localPosition, t.localRotation, t.localScale);
        w.nodes[index].mayaPath = live ? ShapePath(o) : "";
        WriteRenderer(w, index, o);
    }

    // Scene object: the object (with its world transform) and the children that render something.
    private static void AddScene(Writer w, Transform t, int parent, bool top)
    {
        if (CS_MayaPreviewInternal.Is(t)) return;
        if (!HasRenderer(t) && !top) return;
        int index = top
            ? AddNode(w, t.name, parent, t.position, t.rotation, t.lossyScale)
            : AddNode(w, t.name, parent, t.localPosition, t.localRotation, t.localScale);
        WriteRenderer(w, index, t.gameObject);
        foreach (Transform child in t) AddScene(w, child, index, false);
    }

    private static bool HasRenderer(Transform t)
    {
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            if (CS_MayaPreviewSelection.Selectable(r)) return true;
        return false;
    }

    private static void WriteRenderer(Writer w, int index, GameObject o)
    {
        Renderer renderer = o.GetComponent<Renderer>();
        if (renderer == null || !CS_MayaPreviewSelection.Selectable(renderer)) return;
        CS_MayaPreviewExportNode node = w.nodes[index];
        Mesh mesh = null;
        Mesh baked = null;
        Matrix4x4 toNode = Matrix4x4.identity;
        if (renderer is SkinnedMeshRenderer skinned)
        {
            baked = new Mesh { name = skinned.sharedMesh != null ? skinned.sharedMesh.name : o.name };
            try
            {
                skinned.BakeMesh(baked, true);
                Transform t = skinned.transform;
                Matrix4x4 unscaled = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
                Matrix4x4 scaled = t.localToWorldMatrix;
                Bounds target = skinned.bounds;
                Matrix4x4 toWorld = Fit(baked, unscaled, target) <= Fit(baked, scaled, target) ? unscaled : scaled;
                toNode = t.worldToLocalMatrix * toWorld;
                mesh = baked;
                w.warnings.Add("スキンメッシュは動かない形で書き出しました: " + o.name);
            }
            catch (Exception exception) { w.warnings.Add("形を固定できませんでした " + o.name + ": " + exception.Message); }
        }
        else
        {
            var filter = o.GetComponent<MeshFilter>();
            mesh = filter != null ? filter.sharedMesh : null;
        }

        try
        {
            if (mesh == null) return;
            if (!mesh.isReadable) { w.warnings.Add("メッシュを読めません（モデルの Read/Write を有効に）: " + o.name); return; }
            CS_MayaPreviewFbxMesh data = CS_MayaPreviewFbxMesh.From(mesh, toNode, node.name);
            Material[] shared = renderer.sharedMaterials;
            var indices = new int[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = AddMaterial(w, shared.Length > 0 ? shared[Mathf.Min(i, shared.Length - 1)] : null);
            data.materials = indices;
            w.fbx[index].mesh = data;
            node.mesh = node.name;
            node.materials = indices;
            w.meshCount++;
        }
        finally { if (baked != null) UnityEngine.Object.Destroy(baked); }
    }

    private static float Fit(Mesh mesh, Matrix4x4 matrix, Bounds target)
    {
        Bounds local = mesh.bounds;
        Vector3 c = matrix.MultiplyPoint3x4(local.center);
        Vector3 e = matrix.MultiplyVector(local.extents);
        return (c - target.center).magnitude + Mathf.Abs(e.magnitude - target.extents.magnitude);
    }

    private static string UniqueFile(Writer w, string sub, string baseName, string extension)
    {
        string file = sub + "/" + baseName + extension;
        for (int i = 2; w.usedFiles.Contains(file); i++) file = sub + "/" + baseName + "_" + i + extension;
        w.usedFiles.Add(file);
        return file;
    }

    private static int AddMaterial(Writer w, Material material)
    {
        if (material == null) return -1;
        if (w.materialIndex.TryGetValue(material, out int index)) return index;

        var library = CS_MayaPreviewMaterialLibrary.Current;
        string mayaName = null;
        bool maya = library != null && library.Owns(material, out mayaName);
        Shader shader = material.shader;
        var data = new CS_MayaPreviewExportMaterial
        {
            name = maya ? mayaName : material.name.Replace(" (Instance)", ""),
            shader = shader.name,
            maya = maya,
            mayaSettings = maya ? library.Snapshot(mayaName) : "",
            renderQueue = material.renderQueue,
            keywords = material.shaderKeywords
        };

        // Asset name: always "MT_...", unique in this export.
        string asset = MaterialAssetName(data.name, w.type);
        string unique = asset;
        for (int n = 2; !w.materialAssets.Add(unique); n++) unique = asset + "_" + n;
        data.asset = unique;
        if (unique != data.name) w.renamed.Add(data.name + " -> " + unique);

        var floats = new List<CS_MayaPreviewExportFloat>();
        var ints = new List<CS_MayaPreviewExportFloat>();
        var colors = new List<CS_MayaPreviewExportColor>();
        var vectors = new List<CS_MayaPreviewExportVector>();
        var textures = new List<CS_MayaPreviewExportTexture>();
        int count = shader.GetPropertyCount();
        for (int p = 0; p < count; p++)
        {
            string prop = shader.GetPropertyName(p);
            switch (shader.GetPropertyType(p))
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    floats.Add(new CS_MayaPreviewExportFloat { prop = prop, value = material.GetFloat(prop) });
                    break;
                case ShaderPropertyType.Int:
                    ints.Add(new CS_MayaPreviewExportFloat { prop = prop, value = material.GetInteger(prop) });
                    break;
                case ShaderPropertyType.Color:
                    colors.Add(new CS_MayaPreviewExportColor { prop = prop, value = material.GetColor(prop) });
                    break;
                case ShaderPropertyType.Vector:
                    vectors.Add(new CS_MayaPreviewExportVector { prop = prop, value = material.GetVector(prop) });
                    break;
                case ShaderPropertyType.Texture:
                    if (shader.GetPropertyTextureDimension(p) != TextureDimension.Tex2D) break;
                    Texture texture = material.GetTexture(prop);
                    if (texture == null) break;
                    bool normal = maya ? library.IsNormalProp(mayaName, prop) : (shader.GetPropertyFlags(p) & ShaderPropertyFlags.Normal) != 0;
                    bool linear = maya ? library.IsLinearProp(mayaName, prop) : normal;
                    var entry = new CS_MayaPreviewExportTexture
                    {
                        prop = prop, textureName = texture.name, normal = normal, linear = linear,
                        scale = material.GetTextureScale(prop), offset = material.GetTextureOffset(prop),
                        source = maya ? library.SourcePath(mayaName, prop) : "", file = ""
                    };
                    entry.file = CopyTexture(w, entry.source, texture);
                    textures.Add(entry);
                    break;
            }
        }
        data.floats = floats.ToArray();
        data.ints = ints.ToArray();
        data.colors = colors.ToArray();
        data.vectors = vectors.ToArray();
        data.textures = textures.ToArray();
        w.materials.Add(data);
        w.materialColors.Add(material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
            : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white);
        index = w.materials.Count - 1;
        w.materialIndex[material] = index;
        return index;
    }

    // Texture files of Maya materials go to <Folder>/Texture. Textures that are already in the
    // project (scene materials) are found by name in the editor and not copied.
    private static string CopyTexture(Writer w, string source, Texture texture)
    {
        try
        {
            if (w.textureFolder == null || string.IsNullOrEmpty(source) || !File.Exists(source)) return "";
            if (w.copiedTextures.TryGetValue(source, out string done)) return done;
            string file = UniqueFile(w, w.textureFolder,SafeName(Path.GetFileNameWithoutExtension(source)), Path.GetExtension(source).ToLowerInvariant());
            string target = w.root + "/" + file;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (!SameFile(source, target)) File.Copy(source, target, true);
            w.copiedTextures[source] = file;
            return file;
        }
        catch (Exception exception) { w.warnings.Add("テクスチャ " + (texture != null ? texture.name : source) + ": " + exception.Message); }
        return "";
    }

    // Unchanged copies are skipped (no reimport in Unity).
    private static bool SameFile(string source, string target)
    {
        if (!File.Exists(target)) return false;
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) return true;
        var a = new FileInfo(source);
        var b = new FileInfo(target);
        return a.Length == b.Length && a.LastWriteTimeUtc == b.LastWriteTimeUtc;
    }


    // =========================================================
    // Load (continue shader work from an export)
    // =========================================================

    // file: a .mpx.json manifest (an old export folder also works).
    public static string Load(string file, List<Item> items)
    {
        string path = Directory.Exists(file) ? Path.Combine(file, "export.mpx.json") : file;
        CS_MayaPreviewExportManifest manifest;
        try { manifest = JsonUtility.FromJson<CS_MayaPreviewExportManifest>(File.ReadAllText(path)); }
        catch (Exception exception) { return "読み込めませんでした " + path + "\n" + exception.Message; }
        if (manifest == null) return "読み込めませんでした " + path;

        int restoredMaterials = 0, movedObjects = 0;
        s_renames.Clear();
        if (manifest.renames != null)
            foreach (CS_MayaPreviewExportRename rename in manifest.renames)
                if (rename != null) Rename(rename.path, rename.name);
        var library = CS_MayaPreviewMaterialLibrary.Current;
        if (library != null && manifest.materials != null)
        {
            foreach (CS_MayaPreviewExportMaterial material in manifest.materials)
            {
                if (material == null || !material.maya || string.IsNullOrEmpty(material.name)) continue;
                CS_MayaPreviewUndo.RecordMayaMaterial(library, material.name, "Load export", false);
                library.Restore(material.name, material.mayaSettings);
                CS_MayaPreviewUndo.Close();
                restoredMaterials++;
            }
        }

        Transform root = ModelRoot();
        if (root != null && manifest.nodes != null)
        {
            foreach (CS_MayaPreviewExportNode node in manifest.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.mayaPath)) continue;
                Transform live = root.Find(LivePrefix + node.mayaPath);
                if (live == null) continue;
                live.localPosition = node.position;
                live.localRotation = node.rotation;
                live.localScale = node.scale;
                movedObjects++;
            }
        }

        if (items != null && manifest.entries != null)
        {
            items.Clear();
            foreach (CS_MayaPreviewExportEntry entry in manifest.entries)
            {
                if (entry == null) continue;
                if (!string.IsNullOrEmpty(entry.mayaGroup))
                    items.Add(new Item { label = entry.label, mayaGroup = entry.mayaGroup, kind = CS_MayaPreviewKind.MayaGroup });
                else if (!string.IsNullOrEmpty(entry.mayaMesh) && root != null)
                {
                    Transform live = root.Find(LivePrefix + entry.mayaMesh);
                    if (live != null && !CS_MayaPreviewSelection.IsDeleted(live.gameObject)) items.Add(new Item { label = entry.label, target = live.gameObject, kind = CS_MayaPreviewKind.MayaMesh });
                }
                else if (!string.IsNullOrEmpty(entry.scenePath))
                {
                    GameObject found = GameObject.Find(entry.scenePath);
                    if (found != null) items.Add(new Item { label = entry.label, target = found, kind = CS_MayaPreviewIcons.KindOf(found.transform) });
                }
            }
        }
        return "マテリアル " + restoredMaterials + "、位置 " + movedObjects + " を戻しました";
    }
}
