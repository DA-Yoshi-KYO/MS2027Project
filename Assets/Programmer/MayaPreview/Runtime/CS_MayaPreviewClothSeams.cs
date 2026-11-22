using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewClothSeams
 Summary: Where SHG_NTE_Cloth sews by itself: along the borders of the UV islands (= the pieces the
          garment is cut from: hems, cuffs, seams). Measured only, the same for every model.
          One map per material (meshes that share a material share its picture):
            R    = distance from the border, 0..Reach metres (1 = none near)
            G, B = length along the border, 16 bit of 0..Wrap metres (stitches keep their real length)
            A    = direction of the border on the texture (metres, 0..1 = one turn)
          A texel only listens to the borders of the piece that covers it: nothing leaks into the next piece.
          Read by NTE_SeamAt in NTE_Toon.hlsl. Made when stitches along the edge are on, written as a
          png for the export. Made on a worker thread: a heavy costume does not stop the preview
          (the stitches appear when it is done).
 History: 2026/11/22 New
          2026/11/22 Worker thread, keys without collisions (heavy meshes)
=====================================+
*/
public static class CS_MayaPreviewClothSeams
{
    public const string MapProp = "_ClothSeamMap";
    private const string StrengthProp = "_ClothStitchStrength";
    private const string EdgeProp = "_ClothStitchEdge";
    private const float Reach = 0.025f;         // NTE_SEAM_REACH
    private const float Wrap = 20f;             // NTE_SEAM_WRAP
    private const float OutsideTexels = 1.5f;   // a little over the border, for the filtering
    private const int RepeatLimit = 8;          // a tiled UV is followed this many times round the picture

    private struct Segment
    {
        public Vector2 a, b;                    // the inside of the island is on the left of a -> b
        public Vector2 size;                    // metres in one UV unit
    }

    // What a mesh gave: copies of its arrays, which nobody writes to afterwards.
    private class Source
    {
        public Vector3[] vertices;
        public Vector2[] uv, local;
        public int[] triangles;
    }

    private class Made
    {
        public bool same;       // the map there is still right
        public long hash;
        public int resolution;
        public Color32[] pixels;        // null = no borders
    }

    private class State
    {
        public Material material;
        public readonly Dictionary<Mesh, List<Source>> meshes = new Dictionary<Mesh, List<Source>>();
        public Texture2D map;
        public long hash;
        public bool dirty;
        public Task<Made> work;
    }

    private sealed class EdgeComparer : IEqualityComparer<long>
    {
        public bool Equals(long a, long b) => a == b;

        public int GetHashCode(long key)
        {
            // not long's own (low ^ high): it collides for neighbours
            unchecked { return (int)(((ulong)key * 0x9E3779B97F4A7C15ul) >> 32); }
        }
    }

    private static readonly EdgeComparer s_edgeComparer = new EdgeComparer();

    private static readonly List<State> s_states = new List<State>();


    // =========================================================
    // From CS_MayaPreviewMeshData
    // =========================================================

    // The mesh is measured again: what it gave before does not count any more.
    public static void Forget(Mesh mesh)
    {
        foreach (State state in s_states)
            if (state.meshes.Remove(mesh)) state.dirty = true;
    }

    // local = the island sizes of CS_MayaPreviewMeshData (+ ClothOffset).
    public static void Collect(Material material, Mesh mesh, Vector3[] vertices, Vector2[] uv, int[] triangles, Vector2[] local)
    {
        State state = StateOf(material);
        if (state == null)
        {
            state = new State { material = material };
            s_states.Add(state);
        }
        if (!state.meshes.TryGetValue(mesh, out List<Source> sources))
        {
            sources = new List<Source>();
            state.meshes.Add(mesh, sources);
        }
        sources.Add(new Source { vertices = vertices, uv = uv, triangles = triangles, local = local });
        state.dirty = true;
    }

