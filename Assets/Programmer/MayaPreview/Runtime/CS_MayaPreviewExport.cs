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
 Summary: Export from the preview player, for import into the Unity project.
          <root>/<name>/
            export.mpx.json   manifest: hierarchy, transforms, materials (shader + every value)
            meshes/*.mpxmesh  meshes (binary, Unity space, metres)
            textures/*        texture files used by the materials (copied)
          - Maya meshes are already frozen (world space vertices); the Unity side offset
            moved in the preview is kept as the object's transform.
          - Scene objects keep their Unity transforms; skinned meshes are baked (static).
          - Load() reads an export back: Maya material settings and offsets are restored,
            so shader work can continue from that export.
          The Editor importer (CSE_MayaPreviewExportImporter) turns the folder into
          meshes, materials and a prefab.
 History: 2026/10/11 New
=====================================+
*/

[Serializable]
public class CS_MayaPreviewExportManifest
{
    public int version = 1;
    public string name;
    public string exportedAt;
    public string units = "1 unit = 1 m (Maya cm x 0.01)";
    public CS_MayaPreviewExportNode[] nodes;
    public CS_MayaPreviewExportMaterial[] materials;
    public CS_MayaPreviewExportEntry[] entries;
}

[Serializable]
public class CS_MayaPreviewExportNode
{
    public string name;
    public int parent = -1;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;
    public string mesh;          // meshes/xxx.mpxmesh ("" = no mesh)
    public int[] materials;      // indices into manifest.materials, one per submesh
    public string mayaPath;      // Maya shape path for Maya meshes ("" for scene objects)
}

[Serializable]
public class CS_MayaPreviewExportMaterial
{
    public string name;
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
    public string file;          // textures/xxx.png ("" = not copied, see textureName)
    public string textureName;   // texture asset name (the importer can look it up in the project)
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
// Mesh file (.mpxmesh)
// =========================================================

public static class CS_MayaPreviewMeshIO
{
    private const int Version = 1;
    private const int HasNormals = 1, HasTangents = 2, HasUv0 = 4, HasUv1 = 8, HasColors = 16;

