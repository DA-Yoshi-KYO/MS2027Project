using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/*
+=====================================
 File   : CS_MayaPreviewSelection
 Summary: Selection for the preview player.
          - One or more objects (a Maya group selects all of its meshes).
          - Click in the view picks any MeshRenderer or SkinnedMeshRenderer. A character
            (Animator) is selected as a whole first; clicking it again selects the part.
          - Every renderer under the selection gets Maya-style yellow edges, three pixels
            wide at any zoom (skinned meshes are baked once at selection time).
            ShowOutlines turns them off (the setting is kept between runs).
          - Objects deleted in the preview are kept hidden (for undo) and registered here.
 History: 2026/10/11 Multi-selection, skinned meshes, character roots, outline on/off
=====================================+
*/
[DefaultExecutionOrder(100)]
public class CS_MayaPreviewSelection : MonoBehaviour
{
    public static event Action<Mesh> MeshChanged;
    public static void NotifyMeshChanged(Mesh mesh) => MeshChanged?.Invoke(mesh);

    // Raised whenever the selection changes.
    public event Action Changed;

    private const int EdgeBudget = 60000;
    private const string OutlinePrefsKey = "MayaPreview.SelectionOutline";
    private const string OutlineShader = "Shader Graphs/SHG_Outline";
    private static readonly Color EdgeColor = new Color(1f, 0.85f, 0f);

    private class Outline
    {
        public Transform target;      // edges are stored in this transform's local space
        public Mesh source;           // live mesh (null for baked skinned meshes)
        public Renderer renderer;
        public Vector3[] points;
        public Vector3[] ribbon;
        public Mesh mesh;
        public GameObject go;
        public bool gpu;              // drawn by an outline shader (mesh not readable on the CPU)
        public Material gpuMaterial;
    }

    private readonly List<GameObject> _objects = new List<GameObject>();
    private readonly List<Outline> _outlines = new List<Outline>();
    private Material _material;
    private Camera _camera;

    public GameObject SelectedObject => _objects.Count > 0 ? _objects[0] : null;
    public IReadOnlyList<GameObject> Selected => _objects;
    public int Count => _objects.Count;
    // Set when a Maya group (Hierarchy) is selected: its display name and Maya path.
    public string GroupLabel { get; private set; }
    public string GroupPath { get; private set; }

    // Yellow outline of the selected objects on / off.
    private bool _showOutlines = true;
    public bool ShowOutlines
    {
        get => _showOutlines;
        set
        {
            if (_showOutlines == value) return;
            _showOutlines = value;
            PlayerPrefs.SetInt(OutlinePrefsKey, value ? 1 : 0);
            if (value) RebuildOutlines();
            else ClearOutlines();
        }
    }

    // Objects deleted with the Delete key: inactive, but kept so Ctrl+Z can restore them.
    private static readonly HashSet<GameObject> s_deleted = new HashSet<GameObject>();
    public static bool IsDeleted(GameObject o) => o != null && s_deleted.Contains(o);
    public static void SetDeleted(GameObject o, bool deleted)
    {
        s_deleted.RemoveWhere(d => d == null);
        if (o == null) return;
        if (deleted) s_deleted.Add(o);
        else s_deleted.Remove(o);
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _showOutlines = PlayerPrefs.GetInt(OutlinePrefsKey, 1) != 0;
    }
    private void OnEnable() => MeshChanged += OnMeshChanged;
    private void OnDisable() { MeshChanged -= OnMeshChanged; ClearOutlines(); }
    private void OnDestroy() { if (_material != null) Destroy(_material); }


    // =========================================================
    // Selecting
    // =========================================================

    public void SelectObject(GameObject target) => Select(target != null ? new[] { target } : null, null, null);

    public void Select(IList<GameObject> objects, string groupLabel = null, string groupPath = null)
    {
        _objects.Clear();
        if (objects != null)
            foreach (GameObject o in objects)
                if (o != null && !_objects.Contains(o)) _objects.Add(o);
        GroupLabel = _objects.Count > 0 ? groupLabel : null;
        GroupPath = _objects.Count > 0 ? groupPath : null;
        RebuildOutlines();
        Changed?.Invoke();
    }