    // Worker thread.
    private static void AddBorders(List<Segment> segments, List<Vector2> covered, Source source)
    {
        Vector3[] vertices = source.vertices;
        Vector2[] uv = source.uv, local = source.local;
        int[] triangles = source.triangles;
        // The same place on the model and on the texture = one point, however the mesh splits its vertices.
        var points = new Dictionary<CS_MayaPreviewMeshData.PointKey, int>(triangles.Length);
        var edges = new Dictionary<long, int>(triangles.Length, s_edgeComparer);
        var faces = new HashSet<(int, int, int)>();
        var firsts = new List<(int a, int b, int c)>();
        var uses = new List<int>();

        int Point(int vertex)
        {
            var key = new CS_MayaPreviewMeshData.PointKey(vertices[vertex], uv[vertex]);
            if (!points.TryGetValue(key, out int point))
            {
                point = points.Count;
                points.Add(key, point);
            }
            return point;
        }

        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int va = triangles[i], vb = triangles[i + 1], vc = triangles[i + 2];
            if (Cross(uv[vb] - uv[va], uv[vc] - uv[va]) == 0f) continue;
            covered.Add(uv[va]);
            covered.Add(uv[vb]);
            covered.Add(uv[vc]);
            int pa = Point(va), pb = Point(vb), pc = Point(vc);
            int low = Math.Min(pa, Math.Min(pb, pc)), high = Math.Max(pa, Math.Max(pb, pc));
            if (!faces.Add((low, pa + pb + pc - low - high, high))) continue;       // the back of a doubled face

            for (int e = 0; e < 3; e++)
            {
                int from = e == 0 ? va : e == 1 ? vb : vc;
                int to = e == 0 ? vb : e == 1 ? vc : va;
                int other = e == 0 ? vc : e == 1 ? va : vb;
                int p = e == 0 ? pa : e == 1 ? pb : pc;
                int q = e == 0 ? pb : e == 1 ? pc : pa;
                long key = ((long)Math.Min(p, q) << 32) | (uint)Math.Max(p, q);
                if (edges.TryGetValue(key, out int index)) uses[index]++;
                else
                {
                    edges.Add(key, firsts.Count);
                    firsts.Add((from, to, other));
                    uses.Add(1);
                }
            }
        }

        float offset = CS_MayaPreviewMeshData.ClothOffset;
        for (int i = 0; i < firsts.Count; i++)
        {
            if (uses[i] != 1) continue;
            (int a, int b, int c) = firsts[i];
            bool left = Cross(uv[b] - uv[a], uv[c] - uv[a]) > 0f;
            segments.Add(new Segment
            {
                a = uv[left ? a : b],
                b = uv[left ? b : a],
                size = new Vector2(Mathf.Max(local[a].x - offset, 1e-5f), Mathf.Max(local[a].y - offset, 1e-5f))
            });
        }
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static State StateOf(Material material)
    {
        foreach (State state in s_states)
            if (ReferenceEquals(state.material, material)) return state;
        return null;
    }


    // =========================================================
    // Map
    // =========================================================

    // Every frame from the Inspector: only does something after the mesh or the switches changed.
    public static void Tick()
    {
        for (int i = s_states.Count - 1; i >= 0; i--)
        {
            State state = s_states[i];
            if (state.material == null)
            {
                Discard(state.map);
                s_states.RemoveAt(i);
                continue;
            }
            if (state.work != null)
            {
                if (!state.work.IsCompleted) continue;      // still being made: look again next frame
                Take(state);
            }
            foreach (Mesh mesh in state.meshes.Keys)
            {
                if (mesh != null) continue;
                state.dirty = true;         // deleted in Maya / another .mb
                break;
            }
            if (state.dirty) Begin(state);
        }
    }

    // Waits until every map is up to date (the export).
    public static void Complete()
    {
        Tick();
        foreach (State state in s_states) Complete(state);
    }

