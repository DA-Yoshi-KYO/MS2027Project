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
    private Camera _camera;
    private GUIStyle _label;
    private readonly Vector3[] _axes = { Vector3.right, Vector3.up, Vector3.forward };
    private readonly Color[] _colors = {
        new Color(1f, 0.25f, 0.25f), new Color(0.35f, 1f, 0.35f), new Color(0.3f, 0.6f, 1f)
    };
    private readonly string[] _names = { "X", "Y", "Z" };
    private readonly int[] _order = { 0, 1, 2 };
    private readonly Vector3[] _directions = new Vector3[3];

    private void Awake()
    {
        _camera = GetComponent<Camera>();
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

    private void OnGUI()
    {
        if (!_camera.enabled || Event.current.type != EventType.Repaint) return;
        if (_label == null) _label = new GUIStyle(GUI.skin.label) {
            alignment = TextAnchor.MiddleCenter, fontSize = 10, fontStyle = FontStyle.Bold
        };
        Rect viewport = _camera.pixelRect;
        Vector2 centre = new Vector2(viewport.xMax - 32, Screen.height - viewport.yMax + 32);
        Color previous = GUI.color;
        GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.8f);
        GUI.DrawTexture(new Rect(centre.x - 22, centre.y - 22, 44, 44), Texture2D.whiteTexture);
        for (int i = 0; i < 3; i++) _directions[i] = transform.InverseTransformDirection(_axes[i]);
        // Draw farther axes first so front-facing axes remain visible.
        for (int i = 0; i < 2; i++)
            for (int j = i + 1; j < 3; j++)
                if (_directions[_order[i]].z < _directions[_order[j]].z)
                { int swap = _order[i]; _order[i] = _order[j]; _order[j] = swap; }
        foreach (int i in _order)
        {
            Vector2 direction = new Vector2(_directions[i].x, -_directions[i].y);
            GUI.color = _colors[i] * new Color(0.45f, 0.45f, 0.45f, 1f);
            DrawLine(centre, centre - direction * 12, 1);
            GUI.color = _colors[i];
            DrawLine(centre, centre + direction * 13, 1.5f);
            Vector2 label = centre + direction * 17;
            GUI.Label(new Rect(label.x - 6, label.y - 7, 12, 14), _names[i], _label);
        }
        GUI.color = previous;
    }

    private static void DrawLine(Vector2 start, Vector2 end, float width)
    {
        Matrix4x4 previous = GUI.matrix;
        Vector2 delta = end - start;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
        GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
        GUI.matrix = previous;
    }

    private void OnDestroy()
    {
        if (_grid != null) Destroy(_grid);
        if (_mesh != null) Destroy(_mesh);
        if (_materials != null) foreach (var material in _materials) Destroy(material);
    }
}
