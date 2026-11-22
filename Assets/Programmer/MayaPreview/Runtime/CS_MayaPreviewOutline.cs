using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
+=====================================
 File   : CS_MayaPreviewOutline
 Summary: Outline made in the preview (right click > アウトライン作成).
          A child object "<name>_Outline" draws the same mesh with SHG_Outline
          (Shader Graphs/SHG_Outline: the hull pushed out along the normals, front faces cut),
          so the object gets a line around it. Made like the game-ready character (MT_Mutou_Outline_*):
          - One outline material per part (submesh), each coloured with that part's own texture
            (darkened) and cut by its alpha, so the line blends into each part.
          - The hull (CS_MayaPreviewOutlineHull) is pushed out along normals made from the shape itself,
            one per position, and is closed at open edges so a line starts at the edge. The mesh's own normals are for shading only: normals transferred from a sphere
            (toon face / hair) can point into the surface, which drew double and broken lines.
          - The hull is moved away from the camera by a few line widths (_OutlineDepthPush): the
            silhouette stays, lines on small steps inside (hair strands, mouth corners) are hidden.
          - Transparent parts get no line (the hull would show through them), nor does a part set to
            "nose". Nothing is left out by guessing: lines are erased with the mask and the depth push.
          - A new outline always starts the same (DefaultWorldWidth in metres, the shader's colour /
            blend), whatever the scene or the size of the model.
          - The line follows the part's base colour too (_OutlineTint), not only its texture.
          - Single-sided parts are made double-sided while the outline is on: through a gap
            (an open sleeve, lace) the inside of the hull would show as a dark shell otherwise.
            Restored when the outline is removed.
          Width and colour are set in the Inspector on one settings material (Material), which
          every part follows.
          - Follows the object: same transform, and the hull is made again when Maya edits the mesh
            (live meshes are rewritten in place: the server calls NotifyMeshChanged).
          - Marked internal: never picked or listed in the Hierarchy. The export writes it as a
            child "<name>_Outline" of its object (CS_MayaPreviewExport), with one SHG_Outline
            material per part.
          - Kept between runs: every outline (object key + width, colour, blend, darken, mask file)
            is saved to <persistentDataPath>/MayaPreviewOutlines.json and made again when its
            object is back (Tick). Remove() forgets it; quitting or the object going away does not.
 History: 2026/11/13 New
          2026/11/18 Per-part materials, smoothed normals, transparent / single-sided parts,
                     saved between runs, exported
          2026/11/22 Hull normals from the triangles instead of the shading normals,
                     depth push against lines inside the shape, follows edits of the mesh,
                     base colour on the line
          2026/11/22 Closed at open edges (no floating / broken lines), width limit in pixels
=====================================+
*/
[DisallowMultipleComponent]
public class CS_MayaPreviewOutline : MonoBehaviour
{
    public const string ShaderName = "Shader Graphs/SHG_Outline";
    public const string Suffix = "_Outline";
    private const string WidthProp = "_OutlineWidth";
    private const string ColorProp = "_OutlineColor";
    private const string BlendProp = "_OutlineTexBlend";
    private const string DarkenProp = "_OutlineTexDarken";
    private const string TexProp = "_BaseMap";
    private const string MaskProp = "_OutlineWidthMask";
    private const string PushProp = "_OutlineDepthPush";
    private const string TintProp = "_OutlineTint";
    private const float DefaultDepthPush = 4f;       // in line widths
    private const string MaxPixelsProp = "_OutlineMaxPixels";
    private const float DefaultMaxPixels = 3f;       // of a 1080 high picture
    private const string DoubleSidedProp = "_DoubleSidedEnable";
    private const float DefaultWorldWidth = 0.0025f; // metres
    private static readonly string[] SourceTexProps = { "_BaseMap", "_BaseColorMap", "_MainTex" };
    private static readonly string[] SourceColorProps = { "_BaseColor", "_Color" };
    private static readonly List<Material> s_sourceMaterials = new List<Material>();