    private static void Complete(State state)
    {
        if (state.work != null) Take(state);
        if (state.dirty) Begin(state);
        if (state.work != null) Take(state);
    }

    private static void Begin(State state)
    {
        Material material = state.material;
        List<Mesh> gone = null;
        foreach (Mesh mesh in state.meshes.Keys)
            if (mesh == null) (gone ?? (gone = new List<Mesh>())).Add(mesh);
        if (gone != null)
            foreach (Mesh mesh in gone) state.meshes.Remove(mesh);
        if (!material.HasProperty(MapProp) || !material.HasProperty(EdgeProp) || !material.HasProperty(StrengthProp)) return;
        if (material.GetFloat(StrengthProp) <= 0f || material.GetFloat(EdgeProp) < 0.5f) return;    // made when switched on
        state.dirty = false;
        var sources = new List<Source>();
        foreach (List<Source> list in state.meshes.Values) sources.AddRange(list);
        bool had = state.map != null;
        long before = state.hash;
        state.work = Task.Run(() => Make(sources, had, before));
    }

    // Main thread: the picture of what the worker made.
    private static void Take(State state)
    {
        Task<Made> work = state.work;
        state.work = null;
        Made made;
        try { made = work.GetAwaiter().GetResult(); }
        catch (Exception exception)
        {
            Debug.LogWarning("Maya Preview : the cloth's seam map could not be made\n" + exception);
            return;
        }
        if (state.material == null) return;
        if (!made.same)
        {
            Discard(state.map);
            state.map = made.pixels != null ? MapOf(made) : null;
            state.hash = made.hash;
            CS_MayaPreviewIdleThrottle.Wake();      // draw it
        }
        state.material.SetTexture(MapProp, state.map);
    }

    // Worker thread.
    private static Made Make(List<Source> sources, bool had, long before)
    {
        var found = new List<Segment>();
        var triangles = new List<Vector2>();
        foreach (Source source in sources) AddBorders(found, triangles, source);

        var all = new List<Segment>(found.Count);
        var seen = new HashSet<(Vector2Int, Vector2Int)>();
        float longest = 0f;
        long hash = triangles.Count;
        foreach (Segment segment in found)
        {
            Vector2Int a = Rounded(segment.a), b = Rounded(segment.b);
            if (a == b || !seen.Add((a, b))) continue;                  // pieces lying on each other
            all.Add(segment);
            longest = Mathf.Max(longest, Mathf.Max(segment.size.x, segment.size.y));
            unchecked
            {
                long one = a.x;
                one = (one * 1000003L + a.y) * 1000003L + b.x;
                one = one * 1000003L + b.y;
                // sizes to 2 %: dragging a vertex in Maya does not make the map again on every step
                one = one * 1000003L + Mathf.RoundToInt(Mathf.Log(segment.size.x) * 50f);
                one = one * 1000003L + Mathf.RoundToInt(Mathf.Log(segment.size.y) * 50f);
                hash += (one ^ (one >> 29)) * -7046029254386353131L;    // a sum: the order of the meshes does not matter
            }
        }
        if (had && hash == before) return new Made { same = true };
        var made = new Made { hash = hash, resolution = longest > 2f ? 2048 : 1024 };
        if (all.Count > 0) made.pixels = Bake(all, triangles, made.resolution);
        return made;
    }