    public static void Write(Mesh mesh, string path, Matrix4x4 transform, string name = null)
    {
        bool identity = transform == Matrix4x4.identity;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        Vector2[] uv0 = mesh.uv, uv1 = mesh.uv2;
        Color[] colors = mesh.colors;
        int count = vertices.Length;
        if (!identity)
        {
            Matrix4x4 normalMatrix = transform.inverse.transpose;
            for (int i = 0; i < count; i++) vertices[i] = transform.MultiplyPoint3x4(vertices[i]);
            if (normals.Length == count)
                for (int i = 0; i < count; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            if (tangents.Length == count)
                for (int i = 0; i < count; i++)
                {
                    Vector3 t = transform.MultiplyVector(tangents[i]).normalized;
                    tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
                }
        }
        int flags = (normals.Length == count ? HasNormals : 0) | (tangents.Length == count ? HasTangents : 0)
            | (uv0.Length == count ? HasUv0 : 0) | (uv1.Length == count ? HasUv1 : 0) | (colors.Length == count ? HasColors : 0);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("MPXM"));
            writer.Write(Version);
            writer.Write(name ?? mesh.name ?? "");
            writer.Write(count);
            writer.Write(flags);
            foreach (Vector3 v in vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
            if ((flags & HasNormals) != 0) foreach (Vector3 v in normals) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
            if ((flags & HasTangents) != 0) foreach (Vector4 v in tangents) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); writer.Write(v.w); }
            if ((flags & HasUv0) != 0) foreach (Vector2 v in uv0) { writer.Write(v.x); writer.Write(v.y); }
            if ((flags & HasUv1) != 0) foreach (Vector2 v in uv1) { writer.Write(v.x); writer.Write(v.y); }
            if ((flags & HasColors) != 0) foreach (Color v in colors) { writer.Write(v.r); writer.Write(v.g); writer.Write(v.b); writer.Write(v.a); }
            writer.Write(mesh.subMeshCount);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] indices = mesh.GetIndices(s);
                writer.Write((int)mesh.GetTopology(s));
                writer.Write(indices.Length);
                foreach (int index in indices) writer.Write(index);
            }
        }
    }

    public static Mesh Read(string path)
    {
        using (var reader = new BinaryReader(File.OpenRead(path)))
        {
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MPXM") throw new InvalidDataException("Not a .mpxmesh file: " + path);
            int version = reader.ReadInt32();
            if (version > Version) throw new InvalidDataException("Newer .mpxmesh version " + version);
            var mesh = new Mesh { name = reader.ReadString() };
            int count = reader.ReadInt32();
            int flags = reader.ReadInt32();
            mesh.indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            var vertices = new Vector3[count];
            for (int i = 0; i < count; i++) vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            mesh.vertices = vertices;
            if ((flags & HasNormals) != 0)
            {
                var normals = new Vector3[count];
                for (int i = 0; i < count; i++) normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                mesh.normals = normals;
            }
            if ((flags & HasTangents) != 0)
            {
                var tangents = new Vector4[count];
                for (int i = 0; i < count; i++) tangents[i] = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                mesh.tangents = tangents;
            }
            if ((flags & HasUv0) != 0)
            {
                var uv = new Vector2[count];
                for (int i = 0; i < count; i++) uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                mesh.uv = uv;
            }
            if ((flags & HasUv1) != 0)
            {
                var uv = new Vector2[count];
                for (int i = 0; i < count; i++) uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                mesh.uv2 = uv;
            }
            if ((flags & HasColors) != 0)
            {
                var colors = new Color[count];
                for (int i = 0; i < count; i++) colors[i] = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                mesh.colors = colors;
            }
            int subMeshCount = reader.ReadInt32();
            mesh.subMeshCount = subMeshCount;
            for (int s = 0; s < subMeshCount; s++)
            {
                var topology = (MeshTopology)reader.ReadInt32();
                int length = reader.ReadInt32();
                var indices = new int[length];
                for (int i = 0; i < length; i++) indices[i] = reader.ReadInt32();
                mesh.SetIndices(indices, topology, s, false);
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}


// =========================================================
// Export / load
// =========================================================

public static class CS_MayaPreviewExport
{
    public const string ManifestFile = "export.mpx.json";
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
        public string folder;
        public string name;
        public DateTime time;
    }


    // =========================================================
    // Paths
    // =========================================================

    // <Maya project>/unityPreview/exports, or Exports beside the exe without a Maya session.
    public static string DefaultRoot()
    {
        var library = CS_MayaPreviewMaterialLibrary.Current;
        if (library != null && !string.IsNullOrEmpty(library.Folder))
        {
            string parent = Path.GetDirectoryName(library.Folder.TrimEnd('/', '\\'));
            if (!string.IsNullOrEmpty(parent)) return (parent + "/exports").Replace('\\', '/');
        }
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Exports")).Replace('\\', '/');
    }

    public static List<Saved> FindExports(string root)
    {
        var result = new List<Saved>();
        try
        {
            if (!Directory.Exists(root)) return result;
            foreach (string folder in Directory.GetDirectories(root))
            {
                string manifest = Path.Combine(folder, ManifestFile);
                if (!File.Exists(manifest)) continue;
                result.Add(new Saved
                {
                    folder = folder.Replace('\\', '/'),
                    name = Path.GetFileName(folder),
                    time = File.GetLastWriteTime(manifest)
                });
            }
        }
        catch (Exception exception) { Debug.LogWarning("Maya Preview Export : " + exception.Message); }
        result.Sort((a, b) => b.time.CompareTo(a.time));
        return result;
    }

    public static string SafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "Export";
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim().Replace(' ', '_');
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
        public string folder;
        public readonly List<CS_MayaPreviewExportNode> nodes = new List<CS_MayaPreviewExportNode>();
        public readonly List<CS_MayaPreviewExportMaterial> materials = new List<CS_MayaPreviewExportMaterial>();
        public readonly Dictionary<Material, int> materialIndex = new Dictionary<Material, int>();
        public readonly Dictionary<string, string> copiedTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> usedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> warnings = new List<string>();
        public int meshCount;
    }

    public static string Export(IList<Item> items, string name, string root)
    {
        if (items == null || items.Count == 0) return "Nothing to export: drag objects from the Hierarchy first.";
        name = SafeName(name);
        string folder = Path.Combine(root, name).Replace('\\', '/');
        var w = new Writer { folder = folder };
        try
        {
            Directory.CreateDirectory(folder);
            ClearFolder(Path.Combine(folder, "meshes"));
            ClearFolder(Path.Combine(folder, "textures"));
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
                    AddMesh(w, item.target, -1, LastSegment(TransformPath(ShapePath(item.target))), true);
                    entries.Add(new CS_MayaPreviewExportEntry { label = item.label, mayaMesh = ShapePath(item.target) });
                }
                else if (item.target != null)
                {
                    AddScene(w, item.target.transform, -1, true);
                    entries.Add(new CS_MayaPreviewExportEntry { label = item.label, scenePath = ScenePath(item.target.transform) });
                }
            }

            var manifest = new CS_MayaPreviewExportManifest
            {
                name = name,
                exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                nodes = w.nodes.ToArray(),
                materials = w.materials.ToArray(),
                entries = entries.ToArray()
            };
            File.WriteAllText(Path.Combine(folder, ManifestFile), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return "Export failed: " + exception.Message;
        }

        string message = "Exported " + w.nodes.Count + " objects, " + w.meshCount + " meshes, " + w.materials.Count +
                         " materials, " + w.copiedTextures.Count + " textures\n" + folder;
        if (w.warnings.Count > 0) message += "\n" + string.Join("\n", w.warnings);
        Debug.Log("Maya Preview Export : " + message);
        return message;
    }

    private static void ClearFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
    }

    private static int AddNode(Writer w, string name, int parent, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        w.nodes.Add(new CS_MayaPreviewExportNode
        {
            name = name, parent = parent, position = position, rotation = rotation, scale = scale,
            mesh = "", materials = new int[0], mayaPath = ""
        });
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
        groups[groupPath] = AddNode(w, LastSegment(groupPath), -1, Vector3.zero, Quaternion.identity, Vector3.one);

        int Group(string path)
        {
            if (groups.TryGetValue(path, out int index)) return index;
            int bar = path.LastIndexOf('|');
            int parent = bar > 0 && path.Length > groupPath.Length ? Group(path.Substring(0, bar)) : groups[groupPath];
            index = AddNode(w, LastSegment(path), parent, Vector3.zero, Quaternion.identity, Vector3.one);
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
            AddMesh(w, o, parent, LastSegment(path), true);
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
                w.warnings.Add("Skinned mesh baked as a static mesh: " + o.name);
            }
            catch (Exception exception) { w.warnings.Add("Could not bake " + o.name + ": " + exception.Message); }
        }
        else
        {
            var filter = o.GetComponent<MeshFilter>();
            mesh = filter != null ? filter.sharedMesh : null;
        }

        try
        {
            if (mesh == null) return;
            if (!mesh.isReadable) { w.warnings.Add("Mesh is not readable (enable Read/Write on the model): " + o.name); return; }
            string file = UniqueFile(w, "meshes", SafeName(o.name.StartsWith(LivePrefix) ? node.name : o.name), ".mpxmesh");
            CS_MayaPreviewMeshIO.Write(mesh, Path.Combine(w.folder, file), toNode, Path.GetFileNameWithoutExtension(file));
            node.mesh = file;
            w.meshCount++;

            Material[] shared = renderer.sharedMaterials;
            var indices = new int[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = AddMaterial(w, shared.Length > 0 ? shared[Mathf.Min(i, shared.Length - 1)] : null);
            node.materials = indices;
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
        index = w.materials.Count - 1;
        w.materialIndex[material] = index;
        return index;
    }

    private static string CopyTexture(Writer w, string source, Texture texture)
    {
        try
        {
            if (!string.IsNullOrEmpty(source) && File.Exists(source))
            {
                if (w.copiedTextures.TryGetValue(source, out string done)) return done;
                string file = UniqueFile(w, "textures", SafeName(Path.GetFileNameWithoutExtension(source)), Path.GetExtension(source).ToLowerInvariant());
                Directory.CreateDirectory(Path.Combine(w.folder, "textures"));
                File.Copy(source, Path.Combine(w.folder, file), true);
                w.copiedTextures[source] = file;
                return file;
            }
            if (texture is Texture2D texture2D && texture2D.isReadable)
            {
                string key = "#" + texture.GetInstanceID();
                if (w.copiedTextures.TryGetValue(key, out string done)) return done;
                string file = UniqueFile(w, "textures", SafeName(texture.name), ".png");
                Directory.CreateDirectory(Path.Combine(w.folder, "textures"));
                File.WriteAllBytes(Path.Combine(w.folder, file), texture2D.EncodeToPNG());
                w.copiedTextures[key] = file;
                return file;
            }
        }
        catch (Exception exception) { w.warnings.Add("Texture " + texture.name + ": " + exception.Message); }
        return "";   // the importer looks the texture up in the project by name
    }


    // =========================================================
    // Load (continue shader work from an export)
    // =========================================================

    public static string Load(string folder, List<Item> items)
    {
        string path = Path.Combine(folder, ManifestFile);
        CS_MayaPreviewExportManifest manifest;
        try { manifest = JsonUtility.FromJson<CS_MayaPreviewExportManifest>(File.ReadAllText(path)); }
        catch (Exception exception) { return "Could not read " + path + "\n" + exception.Message; }
        if (manifest == null) return "Could not read " + path;

        int restoredMaterials = 0, movedObjects = 0;
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
        return "Loaded " + manifest.name + ": " + restoredMaterials + " Maya materials, " + movedObjects + " offsets";
    }
}
