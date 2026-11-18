/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-16 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// 配置ツールで、悪人・警察のグループ・巡回ドローンの道具を選んでいる間に、Sceneビューへ重ねる、配置の分布の表示
/// ・地面の色: 一番近い物の影響範囲までの距離で、緑(足りている)〜赤(過疎)に塗り分ける
///   影響範囲は、巡回ルート(線)。悪人は、設定で、出現点からの円にもできる
/// ・巡回ルート: 1つずつの色の線(悪人はパターンのルートの色、警察・ドローンは、グループごとに別の色)
/// ・区画の境界線と、区画ごとの個数・密度(歩ける広さ1万㎡あたり)。平均の半分より少ない区画は「過疎」
/// ・悪人の印: 区画の色の丸(地上)・ひし形(屋上)。親の犯罪完遂エリアが、区画と合わない物には「!」
/// ・空いている場所: 近くに何も無い場所の候補(最大5か所)
/// 地面の色は、悪人は地上の物だけ、警察は地上、ドローンは飛んでいる高さに関わらず、水平の距離で測る
/// </summary>
[InitializeOnLoad]
public static class CSED_PlacementOverlay
{
    /// <summary>
    /// 分布を表示する物の種類
    /// </summary>
    public enum Kind
    {
        Villain,    // 悪人の出現点(巡回グループ)
        Police,     // 警察のグループ
        Drone,      // 巡回ドローン
    }

    private const float _cellSize = 12.0f;               // 地面を塗り分ける升目の大きさ(m)
    private const float _groundHeight = 3.0f;            // これより低い所は、地上(m)
    private const float _gapSpacing = 50.0f;             // 空いている場所の候補同士を離す距離(m)
    private const int _gapCount = 5;                     // 空いている場所の候補の数
    private const float _sparseRatio = 0.5f;             // 平均の密度のこの割合より少ない区画は、過疎とみなす
    private const float _maxOffset = 15.0f;              // 出現点が、地面から浮いている高さの上限(m。巡回パターンから求める値の範囲)
    private static readonly Color[] _sectorColors = { Color.white, new Color(0.3f, 1.0f, 0.4f), new Color(1.0f, 0.3f, 0.3f), new Color(1.0f, 0.9f, 0.2f) };
    private static readonly string[] _kindNames = { "悪人", "警察のグループ", "巡回ドローン" };
    private static readonly string[] _kindUnits = { "個", "グループ", "機" };

    /// <summary>
    /// 1つ分(悪人の出現点・警察のグループ・巡回ドローン)の情報
    /// </summary>
    public struct UnitInfo
    {
        public string name;
        public Vector3 ground;      // 代表の位置(悪人は、出現点を地面へ下ろした位置 / 警察・ドローンは、ルートの最初の点)
        public bool isRoof;         // 屋上か(悪人だけ。地面の高さが3m以上)。地面の色には数えない
        public int sector;          // 位置の区画(1〜3)
        public int parentArea;      // 今の親の犯罪完遂エリアの番号(悪人だけ。1〜3。それ以外は0。悪人でなければ -1)
        public List<Vector3[]> routes;      // ルート(ワールドの点の並び。周回は、最後に最初の点を足してある)
        public List<Color> routeColors;     // ルートの色
    }

    /// <summary>
    /// 区画ごとの集計
    /// </summary>
    public struct SectorStats
    {
        public int count;           // 区画の位置にある物の数
        public int mismatch;        // うち、親の犯罪完遂エリアが、区画と合わない数(悪人だけ)
        public float area;          // 歩ける地上の広さ(㎡)
        public float density;       // 歩ける広さ1万㎡あたりの数
        public bool sparse;         // 過疎か
    }

    private struct Cell
    {
        public Vector3 center;
        public int sector;
    }

    // 種類ごとの、集めた物・升目ごとの距離・集計・空いている場所
    private class Layer
    {
        public readonly List<UnitInfo> units = new List<UnitInfo>();
        public float[] distances = new float[0];
        public readonly SectorStats[] stats = new SectorStats[4];
        public readonly List<Vector3> gaps = new List<Vector3>();
        public bool dirty = true;
        public double lastRefresh;
        public float lastRadius = -1.0f;
        public bool lastUseRoutes = true;
    }