    private static Vector2Int Rounded(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x * 100000f), Mathf.RoundToInt(p.y * 100000f));

    // Which piece covers every texel (-1 = none). Connected on the texture = one piece.
    private static int[] Cover(List<Vector2> corners, int resolution, Dictionary<Vector2Int, int> pointOf, out int[] pieceOf)
    {
        var parent = new List<int>();
        var points = new int[corners.Count];
        for (int i = 0; i < corners.Count; i++)
        {
            Vector2Int key = Rounded(corners[i]);
            if (!pointOf.TryGetValue(key, out int point))
            {
                point = parent.Count;
                parent.Add(point);
                pointOf.Add(key, point);
            }
            points[i] = point;
        }
        for (int i = 0; i + 2 < points.Length; i += 3)
        {
            int a = Root(parent, points[i]), b = Root(parent, points[i + 1]);
            if (a != b) parent[b] = a;
            int c = Root(parent, points[i + 2]);
            if (a != c) parent[c] = a;
        }
        pieceOf = new int[parent.Count];
        for (int i = 0; i < pieceOf.Length; i++) pieceOf[i] = Root(parent, i);

        var cover = new int[resolution * resolution];
        for (int i = 0; i < cover.Length; i++) cover[i] = -1;
        for (int i = 0; i + 2 < corners.Count; i += 3)
        {
            Vector2 a = corners[i] * resolution, b = corners[i + 1] * resolution, c = corners[i + 2] * resolution;
            float area = Cross(b - a, c - a);
            if (area == 0f) continue;
            float turn = area > 0f ? 1f : -1f;
            int minX = Mathf.CeilToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 0.5f);
            int minY = Mathf.CeilToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - 0.5f);
            int maxX = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - 0.5f);
            int maxY = Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - 0.5f);
            maxX = Mathf.Min(maxX, minX + resolution * RepeatLimit);
            maxY = Mathf.Min(maxY, minY + resolution * RepeatLimit);
            int piece = pieceOf[points[i]];
            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;
                int row = ((y % resolution) + resolution) % resolution * resolution;
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f;
                    if (((b.x - a.x) * (py - a.y) - (b.y - a.y) * (px - a.x)) * turn < -0.001f) continue;
                    if (((c.x - b.x) * (py - b.y) - (c.y - b.y) * (px - b.x)) * turn < -0.001f) continue;
                    if (((a.x - c.x) * (py - c.y) - (a.y - c.y) * (px - c.x)) * turn < -0.001f) continue;
                    cover[row + ((x % resolution) + resolution) % resolution] = piece;
                }
            }
        }
        return cover;
    }

    private static int Root(List<int> parent, int point)
    {
        while (parent[point] != point)
        {
            parent[point] = parent[parent[point]];
            point = parent[point];
        }
        return point;
    }

    private static Color32[] Bake(List<Segment> segments, List<Vector2> triangles, int resolution)
    {
        var pointOf = new Dictionary<Vector2Int, int>(triangles.Count);
        int[] cover = Cover(triangles, resolution, pointOf, out int[] pieceOf);
        int count = segments.Count;
        var lengths = new float[count];
        var directions = new Vector2[count];
        var along = new float[count];
        var next = new int[count];
        var previous = new int[count];
        var starts = new Dictionary<Vector2Int, int>(count);
        for (int i = 0; i < count; i++)
        {
            Vector2 d = Vector2.Scale(segments[i].b - segments[i].a, segments[i].size);
            lengths[i] = d.magnitude;
            directions[i] = d / Mathf.Max(lengths[i], 1e-12f);
            next[i] = previous[i] = -1;
            starts[Rounded(segments[i].a)] = i;
        }
        for (int i = 0; i < count; i++)
        {
            if (!starts.TryGetValue(Rounded(segments[i].b), out int after) || previous[after] >= 0) continue;
            next[i] = after;
            previous[after] = i;
        }

        // length along every chain (open chains from their start first, then the closed ones)
        var done = new bool[count];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int first = 0; first < count; first++)
            {
                if (done[first] || (pass == 0 && previous[first] >= 0)) continue;
                float length = 0f;
                for (int i = first; i >= 0 && !done[i]; i = next[i])
                {
                    done[i] = true;
                    along[i] = length;
                    length += lengths[i];
                }
            }
        }

        var pixels = new Color32[resolution * resolution];
        var nearest = new float[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(255, 0, 0, 0);
            nearest[i] = float.MaxValue;
        }

        for (int k = 0; k < count; k++)
        {
            Segment segment = segments[k];
            float ux = directions[k].x, uy = directions[k].y, length = lengths[k];
            if (length <= 0f) continue;
            if (!pointOf.TryGetValue(Rounded(segment.a), out int startPoint)) continue;
            int piece = pieceOf[startPoint];
            float sx = segment.size.x / resolution, sy = segment.size.y / resolution;     // metres in one texel
            float outside = OutsideTexels * Mathf.Max(sx, sy);
            float ax = segment.a.x * resolution, ay = segment.a.y * resolution;
            float bx = segment.b.x * resolution, by = segment.b.y * resolution;
            float padX = Mathf.Min(Reach / sx + 1f, resolution / 4), padY = Mathf.Min(Reach / sy + 1f, resolution / 4);
            int minX = Mathf.FloorToInt(Mathf.Min(ax, bx) - padX), minY = Mathf.FloorToInt(Mathf.Min(ay, by) - padY);
            int maxX = Mathf.Min(Mathf.CeilToInt(Mathf.Max(ax, bx) + padX), minX + resolution * RepeatLimit);
            int maxY = Mathf.Min(Mathf.CeilToInt(Mathf.Max(ay, by) + padY), minY + resolution * RepeatLimit);
            byte angle = (byte)(Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(uy, ux) / (2f * Mathf.PI), 1f) * 255f) % 255);

            for (int y = minY; y <= maxY; y++)
            {
                float qy = (y + 0.5f - ay) * sy;
                int row = ((y % resolution) + resolution) % resolution * resolution;
                for (int x = minX; x <= maxX; x++)
                {
                    int at = row + ((x % resolution) + resolution) % resolution;
                    bool inside = cover[at] == piece;
                    if (!inside && cover[at] >= 0) continue;        // another piece's
                    float qx = (x + 0.5f - ax) * sx;
                    float t = qx * ux + qy * uy;
                    float clamped = t < 0f ? 0f : t > length ? length : t;
                    float px = qx - ux * clamped, py = qy - uy * clamped;
                    float squared = px * px + py * py;
                    if (squared >= Reach * Reach) continue;
                    float distance = Mathf.Sqrt(squared);
                    float rank = distance;
                    if (!inside)
                    {
                        if (distance > outside) continue;
                        rank += Reach;      // any inside texel wins
                        distance = 0f;
                    }
                    if (rank >= nearest[at]) continue;
                    nearest[at] = rank;
                    float position = Mathf.Repeat(along[k] + t, Wrap) / Wrap;
                    int stored = Mathf.Min((int)(position * 65535f + 0.5f), 65535);
                    pixels[at] = new Color32((byte)Mathf.Min(254, (int)(distance / Reach * 255f + 0.5f)),
                                             (byte)(stored >> 8), (byte)(stored & 255), angle);
                }
            }
        }

        return pixels;
    }

    private static Texture2D MapOf(Made made)
    {
        var map = new Texture2D(made.resolution, made.resolution, TextureFormat.RGBA32, false, true)
        {
            name = "ClothSeams", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave
        };
        map.SetPixels32(made.pixels);
        map.Apply(false, false);        // readable: the export writes it as png
        return map;
    }

    private static void Discard(Texture2D map)
    {
        if (map == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(map);
        else UnityEngine.Object.DestroyImmediate(map);
    }


    // =========================================================
    // Export
    // =========================================================

    // The png of the material's map, or null.
    public static string FileOf(Material material)
    {
        State state = StateOf(material);
        if (state == null) return null;
        Complete(state);
        if (state.map == null) return null;
        string name = material.name;
        foreach (char bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        string folder = Path.Combine(Application.temporaryCachePath, "ClothSeams", state.hash.ToString("X16"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "TX_" + name + "_Seams.png");
        File.WriteAllBytes(file, state.map.EncodeToPNG());
        return file.Replace('\\', '/');
    }
}