    private Renderer _source;
    private Renderer _renderer;
    private Material _material;                                  // settings (shown in the Inspector)
    private readonly List<Material> _parts = new List<Material>();   // one per submesh, follow _material
    private Mesh _meshFrom;                                      // source mesh _outlineMesh was made from
    private Mesh _outlineMesh;                                   // smoothed normals, transparent parts emptied
    private bool[] _skipped = new bool[0];
    private bool _meshChanged;
    private readonly Dictionary<Material, float> _madeDoubleSided = new Dictionary<Material, float>();
    private readonly HashSet<Material> _sidesChecked = new HashSet<Material>();
    private Material[] _partSources = new Material[0];           // source material each part was named after
    private string _key;                                         // KeyOf(target), for the save file
    private string _maskPath = "";                               // file of the width mask ("" = none)
    private Texture2D _ownedMask;                                // mask loaded here when restored

    public Material Material => _material;
    public Renderer Source => _source;
    public Renderer OutlineRenderer => _renderer;
    // Width in world units (the export converts it to the imported model's own scale).
    public float WorldWidth => _source != null ? Width * MetresPerUnit(_source.transform) : Width;

    // =========================================================
    // Saved between runs
    // =========================================================

    [Serializable]
    private class SavedOutline
    {
        public string key;
        public float width;
        public Color color = Color.black;
        public float blend;
        public float darken = 1f;
        public string mask = "";
        public bool hasPush;     // false = saved before the depth push existed: DefaultDepthPush
        public float push;
        public bool hasMaxPixels;
        public float maxPixels;
    }

    [Serializable]
    private class SavedFile
    {
        public int version = 1;
        public List<SavedOutline> outlines = new List<SavedOutline>();
    }

    private const float TickInterval = 0.5f;
    private const string SaveFileName = "MayaPreviewOutlines.json";
    private static SavedFile s_file;
    private static bool s_dirty;
    private static float s_nextTick;
    private static readonly List<CS_MayaPreviewOutline> s_all = new List<CS_MayaPreviewOutline>();
    private static readonly HashSet<string> s_failed = new HashSet<string>();

    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    // "maya:|Grp|MeshShape" for Maya meshes, "scene:Root/Child" for scene objects.
    public static string KeyOf(GameObject target) =>
        CS_MayaPreviewExport.IsLive(target) ? "maya:" + CS_MayaPreviewExport.ShapePath(target)
                                            : "scene:" + CS_MayaPreviewExport.ScenePath(target.transform);

    private static GameObject FindKey(string key)
    {
        if (key.StartsWith("maya:"))
        {
            string path = key.Substring(5);
            foreach (GameObject o in CS_MayaPreviewExport.LiveObjects())
                if (CS_MayaPreviewExport.ShapePath(o) == path) return o;
            return null;
        }
        return key.StartsWith("scene:") ? GameObject.Find(key.Substring(6)) : null;
    }

    private static void LoadFile()
    {
        if (s_file != null) return;
        s_file = new SavedFile();
        try
        {
            if (File.Exists(SavePath)) s_file = JsonUtility.FromJson<SavedFile>(File.ReadAllText(SavePath)) ?? new SavedFile();
        }
        catch (Exception exception) { Debug.LogWarning("Maya Preview Outline : " + SavePath + "\n" + exception.Message); }
        if (s_file.outlines == null) s_file.outlines = new List<SavedOutline>();
    }

    private static void SaveFile()
    {
        s_dirty = false;
        try { File.WriteAllText(SavePath, JsonUtility.ToJson(s_file, true)); }
        catch (Exception exception) { Debug.LogWarning("Maya Preview Outline : " + SavePath + "\n" + exception.Message); }
    }

    // Every frame (CS_MayaPreviewInspector.Update): saves changed settings and makes the saved
    // outlines again once their objects are there (Maya sends its meshes after the start).
    public static void Tick()
    {
        if (Time.unscaledTime < s_nextTick) return;
        s_nextTick = Time.unscaledTime + TickInterval;
        LoadFile();
        foreach (CS_MayaPreviewOutline outline in s_all) outline.Store();
        foreach (SavedOutline saved in s_file.outlines.ToArray())
        {
            if (string.IsNullOrEmpty(saved.key) || s_failed.Contains(saved.key) || s_all.Exists(o => o._key == saved.key)) continue;
            GameObject target = FindKey(saved.key);
            if (target == null || !CanHave(target)) continue;
            if (Create(target, out string _, saved) == null) s_failed.Add(saved.key);
        }
        if (s_dirty) SaveFile();
    }

