using System.Collections.Generic;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewMeshData
 Summary: What the toon shaders need from a mesh. Measured only: no thresholds, nothing is recognised,
          so every model from every .mb is handled the same way. What a part IS (face / body / nose)
          is chosen in the Material panel (_SkinPart).
          - Eye  : where in its eyeball every point is, seen along the gaze both eyes share, in the second
                   UV set, and that gaze in _EyeGaze (CS_ToonEyeShape). Does not depend on the eyes' UVs.
          - Cloth: the metres in one UV unit of every UV island, along U and along V, in the second UV set
                   (stored +ClothOffset): threads and stitches are the same real size on every model.
                   The borders of those islands go to CS_MayaPreviewClothSeams (stitches along the edges).
          - Skin : the size of the part (_NoseSize: the nose sinks by multiples of it) and the side the
                   texture's U grows to (_FaceRight, for the SDF map).
          Both are in the mesh / material, so the export carries them.
 History: 2026/11/22 New
=====================================+
*/
public static class CS_MayaPreviewMeshData
{
    public const string SkinShader = "Shader Graphs/SHG_NTE_Skin";
    public const string EyeShader = "Shader Graphs/SHG_NTE_Eye";
    public const string ClothShader = "Shader Graphs/SHG_NTE_Cloth";
    public const string PartProp = "_SkinPart";
    public const int PartFace = 0, PartBody = 1, PartNose = 2;
    public const float ClothOffset = 4f;            // NTE_CLOTH_UV_OFFSET in NTE_Toon.hlsl

    // 0 face / 1 body / 2 nose, or -1 when it is not the skin shader.
    public static int PartOf(Material material)
    {
        if (material == null || material.shader == null || material.shader.name != SkinShader) return -1;
        return Mathf.Clamp(Mathf.RoundToInt(material.GetFloat(PartProp)), PartFace, PartNose);
    }

    // Called whenever Maya sends the mesh or one of its materials is made again.
    public static void Apply(Mesh mesh, Material[] materials)
    {
        if (mesh == null || !mesh.isReadable || materials == null) return;
        CS_MayaPreviewClothSeams.Forget(mesh);
        Vector3[] vertices = null;
        Vector2[] uv = null, local = null;
        List<int[]> eyeParts = null;
        List<Material> eyeMaterials = null;
        int count = Mathf.Min(mesh.subMeshCount, materials.Length);
        for (int i = 0; i < count; i++)
        {
            Material material = materials[i];
            if (material == null || material.shader == null) continue;
            bool eye = material.shader.name == EyeShader;
            bool cloth = material.shader.name == ClothShader;
            if (!eye && !cloth && material.shader.name != SkinShader) continue;
            if (uv == null) uv = mesh.uv;
            if (cloth)
            {
                if (uv.Length != mesh.vertexCount) continue;
                if (vertices == null) vertices = mesh.vertices;
                if (local == null) local = new Vector2[uv.Length];
                int[] triangles = mesh.GetTriangles(i);
                FillClothSizes(uv, vertices, triangles, local);
                CS_MayaPreviewClothSeams.Collect(material, mesh, vertices, uv, triangles, local);
            }
            else if (eye)
            {
                if (eyeParts == null)
                {
                    eyeParts = new List<int[]>();
                    eyeMaterials = new List<Material>();
                }
                eyeParts.Add(mesh.GetTriangles(i));     // measured together: both eyes share one gaze
                eyeMaterials.Add(material);
            }
            else
            {
                if (vertices == null) vertices = mesh.vertices;
                Measure(material, vertices, uv.Length == vertices.Length ? uv : null, mesh.GetTriangles(i));
            }
        }
        if (eyeParts != null)
        {
            if (vertices == null) vertices = mesh.vertices;
            CS_ToonEyeShape shape = CS_ToonEyeShape.Measure(vertices, eyeParts);
            if (shape != null)
            {
                if (local == null) local = new Vector2[vertices.Length];
                Vector2[] coords = shape.Coords();
                for (int i = 0; i < coords.Length; i++) local[shape.vertices[i]] = coords[i];
                var gaze = new Vector4(shape.front.x, shape.front.y, shape.front.z, 1f);
                foreach (Material material in eyeMaterials) material.SetVector("_EyeGaze", gaze);
            }
        }
        if (local != null) mesh.uv2 = local;
    }


    // =========================================================
    // Skin
    // =========================================================

    private static void Measure(Material material, Vector3[] vertices, Vector2[] uv, int[] triangles)
    {
        if (triangles.Length == 0) return;
        Vector3 min = vertices[triangles[0]], max = min;
        double sumX = 0, sumU = 0, sumXU = 0;
        foreach (int v in triangles)
        {
            Vector3 p = vertices[v];
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            if (uv == null) continue;
            sumX += p.x;
            sumU += uv[v].x;
            sumXU += (double)p.x * uv[v].x;
        }
        material.SetFloat("_NoseSize", (max - min).magnitude);
        if (uv == null) return;
        double together = sumXU - sumX * sumU / triangles.Length;   // covariance: does U grow with +X or with -X
        material.SetVector("_FaceRight", new Vector4(together < 0 ? -1f : 1f, 0f, 0f, 0f));
    }


