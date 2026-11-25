using System.Collections.Generic;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewOutlineHull
 Summary: The mesh an outline is drawn with (inverted hull), from a mesh's positions and triangles.
          Shape only: no names, no thresholds of one model.
          - One push direction per position, from the faces around it (points closer than
            WeldPart of the model's size count as one, so mirrored halves do not split).
          - Open edges (hems, openings, hair cards) get a band from the edge to the pushed line.
            Without it the line floated beside the edge (double line) and broke off: the hull has a
            hole there. SHG_Outline reads the band from the second UV set: y = BandMark,
            x = OnEdge (stays on the edge) / AtTip (pushed, in the sheet's own plane).
          - A hem folded over (a crease next to an open edge) gets the band too, and is pushed back as little:
            its line starts at the crease and does not come and go with the distance to what is under it.
          - A sheet drawn from both sides by a second, reversed copy counts as one sheet, however the copy
            is split into triangles.
 History: 2026/11/22 New
=====================================+
*/
public sealed class CS_MayaPreviewOutlineHull
{
    public const float BandMark = -1f, OnEdge = 0f, AtTip = 0.5f;
    private const double WeldPart = 1e-5;       // of the bounds' diagonal
    private const float SameSide = 0.9999f;     // two faces of an edge lying on each other: a copy, not a fold
    private const float FoldCos = 0.707f;       // a crease: the faces of the edge turn more than 45 degrees
    private const float Cancelled = 1e-3f;      // of the summed weights: the faces around a point cancel out
    private const float TipPart = 2e-5f;        // keeps the band's triangles from being dropped as empty on export

    public Vector3[] positions;
    public Vector3[] directions;
    public Vector2[] band;
    public int[] from;                          // the vertex of the source mesh each vertex was copied from
    public int[][] triangles;                   // per submesh
    public int bandEdges;

    private struct Edge
    {
        public int uses, part, a, b;            // a, b = vertices of the source, in the face's order
        public int third, third2;               // the other corner of the first / second face
        public Vector3 face2;
        public bool folded;                     // the second face lies on the first
        public Vector3 face;
    }

    // parts = triangles per submesh. skip = submeshes that get no line. fallback = the mesh's own normals.
    public static CS_MayaPreviewOutlineHull Build(Vector3[] vertices, int[][] parts, bool[] skip, Vector3[] fallback)
    {
        int count = vertices.Length;
        float diagonal = Diagonal(vertices);
        int[] point = Weld(vertices, diagonal * WeldPart, out int points);

        var sums = new Vector3[points];
        var weights = new float[points];
        var outward = new Vector3[points];
        var firstFace = new Dictionary<(int, int, int), Vector3>();
        var edges = new Dictionary<(int, int), Edge>();     // not a long: its hash (low ^ high) collides for neighbours
        var primary = new bool[count];
        var reversed = new bool[count];

        for (int s = 0; s < parts.Length; s++)
        {
            int[] t = parts[s];
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                int a = t[i], b = t[i + 1], c = t[i + 2];
                int pa = point[a], pb = point[b], pc = point[c];
                if (pa == pb || pb == pc || pc == pa) continue;
                Vector3 face = Unit(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]));
                (int, int, int) key = Sorted(pa, pb, pc);
                if (firstFace.TryGetValue(key, out Vector3 first))
                {
                    if (Vector3.Dot(face, first) < 0f) reversed[a] = reversed[b] = reversed[c] = true;
                    continue;
                }
                firstFace.Add(key, face);
                primary[a] = primary[b] = primary[c] = true;
                AddEdge(edges, vertices, pa, pb, a, b, c, s, face);
                AddEdge(edges, vertices, pb, pc, b, c, a, s, face);
                AddEdge(edges, vertices, pc, pa, c, a, b, s, face);
                float atA = Angle(vertices[a], vertices[b], vertices[c]);
                float atB = Angle(vertices[b], vertices[c], vertices[a]);
                float atC = Angle(vertices[c], vertices[a], vertices[b]);
                sums[pa] += face * atA; weights[pa] += atA;
                sums[pb] += face * atB; weights[pb] += atB;
                sums[pc] += face * atC; weights[pc] += atC;
            }
        }

        var open = new List<Edge>();
        foreach (KeyValuePair<(int, int), Edge> pair in edges)
        {
            Edge e = pair.Value;
            bool isOpen = e.uses == 1 || (e.uses == 2 && e.folded);
            if (!isOpen || (e.part < skip.Length && skip[e.part]) || IsZero(e.face)) continue;
            open.Add(e);
            Vector3 out_ = Unit(Vector3.Cross(vertices[e.b] - vertices[e.a], e.face));
            outward[point[e.a]] += out_;
            outward[point[e.b]] += out_;
        }

        var average = new Vector3[points];
        for (int p = 0; p < points; p++)
            if (sums[p].magnitude > weights[p] * Cancelled) average[p] = Unit(sums[p]);

        // Creases next to an open edge: both ends within one triangle of it, bent outwards.
        var atOpen = new bool[points];
        foreach (Edge e in open) atOpen[point[e.a]] = atOpen[point[e.b]] = true;
        var nearOpen = new bool[points];
        foreach (int[] t in parts)
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                int pa = point[t[i]], pb = point[t[i + 1]], pc = point[t[i + 2]];
                if (atOpen[pa] || atOpen[pb] || atOpen[pc]) nearOpen[pa] = nearOpen[pb] = nearOpen[pc] = true;
            }
        var creases = new List<Edge>();
        var foldedHem = new bool[points];
        foreach (KeyValuePair<(int, int), Edge> pair in edges)
        {
            Edge e = pair.Value;
            if (e.uses != 2 || e.folded || !nearOpen[pair.Key.Item1] || !nearOpen[pair.Key.Item2]) continue;
            if (e.part < skip.Length && skip[e.part]) continue;
            if (IsZero(e.face) || IsZero(e.face2) || Vector3.Dot(e.face, e.face2) >= FoldCos) continue;
            if (Vector3.Dot(e.face, vertices[e.third2] - vertices[e.a]) >= 0f) continue;     // bent inwards
            creases.Add(e);
            foldedHem[pair.Key.Item1] = foldedHem[pair.Key.Item2] = true;
        }

        var positions = new List<Vector3>(vertices);
        var directions = new List<Vector3>(count);
        var band = new List<Vector2>(count);
        var from = new List<int>(count);
        for (int v = 0; v < count; v++)
        {
            Vector3 n = average[point[v]];
            if (IsZero(n)) n = v < fallback.Length ? Unit(fallback[v]) : n;
            else if (reversed[v] && !primary[v]) n = -n;        // the back copy of a sheet
            directions.Add(n);
            band.Add(foldedHem[point[v]] ? new Vector2(AtTip, BandMark) : new Vector2(0f, 0f));      // at a fold: pushed as the band's tip
            from.Add(v);
        }

        var lists = new List<int>[parts.Length];
        for (int s = 0; s < parts.Length; s++)
            lists[s] = s < skip.Length && skip[s] ? new List<int>() : new List<int>(parts[s]);

        var bandOf = new Dictionary<int, int>();        // source vertex -> its vertex on the edge (the tip is the next one)
        float tip = diagonal * TipPart;
        foreach (Edge e in open)
        {
            Vector3 own = Unit(Vector3.Cross(vertices[e.b] - vertices[e.a], e.face));
            int a = BandVertex(e.a, own), b = BandVertex(e.b, own);
            List<int> to = lists[e.part];
            Quad(to, a, b, b + 1, a + 1);               // edge -> tip
            Quad(to, a + 1, b + 1, e.b, e.a);           // tip  -> the pushed hull
        }
        foreach (Edge e in creases)
        {
            Vector3 own = Unit(Vector3.Cross(vertices[e.b] - vertices[e.a], e.face));
            Quad(lists[e.part], BandVertex(e.a, own), BandVertex(e.b, own), e.b, e.a);      // crease -> the pushed hull
        }

        // The vertex that stays on the edge; the tip is the next one.
        int BandVertex(int v, Vector3 own)
        {
            if (bandOf.TryGetValue(v, out int index)) return index;
            Vector3 direction = Unit(outward[point[v]]);
            if (IsZero(direction)) direction = own;
            index = positions.Count;
            positions.Add(vertices[v] - directions[v] * (tip * 0.5f));     // a hair inside: apart from the hull vertex on export
            directions.Add(directions[v]);
            band.Add(new Vector2(OnEdge, BandMark));
            from.Add(v);
            positions.Add(vertices[v] + direction * tip);
            directions.Add(direction);
            band.Add(new Vector2(AtTip, BandMark));
            from.Add(v);
            bandOf.Add(v, index);
            return index;
        }

        var triangles = new int[parts.Length][];
        for (int s = 0; s < parts.Length; s++) triangles[s] = lists[s].ToArray();
        return new CS_MayaPreviewOutlineHull
        {
            positions = positions.ToArray(), directions = directions.ToArray(), band = band.ToArray(), from = from.ToArray(),
            triangles = triangles, bandEdges = open.Count
        };
    }

    // Both windings: the shader cuts front faces, so exactly one of them shows from either side.
    private static void Quad(List<int> to, int a, int b, int c, int d)
    {
        to.Add(a); to.Add(b); to.Add(c);
        to.Add(a); to.Add(c); to.Add(d);
        to.Add(c); to.Add(b); to.Add(a);
        to.Add(d); to.Add(c); to.Add(a);
    }

    private static void AddEdge(Dictionary<(int, int), Edge> edges, Vector3[] vertices,
                                int from, int to, int a, int b, int third, int part, Vector3 face)
    {
        (int, int) key = from < to ? (from, to) : (to, from);
        if (edges.TryGetValue(key, out Edge edge))
        {
            edge.uses++;
            if (edge.uses == 2)
            {
                edge.third2 = third;
                edge.face2 = face;
            }
            if (edge.uses == 2 && Vector3.Dot(face, edge.face) < -SameSide)
            {
                Vector3 along = Unit(vertices[edge.b] - vertices[edge.a]);
                edge.folded = Vector3.Dot(Across(vertices[edge.third] - vertices[edge.a], along),
                                          Across(vertices[third] - vertices[edge.a], along)) > SameSide;
            }
        }
        else edge = new Edge { uses = 1, part = part, a = a, b = b, third = third, face = face };
        edges[key] = edge;
    }

    private static (int, int, int) Sorted(int a, int b, int c)
    {
        int low = System.Math.Min(a, System.Math.Min(b, c)), high = System.Math.Max(a, System.Math.Max(b, c));
        return (low, a + b + c - low - high, high);
    }

    // point[v] = the welded point of vertex v
    private static int[] Weld(Vector3[] vertices, double near, out int points)
    {
        var point = new int[vertices.Length];
        var at = new List<Vector3>();
        var next = new List<int>();                     // chain of the points in one cell
        var head = new Dictionary<(long, long, long), int>(vertices.Length);
        var exact = new Dictionary<(float, float, float), int>(vertices.Length);     // not Vector3: its hash collides on grids
        double size = near > 0 ? near * 2 : 1;
        for (int v = 0; v < vertices.Length; v++)
        {
            Vector3 p = vertices[v];
            if (exact.TryGetValue((p.x, p.y, p.z), out int found)) { point[v] = found; continue; }
            found = -1;
            long x = (long)System.Math.Floor((p.x - near) / size);
            long y = (long)System.Math.Floor((p.y - near) / size);
            long z = (long)System.Math.Floor((p.z - near) / size);
            for (int k = 0; k < 8 && found < 0; k++)
            {
                if (!head.TryGetValue((x + (k & 1), y + ((k >> 1) & 1), z + ((k >> 2) & 1)), out int q)) continue;
                for (; q >= 0; q = next[q])
                {
                    if ((at[q] - p).magnitude > near) continue;
                    found = q;
                    break;
                }
            }
            if (found < 0)
            {
                found = at.Count;
                (long, long, long) cell = ((long)System.Math.Floor(p.x / size), (long)System.Math.Floor(p.y / size), (long)System.Math.Floor(p.z / size));
                next.Add(head.TryGetValue(cell, out int before) ? before : -1);
                head[cell] = found;
                at.Add(p);
            }
            exact[(p.x, p.y, p.z)] = found;
            point[v] = found;
        }
        points = at.Count;
        return point;
    }

    private static Vector3 Across(Vector3 v, Vector3 along) => Unit(v - along * Vector3.Dot(v, along));

    private static float Diagonal(Vector3[] vertices)
    {
        if (vertices.Length == 0) return 0f;
        Vector3 min = vertices[0], max = min;
        foreach (Vector3 p in vertices)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return (max - min).magnitude;
    }

    private static float Angle(Vector3 at, Vector3 next, Vector3 last)
    {
        double d = Vector3.Dot(Unit(next - at), Unit(last - at));
        return (float)System.Math.Acos(d < -1 ? -1 : d > 1 ? 1 : d);
    }

    // Not Vector3.normalized / == : they treat very small triangles (a model in metres) as zero.
    private static Vector3 Unit(Vector3 v)
    {
        float length = v.magnitude;
        return length > 1e-20f ? v / length : new Vector3(0f, 0f, 0f);
    }

    private static bool IsZero(Vector3 v) => v.x == 0f && v.y == 0f && v.z == 0f;
}
