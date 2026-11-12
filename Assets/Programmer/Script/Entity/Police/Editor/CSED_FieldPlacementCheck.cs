/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-09 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 開いているシーンの、警察・防犯装置・悪人の出現点・SAポイント(ハイドポイント)の配置を調べるエディタ専用ツール
/// 歩ける場所(NavMesh)の上にあるか・建物などの中に埋まっていないかを調べて、結果をファイルに書き出す
/// メニュー「Tools/Police/フィールドの配置を調べる」で実行する(MainSceneなど、フィールドを置いたシーンを開いてから実行する)
/// 結果は プロジェクトの Temp/FieldPlacementReport.txt に出る(Gitに入らない)
/// </summary>
public static class CSED_FieldPlacementCheck
{
    // 歩ける場所(NavMesh)を探す距離(m)。これより遠ければ、歩ける場所の上に無いとみなす
    private const float _navSearchDistance = 3.0f;

    // 建物などの中に埋まっているかを調べる球の半径(m)と、足元からの高さ(m。地面そのものに当たって、埋まっていると誤判定しないよう、少し上で調べる)
    private const float _buriedRadius = 0.3f;
    private const float _buriedHeight = 0.9f;

    // 地面を探す、上からの高さと長さ(m)
    private const float _groundRayStart = 1.0f;
    private const float _groundRayLength = 60.0f;

    // 1種類ごとに、問題のあるものを書き出す上限
    private const int _maxProblemsPerKind = 40;

    private const string _reportName = "FieldPlacementReport.txt";

    // 詳しく調べる遊び方(Field_Multi / Field_Solo1)。配置ツールの配置先に合わせる
    private const string _targetMode = "Field_Solo1";

    /// <summary>
    /// 配置を調べて、結果を書き出すメソッド
    /// </summary>
    [MenuItem("Tools/Police/フィールドの配置を調べる")]
    private static void Check()
    {
        StringBuilder report = new StringBuilder();
        AppendNavMeshInfo(report);

        CheckSquads(report);
        AppendSquadOverlaps(report);
        AppendSquadMembers(report);
        AppendBlockedRouteSegments(report);
        CheckPoints<CS_SecurityLaser>(report, "防犯レーザー", true);
        AppendLaserSweeps(report);
        CheckPoints<CS_SecurityLight>(report, "ストリートライト", true);
        CheckPoints<CS_SecurityDroneSpawner>(report, "巡回ドローンの出現位置", false);
        AppendDronePatrolPoints(report);
        AppendBuriedSaObjects(report);
        AppendHidePointStands(report);
        AppendSaObjectSpacing(report);
        CheckPoints<CS_VillainSpawnPoint>(report, "悪人の出現点", true);
        AppendVillainPatrols(report);

        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", _reportName);
        File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
        Debug.Log($"CSED_FieldPlacementCheck: 配置を調べました。結果: {path}");
    }

