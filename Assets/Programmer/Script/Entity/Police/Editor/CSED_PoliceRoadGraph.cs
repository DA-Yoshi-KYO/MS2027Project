/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-19 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 灰色の道を、交点でつないだ「道の網(グラフ)」にし、その上を一周する巡回ルートを作るエディタ専用クラス
/// ・道の中心線を、交点・T字・路地裏との境目で区切り、その点(ノード)を道に沿ってつなぐ
/// ・路地裏(余白を含む)にかかる部分は使わない。路地裏の手前で道を切るので、手前までは使える
/// ・担当区画(X・Z・Yの範囲の箱)の外の道は使わない。区画の中で、一番大きくつながった道だけを使う(設定で、離れた道も使える)
/// ・巡回ルートは、使える道を全部通って、出発点へ戻る一周にする(同じ入力なら、いつも同じルートになる)
/// ・使える道・離れていて外れた道・路地裏にかかって通らない道は、後から取り出して、Sceneビューに表示できる
/// </summary>
public class CSED_PoliceRoadGraph
{
    /// <summary>
    /// 灰色の道1本分(中心線と幅)
    /// </summary>
    public readonly struct Road
    {
        private readonly Vector3 _start;
        private readonly Vector3 _end;
        private readonly float _width;

        // 中心線の端(ワールド座標)
        public Vector3 start => _start;
        public Vector3 end => _end;

        // 道の幅(m)
        public float width => _width;

        public Road(Vector3 start, Vector3 end, float width)
        {
            _start = start;
            _end = end;
            _width = width;
        }
    }

    /// <summary>
    /// 通らない路地裏1か所分(水平方向の向きのある長方形)
    /// </summary>
    public readonly struct Alley
    {
        private readonly Vector3 _center;
        private readonly Quaternion _rotation;
        private readonly Vector2 _halfSize;

        public Alley(Vector3 center, Quaternion rotation, Vector2 halfSize)
        {
            _center = center;
            _rotation = rotation;
            _halfSize = halfSize;
        }

        /// <summary>
        /// 位置が路地裏の中(余白を含む)にあるかを判定するメソッド
        /// </summary>
        /// <param name="point">判定する位置</param>
        /// <param name="margin">路地裏の外側にあける余白(m)</param>
        /// <returns>中にあればtrue</returns>
        public bool Contains(Vector3 point, float margin)
        {
            Vector3 local = Quaternion.Inverse(_rotation) * (point - _center);
            return Mathf.Abs(local.x) <= _halfSize.x + margin && Mathf.Abs(local.z) <= _halfSize.y + margin;
        }
    }

    // 道を調べる間隔(m)。路地裏との境目はこの間隔で見つける
    private const float _sampleStep = 0.5f;

    // この距離より近い点は、同じ点(ノード)として扱う(m)
    private const float _mergeDistance = 2.0f;

    // 道の端が、他の道の幅の半分にこの距離を足した範囲に入っていたら、その道につなぐ(T字。m)
    private const float _junctionMargin = 2.0f;

    // 道の上の、交点や境目などの目印の点
    private readonly struct KeyPoint
    {
        private readonly int _index;
        private readonly Vector3 _position;

        public int index => _index;
        public Vector3 position => _position;

        public KeyPoint(int index, Vector3 position)
        {
            _index = index;
            _position = position;
        }
    }

    // 道1本分(中心線を細かく区切った点と、各点が路地裏にかかるか)
    private class Strip
    {
        private readonly Road _road;
        private readonly int _segmentCount;
        private readonly Vector3[] _points;
        private readonly bool[] _blocked;
        private readonly List<KeyPoint> _keys = new List<KeyPoint>();

        public Strip(Road road, IReadOnlyList<Alley> alleys, float margin)
        {
            _road = road;
            float length = Vector2.Distance(Flat(road.start), Flat(road.end));
            _segmentCount = Mathf.Max(2, Mathf.CeilToInt(length / _sampleStep));
            _points = new Vector3[_segmentCount + 1];
            _blocked = new bool[_segmentCount + 1];

            for (int i = 0; i <= _segmentCount; i++)
            {
                _points[i] = Vector3.Lerp(road.start, road.end, (float)i / _segmentCount);
                for (int a = 0; a < alleys.Count && !_blocked[i]; a++)
                {
                    _blocked[i] = alleys[a].Contains(_points[i], margin);
                }
            }
        }