    // =========================================================
    // UV islands
    // =========================================================

    // corners[i] = the point on the texture of triangle corner i. Connected there = the same island (Root).
    private static int[] Islands(Vector2[] uv, int[] triangles, out List<int> parent)
    {
        var pointOf = new Dictionary<PointKey, int>(triangles.Length);
        parent = new List<int>();
        var corners = new int[triangles.Length];
        for (int i = 0; i < triangles.Length; i++)
        {
            var at = new PointKey(uv[triangles[i]]);
            if (!pointOf.TryGetValue(at, out int point))
            {
                point = parent.Count;
                parent.Add(point);
                pointOf.Add(at, point);
            }
            corners[i] = point;
        }
        for (int i = 0; i + 2 < corners.Length; i += 3)
        {
            Join(parent, corners[i], corners[i + 1]);
            Join(parent, corners[i], corners[i + 2]);
        }
        return corners;
    }


    // =========================================================
    // Cloth
    // =========================================================

    // |dP/du| and |dP/dv| of every island, averaged by area.
    private static void FillClothSizes(Vector2[] uv, Vector3[] vertices, int[] triangles, Vector2[] local)
    {
        int[] corners = Islands(uv, triangles, out List<int> parent);
        var alongU = new double[parent.Count];
        var alongV = new double[parent.Count];
        var areas = new double[parent.Count];
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            Vector3 e1 = vertices[triangles[i + 1]] - vertices[triangles[i]], e2 = vertices[triangles[i + 2]] - vertices[triangles[i]];
            Vector2 a = uv[triangles[i]], b = uv[triangles[i + 1]], c = uv[triangles[i + 2]];
            double du1 = b.x - a.x, dv1 = b.y - a.y, du2 = c.x - a.x, dv2 = c.y - a.y;
            double det = du1 * dv2 - du2 * dv1;
            if (det == 0) continue;
            double area = Length((double)e1.y * e2.z - (double)e1.z * e2.y, (double)e1.z * e2.x - (double)e1.x * e2.z,
                                 (double)e1.x * e2.y - (double)e1.y * e2.x);
            int island = Root(parent, corners[i]);
            areas[island] += area;
            alongU[island] += area * Length((e1.x * dv2 - e2.x * dv1) / det, (e1.y * dv2 - e2.y * dv1) / det, (e1.z * dv2 - e2.z * dv1) / det);
            alongV[island] += area * Length((e2.x * du1 - e1.x * du2) / det, (e2.y * du1 - e1.y * du2) / det, (e2.z * du1 - e1.z * du2) / det);
        }
        for (int i = 0; i < triangles.Length; i++)
        {
            int island = Root(parent, corners[i]);
            double toMetres = areas[island] > 0 ? CS_MayaPreviewExport.MayaUnitsToMetres / areas[island] : 0;
            local[triangles[i]] = new Vector2(
                ClothOffset + (toMetres > 0 ? (float)(alongU[island] * toMetres) : 1f),
                ClothOffset + (toMetres > 0 ? (float)(alongV[island] * toMetres) : 1f));
        }
    }

    // A place as a dictionary key. Not Vector2 / Vector3: their hashes collide on grids (a sleeve of 100 000
    // triangles took 14 seconds to measure).
    public readonly struct PointKey : System.IEquatable<PointKey>
    {
        private readonly float _a, _b, _c, _d, _e;
        private readonly int _hash;

        public PointKey(Vector2 uv) : this(uv.x, uv.y, 0f, 0f, 0f) { }
        public PointKey(Vector3 position, Vector2 uv) : this(uv.x, uv.y, position.x, position.y, position.z) { }

        private PointKey(float a, float b, float c, float d, float e)
        {
            _a = a + 0f; _b = b + 0f; _c = c + 0f; _d = d + 0f; _e = e + 0f;        // + 0: -0 is 0
            _hash = Mix(Mix(Mix(Mix(Mix(17, _a.GetHashCode()), _b.GetHashCode()), _c.GetHashCode()), _d.GetHashCode()), _e.GetHashCode());
        }

        private static int Mix(int hash, int value)
        {
            unchecked
            {
                uint h = ((uint)hash ^ (uint)value) * 0x9E3779B1u;
                h ^= h >> 15;
                h *= 0x85EBCA77u;
                return (int)(h ^ (h >> 13));
            }
        }

        public bool Equals(PointKey other) => _a == other._a && _b == other._b && _c == other._c && _d == other._d && _e == other._e;
        public override bool Equals(object other) => other is PointKey key && Equals(key);
        public override int GetHashCode() => _hash;
    }

    private static double Length(double x, double y, double z) => System.Math.Sqrt(x * x + y * y + z * z);


    private static int Root(List<int> parent, int point)
    {
        while (parent[point] != point)
        {
            parent[point] = parent[parent[point]];
            point = parent[point];
        }
        return point;
    }

    private static void Join(List<int> parent, int a, int b)
    {
        int ra = Root(parent, a), rb = Root(parent, b);
        if (ra != rb) parent[rb] = ra;
    }
}