    public void Toggle(GameObject target)
    {
        if (target == null) return;
        var list = new List<GameObject>(_objects);
        if (!list.Remove(target)) list.Add(target);
        Select(list);
    }

    public bool IsSelected(GameObject target) => target != null && _objects.Contains(target);

    // Drop destroyed objects (Maya meshes can disappear at any time).
    public void Prune()
    {
        if (_objects.RemoveAll(o => o == null) > 0)
        {
            if (_objects.Count == 0) { GroupLabel = null; GroupPath = null; }
            RebuildOutlines();
            Changed?.Invoke();
        }
    }

    // Every renderer under the selection (selected objects and their children).
    public List<Renderer> Renderers()
    {
        var result = new List<Renderer>();
        foreach (GameObject o in _objects)
        {
            if (o == null) continue;
            foreach (Renderer r in o.GetComponentsInChildren<Renderer>())
                if (!result.Contains(r) && Selectable(r)) result.Add(r);
        }
        return result;
    }

    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in Renderers())
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (any) return true;
        foreach (GameObject o in _objects)
        {
            if (o == null) continue;
            var point = new Bounds(o.transform.position, Vector3.one * 0.5f);
            if (!any) { bounds = point; any = true; }
            else bounds.Encapsulate(point);
        }
        return any;
    }

    public static bool Selectable(Renderer r)
    {
        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return false;
        if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) return false;
        return !CS_MayaPreviewInternal.Is(r);
    }


    // =========================================================
    // Picking in the view
    // =========================================================

    public void Pick(Camera camera, Vector2 position, bool additive)
    {
        // The orientation widget is not part of the selectable scene.
        Rect viewport = camera.pixelRect;
        if (position.x >= viewport.xMax - 54 && position.y >= viewport.yMax - 54) return;
        Renderer hit = Raycast(camera, camera.ScreenPointToRay(position), out _);
        if (hit == null)
        {
            if (!additive) Select(null);
            return;
        }

        GameObject root = SelectionRoot(hit.gameObject);
        GameObject target = root;
        // Clicking an already selected character again selects the part under the mouse.
        if (_objects.Count == 1 && _objects[0] == root && root != hit.gameObject) target = hit.gameObject;
        if (additive) Toggle(target);
        else Select(new[] { target });
    }

    public Renderer Raycast(Camera camera, Ray ray, out float distance)
    {
        Renderer closest = null;
        float nearest = float.PositiveInfinity;
        var baked = new Mesh();
        try
        {
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!Selectable(renderer) || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                if (!renderer.bounds.IntersectRay(ray, out float boxDistance) || boxDistance >= nearest) continue;

                Mesh mesh = null;
                Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    if (TryBake(skinned, baked, out toWorld)) mesh = baked;
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                }

                float t;
                if (mesh != null && mesh.isReadable) t = RayMesh(ray, mesh, toWorld);
                else t = boxDistance;   // not readable: the bounding box is the best we have
                if (t < 0 || t >= nearest) continue;
                float depth = camera.transform.InverseTransformPoint(ray.GetPoint(t)).z;
                if (depth < camera.nearClipPlane || depth > camera.farClipPlane) continue;
                nearest = t;
                closest = renderer;
            }
        }
        finally { Destroy(baked); }
        distance = nearest;
        return closest;
    }

    // Characters are selected as a whole; Maya meshes and other objects by themselves.
    public static GameObject SelectionRoot(GameObject hit)
    {
        if (hit == null) return null;
        if (hit.name.StartsWith("MayaLive_")) return hit;
        Animator top = null;
        for (Transform t = hit.transform; t != null; t = t.parent)
            if (t.TryGetComponent(out Animator animator)) top = animator;
        return top != null ? top.gameObject : hit;
    }

    private static float RayMesh(Ray ray, Mesh mesh, Matrix4x4 toWorld)
    {
        Matrix4x4 toLocal = toWorld.inverse;
        Vector3 origin = toLocal.MultiplyPoint3x4(ray.origin);
        Vector3 direction = toLocal.MultiplyVector(ray.direction);
        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;
        float best = float.PositiveInfinity;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            Vector3 a = vertices[indices[i]], e1 = vertices[indices[i + 1]] - a, e2 = vertices[indices[i + 2]] - a;
            Vector3 p = Vector3.Cross(direction, e2);
            float determinant = Vector3.Dot(e1, p);
            if (Mathf.Abs(determinant) < 1e-12f) continue;
            Vector3 offset = origin - a;
            float u = Vector3.Dot(offset, p) / determinant;
            if (u < 0 || u > 1) continue;
            Vector3 q = Vector3.Cross(offset, e1);
            float v = Vector3.Dot(direction, q) / determinant;
            if (v < 0 || u + v > 1) continue;
            float t = Vector3.Dot(e2, q) / determinant;   // the direction keeps world length, so t is in world units
            if (t >= 0 && t < best) best = t;
        }
        return float.IsPositiveInfinity(best) ? -1f : best;
    }

    // Bakes a skinned mesh and finds the matrix that puts it where the renderer draws it.
    private static bool TryBake(SkinnedMeshRenderer skinned, Mesh baked, out Matrix4x4 toWorld)
    {
        toWorld = skinned.transform.localToWorldMatrix;
        try { skinned.BakeMesh(baked, true); }
        catch { return false; }
        if (baked.vertexCount == 0) return false;
        Transform t = skinned.transform;
        Matrix4x4 unscaled = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
        Bounds target = skinned.bounds;
        float scoreUnscaled = Score(baked, unscaled, target), scoreScaled = Score(baked, toWorld, target);
        if (scoreUnscaled <= scoreScaled) toWorld = unscaled;
        return true;
    }

    private static float Score(Mesh mesh, Matrix4x4 matrix, Bounds target)
    {
        Bounds local = mesh.bounds;
        Vector3 c = matrix.MultiplyPoint3x4(local.center);
        Vector3 e = matrix.MultiplyVector(local.extents);
        return (c - target.center).magnitude + Mathf.Abs(e.magnitude - target.extents.magnitude);
    }


    // =========================================================
    // Outlines
    // =========================================================

    private void OnMeshChanged(Mesh mesh)
    {
        foreach (Outline outline in _outlines)
        {
            if (outline.source != mesh) continue;
            RebuildOutlines();
            return;
        }
    }

    private void RebuildOutlines()
    {
        ClearOutlines();
        if (_objects.Count == 0 || !_showOutlines) return;
        var guides = GetComponent<CS_MayaPreviewGuides>();
        if (guides == null || guides.GuideMaterial == null) return;
        if (_material == null)
        {
            _material = new Material(guides.GuideMaterial) { hideFlags = HideFlags.DontSave };
            _material.SetColor("_UnlitColor", EdgeColor);
        }

        int budget = EdgeBudget;
        var baked = new Mesh();
        try
        {
            foreach (Renderer renderer in Renderers())
            {
                if (budget <= 0) break;
                Mesh mesh;
                Matrix4x4 toTarget = Matrix4x4.identity;
                Mesh source = null;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    bool readable = skinned.sharedMesh != null && skinned.sharedMesh.isReadable;
                    if (!readable || !TryBake(skinned, baked, out Matrix4x4 toWorld))
                    {
                        Outline gpu = CreateGpuOutline(renderer);
                        if (gpu != null) _outlines.Add(gpu);
                        continue;
                    }
                    mesh = baked;
                    toTarget = renderer.transform.worldToLocalMatrix * toWorld;
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                    source = mesh;
                    if (mesh != null && !mesh.isReadable)
                    {
                        // Imported models without Read/Write: outline drawn on the GPU instead.
                        Outline gpu = CreateGpuOutline(renderer);
                        if (gpu != null) _outlines.Add(gpu);
                        continue;
                    }
                }
                if (mesh == null || !mesh.isReadable) continue;
                Vector3[] points = EdgePoints(mesh, toTarget, budget);
                if (points.Length == 0) continue;
                budget -= points.Length / 2;
                _outlines.Add(CreateOutline(renderer, source, points));
            }
        }
        finally { Destroy(baked); }
        LateUpdate();
    }

    private Outline CreateOutline(Renderer renderer, Mesh source, Vector3[] points)
    {
        var outline = new Outline
        {
            target = renderer.transform, source = source, renderer = renderer, points = points,
            ribbon = new Vector3[points.Length * 2]
        };
        int[] indices = new int[points.Length / 2 * 6];
        for (int i = 0; i < points.Length / 2; i++)
        {
            int v = i * 4, t = i * 6;
            indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
            indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
        }
        outline.mesh = new Mesh { name = "Selected object edges", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
        outline.mesh.MarkDynamic();
        outline.mesh.vertices = outline.ribbon;
        outline.mesh.SetIndices(indices, MeshTopology.Triangles, 0);

        outline.go = new GameObject("Selection Edges");
        outline.go.AddComponent<CS_MayaPreviewInternal>();
        outline.go.layer = renderer.gameObject.layer;
        outline.go.transform.SetParent(renderer.transform, false);
        outline.go.AddComponent<MeshFilter>().sharedMesh = outline.mesh;
        var meshRenderer = outline.go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = _material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        return outline;
    }

    // Inverted-hull outline with the project's outline shader (same mesh, same bones).
    private static Material s_outlineTemplate;
    private Outline CreateGpuOutline(Renderer renderer)
    {
        Mesh mesh = renderer is SkinnedMeshRenderer s ? s.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null) return null;
        if (s_outlineTemplate == null)
        {
            foreach (Material candidate in Resources.FindObjectsOfTypeAll<Material>())
                if (candidate != null && candidate.shader != null && candidate.shader.name == OutlineShader) { s_outlineTemplate = candidate; break; }
        }
        Material material;
        if (s_outlineTemplate != null) material = new Material(s_outlineTemplate);
        else
        {
            Shader shader = Shader.Find(OutlineShader);
            if (shader == null) return null;
            material = new Material(shader);
        }
        material.hideFlags = HideFlags.DontSave;
        material.name = "Selection Outline";
        if (material.HasProperty("_OutlineColor")) material.SetColor("_OutlineColor", EdgeColor);
        if (material.HasProperty("_OutlineTexBlend")) material.SetFloat("_OutlineTexBlend", 0f);
        if (material.HasProperty("_OutlineTexDarken")) material.SetFloat("_OutlineTexDarken", 0f);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
        Vector3 lossy = renderer.transform.lossyScale;
        float scale = Mathf.Max(1e-6f, Mathf.Max(Mathf.Abs(lossy.x), Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z))));
        float width = Mathf.Clamp(renderer.bounds.size.magnitude * 0.0035f, 0.0015f, 0.05f);
        if (material.HasProperty("_OutlineWidth")) material.SetFloat("_OutlineWidth", width / scale);

        var go = new GameObject("Selection Outline");
        go.AddComponent<CS_MayaPreviewInternal>();
        go.layer = renderer.gameObject.layer;
        go.transform.SetParent(renderer.transform, false);
        var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        Renderer copy;
        if (renderer is SkinnedMeshRenderer skinned)
        {
            var skinnedCopy = go.AddComponent<SkinnedMeshRenderer>();
            skinnedCopy.sharedMesh = mesh;
            skinnedCopy.bones = skinned.bones;
            skinnedCopy.rootBone = skinned.rootBone;
            skinnedCopy.localBounds = skinned.localBounds;
            skinnedCopy.updateWhenOffscreen = skinned.updateWhenOffscreen;
            for (int i = 0; i < mesh.blendShapeCount; i++) skinnedCopy.SetBlendShapeWeight(i, skinned.GetBlendShapeWeight(i));
            copy = skinnedCopy;
        }
        else
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            copy = go.AddComponent<MeshRenderer>();
        }
        copy.sharedMaterials = materials;
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = false;
        return new Outline { target = renderer.transform, renderer = renderer, go = go, gpu = true, gpuMaterial = material, points = new Vector3[0] };
    }

    private struct Edge
    {
        public Vector3 a, b, normal;
        public int count;
        public bool crease;
    }

    // Maya-like: border edges and edges between faces that are not coplanar (hides quad diagonals).
    // Very dense meshes keep only borders and hard edges so the selection stays fast.
    private static Vector3[] EdgePoints(Mesh mesh, Matrix4x4 toTarget, int budget)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        var edges = new Dictionary<(Vector3, Vector3), Edge>(triangles.Length);
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            AddEdge(edges, a, b, normal); AddEdge(edges, b, c, normal); AddEdge(edges, c, a, normal);
        }
        int all = 0;
        foreach (var edge in edges.Values) if (edge.count != 2 || edge.crease) all++;
        bool dense = all > budget;
        var points = new List<Vector3>();
        foreach (var edge in edges.Values)
        {
            bool keep = dense ? edge.count != 2 : edge.count != 2 || edge.crease;
            if (!keep) continue;
            points.Add(toTarget.MultiplyPoint3x4(edge.a));
            points.Add(toTarget.MultiplyPoint3x4(edge.b));
            if (points.Count / 2 >= budget) break;
        }
        return points.ToArray();
    }

    private static void AddEdge(Dictionary<(Vector3, Vector3), Edge> edges, Vector3 a, Vector3 b, Vector3 normal)
    {
        if (a.x > b.x || (a.x == b.x && (a.y > b.y || (a.y == b.y && a.z > b.z))))
        { Vector3 swap = a; a = b; b = swap; }
        var key = (a, b);
        if (!edges.TryGetValue(key, out Edge edge)) edge = new Edge { a = a, b = b, normal = normal };
        else edge.crease |= Vector3.Dot(edge.normal, normal) < 0.9999f;
        edge.count++;
        edges[key] = edge;
    }

    private void LateUpdate()
    {
        if (_outlines.Count == 0 || _camera == null) return;
        if (_outlines.Exists(o => o.target == null || o.go == null))
        {
            _objects.RemoveAll(o => o == null);
            RebuildOutlines();
            Changed?.Invoke();
            return;
        }
        float near = _camera.nearClipPlane + 0.001f;
        Transform view = _camera.transform;
        foreach (Outline outline in _outlines)
        {
            Transform target = outline.target;
            bool visible = outline.renderer != null && outline.renderer.enabled && outline.renderer.gameObject.activeInHierarchy;
            outline.go.SetActive(visible);
            if (!visible || outline.gpu) continue;
            Vector3[] points = outline.points, ribbon = outline.ribbon;
            for (int i = 0; i < points.Length; i += 2)
            {
                // Camera-facing ribbons keep the edges three pixels wide at any zoom.
                Vector3 a = view.InverseTransformPoint(target.TransformPoint(points[i]));
                Vector3 b = view.InverseTransformPoint(target.TransformPoint(points[i + 1]));
                int v = i * 2;
                if (a.z < near && b.z < near)
                {
                    ribbon[v] = ribbon[v + 1] = ribbon[v + 2] = ribbon[v + 3] = Vector3.zero;
                    continue;
                }
                if (a.z < near) a = Vector3.Lerp(a, b, (near - a.z) / (b.z - a.z));
                if (b.z < near) b = Vector3.Lerp(b, a, (near - b.z) / (a.z - b.z));
                a = _camera.WorldToScreenPoint(view.TransformPoint(a));
                b = _camera.WorldToScreenPoint(view.TransformPoint(b));
                Vector2 direction = new Vector2(b.x - a.x, b.y - a.y).normalized;
                Vector3 offset = new Vector3(-direction.y, direction.x, 0) * 1.5f;
                // A small depth bias prevents the lines flickering against their own surface.
                a.z = Mathf.Max(near, a.z - a.z * 0.0001f);
                b.z = Mathf.Max(near, b.z - b.z * 0.0001f);
                ribbon[v] = target.InverseTransformPoint(_camera.ScreenToWorldPoint(a - offset));
                ribbon[v + 1] = target.InverseTransformPoint(_camera.ScreenToWorldPoint(a + offset));
                ribbon[v + 2] = target.InverseTransformPoint(_camera.ScreenToWorldPoint(b + offset));
                ribbon[v + 3] = target.InverseTransformPoint(_camera.ScreenToWorldPoint(b - offset));
            }
            outline.mesh.vertices = ribbon;
            outline.mesh.RecalculateBounds();
        }
    }

    private void ClearOutlines()
    {
        foreach (Outline outline in _outlines)
        {
            if (outline.go != null) { outline.go.SetActive(false); Destroy(outline.go); }
            if (outline.mesh != null) Destroy(outline.mesh);
            if (outline.gpuMaterial != null) Destroy(outline.gpuMaterial);
        }
        _outlines.Clear();
    }
}