    private static readonly Layer[] _layers = { new Layer(), new Layer(), new Layer() };
    private static readonly List<Cell> _cells = new List<Cell>();
    private static bool _cellsBuilt;
    private static int _navMeshVertexCount = -1;

    static CSED_PlacementOverlay()
    {
        EditorApplication.hierarchyChanged += MarkDirty;
        Undo.undoRedoPerformed += MarkDirty;
    }

    public static void MarkDirty()
    {
        foreach (Layer layer in _layers) layer.dirty = true;
    }

    public static string GetKindName(Kind kind)
    {
        return _kindNames[(int)kind];
    }

    /// <summary>
    /// 区画ごとの集計を返すメソッド(1〜3。0は使わない)。ウィンドウの表示に使う
    /// </summary>
    public static SectorStats[] GetStats(Kind kind)
    {
        Refresh(kind);
        return _layers[(int)kind].stats;
    }

    public static int GetCount(Kind kind)
    {
        return _layers[(int)kind].units.Count;
    }

    /// <summary>
    /// 位置から、一番近い物の影響範囲までの水平距離(m)を返すメソッド(地面の色に数える物がなければ -1)
    /// </summary>
    public static float GetNearestDistance(Kind kind, Vector3 position)
    {
        Refresh(kind);
        float min = float.MaxValue;
        foreach (UnitInfo unit in _layers[(int)kind].units)
        {
            if (unit.isRoof) continue;

            min = Mathf.Min(min, GetInfluenceDistance(kind, unit, new Vector2(position.x, position.z)));
        }
        return min == float.MaxValue ? -1.0f : min;
    }

    // =====================================================================
    // 集計
    // =====================================================================

    /// <summary>
    /// 物の位置・升目ごとの距離・区画ごとの集計を、最新にするメソッド(階層が変わった時・設定が変わった時だけ、やり直す)
    /// </summary>
    private static void Refresh(Kind kind)
    {
        Layer layer = _layers[(int)kind];
        bool settingChanged = !Mathf.Approximately(layer.lastRadius, CSED_PlacementState.GetCoverRadius(kind)) || layer.lastUseRoutes != CSED_PlacementState.villainUseRoutes;
        if (!layer.dirty && !settingChanged) return;

        // 階層の変更が続いても、毎回やり直さない(0.3秒に1回まで)
        if (!settingChanged && EditorApplication.timeSinceStartup - layer.lastRefresh < 0.3) return;

        layer.dirty = false;
        layer.lastRadius = CSED_PlacementState.GetCoverRadius(kind);
        layer.lastUseRoutes = CSED_PlacementState.villainUseRoutes;
        layer.lastRefresh = EditorApplication.timeSinceStartup;
        Collect(kind, layer);
        BuildCells();
        ComputeDistances(kind, layer);
        Aggregate(kind, layer);
    }

    private static void Collect(Kind kind, Layer layer)
    {
        layer.units.Clear();
        switch (kind)
        {
            case Kind.Villain:
                CollectVillains(layer);
                break;
            case Kind.Police:
                int policeIndex = 0;
                foreach (CS_PoliceSquad squad in CSED_PlacementBuilder.GetInField<CS_PoliceSquad>())
                {
                    List<Transform> points = squad.GetRouteDrawPoints();
                    if (points.Count == 0) continue;

                    Vector3[] positions = new Vector3[points.Count];
                    for (int i = 0; i < points.Count; i++) positions[i] = points[i].position;
                    AddUnit(layer, squad.name, positions, Color.HSVToRGB((policeIndex++ * 0.13f) % 1.0f, 0.55f, 1.0f));
                }
                break;
            default:
                int droneIndex = 0;
                foreach (CS_SecurityDroneSpawner drone in CSED_PlacementBuilder.GetInField<CS_SecurityDroneSpawner>())
                {
                    if (drone.transform.childCount == 0) continue;

                    Vector3[] positions = new Vector3[drone.transform.childCount];
                    for (int i = 0; i < positions.Length; i++) positions[i] = drone.transform.GetChild(i).position;
                    AddUnit(layer, drone.name, positions, Color.HSVToRGB(0.08f + (droneIndex++ * 0.09f) % 0.3f, 0.8f, 1.0f));
                }
                break;
        }
    }