    private void Store()
    {
        if (_material == null || string.IsNullOrEmpty(_key)) return;
        SavedOutline saved = s_file.outlines.Find(s => s.key == _key);
        if (saved == null) { saved = new SavedOutline { key = _key }; s_file.outlines.Add(saved); s_dirty = true; }
        float blend = _material.HasProperty(BlendProp) ? _material.GetFloat(BlendProp) : 0f;
        float darken = _material.HasProperty(DarkenProp) ? _material.GetFloat(DarkenProp) : 1f;
        float push = _material.HasProperty(PushProp) ? _material.GetFloat(PushProp) : 0f;
        float maxPixels = _material.HasProperty(MaxPixelsProp) ? _material.GetFloat(MaxPixelsProp) : 0f;
        if (saved.width != Width || saved.color != Color || saved.blend != blend || saved.darken != darken || saved.mask != _maskPath ||
            !saved.hasPush || saved.push != push || !saved.hasMaxPixels || saved.maxPixels != maxPixels)
        {
            saved.width = Width; saved.color = Color; saved.blend = blend; saved.darken = darken; saved.mask = _maskPath;
            saved.hasPush = true; saved.push = push;
            saved.hasMaxPixels = true; saved.maxPixels = maxPixels;
            s_dirty = true;
        }
    }

