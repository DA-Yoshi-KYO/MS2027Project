/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-15 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 配置ツールのうち、街(ビル・NavMesh)が変わった後に、今ある悪人の出現点を直す部分
/// 直す物は、Field_Solo1.prefab の中の物と、Field.prefab が Field_Solo1 に足している物の、どちらも対象にする
/// 巡回パターン(ScriptableObject。複数の出現点で使い回される)は、変えない。出現点の位置・向きだけを変える
/// </summary>
public static partial class CSED_FieldMultiLayoutGenerator
{
    private const float _villainSameSpot = 8.0f;          // 別の出現点が、この距離以内にあれば、被っているとみなす(m)
    private const float _villainMinSpacing = 30.0f;       // 動かす先で、他の出現点から離す距離(m)
    private const float _villainOverlapNear = 6.0f;       // 別の出現点の巡回ルートが、この距離以内なら、重なっているとみなす(m)
    private const float _villainOverlapRatio = 0.5f;      // 巡回ルートの何割が重なっていたら、被っているとみなすか
    private const float _villainMoveOverlap = 0.25f;      // 動かす先で許す、巡回ルートの重なりの割合
    private const float _villainSampleStep = 4.0f;        // 巡回ルートの線を調べる間隔(m)
    private const float _villainSnapRadius = 2.0f;        // 巡回ポイントを、歩ける場所に合わせる半径(m。ゲーム側の CS_VillainPatrolRoute と同じ)
    private const float _villainSameLevel = 2.5f;         // 巡回ポイントが、出現点と同じ高さとみなす差(m)
    private const float _villainEdgeMin = 0.7f;           // 出現点を、歩ける場所の縁から離す距離(m)
    private const float _villainSearchRadius = 120.0f;    // 動かす先を、元の場所から探す半径(m)
    private const int _villainTryLimit = 500;             // 動かす先の候補を、1つの出現点につき調べる数の上限
    private const float _villainRoofHeight = 5.0f;        // 出現点の地面の高さ(出現点の高さ - heightOffset)が、この高さ以上なら、屋上にあったとみなす(m)
    private const float _villainMaxHeightGap = 30.0f;     // 屋上の出現点を動かす先の、元の高さとの差の上限(m)
    private const float _villainMaxOffset = 15.0f;        // 出現点が、巡回ポイントの地面より浮いていてよい高さの上限(m)

    private enum VillainAction
    {
        None,        // そのまま
        FixHeight,   // 高さだけを、歩ける場所に合わせる
        Move,        // 別の場所へ動かす
        Failed,      // 動かしたいが、動かす先が無い
    }

    private class VillainEntry
    {
        public CS_VillainSpawnPoint scene;
        public Transform target;                 // プレハブの中の同じ物(無ければnull)
        public bool inField;                     // Field.prefab の中の物か
        public Vector3 offset;                   // シーン - プレハブの中 の位置のずれ
        public string name;
        public Vector3 position;                 // 今のシーン上の位置
        public float yaw;                        // 今のシーン上の向き(Y軸の回転)
        public bool isRoof;                      // 屋上に置かれていたか
        public float heightOffset;               // 出現点が、巡回ポイントの地面より浮いている高さ(m。巡回パターンの最初のポイントの高さ(相対)の逆。パターンが無ければ0)
        public bool hasPattern;
        public string patternName = "";
        public bool hasPatrolArea;
        public float groupRadius;
        public List<List<Vector3>> routes = new List<List<Vector3>>();   // 巡回ルートごとの、出現点からの相対位置(向きを掛ける前)
        public List<bool> loops = new List<bool>();                       // ルートが周回か(falseは往復)
        public string problem;                   // 今の位置の問題(無ければnull)
        public VillainAction action;
        public Vector3 newPosition;
        public float newYaw;
    }

    /// <summary>
    /// 悪人の出現点を、動かさずに調べて、報告に書くメソッド(結果は Temp/FieldVillainFixReport.txt)
    /// </summary>
    [MenuItem("Tools/Police/悪人の出現点を調べる(動かさない)")]
    private static void CheckVillains()
    {
        FixVillains(false);
    }

