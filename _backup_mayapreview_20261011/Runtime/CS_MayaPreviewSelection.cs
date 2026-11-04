using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(100)]
public class CS_MayaPreviewSelection : MonoBehaviour
{
    public static event System.Action<Mesh> MeshChanged;
    public static void NotifyMeshChanged(Mesh mesh) => MeshChanged?.Invoke(mesh);
    private MeshFilter _selected;
    private GameObject _outline;
    private Mesh _edges;
    private Material _material;
    private Vector3[] _edgePoints;
    private Vector3[] _ribbonVertices;
    private Camera _camera;
    public GameObject SelectedObject { get; private set; }

    public void SelectObject(GameObject target)
    {
        Clear();
        SelectedObject = target;
        _selected = target != null ? target.GetComponent<MeshFilter>() : null;
        if (_selected != null && _selected.sharedMesh != null) Refresh(_selected.sharedMesh);
    }

    private void Awake() => _camera = GetComponent<Camera>();

    private void OnEnable() => MeshChanged += Refresh;
    private void OnDisable() { MeshChanged -= Refresh; Clear(); }

    public bool TryGetBounds(out Bounds bounds)
    {
        var renderer = _selected != null ? _selected.GetComponent<MeshRenderer>() : null;
        bounds = renderer != null ? renderer.bounds : default;
        return renderer != null;
    }

    public void Pick(Camera camera, Vector2 position)
    {
        // The orientation widget is not part of the selectable scene.
        Rect viewport = camera.pixelRect;
        if (position.x >= viewport.xMax - 54 && position.y >= viewport.yMax - 54) return;
        Ray ray = camera.ScreenPointToRay(position);
        MeshFilter closest = null;
        float nearest = float.PositiveInfinity;
        foreach (var filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null || !filter.sharedMesh.isReadable || renderer == null || !renderer.enabled
                || filter.gameObject == _outline || filter.name == "Maya Preview Grid (100m)"
                || (camera.cullingMask & (1 << filter.gameObject.layer)) == 0
                || !renderer.bounds.IntersectRay(ray)) continue;
            Vector3[] vertices = filter.sharedMesh.vertices;
            int[] indices = filter.sharedMesh.triangles;
            // Keep ray parameter in world units, including non-uniform object scale.
            Vector3 origin = filter.transform.InverseTransformPoint(ray.origin);
            Vector3 direction = filter.transform.InverseTransformVector(ray.direction);
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], e1 = vertices[indices[i + 1]] - a, e2 = vertices[indices[i + 2]] - a;
                Vector3 p = Vector3.Cross(direction, e2);
                float determinant = Vector3.Dot(e1, p);
                if (Mathf.Abs(determinant) < 1e-8f) continue;
                Vector3 offset = origin - a;
                float u = Vector3.Dot(offset, p) / determinant;
                Vector3 q = Vector3.Cross(offset, e1);
                float v = Vector3.Dot(direction, q) / determinant;
                float t = Vector3.Dot(e2, q) / determinant;
                if (u < 0 || v < 0 || u + v > 1 || t < 0 || t >= nearest) continue;
                float depth = camera.transform.InverseTransformPoint(ray.GetPoint(t)).z;
                if (depth < camera.nearClipPlane || depth > camera.farClipPlane) continue;
                nearest = t;
                closest = filter;
            }
        }
        SelectObject(closest != null ? closest.gameObject : null);
    }

    private struct Edge
    {
        public Vector3 a, b, normal;
        public int count;
        public bool crease;
    }

    private void Refresh(Mesh mesh)
    {
        if (_selected == null || _selected.sharedMesh != mesh) return;
        if (_edges != null) Destroy(_edges);
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var edges = new Dictionary<(Vector3, Vector3), Edge>();
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            AddEdge(edges, a, b, normal); AddEdge(edges, b, c, normal); AddEdge(edges, c, a, normal);
        }
        var points = new List<Vector3>();
        foreach (var edge in edges.Values)
            if (edge.count != 2 || edge.crease) { points.Add(edge.a); points.Add(edge.b); }
        _edgePoints = points.ToArray();
        _ribbonVertices = new Vector3[points.Count * 2];
        int[] indices = new int[points.Count / 2 * 6];
        for (int i = 0; i < points.Count / 2; i++)
        {
            int v = i * 4, t = i * 6;
            indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
            indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
        }
        _edges = new Mesh { name = "Selected object edges", indexFormat = IndexFormat.UInt32 };
        _edges.MarkDynamic();
        _edges.vertices = _ribbonVertices;
        _edges.SetIndices(indices, MeshTopology.Triangles, 0);
        if (_outline == null)
        {
            var guides = GetComponent<CS_MayaPreviewGuides>();
            if (guides == null || guides.GuideMaterial == null) return;
            _material = new Material(guides.GuideMaterial);
            _material.SetColor("_UnlitColor", new Color(1f, 0.85f, 0f));
            _outline = new GameObject("Selection Edges");
            _outline.layer = _selected.gameObject.layer;
            _outline.transform.SetParent(_selected.transform, false);
            _outline.AddComponent<MeshFilter>();
            var renderer = _outline.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        _outline.GetComponent<MeshFilter>().sharedMesh = _edges;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (_selected == null || _edges == null || _outline == null) return;
        // Camera-facing ribbons keep the selection edges three pixels wide at any zoom.
        Transform target = _selected.transform;
        float near = _camera.nearClipPlane + 0.001f;
        for (int i = 0; i < _edgePoints.Length; i += 2)
        {
            Vector3 a = _camera.transform.InverseTransformPoint(target.TransformPoint(_edgePoints[i]));
            Vector3 b = _camera.transform.InverseTransformPoint(target.TransformPoint(_edgePoints[i + 1]));
            int v = i * 2;
            if (a.z < near && b.z < near)
            {
                for (int j = 0; j < 4; j++) _ribbonVertices[v + j] = Vector3.zero;
                continue;
            }
            if (a.z < near) a = Vector3.Lerp(a, b, (near - a.z) / (b.z - a.z));
            if (b.z < near) b = Vector3.Lerp(b, a, (near - b.z) / (a.z - b.z));
            a = _camera.WorldToScreenPoint(_camera.transform.TransformPoint(a));
            b = _camera.WorldToScreenPoint(_camera.transform.TransformPoint(b));
            Vector2 direction = new Vector2(b.x - a.x, b.y - a.y).normalized;
            Vector3 offset = new Vector3(-direction.y, direction.x, 0) * 1.5f;
            // A small depth bias prevents the lines flickering against their own surface.
            a.z = Mathf.Max(near, a.z - a.z * 0.0001f);
            b.z = Mathf.Max(near, b.z - b.z * 0.0001f);
            _ribbonVertices[v] = target.InverseTransformPoint(_camera.ScreenToWorldPoint(a - offset));
            _ribbonVertices[v + 1] = target.InverseTransformPoint(_camera.ScreenToWorldPoint(a + offset));
            _ribbonVertices[v + 2] = target.InverseTransformPoint(_camera.ScreenToWorldPoint(b + offset));
            _ribbonVertices[v + 3] = target.InverseTransformPoint(_camera.ScreenToWorldPoint(b - offset));
        }
        _edges.vertices = _ribbonVertices;
        _edges.RecalculateBounds();
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

    private void Clear()
    {
        if (_outline != null) { _outline.SetActive(false); Destroy(_outline); }
        if (_edges != null) Destroy(_edges);
        if (_material != null) Destroy(_material);
        _outline = null; _edges = null; _material = null; _selected = null;
        _edgePoints = null; _ribbonVertices = null;
        SelectedObject = null;
    }
}