        public Road road => _road;
        public int segmentCount => _segmentCount;
        public Vector3[] points => _points;
        public bool[] blocked => _blocked;
        public List<KeyPoint> keys => _keys;
    }

    // 路地裏にかかっていて、使わない道の区間(両端の位置)
    private readonly struct BlockedSpan
    {
        private readonly Vector3 _from;
        private readonly Vector3 _to;

        public Vector3 from => _from;
        public Vector3 to => _to;

        public BlockedSpan(Vector3 from, Vector3 to)
        {
            _from = from;
            _to = to;
        }
    }

    // ノード(道の交点や端など)の位置
    private readonly List<Vector3> _nodes = new List<Vector3>();

    // ノードごとの、つながっているノード(使える道だけ)
    private List<List<int>> _adjacency = new List<List<int>>();

    // 道の種類ごとの、両端のノードの組(Sceneビューへの表示用)
    private readonly List<Vector2Int> _usableEdges = new List<Vector2Int>();
    private readonly List<Vector2Int> _excludedEdges = new List<Vector2Int>();
    private readonly List<Vector2Int> _blockedEdges = new List<Vector2Int>();

    // ノードの数
    public int nodeCount => _nodes.Count;

    // 巡回に使う道(範囲の中で、使うつながりに入っている道)
    public IReadOnlyList<Vector2Int> usableEdges => _usableEdges;

    // 範囲の中にあるのに、他の道とつながっていないので外れた道
    public IReadOnlyList<Vector2Int> excludedEdges => _excludedEdges;

    // 路地裏にかかっているので通らない道
    public IReadOnlyList<Vector2Int> blockedEdges => _blockedEdges;

    /// <summary>
    /// ノードの位置を取得するメソッド
    /// </summary>
    /// <param name="node">ノードの番号</param>
    /// <returns>ノードの位置</returns>
    public Vector3 GetNode(int node)
    {
        return _nodes[node];
    }

    /// <summary>
    /// ノードからつながっているノード(使える道の先)を取得するメソッド
    /// </summary>
    /// <param name="node">ノードの番号</param>
    /// <returns>つながっているノードの番号</returns>
    public IReadOnlyList<int> GetNeighbors(int node)
    {
        return _adjacency[node];
    }

    /// <summary>
    /// 道の網を作るメソッド
    /// </summary>
    /// <param name="roads">灰色の道</param>
    /// <param name="alleys">通らない路地裏</param>
    /// <param name="alleyMargin">路地裏の外側にあける余白(m)</param>
    /// <param name="area">担当区画(ワールド座標の箱。Yを無視したい時は、Yの範囲を十分に広くする)</param>
    /// <param name="keepLargestOnly">trueなら一番大きくつながった道だけを使う。falseなら、離れた道も全部使う</param>
    /// <returns>作った道の網</returns>
    public static CSED_PoliceRoadGraph Build(IReadOnlyList<Road> roads, IReadOnlyList<Alley> alleys, float alleyMargin, Bounds area, bool keepLargestOnly = true)
    {
        CSED_PoliceRoadGraph graph = new CSED_PoliceRoadGraph();

        List<Strip> strips = new List<Strip>();
        foreach (Road road in roads)
        {
            strips.Add(new Strip(road, alleys, alleyMargin));
        }

        CollectKeyPoints(strips);
        HashSet<Vector2Int> edges = graph.CreateEdges(strips);
        graph.FinishGraph(edges, area, keepLargestOnly);
        return graph;
    }

    /// <summary>
    /// 一番近い、使えるノードを探すメソッド
    /// </summary>
    /// <param name="position">探す基準の位置</param>
    /// <returns>ノードの番号(使えるノードが無ければ-1)</returns>
    public int FindNearest(Vector3 position)
    {
        int nearest = -1;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_adjacency[i].Count == 0) continue;