    // 警察・ドローン: 巡回ポイントを順につないだ、周回するルート(3つ以上なら、最後から最初へもつなぐ)を、1つの物として足す
    private static void AddUnit(Layer layer, string unitName, Vector3[] points, Color color)
    {
        Vector3 centroid = Vector3.zero;
        foreach (Vector3 point in points) centroid += point;
        centroid /= points.Length;

        Vector3[] route = points;
        if (points.Length > 2)
        {
            route = new Vector3[points.Length + 1];
            points.CopyTo(route, 0);
            route[points.Length] = points[0];
        }

        layer.units.Add(new UnitInfo
        {
            name = unitName,
            ground = points[0],
            isRoof = false,
            sector = CSED_FieldMultiLayoutGenerator.GetVillainSector(centroid),
            parentArea = -1,
            routes = new List<Vector3[]> { route },
            routeColors = new List<Color> { color },
        });
    }

    private static void CollectVillains(Layer layer)
    {
        foreach (CS_VillainSpawnPoint spawn in CSED_PlacementBuilder.GetVillains())
        {
            float offset = 0.0f;
            CS_VillainPatrolRoute route = spawn.GetComponent<CS_VillainPatrolRoute>();
            if (route != null && route.hasPattern && route.pattern.routes[0].points.Count > 0) offset = Mathf.Clamp(-route.pattern.routes[0].points[0].localPosition.y, 0.0f, _maxOffset);

            Vector3 ground = spawn.transform.position - Vector3.up * offset;
            int parentArea = 0;
            if (spawn.transform.parent != null && spawn.transform.parent.name.StartsWith("CrimeArea_0") && spawn.transform.parent.name.Length == 12) parentArea = spawn.transform.parent.name[11] - '0';

            CollectRoutes(spawn, route, ground, out List<Vector3[]> routes, out List<Color> routeColors);
            layer.units.Add(new UnitInfo
            {
                routes = routes,
                routeColors = routeColors,
                name = spawn.name,
                ground = ground,
                isRoof = ground.y >= _groundHeight,
                sector = CSED_FieldMultiLayoutGenerator.GetVillainSector(ground),
                parentArea = parentArea,
            });
        }
    }

    /// <summary>
    /// 悪人グループの巡回ルート(ワールドの点の並び)を集めるメソッド
    /// 巡回パターンがあれば、パターンのルートごと(出現点の向きを掛けた位置。周回は、最後から最初へもつなぐ)。無ければ、往復の2点(出現点の前後)をつなぐ線。どちらも無ければ、出現点の地面の1点
    /// </summary>
    private static void CollectRoutes(CS_VillainSpawnPoint spawn, CS_VillainPatrolRoute route, Vector3 ground, out List<Vector3[]> routes, out List<Color> colors)
    {
        routes = new List<Vector3[]>();
        colors = new List<Color>();
        if (route != null && route.hasPattern)
        {
            foreach (CSO_VillainPatrolPattern.Route patrolRoute in route.pattern.routes)
            {
                int count = patrolRoute.points.Count;
                if (count == 0) continue;

                bool closed = patrolRoute.loopMode == CSE_VillainPatrolLoopMode.Loop && count > 2;
                Vector3[] points = new Vector3[closed ? count + 1 : count];
                for (int i = 0; i < count; i++) points[i] = route.ToWorld(patrolRoute.points[i].localPosition);
                if (closed) points[count] = points[0];
                routes.Add(points);
                colors.Add(patrolRoute.color);
            }
        }
        else
        {
            spawn.GetPatrolEnds(out Vector3 endA, out Vector3 endB);
            routes.Add(new[] { endA, endB });
            colors.Add(Color.white);
        }

        if (routes.Count == 0)
        {
            routes.Add(new[] { ground });
            colors.Add(Color.white);
        }
    }