    private void Apply(SavedOutline saved)
    {
        if (_material == null) return;
        if (_material.HasProperty(WidthProp)) _material.SetFloat(WidthProp, Mathf.Max(0f, saved.width));
        if (_material.HasProperty(ColorProp)) _material.SetColor(ColorProp, saved.color);
        if (_material.HasProperty(BlendProp)) _material.SetFloat(BlendProp, saved.blend);
        if (_material.HasProperty(DarkenProp)) _material.SetFloat(DarkenProp, saved.darken);
        if (_material.HasProperty(PushProp)) _material.SetFloat(PushProp, saved.hasPush ? saved.push : DefaultDepthPush);
        if (_material.HasProperty(MaxPixelsProp)) _material.SetFloat(MaxPixelsProp, saved.hasMaxPixels ? saved.maxPixels : DefaultMaxPixels);
        if (!string.IsNullOrEmpty(saved.mask) && File.Exists(saved.mask))
        {
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false) { name = Path.GetFileName(saved.mask) };
                if (texture.LoadImage(File.ReadAllBytes(saved.mask), false))
                {
                    _ownedMask = texture;
                    _maskPath = saved.mask;
                    if (_material.HasProperty(MaskProp)) _material.SetTexture(MaskProp, texture);
                }
                else Destroy(texture);
            }
            catch (Exception exception) { Debug.LogWarning("Maya Preview Outline : " + saved.mask + "\n" + exception.Message); }
        }
    }

    // The server rewrote this mesh in place (an edit in Maya).
    public static void NotifyMeshChanged(Mesh mesh)
    {
        foreach (CS_MayaPreviewOutline outline in s_all)
            if (outline._meshFrom == mesh) outline._meshChanged = true;
    }

    // Removed by the user: forgotten in the save file too.
    public void Remove()
    {
        LoadFile();
        if (s_file.outlines.RemoveAll(s => s.key == _key) > 0) SaveFile();
        Destroy(gameObject);
    }

    // =========================================================
    // Texture files (Inspector / export)
    // =========================================================

    private static CS_MayaPreviewOutline OwnerOf(Material material, out int part)
    {
        part = -1;
        if (material == null) return null;
        foreach (CS_MayaPreviewOutline outline in s_all)
        {
            if (outline._material == material) return outline;
            part = outline._parts.IndexOf(material);
            if (part >= 0) return outline;
        }
        return null;
    }

    // The Inspector set a texture file on a material: remembered when it is an outline's mask.
    public static void NoteTexture(Material material, string prop, string path)
    {
        CS_MayaPreviewOutline outline = OwnerOf(material, out int _);
        if (outline == null || prop != MaskProp) return;
        outline._maskPath = path ?? "";
        if (outline._ownedMask != null && outline._material.GetTexture(MaskProp) != outline._ownedMask)
        {
            Destroy(outline._ownedMask);
            outline._ownedMask = null;
        }
        outline.Refresh();
    }

    // The settings material of an outline made here. Its texture slot means nothing: every part follows its own.
    public static bool IsSettingsMaterial(Material material)
    {
        foreach (CS_MayaPreviewOutline outline in s_all)
            if (outline._material == material) return true;
        return false;
    }

    // The file behind a texture of an outline material (settings or part), or null.
    public static string TexturePathOf(Material material, string prop)
    {
        CS_MayaPreviewOutline outline = OwnerOf(material, out int part);
        if (outline == null) return null;
        if (prop == MaskProp) return outline._maskPath;
        if (prop != TexProp) return null;
        if (part < 0) part = 0;
        if (outline._source == null) return null;
        outline._source.GetSharedMaterials(s_sourceMaterials);
        Material source = part < s_sourceMaterials.Count ? s_sourceMaterials[part] : null;
        string sourceProp = SourceTextureProp(source);
        var library = CS_MayaPreviewMaterialLibrary.Current;
        return sourceProp != null && library != null && library.Owns(source, out string mayaName) ? library.TexturePath(mayaName, sourceProp) : null;
    }

    // World width of an outline part material (> 0), else 0.
    public static float WorldWidthOf(Material material)
    {
        CS_MayaPreviewOutline outline = OwnerOf(material, out int part);
        return outline != null && part >= 0 ? outline.WorldWidth : 0f;
    }

    public float Width
    {
        get => _material != null && _material.HasProperty(WidthProp) ? _material.GetFloat(WidthProp) : 0f;
        set { if (_material != null && _material.HasProperty(WidthProp)) { _material.SetFloat(WidthProp, Mathf.Max(0f, value)); Refresh(); } }
    }

    public Color Color
    {
        get => _material != null && _material.HasProperty(ColorProp) ? _material.GetColor(ColorProp) : Color.black;
        set { if (_material != null && _material.HasProperty(ColorProp)) { _material.SetColor(ColorProp, value); Refresh(); } }
    }

    // The outline of target (a direct child made by Create), or null.
    public static CS_MayaPreviewOutline Of(GameObject target)
    {
        if (target == null) return null;
        foreach (Transform child in target.transform)
            if (child.TryGetComponent(out CS_MayaPreviewOutline outline)) return outline;
        return null;
    }

    public static bool CanHave(GameObject target) =>
        target != null && Of(target) == null && !CS_MayaPreviewInternal.Is(target.transform) &&
        (target.TryGetComponent(out SkinnedMeshRenderer _) || (target.TryGetComponent(out MeshRenderer _) && target.TryGetComponent(out MeshFilter _)));

    // null + error when the shader is not in the player or the object has no mesh.
    public static CS_MayaPreviewOutline Create(GameObject target, out string error) => Create(target, out error, null);

    private static CS_MayaPreviewOutline Create(GameObject target, out string error, SavedOutline saved)
    {
        error = null;
        if (!CanHave(target)) { error = "メッシュがないので、アウトラインを作れません。"; return null; }
        Material template = Template();     // only for HDRP's render state
        Shader shader = template != null ? template.shader : Shader.Find(ShaderName);
        if (shader == null) { error = "SHG_Outline シェーダーが exe に入っていません。"; return null; }

        var holder = new GameObject(target.name + Suffix);
        holder.layer = target.layer;
        holder.AddComponent<CS_MayaPreviewInternal>();
        holder.transform.SetParent(target.transform, false);
        var outline = holder.AddComponent<CS_MayaPreviewOutline>();
        outline._source = target.GetComponent<Renderer>();
        // A copy of an outline material of the scene keeps HDRP's render state (queue, depth,
        // keywords); a bare new Material(shader) drew a grey hull over the object.
        outline._material = template != null ? new Material(template) : new Material(shader);
        outline._material.name = "MT_Outline_" + target.name;
        try { HDMaterial.ValidateMaterial(outline._material); } catch (System.Exception) { /* not HDRP */ }
        // The width mask of the copied material belongs to another model: full width everywhere.
        if (outline._material.HasProperty(MaskProp)) outline._material.SetTexture(MaskProp, Texture2D.whiteTexture);
        if (outline._material.HasProperty(PushProp)) outline._material.SetFloat(PushProp, DefaultDepthPush);
        if (outline._material.HasProperty(MaxPixelsProp)) outline._material.SetFloat(MaxPixelsProp, DefaultMaxPixels);
        if (outline._source is SkinnedMeshRenderer)
            outline._renderer = holder.AddComponent<SkinnedMeshRenderer>();
        else
        {
            holder.AddComponent<MeshFilter>();
            outline._renderer = holder.AddComponent<MeshRenderer>();
        }
        outline._renderer.shadowCastingMode = ShadowCastingMode.Off;
        outline._renderer.receiveShadows = false;

        outline.Width = DefaultWorldWidth / Mathf.Max(1e-6f, MetresPerUnit(target.transform));
        SetToDefault(outline._material, BlendProp);
        SetToDefault(outline._material, DarkenProp);
        int color = shader.FindPropertyIndex(ColorProp);
        if (color >= 0) outline.Color = shader.GetPropertyDefaultVectorValue(color);
        if (saved != null) outline.Apply(saved);
        outline._key = KeyOf(target);
        s_all.Add(outline);
        outline.Sync(true);
        LoadFile();
        outline.Store();
        if (s_dirty) SaveFile();
        return outline;
    }

    // An outline material of the scene, or null. Only its render state is used, none of its values.
    private static Material Template()
    {
        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
        {
            if (renderer.GetComponent<CS_MayaPreviewOutline>() != null) continue;   // one made here
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.shader != null && material.shader.name == ShaderName) return material;
        }
        return null;
    }

    private static void SetToDefault(Material material, string prop)
    {
        int index = material.shader.FindPropertyIndex(prop);
        if (index >= 0) material.SetFloat(prop, material.shader.GetPropertyDefaultFloatValue(index));
    }

    // Metres in one object unit. Meshes from Maya are centimetres whatever MayaScaler shows.
    private static float MetresPerUnit(Transform t)
    {
        if (!CS_MayaPreviewExport.IsLive(t.gameObject)) return MaxScale(t);
        Vector3 s = t.localScale;
        return CS_MayaPreviewExport.MayaUnitsToMetres * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
    }

    private static float MaxScale(Transform t)
    {
        Vector3 s = t.lossyScale;
        return Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
    }

    private static bool IsTransparent(Material material) =>
        material != null && (material.renderQueue > (int)RenderQueue.GeometryLast ||
                             (material.HasProperty("_SurfaceType") && material.GetFloat("_SurfaceType") > 0.5f));

    private static string SourceTextureProp(Material material)
    {
        if (material == null) return null;
        foreach (string prop in SourceTexProps)
            if (material.HasProperty(prop) && material.GetTexture(prop) != null) return prop;
        return null;
    }

    private static Texture SourceTexture(Material material)
    {
        string prop = SourceTextureProp(material);
        return prop != null ? material.GetTexture(prop) : null;
    }

    // "Outline_<material of the part>" (the export makes MT_<type>_Outline_<material> of it).
    private static string PartName(Material source, int index, string fallback)
    {
        if (source == null) return fallback + "_" + index;
        var library = CS_MayaPreviewMaterialLibrary.Current;
        string name = library != null && library.Owns(source, out string mayaName) ? mayaName : source.name.Replace(" (Instance)", "");
        if (name.StartsWith(CS_MayaPreviewExport.MaterialPrefix, StringComparison.OrdinalIgnoreCase)) name = name.Substring(CS_MayaPreviewExport.MaterialPrefix.Length);
        return "Outline_" + name;
    }

    private static Color SourceColor(Material material)
    {
        if (material != null)
            foreach (string prop in SourceColorProps)
                if (material.HasProperty(prop)) return material.GetColor(prop);
        return Color.white;
    }

    private Mesh SourceMesh()
    {
        if (_source is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
        return _source != null && _source.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
    }

    // The hull of the source mesh, with the source's attributes on every vertex of it (the band's too).
    // null when the mesh cannot be read (then the source is used as is).
    private static Mesh BuildOutlineMesh(Mesh source, bool[] skip)
    {
        if (source == null || !source.isReadable) return null;
        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;
        var parts = new int[source.subMeshCount][];
        for (int s = 0; s < parts.Length; s++)
            parts[s] = source.GetTopology(s) == MeshTopology.Triangles ? source.GetTriangles(s) : new int[0];
        CS_MayaPreviewOutlineHull hull = CS_MayaPreviewOutlineHull.Build(vertices, parts, skip, normals);

        var mesh = new Mesh { name = source.name + Suffix, hideFlags = HideFlags.DontSave };
        mesh.indexFormat = hull.positions.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.vertices = hull.positions;
        mesh.normals = hull.directions;
        Vector4[] tangents = source.tangents;
        if (tangents.Length == vertices.Length) mesh.tangents = Copied(tangents, hull.from);
        Vector2[] uv = source.uv;
        if (uv.Length == vertices.Length) mesh.uv = Copied(uv, hull.from);
        mesh.uv2 = hull.band;
        Color[] colors = source.colors;
        if (colors.Length == vertices.Length) mesh.colors = Copied(colors, hull.from);
        BoneWeight[] weights = source.boneWeights;
        if (weights.Length == vertices.Length)
        {
            mesh.boneWeights = Copied(weights, hull.from);
            mesh.bindposes = source.bindposes;
        }
        mesh.subMeshCount = parts.Length;
        for (int s = 0; s < parts.Length; s++) mesh.SetTriangles(hull.triangles[s], s, false);

        var moved = new Vector3[vertices.Length];
        var movedNormals = new Vector3[vertices.Length];
        var movedTangents = new Vector3[vertices.Length];
        for (int shape = 0; shape < source.blendShapeCount; shape++)
            for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
            {
                source.GetBlendShapeFrameVertices(shape, frame, moved, movedNormals, movedTangents);
                mesh.AddBlendShapeFrame(source.GetBlendShapeName(shape), source.GetBlendShapeFrameWeight(shape, frame),
                                        Copied(moved, hull.from), Copied(movedNormals, hull.from), Copied(movedTangents, hull.from));
            }
        mesh.bounds = source.bounds;
        return mesh;
    }

    private static T[] Copied<T>(T[] values, int[] from)
    {
        var result = new T[from.Length];
        for (int i = 0; i < from.Length; i++) result[i] = values[from[i]];
        return result;
    }

    // Takes over the mesh (Maya sends a new one on every edit), the parts' textures and show / hide.
    private void Sync(bool force)
    {
        if (_source == null || _renderer == null || _material == null) return;
        Mesh mesh = SourceMesh();
        _source.GetSharedMaterials(s_sourceMaterials);
        int count = Mathf.Max(1, mesh != null ? mesh.subMeshCount : 1);

        bool skipChanged = _skipped.Length != count;
        if (skipChanged) _skipped = new bool[count];
        for (int i = 0; i < count; i++)
        {
            Material source = i < s_sourceMaterials.Count ? s_sourceMaterials[i] : null;
            bool skip = IsTransparent(source) || CS_MayaPreviewMeshData.PartOf(source) == CS_MayaPreviewMeshData.PartNose;
            if (_skipped[i] != skip) { _skipped[i] = skip; skipChanged = true; }
        }

        if (force || skipChanged || _meshChanged || mesh != _meshFrom)
        {
            _meshChanged = false;
            if (_outlineMesh != null) Destroy(_outlineMesh);
            _outlineMesh = BuildOutlineMesh(mesh, _skipped);
            _meshFrom = mesh;
            Mesh use = _outlineMesh != null ? _outlineMesh : mesh;
            if (_renderer is SkinnedMeshRenderer target && _source is SkinnedMeshRenderer skinned)
            {
                target.sharedMesh = use;
                target.bones = skinned.bones;
                target.rootBone = skinned.rootBone;
            }
            else if (TryGetComponent(out MeshFilter filter)) filter.sharedMesh = use;

            while (_parts.Count < count)
            {
                var part = new Material(_material) { name = _material.name + "_" + _parts.Count };
                _parts.Add(part);
            }
            while (_parts.Count > count)
            {
                Destroy(_parts[_parts.Count - 1]);
                _parts.RemoveAt(_parts.Count - 1);
            }
            _renderer.sharedMaterials = _parts.ToArray();
        }

        MakeDoubleSided();
        Refresh();
        _renderer.enabled = _source.enabled;
    }

    // Every part follows the settings material, coloured with its own part's texture
    // (or, without a texture, the part's colour darkened the same way).
    private void Refresh()
    {
        if (_material == null || _source == null) return;
        _source.GetSharedMaterials(s_sourceMaterials);
        float blend = _material.HasProperty(BlendProp) ? _material.GetFloat(BlendProp) : 0f;
        float darken = _material.HasProperty(DarkenProp) ? _material.GetFloat(DarkenProp) : 1f;
        Color color = _material.HasProperty(ColorProp) ? _material.GetColor(ColorProp) : Color.black;
        float width = _material.HasProperty(WidthProp) ? _material.GetFloat(WidthProp) : 0f;
        Texture mask = _material.HasProperty(MaskProp) ? _material.GetTexture(MaskProp) : null;
        float push = _material.HasProperty(PushProp) ? _material.GetFloat(PushProp) : 0f;
        float maxPixels = _material.HasProperty(MaxPixelsProp) ? _material.GetFloat(MaxPixelsProp) : 0f;
        Texture first = null;
        if (_partSources.Length != _parts.Count) _partSources = new Material[_parts.Count];
        for (int i = 0; i < _parts.Count; i++)
        {
            Material part = _parts[i];
            if (part == null) continue;
            Material source = i < s_sourceMaterials.Count ? s_sourceMaterials[i] : null;
            if (_partSources[i] != source || part.name.Length == 0)
            {
                _partSources[i] = source;
                part.name = PartName(source, i, _material.name);
            }
            Texture texture = SourceTexture(source);
            if (first == null) first = texture;
            if (part.HasProperty(WidthProp)) part.SetFloat(WidthProp, width);
            if (part.HasProperty(DarkenProp)) part.SetFloat(DarkenProp, darken);
            if (part.HasProperty(MaskProp)) part.SetTexture(MaskProp, mask);
            if (part.HasProperty(PushProp)) part.SetFloat(PushProp, push);
            if (part.HasProperty(MaxPixelsProp)) part.SetFloat(MaxPixelsProp, maxPixels);
            if (part.HasProperty(TexProp)) part.SetTexture(TexProp, texture);
            if (part.HasProperty(TintProp)) part.SetColor(TintProp, texture != null ? SourceColor(source) : Color.white);
            if (texture != null)
            {
                if (part.HasProperty(BlendProp)) part.SetFloat(BlendProp, blend);
                if (part.HasProperty(ColorProp)) part.SetColor(ColorProp, color);
            }
            else
            {
                Color tint = SourceColor(source) * darken;
                tint.a = 1f;
                if (part.HasProperty(BlendProp)) part.SetFloat(BlendProp, 0f);
                if (part.HasProperty(ColorProp)) part.SetColor(ColorProp, Color.Lerp(color, tint, Mathf.Clamp01(blend)));
            }
        }
        // The settings material shows the texture of the first part (its own is not drawn).
        if (_material.HasProperty(TexProp)) _material.SetTexture(TexProp, first);
    }

    // Opaque single-sided parts (HDRP/Lit default) become double-sided, once per material.
    private void MakeDoubleSided()
    {
        foreach (Material material in s_sourceMaterials)
        {
            if (material == null || !_sidesChecked.Add(material)) continue;
            if (IsTransparent(material) || !material.HasProperty(DoubleSidedProp)) continue;
            float before = material.GetFloat(DoubleSidedProp);
            if (before > 0.5f) continue;
            _madeDoubleSided[material] = before;
            material.SetFloat(DoubleSidedProp, 1f);
            try { HDMaterial.ValidateMaterial(material); } catch (System.Exception) { /* not HDRP */ }
        }
    }

    private void LateUpdate()
    {
        if (_source == null) { Destroy(gameObject); return; }
        Sync(false);
    }

    private void OnDestroy()
    {
        s_all.Remove(this);
        if (_ownedMask != null) Destroy(_ownedMask);
        foreach (KeyValuePair<Material, float> pair in _madeDoubleSided)
        {
            if (pair.Key == null || pair.Key.GetFloat(DoubleSidedProp) < 0.5f) continue;
            pair.Key.SetFloat(DoubleSidedProp, pair.Value);
            try { HDMaterial.ValidateMaterial(pair.Key); } catch (System.Exception) { /* not HDRP */ }
        }
        foreach (Material part in _parts) if (part != null) Destroy(part);
        if (_outlineMesh != null) Destroy(_outlineMesh);
        if (_material != null) Destroy(_material);
    }
}