            float sqr = (Flat(_nodes[i]) - Flat(position)).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            nearest = i;
            nearestSqr = sqr;
        }
        return nearest;
    }

    // ---------------------------------------------------------------
    // 巡回ルート(使える道を全部通って、出発点へ戻る一周)
    // ---------------------------------------------------------------

    /// <summary>
    /// 出発点から、使える道を全部通って、出発点へ戻る一周のルートを作るメソッド
    /// まだ通っていない道を優先し、同じ向きにまっすぐ進める道を選ぶ。通っていない道が隣に無い時は、一番近い場所まで通った道で戻る
    /// </summary>
    /// <param name="start">出発するノード</param>
    /// <returns>通るノードの順番(最後は出発点に戻る手前まで)</returns>
    public List<int> CreateLoop(int start)
    {
        List<int> path = new List<int> { start };
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        int total = CountEdges();
        int current = start;
        int previous = -1;

        // 道の数に比例した上限をつけ、万が一でも終わらなくなるのを防ぐ
        int guard = total * 4 + 100;
        while (visited.Count < total && guard-- > 0)
        {
            int next = ChooseUnvisited(current, previous, visited);
            if (next >= 0)
            {
                visited.Add(MakeEdge(current, next));
                path.Add(next);
                previous = current;
                current = next;
                continue;
            }

            // 隣に通っていない道が無いので、通っていない道がある一番近い場所まで、通った道で移動する
            List<int> detour = FindPathToUnvisited(current, visited);
            if (detour.Count == 0) detour = FindJumpToUnvisited(current, visited);
            if (detour.Count == 0) break;

            foreach (int node in detour)
            {
                path.Add(node);
                previous = current;
                current = node;
            }
        }

        // 出発点まで戻る(出発点自体は、最初のノードと同じなので加えない)
        List<int> back = FindPath(current, start);
        for (int i = 0; i < back.Count - 1; i++) path.Add(back[i]);
        return path;
    }

    /// <summary>
    /// ルートの中から、ほぼまっすぐ通過するだけのノードを減らすメソッド(巡回ポイントを増やしすぎないようにする)
    /// 最初のノードは残す
    /// </summary>
    /// <param name="path">ノードの順番</param>
    /// <param name="minTurnAngle">これ以上向きが変わるノードだけを残す(度)</param>
    /// <returns>減らしたノードの順番</returns>
    public List<int> RemoveStraightNodes(List<int> path, float minTurnAngle)
    {
        List<int> result = new List<int>();
        for (int i = 0; i < path.Count; i++)
        {
            if (i == 0 || IsTurn(path, i, minTurnAngle)) result.Add(path[i]);
        }
        return result;
    }

    /// <summary>
    /// 通っていない道の中から、今の進行方向に一番近い(まっすぐ進める)ものを選ぶメソッド
    /// </summary>
    /// <returns>次のノード(通っていない道が無ければ-1)</returns>
    private int ChooseUnvisited(int current, int previous, HashSet<Vector2Int> visited)
    {
        int best = -1;
        float bestScore = float.MinValue;
        foreach (int neighbor in _adjacency[current])
        {
            if (visited.Contains(MakeEdge(current, neighbor))) continue;

            float score = GetStraightScore(current, previous, neighbor);
            if (score <= bestScore) continue;

            best = neighbor;
            bestScore = score;
        }
        return best;
    }

    /// <summary>
    /// 次の候補が、今までの進行方向と、どれだけ同じ向きかを求めるメソッド(-1〜1。最初は全部同じ)
    /// </summary>
    private float GetStraightScore(int current, int previous, int candidate)
    {
        if (previous < 0) return 0.0f;

        Vector2 forward = (Flat(_nodes[current]) - Flat(_nodes[previous])).normalized;
        Vector2 next = (Flat(_nodes[candidate]) - Flat(_nodes[current])).normalized;
        return Vector2.Dot(forward, next);
    }

    /// <summary>
    /// 今いるノードから、通っていない道がある一番近いノードまでの道順を求めるメソッド
    /// </summary>
    /// <returns>今いるノードを除いた道順(見つからなければ空)</returns>
    private List<int> FindPathToUnvisited(int from, HashSet<Vector2Int> visited)
    {
        return Search(from, node =>
        {
            foreach (int neighbor in _adjacency[node])
            {
                if (!visited.Contains(MakeEdge(node, neighbor))) return true;
            }
            return false;
        });
    }

    /// <summary>
    /// 今いる道とつながっていない場所に通っていない道がある時、直線距離で一番近いノードへ移動する道順を求めるメソッド
    /// (離れた道も使う設定の時だけ必要。実際の移動は、巡回の時にNavMeshの最短の道で行われる)
    /// </summary>
    /// <returns>移動先のノード1つだけの道順(通っていない道が無ければ空)</returns>
    private List<int> FindJumpToUnvisited(int from, HashSet<Vector2Int> visited)
    {
        int nearest = -1;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (i == from || !HasUnvisitedEdge(i, visited)) continue;

            float sqr = (Flat(_nodes[i]) - Flat(_nodes[from])).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            nearest = i;
            nearestSqr = sqr;
        }
        return nearest >= 0 ? new List<int> { nearest } : new List<int>();
    }

    /// <summary>
    /// ノードに、まだ通っていない道がつながっているかを判定するメソッド
    /// </summary>
    private bool HasUnvisitedEdge(int node, HashSet<Vector2Int> visited)
    {
        foreach (int neighbor in _adjacency[node])
        {
            if (!visited.Contains(MakeEdge(node, neighbor))) return true;
        }
        return false;
    }

    /// <summary>
    /// 2つのノードの間の、一番短い(ノードの数が少ない)道順を求めるメソッド
    /// </summary>
    /// <returns>出発点を除いた道順(同じノードなら空)</returns>
    private List<int> FindPath(int from, int to)
    {
        return Search(from, node => node == to);
    }

    /// <summary>
    /// 出発点から道をたどり、条件に合う一番近いノードまでの道順を求めるメソッド(幅優先)
    /// </summary>
    private List<int> Search(int from, System.Func<int, bool> isGoal)
    {
        Dictionary<int, int> cameFrom = new Dictionary<int, int> { { from, -1 } };
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            if (node != from && isGoal(node)) return Backtrack(cameFrom, node);

            foreach (int neighbor in _adjacency[node])
            {
                if (cameFrom.ContainsKey(neighbor)) continue;

                cameFrom[neighbor] = node;
                queue.Enqueue(neighbor);
            }
        }
        return new List<int>();
    }

    /// <summary>
    /// たどってきた記録から、出発点を除いた道順を作るメソッド
    /// </summary>
    private static List<int> Backtrack(Dictionary<int, int> cameFrom, int goal)
    {
        List<int> path = new List<int>();
        for (int node = goal; cameFrom[node] >= 0; node = cameFrom[node]) path.Add(node);

        path.Reverse();
        return path;
    }

    /// <summary>
    /// ルートの中のノードで、向きが一定以上変わるか(または最後か)を判定するメソッド
    /// </summary>
    private bool IsTurn(List<int> path, int index, float minTurnAngle)
    {
        if (index >= path.Count - 1) return true;

        Vector2 incoming = Flat(_nodes[path[index]]) - Flat(_nodes[path[index - 1]]);
        Vector2 outgoing = Flat(_nodes[path[index + 1]]) - Flat(_nodes[path[index]]);
        if (incoming.sqrMagnitude < 0.0001f || outgoing.sqrMagnitude < 0.0001f) return false;

        return Vector2.Angle(incoming, outgoing) >= minTurnAngle;
    }

    /// <summary>
    /// 使える道の数を数えるメソッド
    /// </summary>
    private int CountEdges()
    {
        int count = 0;
        foreach (List<int> neighbors in _adjacency) count += neighbors.Count;
        return count / 2;
    }

    private static Vector2Int MakeEdge(int a, int b)
    {
        return new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b));
    }

    // ---------------------------------------------------------------
    // 道の網の作成
    // ---------------------------------------------------------------

    /// <summary>
    /// 道の上の目印の点(端・交点・T字・路地裏との境目)を集めるメソッド
    /// </summary>
    private static void CollectKeyPoints(List<Strip> strips)
    {
        for (int i = 0; i < strips.Count; i++)
        {
            Strip strip = strips[i];
            strip.keys.Add(new KeyPoint(0, strip.points[0]));
            strip.keys.Add(new KeyPoint(strip.segmentCount, strip.points[strip.segmentCount]));
            AddBoundaryKeys(strip);

            for (int j = i + 1; j < strips.Count; j++)
            {
                AddIntersection(strip, strips[j]);
            }
        }

        // 道の端が、他の道の近くで終わっている(T字)なら、その道へつなぐ
        for (int i = 0; i < strips.Count; i++)
        {
            for (int j = 0; j < strips.Count; j++)
            {
                if (i != j) AddJunction(strips[i], strips[j]);
            }
        }
    }

    /// <summary>
    /// 道が、路地裏に入る直前・出た直後の、使える側の点を目印に加えるメソッド
    /// </summary>
    private static void AddBoundaryKeys(Strip strip)
    {
        for (int k = 1; k <= strip.segmentCount; k++)
        {
            if (strip.blocked[k] == strip.blocked[k - 1]) continue;

            int freeIndex = strip.blocked[k] ? k - 1 : k;
            strip.keys.Add(new KeyPoint(freeIndex, strip.points[freeIndex]));
        }
    }

    /// <summary>
    /// 2本の道の中心線が交わる点を、お互いの目印に加えるメソッド
    /// </summary>
    private static void AddIntersection(Strip a, Strip b)
    {
        Vector2 a0 = Flat(a.road.start);
        Vector2 a1 = Flat(a.road.end);
        Vector2 b0 = Flat(b.road.start);
        Vector2 b1 = Flat(b.road.end);
        if (!TryIntersect(a0, a1, b0, b1, out Vector2 point)) return;

        AddKey(a, ProjectParameter(point, a0, a1), point);
        AddKey(b, ProjectParameter(point, b0, b1), point);
    }

    /// <summary>
    /// 道の端が、他の道の近くにあれば、その道の上の点につなぐ目印を加えるメソッド
    /// </summary>
    private static void AddJunction(Strip from, Strip onto)
    {
        Vector2 o0 = Flat(onto.road.start);
        Vector2 o1 = Flat(onto.road.end);
        for (int end = 0; end < 2; end++)
        {
            Vector3 endPoint = end == 0 ? from.road.start : from.road.end;
            float t = ProjectParameter(Flat(endPoint), o0, o1);
            Vector2 foot = o0 + (o1 - o0) * t;
            if (t <= 0.0f || t >= 1.0f) continue;
            if (Vector2.Distance(Flat(endPoint), foot) > onto.road.width * 0.5f + _junctionMargin) continue;

            AddKey(onto, t, foot);
            from.keys.Add(new KeyPoint(end == 0 ? 0 : from.segmentCount, new Vector3(foot.x, endPoint.y, foot.y)));
        }
    }

    /// <summary>
    /// 道の上の位置(0〜1)に、目印の点を加えるメソッド
    /// </summary>
    private static void AddKey(Strip strip, float t, Vector2 flatPosition)
    {
        int index = Mathf.Clamp(Mathf.RoundToInt(t * strip.segmentCount), 0, strip.segmentCount);
        strip.keys.Add(new KeyPoint(index, new Vector3(flatPosition.x, strip.points[index].y, flatPosition.y)));
    }

    /// <summary>
    /// 目印の点を道に沿ってつなぎ、路地裏にかからない区間だけを「使える道」にするメソッド
    /// </summary>
    /// <returns>使える道(両端のノード番号の組)</returns>
    private HashSet<Vector2Int> CreateEdges(List<Strip> strips)
    {
        HashSet<Vector2Int> edges = new HashSet<Vector2Int>();
        List<BlockedSpan> blockedSpans = new List<BlockedSpan>();
        foreach (Strip strip in strips)
        {
            strip.keys.Sort((a, b) => a.index.CompareTo(b.index));

            for (int i = 1; i < strip.keys.Count; i++)
            {
                KeyPoint from = strip.keys[i - 1];
                KeyPoint to = strip.keys[i];
                if (to.index <= from.index) continue;

                if (!IsFree(strip, from.index, to.index))
                {
                    blockedSpans.Add(new BlockedSpan(from.position, to.position));
                    continue;
                }

                int u = FindOrAddNode(from.position);
                int v = FindOrAddNode(to.position);
                if (u != v) edges.Add(MakeEdge(u, v));
            }
        }

        // 通らない区間のノードは、使える道のノードが決まった後に作る(使える道のノードの位置が動かないように)
        foreach (BlockedSpan span in blockedSpans)
        {
            int u = FindOrAddNode(span.from);
            int v = FindOrAddNode(span.to);
            if (u != v) _blockedEdges.Add(MakeEdge(u, v));
        }
        return edges;
    }

    /// <summary>
    /// 道の区間が、全部使える(路地裏にかからない)かを判定するメソッド
    /// </summary>
    private static bool IsFree(Strip strip, int fromIndex, int toIndex)
    {
        for (int k = fromIndex; k <= toIndex; k++)
        {
            if (strip.blocked[k]) return false;
        }
        return true;
    }

    /// <summary>
    /// 近くに同じノードがあればその番号、無ければ新しく作った番号を返すメソッド
    /// </summary>
    private int FindOrAddNode(Vector3 position)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (Vector2.Distance(Flat(_nodes[i]), Flat(position)) < _mergeDistance) return i;
        }

        _nodes.Add(position);
        return _nodes.Count - 1;
    }

    /// <summary>
    /// 担当区画の中の道だけを残し、(設定によっては)一番大きくつながった道を使うように、つながりを作るメソッド
    /// </summary>
    private void FinishGraph(HashSet<Vector2Int> edges, Bounds area, bool keepLargestOnly)
    {
        List<List<int>> adjacency = new List<List<int>>();
        for (int i = 0; i < _nodes.Count; i++) adjacency.Add(new List<int>());

        List<Vector2Int> edgesInArea = new List<Vector2Int>();
        foreach (Vector2Int edge in edges)
        {
            if (!IsInside(area, _nodes[edge.x]) || !IsInside(area, _nodes[edge.y])) continue;

            edgesInArea.Add(edge);
            adjacency[edge.x].Add(edge.y);
            adjacency[edge.y].Add(edge.x);
        }

        // 並び順を決まった順にして、毎回同じルートになるようにする
        foreach (List<int> neighbors in adjacency) neighbors.Sort();

        _adjacency = keepLargestOnly ? KeepLargestComponent(adjacency) : adjacency;
        SortEdges(edgesInArea);
    }

    /// <summary>
    /// 範囲の中の道を、巡回に使う道と、離れていて外れた道に分けて記録するメソッド
    /// </summary>
    private void SortEdges(List<Vector2Int> edgesInArea)
    {
        foreach (Vector2Int edge in edgesInArea)
        {
            if (_adjacency[edge.x].Contains(edge.y)) _usableEdges.Add(edge);
            else _excludedEdges.Add(edge);
        }
    }

    /// <summary>
    /// つながっているノードのまとまりのうち、一番大きいものだけを残すメソッド(小さな離れた道に迷い込まないようにする)
    /// </summary>
    private List<List<int>> KeepLargestComponent(List<List<int>> adjacency)
    {
        bool[] visited = new bool[adjacency.Count];
        List<int> largest = new List<int>();

        for (int start = 0; start < adjacency.Count; start++)
        {
            if (visited[start] || adjacency[start].Count == 0) continue;

            List<int> component = CollectComponent(adjacency, start, visited);
            if (component.Count > largest.Count) largest = component;
        }

        List<List<int>> result = new List<List<int>>();
        HashSet<int> kept = new HashSet<int>(largest);
        for (int i = 0; i < adjacency.Count; i++)
        {
            result.Add(kept.Contains(i) ? adjacency[i] : new List<int>());
        }
        return result;
    }

    /// <summary>
    /// あるノードから道をたどって行けるノードを、すべて集めるメソッド
    /// </summary>
    /// <param name="adjacency">ノードのつながり</param>
    /// <param name="start">たどり始めるノード</param>
    /// <param name="visited">集め済みのノード(集めたノードは、ここにも記録する)</param>
    /// <returns>たどれるノードの番号</returns>
    private static List<int> CollectComponent(List<List<int>> adjacency, int start, bool[] visited)
    {
        List<int> component = new List<int>();
        Stack<int> stack = new Stack<int>();
        stack.Push(start);
        visited[start] = true;

        while (stack.Count > 0)
        {
            int node = stack.Pop();
            component.Add(node);
            foreach (int next in adjacency[node])
            {
                if (visited[next]) continue;

                visited[next] = true;
                stack.Push(next);
            }
        }
        return component;
    }

    /// <summary>
    /// 位置が担当区画の中にあるかを判定するメソッド
    /// </summary>
    private static bool IsInside(Bounds area, Vector3 position)
    {
        return position.x >= area.min.x && position.x <= area.max.x
            && position.y >= area.min.y && position.y <= area.max.y
            && position.z >= area.min.z && position.z <= area.max.z;
    }

    /// <summary>
    /// 水平方向の位置(XとZ)を2次元の値にするメソッド
    /// </summary>
    private static Vector2 Flat(Vector3 position)
    {
        return new Vector2(position.x, position.z);
    }

    /// <summary>
    /// 線分a-b上で、点pに一番近い位置を、0〜1の値で求めるメソッド
    /// </summary>
    private static float ProjectParameter(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float sqr = ab.sqrMagnitude;
        return sqr > 0.0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqr) : 0.0f;
    }

    /// <summary>
    /// 2本の線分が交わる点を求めるメソッド
    /// </summary>
    private static bool TryIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 point)
    {
        point = Vector2.zero;

        Vector2 r = b - a;
        Vector2 s = d - c;
        float denominator = r.x * s.y - r.y * s.x;
        if (Mathf.Abs(denominator) < 0.000001f) return false;

        float t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / denominator;
        float u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / denominator;
        if (t < -0.001f || t > 1.001f || u < -0.001f || u > 1.001f) return false;

        point = a + r * t;
        return true;
    }
}