    /// <summary>
    /// 水平の点から、1つ分の影響範囲までの距離(m)を返すメソッド
    /// ルート(線)までの一番近い距離(ルートの上は 0)。悪人で「巡回ルートで測る」がオフなら、出現点(を地面へ下ろした点)までの距離
    /// </summary>
    private static float GetInfluenceDistance(Kind kind, UnitInfo unit, Vector2 point)
    {
        if (kind == Kind.Villain && !CSED_PlacementState.villainUseRoutes) return Vector2.Distance(point, new Vector2(unit.ground.x, unit.ground.z));

        float min = float.MaxValue;
        foreach (Vector3[] route in unit.routes)
        {
            if (route.Length == 1)
            {
                min = Mathf.Min(min, Vector2.Distance(point, new Vector2(route[0].x, route[0].z)));
                continue;
            }

            for (int i = 0; i + 1 < route.Length; i++) min = Mathf.Min(min, DistanceToSegment(point, new Vector2(route[i].x, route[i].z), new Vector2(route[i + 1].x, route[i + 1].z)));
        }
        return min;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        float t = lengthSquared < 0.0001f ? 0.0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>
    /// 歩ける地上(NavMesh)を、_cellSize の升目にして、升目の中心・区画を求めるメソッド(NavMeshが変わった時だけ、やり直す)
    /// </summary>
    private static void BuildCells()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        if (_cellsBuilt && triangulation.vertices.Length == _navMeshVertexCount) return;

        _cellsBuilt = true;
        _navMeshVertexCount = triangulation.vertices.Length;
        _cells.Clear();
        Dictionary<Vector2Int, (float sumY, int count)> grid = new Dictionary<Vector2Int, (float, int)>();
        for (int i = 0; i + 2 < triangulation.indices.Length; i += 3)
        {
            Vector3 a = triangulation.vertices[triangulation.indices[i]];
            Vector3 b = triangulation.vertices[triangulation.indices[i + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[i + 2]];
            if (a.y >= _groundHeight || b.y >= _groundHeight || c.y >= _groundHeight) continue;

            int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x) / _cellSize);
            int maxX = Mathf.FloorToInt(Mathf.Max(a.x, b.x, c.x) / _cellSize);
            int minZ = Mathf.FloorToInt(Mathf.Min(a.z, b.z, c.z) / _cellSize);
            int maxZ = Mathf.FloorToInt(Mathf.Max(a.z, b.z, c.z) / _cellSize);
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector2 center = new Vector2((x + 0.5f) * _cellSize, (z + 0.5f) * _cellSize);
                    if (!InsideTriangle(center, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z))) continue;

