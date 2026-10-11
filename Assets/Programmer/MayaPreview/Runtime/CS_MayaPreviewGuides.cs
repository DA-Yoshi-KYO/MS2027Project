using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class CS_MayaPreviewGuides : MonoBehaviour
{
    [SerializeField] private Material gridMaterial;
    public Material GuideMaterial => gridMaterial;
    private GameObject _grid;
    private Mesh _mesh;
    private Material[] _materials;
    private readonly Color[] _colors = {
        new Color(1f, 0.25f, 0.25f), new Color(0.35f, 1f, 0.35f), new Color(0.3f, 0.6f, 1f)
    };

    private void Awake()
    {
        if (gridMaterial == null) return;
        var vertices = new List<Vector3>();
        var triangles = new List<int>[4];
        for (int i = 0; i < 4; i++) triangles[i] = new List<int>();
        // One Unity unit is one metre: 100 m square, centred at world origin.
        for (int i = -50; i <= 50; i++)
        {
            int group = i == 0 ? 2 : i % 10 == 0 ? 1 : 0;
            float width = i == 0 ? 0.035f : i % 10 == 0 ? 0.025f : 0.012f;
            AddLine(vertices, triangles[group], new Vector3(-50, 0, i), new Vector3(50, 0, i), width);
            AddLine(vertices, triangles[i == 0 ? 3 : group], new Vector3(i, 0, -50), new Vector3(i, 0, 50), width);
        }
        _mesh = new Mesh { name = "Maya Preview 100m Grid" };
        _mesh.SetVertices(vertices);
        _mesh.subMeshCount = 4;
        for (int i = 0; i < 4; i++) _mesh.SetTriangles(triangles[i], i);
        _mesh.RecalculateBounds();
        _materials = new Material[4];
        Color[] colors = { new Color(0.18f, 0.18f, 0.18f), new Color(0.32f, 0.32f, 0.32f), _colors[0], _colors[2] };
        for (int i = 0; i < 4; i++)
        {
            _materials[i] = new Material(gridMaterial);
            _materials[i].SetColor("_UnlitColor", colors[i]);
        }
        _grid = new GameObject("Maya Preview Grid (100m)");
        _grid.AddComponent<CS_MayaPreviewInternal>();
        _grid.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = _grid.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = _materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static void AddLine(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, float width)
    {
        Vector3 offset = Vector3.Cross((b - a).normalized, Vector3.up) * width * 0.5f;
        int start = vertices.Count;
        vertices.Add(a - offset); vertices.Add(a + offset);
        vertices.Add(b + offset); vertices.Add(b - offset);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private void OnEnable() { if (_grid != null) _grid.SetActive(true); }
    private void OnDisable() { if (_grid != null) _grid.SetActive(false); }

    private void OnDestroy()
    {
        if (_grid != null) Destroy(_grid);
        if (_mesh != null) Destroy(_mesh);
        if (_materials != null) foreach (var material in _materials) Destroy(material);
    }
}