    /// <summary>
    /// 悪人の出現点を、新しい街に合わせて直すメソッド
    /// 1) 高さが、歩ける場所から浮いている・埋まっている物は、真下(真上)の歩ける場所に合わせる
    /// 2) 位置は良くても、巡回ルートが建物を貫通する・歩ける場所から外れる物と、別の出現点に被っている物は、空いている所へ動かす
    /// 動かす先は、元の場所に近い順に、出現点・巡回ルートが入る所を探す(屋上にあった物は、先に屋上から探す)
    /// 結果は Temp/FieldVillainFixReport.txt
    /// </summary>
    [MenuItem("Tools/Police/悪人の出現点を新しい街に合わせて直す")]
    private static void RepairVillains()
    {
        FixVillains(true);
    }

    private static void FixVillains(bool apply)
    {
        _report = new StringBuilder();
        _multi = FindMulti();
        string path = _multi != null ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(_multi.gameObject)) : "";
        if (_multi == null || !path.EndsWith(_targetFile))
        {
            Debug.LogError("CSED_FieldMultiLayoutGenerator: シーンに Field/Field_Solo1 が見つかりません(MainSceneを開いてください)");
            return;
        }

        GameObject soloContents = null;
        GameObject fieldContents = null;
        try
        {
            CollectGeometry();
            _villainLines = null;
            _saBases = CollectSceneSaBases();
            List<Vector3> roofNodes = _saBases.FindAll(saBase => saBase.y >= _roofVillainMinHeight);
            soloContents = PrefabUtility.LoadPrefabContents(path);
            fieldContents = PrefabUtility.LoadPrefabContents(_fieldPrefabPath);
            _repairSoloRoot = soloContents.transform;
            _repairFieldSolo = null;
            foreach (Transform child in fieldContents.transform)
            {
                if (child.name == _targetName) _repairFieldSolo = child;
            }
            _repairFieldChanged = false;

            List<VillainEntry> entries = CollectVillainEntries();

            // 1) 今の位置を調べる。問題があれば、まず、高さだけを歩ける場所に合わせて、直るか試す
            foreach (VillainEntry entry in entries)
            {
                entry.problem = FindVillainProblem(entry, entry.position, entry.yaw, entry.isRoof);
                if (entry.problem == null) continue;

                if (TryFindVillainGround(entry.position - Vector3.up * entry.heightOffset, entry.isRoof, out Vector3 ground) && FindVillainProblem(entry, ground + Vector3.up * entry.heightOffset, entry.yaw, entry.isRoof) == null)
                {
                    entry.action = VillainAction.FixHeight;
                    entry.newPosition = ground + Vector3.up * entry.heightOffset;
                    entry.newYaw = entry.yaw;
                    entry.problem = null;
                }
            }

            // 2) 問題が無い物から順に、位置を確定する。確定済みの物に被っている物は、動かす対象にする
            List<Vector3> settledSpawns = new List<Vector3>();
            List<Vector3> settledSamples = new List<Vector3>();
            foreach (VillainEntry entry in entries)
            {
                if (entry.problem != null) continue;

                Vector3 position = entry.action == VillainAction.FixHeight ? entry.newPosition : entry.position;
                if (IsVillainOverlapped(entry, position, entry.yaw, settledSpawns, settledSamples))
                {
                    entry.problem = "別の出現点に被っている";
                    entry.action = VillainAction.None;
                    continue;
                }
                SettleVillain(entry, position, entry.yaw, settledSpawns, settledSamples);
            }

            // 3) 問題がある物を、空いている所へ動かす
            foreach (VillainEntry entry in entries)
            {
                if (entry.problem == null) continue;

                if (TryRelocateVillain(entry, roofNodes, settledSpawns, settledSamples, out Vector3 newPosition, out float newYaw))
                {
                    entry.action = VillainAction.Move;
                    entry.newPosition = newPosition;
                    entry.newYaw = newYaw;
                    SettleVillain(entry, newPosition, newYaw, settledSpawns, settledSamples);
                }
                else
                {
                    entry.action = VillainAction.Failed;
                    SettleVillain(entry, entry.position, entry.yaw, settledSpawns, settledSamples);
                }
            }

            WriteVillainReport(entries, apply);

            if (!apply) return;

            foreach (VillainEntry entry in entries)
            {
                if (entry.action != VillainAction.FixHeight && entry.action != VillainAction.Move) continue;
                if (entry.target == null) continue;

                entry.target.position = entry.newPosition - entry.offset;
                if (Mathf.Abs(Mathf.DeltaAngle(entry.yaw, entry.newYaw)) > 0.5f) entry.target.rotation = Quaternion.Euler(0.0f, Mathf.DeltaAngle(entry.yaw, entry.newYaw), 0.0f) * entry.target.rotation;
                _repairFieldChanged |= entry.inField;
            }

            PrefabUtility.SaveAsPrefabAsset(soloContents, path);
            if (_repairFieldChanged) PrefabUtility.SaveAsPrefabAsset(fieldContents, _fieldPrefabPath);
            _report.AppendLine($"Field.prefab(Field_Solo1 に足されている物)も直した: {_repairFieldChanged}");
        }
        finally
        {
            if (soloContents != null) PrefabUtility.UnloadPrefabContents(soloContents);
            if (fieldContents != null) PrefabUtility.UnloadPrefabContents(fieldContents);
            _repairSoloRoot = null;
            _repairFieldSolo = null;
            string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "FieldVillainFixReport.txt");
            File.WriteAllText(reportPath, _report.ToString(), new UTF8Encoding(false));
        }
        Debug.Log(apply ? "CSED_FieldMultiLayoutGenerator: 悪人の出現点を直しました" : "CSED_FieldMultiLayoutGenerator: 悪人の出現点を調べました");
    }

    /// <summary>
    /// シーンの悪人の出現点を集め、それぞれの巡回ルートの形と、プレハブの中の同じ物を調べるメソッド
    /// </summary>
    private static List<VillainEntry> CollectVillainEntries()
    {
        List<VillainEntry> entries = new List<VillainEntry>();
        HashSet<Transform> matched = new HashSet<Transform>();   // 対応づけ済みの、プレハブの中の物(同じ名前・同じ位置の物が複数あっても、別々の物に対応づける)
        foreach (CS_VillainSpawnPoint spawn in _multi.GetComponentsInChildren<CS_VillainSpawnPoint>(true))
        {
            VillainEntry entry = new VillainEntry
            {
                scene = spawn,
                name = spawn.transform.parent != null ? $"{spawn.transform.parent.name}/{spawn.name}" : spawn.name,
                position = spawn.transform.position,
                yaw = spawn.transform.eulerAngles.y,
                groupRadius = spawn.groupRadius,
            };
            entry.target = FindInContents(spawn.transform, out entry.inField, out entry.offset, matched);
            if (entry.target != null)
            {
                matched.Add(entry.target);
                if (!entry.inField) ExcludeFieldCounterpart(entry.target, matched);
            }

            CS_VillainPatrolRoute patrolRoute = spawn.patrolRoute;
            if (patrolRoute != null && patrolRoute.hasPattern)
            {
                entry.hasPattern = true;
                entry.patternName = patrolRoute.pattern.name;
                foreach (CSO_VillainPatrolPattern.Route route in patrolRoute.pattern.routes)
                {
                    if (route.points.Count == 0) continue;

                    List<Vector3> points = new List<Vector3>();
                    foreach (CSO_VillainPatrolPattern.Point point in route.points) points.Add(point.localPosition);
                    entry.routes.Add(points);
                    entry.loops.Add(route.loopMode == CSE_VillainPatrolLoopMode.Loop);
                }
            }
            else
            {
                // 巡回パターンが無ければ、前後(Z方向)の2点を往復する
                SerializedObject serialized = new SerializedObject(spawn);
                float distance = serialized.FindProperty("_fallbackPatrolDistance").floatValue;
                entry.hasPatrolArea = serialized.FindProperty("_patrolArea").objectReferenceValue != null;
                entry.routes.Add(new List<Vector3> { Vector3.forward * distance, Vector3.back * distance });
                entry.loops.Add(false);
            }

            // 巡回パターンのポイントは、出現点より低い所(地面)に作られている(出現点は、地面の上に浮かせて置く設計)。その高さの差は、動かしても保つ
            if (entry.hasPattern) entry.heightOffset = Mathf.Clamp(-entry.routes[0][0].y, 0.0f, _villainMaxOffset);
            entry.isRoof = entry.position.y - entry.heightOffset >= _villainRoofHeight;
            entries.Add(entry);
        }
        return entries;
    }

    /// <summary>
    /// Field_Solo1.prefab の中の物と同じ物が、Field.prefab の中の Field_Solo1 にも、そのまま入っているので、そちらも対応づけ済みにするメソッド
    /// (Field.prefab で足された、同じ名前・同じ位置の別の物を、探す時に、取り違えないため)
    /// </summary>
    private static void ExcludeFieldCounterpart(Transform soloChild, HashSet<Transform> matched)
    {
        if (_repairFieldSolo == null) return;

        List<string> names = new List<string>();
        for (Transform t = soloChild; t != null && t != _repairSoloRoot; t = t.parent) names.Insert(0, t.name);
        string last = names[names.Count - 1];
        names.RemoveAt(names.Count - 1);
        Transform parent = names.Count == 0 ? _repairFieldSolo : _repairFieldSolo.Find(string.Join("/", names));
        if (parent == null) return;

        foreach (Transform child in parent)
        {
            if (child.name == last && !PrefabUtility.IsAddedGameObjectOverride(child.gameObject) && FlatDistance(child.position, soloChild.position) <= _repairSameObject) matched.Add(child);
        }
    }

    /// <summary>
    /// 出現点を、position・yaw に置いた時の問題(歩ける場所の上に無い・埋まっている・巡回ルートが外れる・貫通する)を調べるメソッド
    /// </summary>
    /// <returns>問題の内容(問題が無ければnull)</returns>
    private static string FindVillainProblem(VillainEntry entry, Vector3 position, float yaw, bool roof)
    {
        // 巡回パターンが無い物は、出現点そのものが、歩ける場所の上にある(巡回パターンがある物は、出現点は地面より浮かせて置くので、巡回ポイントだけを調べる)
        float levelY = position.y;
        if (!entry.hasPattern)
        {
            if (!NavMesh.SamplePosition(position, out NavMeshHit own, 0.6f, NavMesh.AllAreas)) return "出現点が、歩ける場所の上に無い(浮いている・外れている)";
            if (CSED_FieldPlacementCheck.FindProblem(position, false) != null) return "出現点が、建物などに埋まっている";
            if (NavMesh.FindClosestEdge(own.position, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance < _villainEdgeMin) return "出現点が、歩ける場所の縁に近すぎる";

            // グループのメンバーが並ぶ円(半径 groupRadius)の上が、埋まっていないか
            int blocked = 0;
            for (int i = 0; i < 8; i++)
            {
                Vector3 member = position + Quaternion.Euler(0.0f, 45.0f * i, 0.0f) * Vector3.forward * entry.groupRadius;
                if (CSED_FieldPlacementCheck.FindProblem(member, false) != null) blocked++;
            }
            if (blocked > 2) return "グループのメンバーが並ぶ所が、建物などに埋まっている";
            levelY = own.position.y;
        }

        Quaternion rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
        for (int r = 0; r < entry.routes.Count; r++)
        {
            List<Vector3> snapped = new List<Vector3>();
            foreach (Vector3 local in entry.routes[r])
            {
                Vector3 world = position + rotation * local;
                if (!NavMesh.SamplePosition(world, out NavMeshHit hit, _villainSnapRadius, NavMesh.AllAreas)) return "巡回ポイントが、歩ける場所から外れている";
                if (entry.hasPattern && r == 0 && snapped.Count == 0) levelY = hit.position.y;
                if (roof ? hit.position.y < _groundHeight : hit.position.y >= _groundHeight) return "巡回ポイントの高さが、地上・屋上の区別と合わない";
                if (Mathf.Abs(hit.position.y - levelY) > _villainSameLevel) return "巡回ポイントが、出現点と違う高さに合う";
                if (CSED_FieldPlacementCheck.FindProblem(hit.position, false) != null) return "巡回ポイントが、建物などに埋まっている";

                snapped.Add(hit.position);
            }

            int segments = entry.loops[r] ? snapped.Count : snapped.Count - 1;
            for (int i = 0; i < segments; i++)
            {
                if (CSED_FieldPlacementCheck.IsBlockedSegment(snapped[i], snapped[(i + 1) % snapped.Count])) return "巡回ルートの区間が、建物を貫通する";
            }
        }
        return null;
    }

    /// <summary>
    /// 位置の真下(屋上なら、同じ高さ)の、歩ける場所を探すメソッド(出現点が浮いている・埋まっている時に、高さだけ合わせる)
    /// </summary>
    private static bool TryFindVillainGround(Vector3 position, bool roof, out Vector3 ground)
    {
        ground = position;
        float best = float.MaxValue;
        bool found = false;
        foreach (float y in new[] { 0.3f, 1.0f, 2.0f, position.y })
        {
            if (!NavMesh.SamplePosition(new Vector3(position.x, y, position.z), out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
            if (FlatDistance(hit.position, position) > 1.5f || (roof ? hit.position.y < _groundHeight : hit.position.y >= _groundHeight)) continue;
            if (CSED_FieldPlacementCheck.FindProblem(hit.position, false) != null) continue;

            // 屋上は、元の高さに近い物。地上は、一番低い物
            float score = roof ? Mathf.Abs(hit.position.y - position.y) : hit.position.y;
            if (score >= best) continue;

            best = score;
            ground = hit.position;
            found = true;
        }
        return found;
    }

    /// <summary>
    /// 出現点の巡回ルートの線の上の点(出現点も含む。_villainSampleStep おき)を返すメソッド
    /// </summary>
    private static List<Vector3> VillainSamples(VillainEntry entry, Vector3 position, float yaw)
    {
        List<Vector3> samples = new List<Vector3> { position };
        Quaternion rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
        for (int r = 0; r < entry.routes.Count; r++)
        {
            List<Vector3> points = entry.routes[r];
            if (points.Count == 1) samples.Add(position + rotation * points[0]);

            int segments = entry.loops[r] ? points.Count : points.Count - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector3 from = position + rotation * points[i];
                Vector3 to = position + rotation * points[(i + 1) % points.Count];
                float length = Vector3.Distance(from, to);
                for (float d = 0.0f; d < length; d += _villainSampleStep) samples.Add(Vector3.Lerp(from, to, length > 0.01f ? d / length : 0.0f));
            }
        }
        return samples;
    }

    /// <summary>
    /// 確定済みの出現点に、被っているか(出現点が _villainSameSpot 以内、または、巡回ルートの _villainOverlapRatio 以上が重なる)を判定するメソッド
    /// </summary>
    private static bool IsVillainOverlapped(VillainEntry entry, Vector3 position, float yaw, List<Vector3> settledSpawns, List<Vector3> settledSamples)
    {
        if (IsNearSameLevel(settledSpawns, position - Vector3.up * entry.heightOffset, _villainSameSpot)) return true;

        return VillainOverlapRatio(entry, position, yaw, settledSamples) >= _villainOverlapRatio;
    }

    private static float VillainOverlapRatio(VillainEntry entry, Vector3 position, float yaw, List<Vector3> settledSamples)
    {
        List<Vector3> mine = VillainSamples(entry, position, yaw);
        int overlapped = 0;
        foreach (Vector3 sample in mine)
        {
            if (IsNearSameLevel(settledSamples, sample, _villainOverlapNear)) overlapped++;
        }
        return (float)overlapped / mine.Count;
    }

    private static void SettleVillain(VillainEntry entry, Vector3 position, float yaw, List<Vector3> settledSpawns, List<Vector3> settledSamples)
    {
        settledSpawns.Add(position - Vector3.up * entry.heightOffset);
        settledSamples.AddRange(VillainSamples(entry, position, yaw));
    }

    /// <summary>
    /// 出現点を動かす先を探すメソッド(元の場所に近い順に、出現点・巡回ルートが問題なく入り、他の出現点から離れた所)
    /// 屋上にあった物は、登れる屋上のSAObjectの近くの屋上から先に探す。無ければ、地上から探す
    /// 向きは、元の向きから 90度ずつ、次に 45度ずつ回して試す
    /// </summary>
    private static bool TryRelocateVillain(VillainEntry entry, List<Vector3> roofNodes, List<Vector3> settledSpawns, List<Vector3> settledSamples, out Vector3 newPosition, out float newYaw, int forceArea = -1)
    {
        newPosition = entry.position;
        newYaw = entry.yaw;

        List<Vector3> roofPool = new List<Vector3>(_roofNav);
        roofPool.AddRange(_roofNavAll);
        float[] yawOffsets = { 0.0f, 90.0f, 180.0f, 270.0f, 45.0f, 135.0f, 225.0f, 315.0f };
        // 屋上にあった物は、屋上 → 地上、地上にあった物は、地上のみ。間隔・重なりは、厳しい条件で探してから、ゆるめて探す
        bool[] roofOrder = entry.isRoof ? new[] { true, false } : new[] { false };
        foreach (float spacing in forceArea >= 0 ? new[] { 60.0f, 40.0f, _villainMinSpacing } : new[] { _villainMinSpacing, _villainSameSpot * 1.5f })
        {
            foreach (bool roof in roofOrder)
            {
                List<(float distance, Vector3 position)> candidates = new List<(float, Vector3)>();
                foreach (Vector3 candidate in roof ? roofPool : _ground)
                {
                    if (forceArea >= 0 && NearestArea(candidate) != forceArea) continue;
                    if (IsNearAny(_startPoints, candidate, _siteMinFromStart) || IsNearSameLevel(settledSpawns, candidate, spacing)) continue;
                    if (roof && !IsNearRoofNode(roofNodes, candidate)) continue;

                    // 元の場所に近く、元の高さに近い物を優先する(低い屋上にあった物が、高いビルの屋上へ飛ばないように)
                    float heightGap = Mathf.Abs(candidate.y - (entry.position.y - entry.heightOffset));
                    if (roof && heightGap > _villainMaxHeightGap) continue;

                    // エリアを指定した時は、そのエリアの中心に近い所から、(間隔を空けて)順に探す
                    float distance = FlatDistance(candidate, forceArea >= 0 ? _areaCenters[forceArea] : entry.position);
                    if (distance <= _villainSearchRadius || !roof || forceArea >= 0) candidates.Add((distance + heightGap * 3.0f, candidate));
                }
                candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

                HashSet<Vector2Int> tried = new HashSet<Vector2Int>();
                int tries = 0;
                foreach ((float distance, Vector3 position) candidate in candidates)
                {
                    // 近い点は同じ候補とみなして、1つだけ調べる
                    if (!tried.Add(new Vector2Int(Mathf.RoundToInt(candidate.position.x / 6.0f), Mathf.RoundToInt(candidate.position.z / 6.0f)))) continue;
                    if (++tries > _villainTryLimit) break;

                    // 出現点そのものを歩ける場所に置く物は、縁から離れた内側へ寄せる(巡回パターンがある物は、出現点を浮かせて置く)
                    Vector3 inside = candidate.position;
                    if (!entry.hasPattern && !PullVillainInside(ref inside, roof)) continue;

                    Vector3 spawn = inside + Vector3.up * entry.heightOffset;
                    foreach (float yawOffset in yawOffsets)
                    {
                        float yaw = entry.yaw + yawOffset;
                        if (FindVillainProblem(entry, spawn, yaw, roof) != null) continue;
                        if (VillainOverlapRatio(entry, spawn, yaw, settledSamples) >= _villainMoveOverlap) continue;

                        newPosition = spawn;
                        newYaw = Mathf.Repeat(yaw, 360.0f);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 水平距離が distance 未満で、高さの差が 4m 未満の点(同じ階層の点)があるかを判定するメソッド(屋上と地上で、水平位置だけ同じ物を、被りにしないため)
    /// </summary>
    private static bool IsNearSameLevel(List<Vector3> points, Vector3 position, float distance)
    {
        foreach (Vector3 point in points)
        {
            if (FlatDistance(point, position) < distance && Mathf.Abs(point.y - position.y) < 4.0f) return true;
        }
        return false;
    }

    /// <summary>
    /// 位置が、登れる屋上のSAObjectの近く(水平距離が _roofVillainNearSa 以内で、同じ高さ)かを判定するメソッド
    /// </summary>
    private static bool IsNearRoofNode(List<Vector3> roofNodes, Vector3 position)
    {
        foreach (Vector3 node in roofNodes)
        {
            if (Mathf.Abs(node.y - position.y) <= 1.5f && FlatDistance(node, position) <= _roofVillainNearSa) return true;
        }
        return false;
    }

    /// <summary>
    /// 候補の点を、歩ける場所の縁(_villainEdgeMin + 余裕)から離れた内側へ寄せるメソッド(寄せられなければ false)
    /// </summary>
    private static bool PullVillainInside(ref Vector3 position, bool roof)
    {
        float minEdge = _villainEdgeMin + 0.8f;
        Vector3 original = position;
        for (int i = 0; i < 4; i++)
        {
            if (!NavMesh.FindClosestEdge(position, out NavMeshHit edge, NavMesh.AllAreas)) return false;
            if (edge.distance >= minEdge) return true;

            Vector3 inward = FlatOf(position - edge.position);
            if (inward.sqrMagnitude < 0.01f) inward = FlatOf(edge.normal);
            if (inward.sqrMagnitude < 0.01f) return false;

            Vector3 moved = position + inward.normalized * (minEdge + 0.3f - edge.distance);
            if (!NavMesh.SamplePosition(moved, out NavMeshHit hit, 1.0f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - original.y) > 1.5f) return false;
            if (roof ? hit.position.y < _groundHeight : hit.position.y >= _groundHeight) return false;

            position = hit.position;
        }
        return NavMesh.FindClosestEdge(position, out NavMeshHit last, NavMesh.AllAreas) && last.distance >= _villainEdgeMin;
    }

    private static void WriteVillainReport(List<VillainEntry> entries, bool apply)
    {
        int ok = 0;
        int fixHeight = 0;
        int moved = 0;
        int failed = 0;
        int notFound = 0;
        foreach (VillainEntry entry in entries)
        {
            if (entry.target == null) notFound++;
            switch (entry.action)
            {
                case VillainAction.None: ok++; break;
                case VillainAction.FixHeight: fixHeight++; break;
                case VillainAction.Move: moved++; break;
                case VillainAction.Failed: failed++; break;
            }
        }
        _report.AppendLine($"{(apply ? "【悪人の出現点を直した】" : "【悪人の出現点を調べた(動かしていない)】")}{entries.Count}個 / そのまま {ok}個 / 高さだけ合わせる {fixHeight}個 / 動かす {moved}個 / 動かせない {failed}個 / プレハブに見つからない {notFound}個");
        foreach (VillainEntry entry in entries)
        {
            string pattern = entry.hasPattern ? $"パターン {entry.patternName}" : $"往復{(entry.hasPatrolArea ? "(路地裏の範囲あり)" : "")}";
            string head = $"  {entry.name} ({entry.position.x:F1}, {entry.position.y:F1}, {entry.position.z:F1}) 向き {entry.yaw:F0}° {(entry.isRoof ? "屋上" : "地上")} / {pattern} / {(entry.inField ? "Field.prefab側" : "Field_Solo1側")}{(entry.target == null ? " / プレハブに見つからない" : "")}";
            switch (entry.action)
            {
                case VillainAction.None:
                    _report.AppendLine($"{head} → そのまま");
                    break;
                case VillainAction.FixHeight:
                    _report.AppendLine($"{head} → 高さを合わせる y {entry.position.y:F1} → {entry.newPosition.y:F1}");
                    break;
                case VillainAction.Move:
                    _report.AppendLine($"{head} → 動かす ({entry.newPosition.x:F1}, {entry.newPosition.y:F1}, {entry.newPosition.z:F1}) 向き {entry.newYaw:F0}° / 移動 {FlatDistance(entry.position, entry.newPosition):F0}m / 理由: {entry.problem}");
                    break;
                default:
                    _report.AppendLine($"{head} → 動かせない / 理由: {entry.problem}");
                    break;
            }
        }
    }
}