                    Vector2Int key = new Vector2Int(x, z);
                    grid.TryGetValue(key, out (float sumY, int count) value);
                    grid[key] = (value.sumY + (a.y + b.y + c.y) / 3.0f, value.count + 1);
                }
            }
        }

        foreach (KeyValuePair<Vector2Int, (float sumY, int count)> pair in grid)
        {
            Vector3 center = new Vector3((pair.Key.x + 0.5f) * _cellSize, pair.Value.sumY / pair.Value.count, (pair.Key.y + 0.5f) * _cellSize);
            _cells.Add(new Cell { center = center, sector = CSED_FieldMultiLayoutGenerator.GetVillainSector(center) });
        }
    }

    // 升目ごとの、一番近い物の影響範囲までの距離
    private static void ComputeDistances(Kind kind, Layer layer)
    {
        if (layer.distances.Length != _cells.Count) layer.distances = new float[_cells.Count];
        for (int i = 0; i < _cells.Count; i++)
        {
            float min = float.MaxValue;
            Vector2 point = new Vector2(_cells[i].center.x, _cells[i].center.z);
            foreach (UnitInfo unit in layer.units)
            {
                if (unit.isRoof) continue;

                min = Mathf.Min(min, GetInfluenceDistance(kind, unit, point));
            }
            layer.distances[i] = min;
        }
    }

    private static bool InsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b);
        float d2 = Cross(p, b, c);
        float d3 = Cross(p, c, a);
        bool negative = d1 < 0 || d2 < 0 || d3 < 0;
        bool positive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(negative && positive);
    }

    private static float Cross(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    /// <summary>
    /// 区画ごとの、数・親の合わない数・歩ける広さ・密度と、空いている場所の候補を求めるメソッド
    /// </summary>
    private static void Aggregate(Kind kind, Layer layer)
    {
        for (int i = 0; i < layer.stats.Length; i++) layer.stats[i] = new SectorStats();
        foreach (UnitInfo unit in layer.units)
        {
            layer.stats[unit.sector].count++;
            if (unit.parentArea >= 0 && unit.parentArea != unit.sector) layer.stats[unit.sector].mismatch++;
        }
        foreach (Cell cell in _cells) layer.stats[cell.sector].area += _cellSize * _cellSize;

        int total = 0;
        float totalArea = 0.0f;
        for (int s = 1; s <= 3; s++)
        {
            layer.stats[s].density = layer.stats[s].area > 0.0f ? layer.stats[s].count / layer.stats[s].area * 10000.0f : 0.0f;
            total += layer.stats[s].count;
            totalArea += layer.stats[s].area;
        }
        float average = totalArea > 0.0f ? total / totalArea * 10000.0f : 0.0f;
        for (int s = 1; s <= 3; s++) layer.stats[s].sparse = layer.stats[s].density < average * _sparseRatio;

        // 空いている場所: 影響範囲から遠い升目を、遠い順に、_gapSpacing 以上離して選ぶ
        layer.gaps.Clear();
        List<int> order = new List<int>();
        for (int i = 0; i < _cells.Count; i++) order.Add(i);
        order.Sort((a, b) => layer.distances[b].CompareTo(layer.distances[a]));
        foreach (int index in order)
        {
            if (layer.gaps.Count >= _gapCount || layer.distances[index] < CSED_PlacementState.GetCoverRadius(kind)) break;

            Vector3 center = _cells[index].center;
            bool near = false;
            foreach (Vector3 gap in layer.gaps)
            {
                if (Vector2.Distance(new Vector2(gap.x, gap.z), new Vector2(center.x, center.z)) < _gapSpacing) near = true;
            }
            if (!near) layer.gaps.Add(center);
        }
    }

    // =====================================================================
    // 描く
    // =====================================================================

    /// <summary>
    /// 配置の分布を、Sceneビューに重ねて描くメソッド(Repaintの時だけ。ウィンドウの設定で、種類ごとに、オフにできる)
    /// </summary>
    public static void Draw(Kind kind)
    {
        if (!CSED_PlacementState.GetOverlayEnabled(kind)) return;

        Refresh(kind);
        Layer layer = _layers[(int)kind];
        if (Event.current.type == EventType.Repaint)
        {
            DrawHeat(kind, layer);
            if (CSED_PlacementState.villainOverlayBorders) DrawBorders(kind, layer);
            DrawUnits(kind, layer);
            DrawGaps(layer);
        }
        DrawHud(kind, layer);
    }

    // 地面の色(最寄りの影響範囲までの距離が、半径の半分以内=緑 / 半径以内=黄緑 / 1.6倍以内=橙 / それより遠い=赤。何も無ければ、すべて赤)
    private static void DrawHeat(Kind kind, Layer layer)
    {
        CompareFunction previous = Handles.zTest;
        Handles.zTest = CompareFunction.LessEqual;
        float radius = CSED_PlacementState.GetCoverRadius(kind);
        float half = _cellSize * 0.5f;
        Vector3[] corners = new Vector3[4];
        for (int i = 0; i < _cells.Count && i < layer.distances.Length; i++)
        {
            Vector3 center = _cells[i].center;
            float distance = layer.distances[i];
            Color color;
            if (distance <= radius * 0.5f) color = new Color(0.2f, 0.9f, 0.3f, 0.28f);
            else if (distance <= radius) color = new Color(0.6f, 0.9f, 0.2f, 0.28f);
            else if (distance <= radius * 1.6f) color = new Color(1.0f, 0.75f, 0.1f, 0.32f);
            else color = new Color(1.0f, 0.2f, 0.2f, 0.36f);

            float y = center.y + 0.15f;
            corners[0] = new Vector3(center.x - half, y, center.z - half);
            corners[1] = new Vector3(center.x - half, y, center.z + half);
            corners[2] = new Vector3(center.x + half, y, center.z + half);
            corners[3] = new Vector3(center.x + half, y, center.z - half);
            Handles.DrawSolidRectangleWithOutline(corners, color, Color.clear);
        }
        Handles.zTest = previous;
    }

    // 区画の境界線(図の線)と、区画ごとの個数・密度の表示
    private static void DrawBorders(Kind kind, Layer layer)
    {
        Vector2[][] polygons = CSED_FieldMultiLayoutGenerator.GetSectorPolygons();
        for (int i = 0; i < polygons.Length; i++)
        {
            Handles.color = _sectorColors[i + 1];
            Vector3 centroid = Vector3.zero;
            for (int k = 0; k < polygons[i].Length; k++)
            {
                Vector3 from = CSED_FieldMultiLayoutGenerator.SectorPixelToWorld(polygons[i][k], 2.0f);
                Vector3 to = CSED_FieldMultiLayoutGenerator.SectorPixelToWorld(polygons[i][(k + 1) % polygons[i].Length], 2.0f);
                Handles.DrawAAPolyLine(4.0f, from, to);
                centroid += from;
            }
            centroid /= polygons[i].Length;

            SectorStats stats = layer.stats[i + 1];
            string status = stats.sparse ? "過疎" : "";
            Handles.Label(centroid + Vector3.up * 12.0f, $"区画{i + 1}(CrimeArea_0{i + 1})" + System.Environment.NewLine + $"{stats.count}{_kindUnits[(int)kind]} / 1万㎡あたり {stats.density:F1} {status}", EditorStyles.boldLabel);
        }
    }

    // ルートの線・印を描く。悪人: 区画の色の丸(地上)・ひし形(屋上)・親のずれの「!」 / 警察・ドローン: グループごとの色のルートと、最初の点の丸
    private static void DrawUnits(Kind kind, Layer layer)
    {
        foreach (UnitInfo unit in layer.units)
        {
            Handles.color = _sectorColors[unit.sector];
            if (kind == Kind.Villain)
            {
                if (unit.isRoof)
                {
                    Handles.DrawWireCube(unit.ground + Vector3.up * 1.0f, Vector3.one * 3.0f);
                }
                else
                {
                    Handles.DrawSolidDisc(unit.ground + Vector3.up * 0.3f, Vector3.up, 3.0f);

                    // 影響範囲の目安の輪は、出現点で測る時だけ。巡回ルートで測る時は、ルートの線を描く(色の塗り分けが、その線からの距離になる)
                    if (!CSED_PlacementState.villainUseRoutes) Handles.DrawWireDisc(unit.ground + Vector3.up * 0.3f, Vector3.up, CSED_PlacementState.GetCoverRadius(kind));
                }
            }

            if (kind != Kind.Villain || CSED_PlacementState.villainUseRoutes)
            {
                for (int r = 0; r < unit.routes.Count; r++)
                {
                    Handles.color = unit.routeColors[r];
                    Vector3[] points = unit.routes[r];
                    for (int i = 0; i + 1 < points.Length; i++) Handles.DrawAAPolyLine(4.0f, points[i], points[i + 1]);
                    foreach (Vector3 point in points) Handles.DrawSolidDisc(point + Vector3.up * 0.3f, Vector3.up, 1.0f);
                }
                if (kind != Kind.Villain)
                {
                    Handles.color = _sectorColors[unit.sector];
                    Handles.DrawSolidDisc(unit.ground + Vector3.up * 0.5f, Vector3.up, 2.5f);
                }
            }

            if (unit.parentArea < 0 || unit.parentArea == unit.sector) continue;

            Handles.color = Color.white;
            Handles.DrawLine(unit.ground, unit.ground + Vector3.up * 10.0f);
        }
    }

    // 空いている場所の候補(近くに何も無い所)
    private static void DrawGaps(Layer layer)
    {
        Handles.color = Color.white;
        foreach (Vector3 gap in layer.gaps)
        {
            Handles.DrawWireDisc(gap + Vector3.up * 0.4f, Vector3.up, 10.0f);
            Handles.DrawLine(gap, gap + Vector3.up * 8.0f);
            Handles.Label(gap + Vector3.up * 9.0f, "空き", EditorStyles.boldLabel);
        }
    }

    // 画面の左上の、まとめ
    private static void DrawHud(Kind kind, Layer layer)
    {
        string unit = _kindUnits[(int)kind];
        string extra = kind == Kind.Villain ? $"(地上 {layer.units.FindAll(v => !v.isRoof).Count} / 屋上 {layer.units.FindAll(v => v.isRoof).Count})" : "";
        Handles.BeginGUI();
        string text = $"{_kindNames[(int)kind]} {layer.units.Count}{unit}{extra}"
            + $"  /  区画1: {layer.stats[1].count}  区画2: {layer.stats[2].count}  区画3: {layer.stats[3].count}"
            + $"  /  色: 緑 = 足りている(ルートから {CSED_PlacementState.GetCoverRadius(kind):F0}m 以内) → 赤 = 過疎  /  白い輪 = 空いている場所";
        GUI.Label(new Rect(12.0f, 28.0f, 1200.0f, 22.0f), text, EditorStyles.boldLabel);
        Handles.EndGUI();
    }
}