    /// <summary>
    /// 歩ける場所(NavMesh)の範囲を書き出すメソッド
    /// </summary>
    private static void AppendNavMeshInfo(StringBuilder report)
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices.Length == 0)
        {
            report.AppendLine("【NavMesh】読み込まれていません(フィールドを置いたシーンを開いているか確認してください)");
            return;
        }

        Bounds bounds = new Bounds(triangulation.vertices[0], Vector3.zero);
        foreach (Vector3 vertex in triangulation.vertices) bounds.Encapsulate(vertex);
        report.AppendLine($"【NavMesh】頂点 {triangulation.vertices.Length}個 / 範囲 X {bounds.min.x:F1}〜{bounds.max.x:F1} / Y {bounds.min.y:F1}〜{bounds.max.y:F1} / Z {bounds.min.z:F1}〜{bounds.max.z:F1}");
    }

    /// <summary>
    /// 警察のグループの、出現位置と巡回ポイントを調べるメソッド
    /// </summary>
    private static void CheckSquads(StringBuilder report)
    {
        CS_PoliceSquad[] squads = Object.FindObjectsByType<CS_PoliceSquad>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int pointCount = 0;
        List<string> problems = new List<string>();
        foreach (CS_PoliceSquad squad in squads)
        {
            List<Transform> points = squad.GetRouteDrawPoints();
            pointCount += points.Count;
            foreach (Transform point in points)
            {
                string problem = FindProblem(point.position, true);
                if (problem != null) problems.Add($"  {GetPath(point)} {Format(point.position)} → {problem}");
            }
        }

        report.AppendLine();
        report.AppendLine($"【警察のグループ】{squads.Length}グループ / 巡回ポイント {pointCount}個 / 問題あり {problems.Count}個");
        AppendProblems(report, problems);
    }

    /// <summary>
    /// 同じ遊び方(同じ親)の警察のグループ同士で、巡回ルートが重なっていないかを調べて書き出すメソッド
    /// 巡回ルートの線(ポイントを順に結んだ線)同士が、_overlapDistance以内まで近づく所があれば、重なっているとみなす
    /// </summary>
    private static void AppendSquadOverlaps(StringBuilder report)
    {
        const float overlapDistance = 12.0f;
        CS_PoliceSquad[] squads = Object.FindObjectsByType<CS_PoliceSquad>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<string> lines = new List<string>();
        for (int i = 0; i < squads.Length; i++)
        {
            for (int j = i + 1; j < squads.Length; j++)
            {
                if (GetMode(squads[i].transform) != GetMode(squads[j].transform)) continue;

                float min = MinRouteDistance(squads[i], squads[j]);
                if (min <= overlapDistance) lines.Add($"  {squads[i].name} と {squads[j].name}({GetMode(squads[i].transform)}): ルート同士が最小 {min:F1}m まで近づく");
            }
        }

        report.AppendLine();
        report.AppendLine($"【警察のグループ同士の重なり(ルートが {overlapDistance}m 以内まで近づく組み合わせ)】{lines.Count}組");
        foreach (string line in lines) report.AppendLine(line);
    }

    /// <summary>
    /// マルチ用の警察のグループの人数と、「離れて巡回」の設定を数えて書き出すメソッド
    /// </summary>
    private static void AppendSquadMembers(StringBuilder report)
    {
        Dictionary<int, int> sizes = new Dictionary<int, int>();
        int total = 0;
        int spread = 0;
        int groups = 0;
        foreach (CS_PoliceSquad squad in Object.FindObjectsByType<CS_PoliceSquad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (GetMode(squad.transform) != _targetMode) continue;

            SerializedObject serialized = new SerializedObject(squad);
            int members = serialized.FindProperty("_memberStatuses").arraySize;
            sizes[members] = sizes.TryGetValue(members, out int count) ? count + 1 : 1;
            total += members;
            groups++;
            if (serialized.FindProperty("_spreadPatrol").boolValue) spread++;
        }

        List<string> parts = new List<string>();
        foreach (KeyValuePair<int, int> pair in sizes) parts.Add($"{pair.Key}人のグループ {pair.Value}個");
        report.AppendLine();
        report.AppendLine($"【警察のグループの人数(マルチ用)】{groups}グループ / 警察 {total}人 / {string.Join(", ", parts)} / 離れて巡回 {spread}グループ");
    }

    /// <summary>
    /// マルチ用の警察のグループの、隣り合う巡回ポイントの間(最後から最初への間も含む)を、まっすぐ結んだ時に、
    /// 歩ける場所(NavMesh)の外へ出る(建物などを貫通する)区間を数えて書き出すメソッド
    /// </summary>
    private static void AppendBlockedRouteSegments(StringBuilder report)
    {
        int segments = 0;
        List<string> blocked = new List<string>();
        foreach (CS_PoliceSquad squad in Object.FindObjectsByType<CS_PoliceSquad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (GetMode(squad.transform) != _targetMode) continue;

            List<Transform> points = squad.GetRouteDrawPoints();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 from = points[i].position;
                Vector3 to = points[(i + 1) % points.Count].position;
                segments++;
                if (IsBlockedSegment(from, to)) blocked.Add($"  {squad.name}: {i} → {(i + 1) % points.Count}(長さ {Vector3.Distance(from, to):F0}m)");
            }
        }

        report.AppendLine();
        report.AppendLine($"【警察の巡回ルート(マルチ用)の、建物などを貫通する区間】全 {segments}区間のうち {blocked.Count}区間");
        AppendProblems(report, blocked);
    }

    /// <summary>
    /// 2点の間をまっすぐ結ぶと、建物などを貫通するか(歩ける場所の外を、途中で大きく横切るか)を判定するメソッド
    /// 端の点が歩ける場所の縁にある時の、端でのわずかな引っかかり(両端1m以内)は、貫通とみなさない
    /// また、歩いて行く道のりが、まっすぐの距離とほとんど変わらない(1.1倍+1m以内)なら、貫通とみなさない
    /// </summary>
    private static bool IsBlockedSegment(Vector3 from, Vector3 to)
    {
        float length = Vector3.Distance(from, to);
        if (!NavMesh.Raycast(from, to, out NavMeshHit hit, NavMesh.AllAreas)) return false;
        if (hit.distance < 1.0f || hit.distance > length - 1.0f) return false;

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return true;

        float walk = 0.0f;
        for (int i = 1; i < path.corners.Length; i++) walk += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return walk > length * 1.1f + 1.0f;
    }

    private static string GetMode(Transform transform)
    {
        for (Transform parent = transform; parent != null; parent = parent.parent)
        {
            if (parent.name == "Field_Multi" || parent.name == "Field_Solo1") return parent.name;
        }
        return "?";
    }

    /// <summary>
    /// 2つのグループの巡回ルート(ポイントを順に結んだ線。最後は最初へ戻る)の間の、最小の水平距離を求めるメソッド
    /// </summary>
    private static float MinRouteDistance(CS_PoliceSquad a, CS_PoliceSquad b)
    {
        List<Transform> pointsA = a.GetRouteDrawPoints();
        List<Transform> pointsB = b.GetRouteDrawPoints();
        float min = float.MaxValue;
        for (int i = 0; i < pointsA.Count; i++)
        {
            Vector3 a0 = pointsA[i].position;
            Vector3 a1 = pointsA[(i + 1) % pointsA.Count].position;
            for (int j = 0; j < pointsB.Count; j++)
            {
                Vector3 b0 = pointsB[j].position;
                Vector3 b1 = pointsB[(j + 1) % pointsB.Count].position;
                min = Mathf.Min(min, SegmentDistance(a0, a1, b0, b1));
            }
        }
        return min;
    }

    /// <summary>
    /// 2つの線分の間の、水平方向の最小距離を求めるメソッド(交わっていれば0。端点から相手の線分への距離で近似する)
    /// </summary>
    private static float SegmentDistance(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
    {
        a0.y = a1.y = b0.y = b1.y = 0.0f;
        if (SegmentsCross(a0, a1, b0, b1)) return 0.0f;

        return Mathf.Min(PointSegment(a0, b0, b1), PointSegment(a1, b0, b1), PointSegment(b0, a0, a1), PointSegment(b1, a0, a1));
    }

    private static float PointSegment(Vector3 point, Vector3 s0, Vector3 s1)
    {
        Vector3 d = s1 - s0;
        float t = d.sqrMagnitude < 0.0001f ? 0.0f : Mathf.Clamp01(Vector3.Dot(point - s0, d) / d.sqrMagnitude);
        return Vector3.Distance(point, s0 + d * t);
    }

    private static bool SegmentsCross(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
    {
        float d1 = Cross(b1 - b0, a0 - b0);
        float d2 = Cross(b1 - b0, a1 - b0);
        float d3 = Cross(a1 - a0, b0 - a0);
        float d4 = Cross(a1 - a0, b1 - a0);
        return d1 * d2 < 0.0f && d3 * d4 < 0.0f;
    }

    private static float Cross(Vector3 u, Vector3 v)
    {
        return u.x * v.z - u.z * v.x;
    }

    /// <summary>
    /// 同じ種類のコンポーネントの位置を調べるメソッド
    /// </summary>
    /// <param name="report">結果の書き出し先</param>
    /// <param name="label">種類の名前</param>
    /// <param name="needsNavMesh">歩ける場所(NavMesh)の上にあるべきか(空中に置く物はfalse)</param>
    private static void CheckPoints<T>(StringBuilder report, string label, bool needsNavMesh) where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<string> problems = new List<string>();
        foreach (T component in found)
        {
            string problem = FindProblem(component.transform.position, needsNavMesh);
            if (problem != null) problems.Add($"  {GetPath(component.transform)} {Format(component.transform.position)} → {problem}");
        }

        report.AppendLine();
        report.AppendLine($"【{label}】{found.Length}個 / 問題あり {problems.Count}個");
        AppendProblems(report, problems);
    }

    /// <summary>
    /// マルチ用の防犯レーザーの、往復する通り道を調べて書き出すメソッド
    /// 始点から終点まで1mおきに、レーザーの箱が、壁・柱などのコライダーに当たらないか、始点・終点が歩ける場所の近くか、を調べる
    /// </summary>
    private static void AppendLaserSweeps(StringBuilder report)
    {
        int total = 0;
        List<string> problems = new List<string>();
        foreach (CS_SecurityLaser laser in Object.FindObjectsByType<CS_SecurityLaser>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (GetMode(laser.transform) != _targetMode) continue;

            total++;
            SerializedObject serialized = new SerializedObject(laser);
            Vector3 direction = laser.transform.rotation * serialized.FindProperty("_moveDirection").vector3Value.normalized;
            float distance = serialized.FindProperty("_moveDistance").floatValue;
            float length = serialized.FindProperty("_beamLength").floatValue;
            float thickness = serialized.FindProperty("_beamThickness").floatValue;
            Vector3 half = new Vector3(length * 0.5f, thickness * 0.5f, thickness * 0.5f);

            string hit = null;
            for (float t = 0.0f; t <= distance + 0.01f && hit == null; t += 1.0f)
            {
                foreach (Collider collider in Physics.OverlapBox(laser.transform.position + direction * t, half, laser.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (collider.transform.IsChildOf(laser.transform)) continue;

                    hit = $"始点から {t:F0}m の所で {collider.name} に当たる";
                    break;
                }
            }

            string ends = null;
            foreach (float t in new[] { 0.0f, distance })
            {
                Vector3 point = laser.transform.position + direction * t;
                if (!NavMesh.SamplePosition(point, out _, 3.0f, NavMesh.AllAreas)) ends = $"{(t == 0.0f ? "始点" : "終点")} {Format(point)} の3m以内に歩ける場所が無い";
            }

            string problem = hit ?? ends;
            if (problem != null) problems.Add($"  {GetPath(laser.transform)} 往復 {distance:F0}m・長さ {length:F1}m → {problem}");
        }

        report.AppendLine();
        report.AppendLine($"【防犯レーザー(マルチ用)の往復】{total}本 / 通り道が壁などに当たる・始点終点が歩ける場所から外れるもの {problems.Count}本");
        AppendProblems(report, problems);
    }

    /// <summary>
    /// マルチ用のSAObject(SAObjects の子)のうち、柱・頭の箱が、壁などのコライダーに埋まっているものを調べて書き出すメソッド
    /// 自分自身(同じSAObjectの子)のコライダーは、数えない
    /// </summary>
    private static void AppendBuriedSaObjects(StringBuilder report)
    {
        int total = 0;
        List<string> buried = new List<string>();
        foreach (Transform root in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!root.name.StartsWith("SAObject") || root.parent == null || root.parent.name != "SAObjects" || GetMode(root) != _targetMode) continue;

            total++;
            string hit = FindBuriedCollider(root);
            if (hit != null) buried.Add($"  {GetPath(root)} ({root.position.x:F1}, {root.position.y:F1}, {root.position.z:F1}) → {hit}");
        }

        report.AppendLine();
        report.AppendLine($"【SAObject(マルチ用)】{total}本 / 壁などに埋まっているもの {buried.Count}本");
        AppendProblems(report, buried);
    }

    private static readonly Vector3[] _insideProbeDirections =
    {
        Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
        new Vector3(1.0f, 1.0f, 1.0f).normalized, new Vector3(-1.0f, 1.0f, 1.0f).normalized,
        new Vector3(1.0f, 1.0f, -1.0f).normalized, new Vector3(-1.0f, 1.0f, -1.0f).normalized,
    };

    /// <summary>
    /// 位置が、建物の中(コライダーの面で囲まれた中身)にあるかを判定するメソッド
    /// コライダーとの重なり判定は、建物の面(表面)にしか反応せず、柱・頭が建物の中に完全に入っていると見逃すため、これで補う
    /// 10方向へ光線を撃って、一番近い当たりが裏面(内側から見た面)のものが、7方向以上なら、建物の中とみなす
    /// </summary>
    /// <param name="self">数えない自分自身のコライダー(SAObjectなど。無ければnull)</param>
    public static bool IsInsideSolid(Vector3 position, Transform self)
    {
        bool oldBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        int backCount = 0;
        foreach (Vector3 direction in _insideProbeDirections)
        {
            RaycastHit[] hits = Physics.RaycastAll(position, direction, 60.0f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (self != null && hit.collider.transform.IsChildOf(self)) continue;
                if (hit.distance > 0.0f && Vector3.Dot(hit.normal, direction) > 0.0f) backCount++;
                break;
            }
        }
        Physics.queriesHitBackfaces = oldBackfaces;
        return backCount >= 7;
    }

    /// <summary>
    /// 足元がbasePositionで、柱の高さがpoleHeightのSAObjectが、建物の中に入っているかを判定するメソッド(柱の途中と、頭の中心・角で調べる)
    /// </summary>
    public static bool IsPoleInsideSolid(Vector3 basePosition, float poleHeight, Transform self)
    {
        const float headSize = 2.0f;
        Vector3 head = basePosition + Vector3.up * (poleHeight + headSize * 0.5f);
        float corner = headSize * 0.4f;
        Vector3[] points =
        {
            basePosition + Vector3.up * 1.1f, basePosition + Vector3.up * (poleHeight * 0.5f), basePosition + Vector3.up * (poleHeight - 0.5f),
            head, head + new Vector3(corner, corner, corner), head + new Vector3(-corner, corner, -corner),
            head + new Vector3(corner, -corner, -corner), head + new Vector3(-corner, -corner, corner),
        };
        foreach (Vector3 point in points)
        {
            if (IsInsideSolid(point, self)) return true;
        }
        return false;
    }

    public static string FindBuriedCollider(Transform saObject)
    {
        Transform pole = saObject.Find("Cube (1)");
        if (pole != null && IsPoleInsideSolid(saObject.position, pole.lossyScale.y, saObject)) return "建物の中に入っている";

        const float groundContact = 0.4f;   // 柱の下側は、地面や縁石に触れていても埋まっているとはみなさない長さ(m)
        foreach (Transform cube in saObject)
        {
            Vector3 center = cube.position;
            Vector3 half = cube.lossyScale * 0.5f;
            if (cube.name == "Cube (1)")
            {
                center += Vector3.up * (groundContact * 0.5f);
                half.y = Mathf.Max(0.05f, half.y - groundContact * 0.5f);
            }

            foreach (Collider hit in Physics.OverlapBox(center, half, cube.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(saObject)) return $"{cube.name} が {hit.name} に埋まっている";
            }
        }
        return null;
    }

    /// <summary>
    /// マルチ用のSAObjectの、近すぎる組み合わせと、道のど真ん中(歩ける場所の縁から遠い)にある物を調べて書き出すメソッド
    /// </summary>
    private static void AppendSaObjectSpacing(StringBuilder report)
    {
        const float tooClose = 12.0f;       // これより近いSAObject同士は、近すぎるとみなす(m)
        const float midRoad = 4.0f;         // 地上の物で、歩ける場所の縁からこれより遠いものは、道のど真ん中とみなす(m)
        List<Transform> objects = new List<Transform>();
        foreach (Transform root in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (root.name.StartsWith("SAObject") && root.parent != null && root.parent.name == "SAObjects" && GetMode(root) == _targetMode) objects.Add(root);
        }

        int closePairs = 0;
        float minDistance = float.MaxValue;
        for (int i = 0; i < objects.Count; i++)
        {
            for (int j = i + 1; j < objects.Count; j++)
            {
                float distance = Vector3.Distance(objects[i].position, objects[j].position);
                minDistance = Mathf.Min(minDistance, distance);
                if (distance < tooClose) closePairs++;
            }
        }

        int midRoadCount = 0;
        float farthestEdge = 0.0f;
        foreach (Transform root in objects)
        {
            if (root.position.y >= 3.0f || !NavMesh.FindClosestEdge(root.position, out NavMeshHit edge, NavMesh.AllAreas)) continue;

            farthestEdge = Mathf.Max(farthestEdge, edge.distance);
            if (edge.distance > midRoad) midRoadCount++;
        }

        report.AppendLine();
        report.AppendLine($"【SAObject(マルチ用)の間隔と位置】{objects.Count}本 / 一番近い組み合わせ {minDistance:F1}m / {tooClose}m より近い組み合わせ {closePairs}組");
        report.AppendLine($"  地上の物のうち、歩ける場所の縁から {midRoad}m より遠い(道のど真ん中)もの {midRoadCount}本 / 縁から一番遠い物 {farthestEdge:F1}m");
    }

    /// <summary>
    /// マルチ用のハイドポイントの、着く位置(SAObjectの頭の上)の上の、隠れる箱が、壁などに埋まっていないかを調べて書き出すメソッド
    /// </summary>
    private static void AppendHidePointStands(StringBuilder report)
    {
        int total = 0;
        List<string> buried = new List<string>();
        foreach (CS_HidePoint point in Object.FindObjectsByType<CS_HidePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (GetMode(point.transform) != _targetMode) continue;

            total++;
            Vector3 center = point.standPosition + Vector3.up * 1.7f;
            Collider[] hits = Physics.OverlapBox(center, new Vector3(0.75f, 1.65f, 0.75f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0) buried.Add($"  {GetPath(point.transform)} 着く位置 {Format(point.standPosition)} → 隠れる箱が {hits[0].name} に埋まっている");
        }

        report.AppendLine();
        report.AppendLine($"【ハイドポイント(マルチ用)の着く位置】{total}個 / 隠れる箱が埋まっているもの {buried.Count}個");
        AppendProblems(report, buried);
    }

    /// <summary>
    /// 巡回ドローンごとの、巡回ポイント(子オブジェクト)の数を書き出すメソッド
    /// </summary>
    private static void AppendDronePatrolPoints(StringBuilder report)
    {
        foreach (CS_SecurityDroneSpawner spawner in Object.FindObjectsByType<CS_SecurityDroneSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            report.AppendLine($"  巡回ポイント {CS_PoliceSquad.CollectChildPoints(spawner.transform, null).Length}個: {GetPath(spawner.transform)}");
        }
    }

    /// <summary>
    /// マルチ用の悪人の出現点の、往復する巡回ルート(両端と、その間の線)を調べて書き出すメソッド
    /// 両端が壁・柱などに埋まっている、両端が歩ける場所に無い、線が建物を貫通する、途中に柱がある、ものを数える
    /// </summary>
    private static void AppendVillainPatrols(StringBuilder report)
    {
        int total = 0;
        List<string> problems = new List<string>();
        foreach (CS_VillainSpawnPoint spawnPoint in Object.FindObjectsByType<CS_VillainSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (GetMode(spawnPoint.transform) != _targetMode) continue;

            total++;
            spawnPoint.GetPatrolEnds(out Vector3 endA, out Vector3 endB);
            List<string> found = new List<string>();
            string problemA = FindProblem(endA, true);
            string problemB = FindProblem(endB, true);
            if (problemA != null) found.Add($"端A {Format(endA)} {problemA}");
            if (problemB != null) found.Add($"端B {Format(endB)} {problemB}");
            if (IsBlockedSegment(endA, endB)) found.Add("端Aと端Bの間が建物を貫通する");
            if (HasColliderOnLine(endA, endB)) found.Add("端Aと端Bの間に柱・壁がある");
            if (found.Count > 0) problems.Add($"  {GetPath(spawnPoint.transform)} → {string.Join(" / ", found)}");
        }

        report.AppendLine();
        report.AppendLine($"【悪人の往復ルート(マルチ用)】{total}個 / 問題あり {problems.Count}個");
        AppendProblems(report, problems);
    }

    /// <summary>
    /// 2点の間の線の上(胸の高さ)に、コライダー(柱・壁など)があるかを判定するメソッド
    /// </summary>
    private static bool HasColliderOnLine(Vector3 from, Vector3 to)
    {
        float length = Vector3.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / 1.0f));
        for (int i = 0; i <= steps; i++)
        {
            Vector3 point = Vector3.Lerp(from, to, i / (float)steps) + Vector3.up * _buriedHeight;
            if (Physics.CheckSphere(point, _buriedRadius, ~0, QueryTriggerInteraction.Ignore)) return true;
        }
        return false;
    }

    /// <summary>
    /// 位置の問題(歩ける場所の上に無い・建物などの中に埋まっている)を調べるメソッド
    /// </summary>
    /// <returns>問題の内容(問題が無ければnull)</returns>
    public static string FindProblem(Vector3 position, bool needsNavMesh)
    {
        List<string> problems = new List<string>();

        if (needsNavMesh && !NavMesh.SamplePosition(position, out NavMeshHit hit, _navSearchDistance, NavMesh.AllAreas))
        {
            problems.Add($"{_navSearchDistance}m以内に歩ける場所が無い");
        }

        Collider[] hits = Physics.OverlapSphere(position + Vector3.up * _buriedHeight, _buriedRadius, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            problems.Add($"コライダーの中に埋まっている({hits[0].name})");
        }

        return problems.Count > 0 ? string.Join(" / ", problems) : null;
    }

    private static void AppendProblems(StringBuilder report, List<string> problems)
    {
        for (int i = 0; i < problems.Count && i < _maxProblemsPerKind; i++) report.AppendLine(problems[i]);
        if (problems.Count > _maxProblemsPerKind) report.AppendLine($"  …ほか {problems.Count - _maxProblemsPerKind}個");
    }

    private static string GetPath(Transform target)
    {
        string path = target.name;
        for (Transform parent = target.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
        return path;
    }

    private static string Format(Vector3 position)
    {
        return $"({position.x:F1}, {position.y:F1}, {position.z:F1})";
    }
}
