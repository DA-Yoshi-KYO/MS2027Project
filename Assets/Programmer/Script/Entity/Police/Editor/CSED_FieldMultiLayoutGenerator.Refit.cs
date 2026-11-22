/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-14 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 配置ツールのうち、街(ビル・NavMesh)が変わった後に、今ある警察のグループを直す・足す部分
/// 直す物は、Field_Solo1.prefab の中の物と、Field.prefab が Field_Solo1 に足している物の、どちらも対象にする
/// </summary>
public static partial class CSED_FieldMultiLayoutGenerator
{
    private const string _fieldPrefabPath = "Assets/Programmer/Prefab/Field/Field.prefab";
    private const float _repairHeightTolerance = 3.0f;   // ずらす先の高さが、元の高さと違ってよい範囲(m)
    private const float _repairPointSearch = 6.0f;       // 警察の巡回ポイントを、歩ける場所へずらす時に探す距離の上限(m)
    private const float _repairEdgeInset = 1.0f;         // 道のりの曲がり角を、歩ける場所の縁から内側へ寄せる距離(m)
    private const float _repairSameHeight = 6.0f;       // 同じ物とみなす、高さの差(m。Field.prefab の中の物は、高さがずれる)
    private const float _repairSameObject = 0.5f;        // シーンの物と、プレハブの中の物が、同じ物とみなす位置の差(m)
    private const float _refitGapMin = 70.0f;            // 空いている所に警察のグループを足す時の、他のグループの巡回ポイントからの距離の下限(m)
    private const int _refitExtraPerArea = 3;            // エリアごとに足す、空いている所のグループの数の上限

    private static Transform _repairSoloRoot;            // Field_Solo1.prefab の中身
    private static Transform _repairFieldSolo;           // Field.prefab の中の、Field_Solo1 のインスタンス
    private static bool _repairFieldChanged;             // Field.prefab の中の物を変えたか
    private static List<Vector3> _movedSquadPoints = new List<Vector3>();   // 動かしたグループの、新しい巡回ポイントの位置(シーンには、まだ反映されていない)

    /// <summary>
    /// 新しい街に合わせて、警察のグループを直す・足すメソッド(悪人・ドローン・レーザーは、そのまま)
    /// 1) 建物に埋まった・歩ける場所から外れた巡回ポイントを、近くの歩ける場所へずらし、建物を貫通する区間に、道のりの曲がり角を中継ポイントとして足す
    /// 2) それでも、歩いて行けない所をつなぐ(NavMeshが切れている)グループは、同じグループの設定のまま、巡回ポイントだけ、新しい街の歩ける道で、作り直す
    /// 3) 悪人の出現点の近く(_policeCoverDistance以内)に警察陣営がいない所に、グループを足す
    /// 4) 各エリアの、他のグループの巡回ポイントから _refitGapMin 以上離れた空いている所に、グループを足す(エリアごとに _refitExtraPerArea 個まで)
    /// 結果は Temp/FieldPoliceRefitReport.txt
    /// </summary>
    [MenuItem("Tools/Police/警察のグループを新しい街に合わせて直す・足す")]
    private static void RefitPolice()
    {
        RefitPoliceCore(true);
    }

    /// <summary>
    /// 警察のグループの、巡回ポイントの浮き・埋まり・建物の貫通だけを直すメソッド(グループは足さない)
    /// </summary>
    [MenuItem("Tools/Police/警察の巡回ルートの浮き・埋まりだけ直す(足さない)")]
    private static void RefitPoliceWithoutAdding()
    {
        RefitPoliceCore(false);
    }

    private static void RefitPoliceCore(bool addSquads)
    {
        _report = new StringBuilder();
        _policeFaction = new List<Vector3>();
        _squadPoints = new List<Vector3>();
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
            References refs = CollectReferences();
            soloContents = PrefabUtility.LoadPrefabContents(path);
            fieldContents = PrefabUtility.LoadPrefabContents(_fieldPrefabPath);
            _repairSoloRoot = soloContents.transform;
            _repairFieldSolo = null;
            foreach (Transform child in fieldContents.transform)
            {
                if (child.name == _targetName) _repairFieldSolo = child;
            }
            _repairFieldChanged = false;

            // 今ある、警察陣営(ドローン・レーザー)の位置
            foreach (CS_SecurityDroneSpawner drone in _multi.GetComponentsInChildren<CS_SecurityDroneSpawner>(true))
            {
                foreach (Transform point in drone.transform) _policeFaction.Add(point.position);
            }
            foreach (CS_SecurityLaser laser in _multi.GetComponentsInChildren<CS_SecurityLaser>(true)) _policeFaction.Add(laser.transform.position);

            // 1) 巡回ポイントをずらし、貫通する区間に中継ポイントを足す。それでも直らないグループは、作り直す一覧に入れる
            List<(CS_PoliceSquad scene, Transform target, bool inField)> rebuild = new List<(CS_PoliceSquad, Transform, bool)>();
            int touched = 0;
            int snapped = 0;
            int removed = 0;
            int inserted = 0;
            int notFound = 0;
            foreach (CS_PoliceSquad squad in _multi.GetComponentsInChildren<CS_PoliceSquad>(true))
            {
                Transform target = FindInContents(squad.transform, out bool inField);
                CS_PoliceSquad targetSquad = target != null ? target.GetComponent<CS_PoliceSquad>() : null;
                if (targetSquad == null)
                {
                    notFound++;
                    foreach (Transform point in squad.GetRouteDrawPoints()) _squadPoints.Add(point.position);
                    continue;
                }

                bool changed = LightRepairSquad(target, targetSquad, ref snapped, ref removed, ref inserted);
                if (changed)
                {
                    touched++;
                    _repairFieldChanged |= inField;
                }

                if (IsRouteBroken(targetSquad.GetRouteDrawPoints()))
                {
                    rebuild.Add((squad, target, inField));
                    continue;
                }

                foreach (Transform point in targetSquad.GetRouteDrawPoints())
                {
                    _squadPoints.Add(point.position);
                    _policeFaction.Add(point.position);
                }
            }
            _report.AppendLine($"警察のグループ: 巡回ポイントをずらした {snapped}個・消した {removed}個 / 中継ポイントを足した {inserted}個 / 直したグループ {touched}個 / プレハブに見つからなかったグループ {notFound}個");

            // 2) それでも、歩いて行けない所をつなぐグループは、巡回ポイントを作り直す
            Transform container = FindOrCreateGroup(soloContents.transform, _policeGroup);
            int rebuilt = 0;
            foreach ((CS_PoliceSquad scene, Transform target, bool inField) item in rebuild)
            {
                if (TryRebuildSquad(item.scene, item.target, item.inField)) rebuilt++;
                else _report.AppendLine($"  作り直せなかった(今のまま残す): {item.scene.name}");
            }
            _report.AppendLine($"歩いて行けない所をつなぐグループ {rebuild.Count}個 → 巡回ポイントを作り直した {rebuilt}個");

            // 3) 悪人の出現点の近くに、警察陣営がいない所へ、グループを足す
            int covered = 0;
            int number = 0;
            foreach (CS_VillainSpawnPoint villain in _multi.GetComponentsInChildren<CS_VillainSpawnPoint>(true))
            {
                if (!addSquads) break;

                Vector3 position = villain.transform.position;
                if (MinDistanceToPoliceFaction(position) <= _policeCoverDistance) continue;

                Site site = new Site { area = NearestArea(position), position = position, isRoof = position.y >= _groundHeight };
                if (TryPlaceSquad(container, refs, GetGroundCenter(site), UniqueSquadName(container, $"{_generatedSquadPrefix}{site.area}_C", ref number))) covered++;
                else _report.AppendLine($"  警察のグループを足せなかった(悪人の近く): {villain.name} ({position.x:F0}, {position.y:F0}, {position.z:F0})");
            }
            _report.AppendLine($"悪人の出現点の近くに、警察陣営が足りなかった所へ、グループを足した {covered}個");

            // 4) 空いている所に、グループを足す
            int extra = 0;
            for (int area = 0; area < _areaCenters.Count && addSquads; area++)
            {
                for (int k = 0; k < _refitExtraPerArea; k++)
                {
                    bool placed = false;
                    foreach (Vector3 center in GetGapCandidates(area))
                    {
                        if (MinFlatDistance(_squadPoints, center) < _refitGapMin) continue;
                        if (!TryPlaceSquad(container, refs, center, UniqueSquadName(container, $"{_generatedSquadPrefix}{area}_N", ref number), _minExtraRouteLength)) continue;

                        placed = true;
                        extra++;
                        break;
                    }
                    if (!placed) break;
                }
            }
            _report.AppendLine($"空いている所へ足したグループ {extra}個");

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
            string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "FieldPoliceRefitReport.txt");
            File.WriteAllText(reportPath, _report.ToString(), new UTF8Encoding(false));
        }
        Debug.Log("CSED_FieldMultiLayoutGenerator: 警察のグループを、新しい街に合わせて直し・足しました");
    }

    /// <summary>
    /// シーンの Transform に対応する、プレハブの中の Transform を探すメソッド
    /// 配置先(Field_Solo1)からの名前の並びで、Field_Solo1.prefab の中を探し、無ければ、Field.prefab の中の Field_Solo1 を探す
    /// 同じ名前の物が複数あれば、位置が同じ(_repairSameObject 以内)物を選ぶ
    /// </summary>
    /// <param name="inField">Field.prefab の中の物か(Field.prefab を保存する必要がある)</param>
    private static Transform FindInContents(Transform scene, out bool inField)
    {
        Transform found = FindInContents(scene, out inField, out Vector3 offset);
        if (found == null || offset.sqrMagnitude <= _repairSameObject * _repairSameObject) return found;

        inField = false;     // シーンとプレハブの中の位置がずれている物は、座標をそのまま書き込めないので、見つからなかったことにする(ずれを扱える処理だけ、3つ目の引数がある版を使う)
        return null;
    }

    /// <summary>
    /// FindInContents の、シーンの位置と、プレハブの中の位置の、ずれ(シーン - プレハブの中。Field.prefab の中の物は、高さがずれることがある)も返す版
    /// 水平に _repairSameObject 以内で、高さの差が _repairSameHeight 以内の、同じ名前の物を、水平に近い順に選ぶ
    /// </summary>
    /// <param name="exclude">対応づけ済みで、選ばない物(同じ名前・同じ位置の物が複数あるとき、1つの物を2回選ばないため)</param>
    private static Transform FindInContents(Transform scene, out bool inField, out Vector3 offset, HashSet<Transform> exclude = null)
    {
        inField = false;
        offset = Vector3.zero;
        List<string> names = new List<string>();
        for (Transform t = scene; t != null && t != _multi; t = t.parent) names.Insert(0, t.name);
        string last = names[names.Count - 1];
        names.RemoveAt(names.Count - 1);
        string parentPath = string.Join("/", names);

        foreach (bool field in new[] { false, true })
        {
            Transform root = field ? _repairFieldSolo : _repairSoloRoot;
            Transform parent = root == null ? null : (parentPath == "" ? root : root.Find(parentPath));
            if (parent == null) continue;

            // シーンで、親の位置がずらされている(CrimeArea_01 など)時は、そのずれの分を引いて、位置を比べる
            Vector3 parentShift = scene.parent != null ? scene.parent.position - parent.position : Vector3.zero;
            Transform best = null;
            float bestDistance = _repairSameObject;
            foreach (Transform child in parent)
            {
                if (exclude != null && exclude.Contains(child)) continue;

                float flat = FlatDistance(child.position + parentShift, scene.position);
                if (child.name != last || flat > bestDistance || Mathf.Abs(child.position.y + parentShift.y - scene.position.y) > _repairSameHeight) continue;

                best = child;
                bestDistance = flat;
            }
            if (best == null) continue;

            inField = field;
            offset = scene.position - best.position;
            return best;
        }
        return null;
    }

    /// <summary>
    /// グループの巡回ポイントを、軽く直すメソッド(建物に埋まった・歩ける場所から外れた点をずらす。ずらせなければ、点が4つ以上残る範囲で消す。建物を貫通する区間に、道のりの曲がり角を足す)
    /// </summary>
    /// <returns>何か変えたか</returns>
    private static bool LightRepairSquad(Transform target, CS_PoliceSquad targetSquad, ref int snapped, ref int removed, ref int inserted)
    {
        List<Transform> points = targetSquad.GetRouteDrawPoints();
        List<Transform> kept = new List<Transform>();
        bool changed = false;
        foreach (Transform point in points)
        {
            if (CSED_FieldPlacementCheck.FindProblem(point.position, true) == null)
            {
                kept.Add(point);
                continue;
            }

            changed = true;
            if (TrySnapPatrolPoint(point.position, out Vector3 moved))
            {
                point.position = moved;
                kept.Add(point);
                snapped++;
            }
            else if (kept.Count + (points.Count - 1 - points.IndexOf(point)) >= 4)
            {
                Object.DestroyImmediate(point.gameObject);
                removed++;
            }
            else
            {
                kept.Add(point);
            }
        }

        int number = 0;
        for (int i = 0; i < kept.Count; i++)
        {
            Vector3 from = kept[i].position;
            Vector3 to = kept[(i + 1) % kept.Count].position;
            if (!CSED_FieldPlacementCheck.IsBlockedSegment(from, to)) continue;

            int at = kept[i].GetSiblingIndex() + 1;
            foreach (Vector3 corner in FindRouteCorners(from, to))
            {
                GameObject waypoint = new GameObject($"RoadPatrolPoint_{target.childCount:000}_fit{++number}");
                waypoint.transform.SetParent(target, false);
                waypoint.transform.position = corner;
                waypoint.transform.SetSiblingIndex(at++);
                inserted++;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// グループの巡回ルートに、問題(建物に埋まった・歩ける場所から外れた点、建物を貫通する区間)があるかを判定するメソッド
    /// </summary>
    private static bool IsRouteBroken(List<Transform> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if (CSED_FieldPlacementCheck.FindProblem(points[i].position, true) != null) return true;
            if (CSED_FieldPlacementCheck.IsBlockedSegment(points[i].position, points[(i + 1) % points.Count].position)) return true;
        }
        return points.Count < 3;
    }

    /// <summary>
    /// グループの巡回ポイントを、そのグループの真ん中のまわりで、歩いて行ける道を一周するように、作り直すメソッド(グループの設定は、そのまま)
    /// 古い巡回ポイントは、新しいポイントを作れた時だけ、消す
    /// </summary>
    private static bool TryRebuildSquad(CS_PoliceSquad scene, Transform target, bool inField)
    {
        CS_PoliceSquad targetSquad = target.GetComponent<CS_PoliceSquad>();
        List<Transform> oldPoints = targetSquad.GetRouteDrawPoints();
        if (oldPoints.Count == 0) return false;

        Vector3 center = Vector3.zero;
        foreach (Transform point in oldPoints) center += point.position;
        center /= oldPoints.Count;
        if (!NavMesh.SamplePosition(center, out NavMeshHit hit, 40.0f, NavMesh.AllAreas)) return false;

        List<Vector3> route = new List<Vector3>();
        foreach (float nearRadius in new[] { _routeNearRadius, 25.0f, 45.0f })
        {
            _nearRadius = nearRadius;
            route = BuildWideRoute(hit.position, 0.0f);
            if (route.Count >= 4) break;
        }
        _nearRadius = _routeNearRadius;
        if (route.Count < 4) return false;

        route = FollowStreets(route);
        route.RemoveAll(point => CSED_FieldPlacementCheck.FindProblem(point, true) != null);   // 建物に埋まる・地面の下にもぐる点は、使わない
        if (route.Count < 4) return false;

        Transform spawn = null;
        foreach (Transform child in target)
        {
            if (!oldPoints.Contains(child)) spawn = child;
        }
        int oldCount = oldPoints.Count;
        foreach (Transform point in oldPoints) Object.DestroyImmediate(point.gameObject);

        // 先に、グループ(または出現位置)を最初のポイントへ動かしてから、ポイントを作る(後で動かすと、ポイントも一緒にずれる)
        if (spawn != null) spawn.position = route[0];
        else target.position = route[0];
        for (int i = 0; i < route.Count; i++)
        {
            GameObject point = new GameObject($"RoadPatrolPoint_{i:000}");
            point.transform.SetParent(target, false);
            point.transform.position = route[i];
        }

        _policeFaction.AddRange(route);
        _repairFieldChanged |= inField;
        _report.AppendLine($"  作り直した {scene.name}: ポイント {oldCount}個 → {route.Count}個 / 一周 {RouteLength(route):F0}m");
        return true;
    }

    private static string UniqueSquadName(Transform container, string prefix, ref int number)
    {
        while (true)
        {
            string result = $"{prefix}{++number:00}";
            if (container.Find(result) == null) return result;
        }
    }

    /// <summary>
    /// 警察の巡回ポイントを、近くの歩ける場所(建物に埋まらない所)へずらすメソッド
    /// </summary>
    private static bool TrySnapPatrolPoint(Vector3 position, out Vector3 moved)
    {
        moved = position;
        for (float radius = 1.0f; radius <= _repairPointSearch; radius += 1.0f)
        {
            for (float angle = 0.0f; angle < 360.0f; angle += 30.0f)
            {
                Vector3 target = position + Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward * (radius - 1.0f);
                if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 1.0f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - position.y) > _repairHeightTolerance) continue;

                Vector3 inset = PullInsideNavMesh(hit.position);
                if (CSED_FieldPlacementCheck.FindProblem(inset, true) != null) continue;

                moved = inset;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 歩ける場所の縁に近い点を、縁から _repairEdgeInset 内側へ寄せるメソッド(壁際の点が、建物に埋まった扱いにならないように)
    /// </summary>
    private static Vector3 PullInsideNavMesh(Vector3 position)
    {
        if (!NavMesh.FindClosestEdge(position, out NavMeshHit edge, NavMesh.AllAreas) || edge.distance >= _repairEdgeInset) return position;

        Vector3 inward = FlatOf(position - edge.position);
        if (inward.sqrMagnitude < 0.01f) inward = FlatOf(edge.normal);
        if (inward.sqrMagnitude < 0.01f) return position;

        Vector3 moved = position + inward.normalized * (_repairEdgeInset - edge.distance + 0.1f);
        return NavMesh.SamplePosition(moved, out NavMeshHit hit, 0.8f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - position.y) < 1.0f ? hit.position : position;
    }

    /// <summary>
    /// 2点の間の、歩ける道のりの曲がり角(両端を除く)を、縁から内側へ寄せて返すメソッド(近すぎる曲がり角は、まとめる)
    /// </summary>
    private static List<Vector3> FindRouteCorners(Vector3 from, Vector3 to)
    {
        List<Vector3> result = new List<Vector3>();
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return result;

        Vector3 previous = from;
        for (int k = 1; k < path.corners.Length - 1; k++)
        {
            Vector3 corner = PullInsideNavMesh(path.corners[k]);
            if (FlatDistance(previous, corner) < _minWaypointSpacing) continue;

            result.Add(corner);
            previous = corner;
        }
        return result;
    }

    // =====================================================================
    // 警察: 被った位置にあるグループを、空いている所へ動かす
    // =====================================================================

    private const float _overlapSampleStep = 5.0f;       // 巡回ルートの線を調べる間隔(m)
    private const float _overlapNear = 6.0f;             // 別のグループのルートが、この距離以内なら、被っているとみなす(m)
    private const float _overlapRatio = 0.5f;            // ルートの何割が被っていたら、被ったグループとして、動かすか
    private const float _overlapDuplicate = 0.6f;       // 手で置いたグループ同士は、ルートの何割以上が、前のグループと被っていたら、ほぼ同じとして動かすか
    private const float _overlapMoveRadius = 140.0f;    // 被ったグループを動かす時に、まず探す、元の場所からの距離(m)
    private const int _overlapMaxPasses = 6;             // 被りを直す、繰り返しの回数の上限

    /// <summary>
    /// 被った位置にある警察のグループ(巡回ルートの半分以上が、別のグループのルートと _overlapNear 以内で重なる)を、空いている所へ動かすメソッド
    /// 被った2つのうち、手で置いたグループ(名前が PoliceSquad_A で始まらない物)は、そのままにして、配置ツールが置いたグループを動かす
    /// どちらも同じ種類なら、後ろの方を動かす。動かす時は、グループの設定は、そのままで、巡回ポイントだけを、他のグループから一番離れた所の道で作り直す
    /// 結果は Temp/FieldPoliceOverlapReport.txt
    /// </summary>
    [MenuItem("Tools/Police/被った位置にある警察のグループを空いている所へ動かす")]
    private static void FixOverlappingSquads()
    {
        _report = new StringBuilder();
        _policeFaction = new List<Vector3>();
        _squadPoints = new List<Vector3>();
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
            soloContents = PrefabUtility.LoadPrefabContents(path);
            fieldContents = PrefabUtility.LoadPrefabContents(_fieldPrefabPath);
            _repairSoloRoot = soloContents.transform;
            _repairFieldSolo = null;
            foreach (Transform child in fieldContents.transform)
            {
                if (child.name == _targetName) _repairFieldSolo = child;
            }
            _repairFieldChanged = false;

            // プレハブの中の、すべてのグループ(シーンの物と、位置で対応づける)
            List<(Transform target, bool inField, string name, bool generated, Vector3 offset, float rootY)> squads = new List<(Transform, bool, string, bool, Vector3, float)>();
            foreach (CS_PoliceSquad squad in _multi.GetComponentsInChildren<CS_PoliceSquad>(true))
            {
                Transform target = FindInContents(squad.transform, out bool inField, out Vector3 offset);
                if (target == null || target.GetComponent<CS_PoliceSquad>() == null)
                {
                    string near = "";
                    Transform police = _repairFieldSolo != null ? _repairFieldSolo.Find("Police") : null;
                    if (police != null)
                    {
                        foreach (Transform child in police)
                        {
                            if (child.name == squad.name) near += $" Field側に同名あり(位置のずれ {Vector3.Distance(child.position, squad.transform.position):F1}m 位置 {child.position})";
                        }
                    }
                    _report.AppendLine($"  プレハブに見つからないグループ: {squad.name} シーン上の位置 {squad.transform.position} 親 {squad.transform.parent.name}{near}");
                    continue;
                }

                squads.Add((target, inField, squad.name, squad.name.StartsWith(_generatedSquadPrefix), offset, Mathf.Abs(offset.y) > 0.05f ? squad.transform.position.y : float.NaN));
            }

            // シーンで、グループの高さが上書きされている物は、巡回ポイントが、歩ける場所の高さから浮いていることがあるので、歩ける場所の高さに合わせる
            int heightFixed = 0;
            foreach ((Transform target, bool inField, string name, bool generated, Vector3 offset, float rootY) squad in squads)
            {
                if (float.IsNaN(squad.rootY)) continue;

                foreach (Transform point in squad.target.GetComponent<CS_PoliceSquad>().GetRouteDrawPoints())
                {
                    Vector3 scenePosition = new Vector3(point.position.x + squad.offset.x, squad.rootY, point.position.z + squad.offset.z);
                    if (!NavMesh.SamplePosition(scenePosition, out NavMeshHit ground, 6.0f, NavMesh.AllAreas)) continue;

                    float localY = ground.position.y - squad.rootY;
                    if (Mathf.Abs(point.localPosition.y - localY) < 0.3f) continue;

                    point.localPosition = new Vector3(point.localPosition.x, localY, point.localPosition.z);
                    heightFixed++;
                    _repairFieldChanged |= squad.inField;
                }
            }
            _report.AppendLine($"巡回ポイントの高さを、歩ける場所の高さに合わせた {heightFixed}個");

            ReportOverlaps(squads, "動かす前");
            int moved = 0;
            for (int pass = 0; pass < _overlapMaxPasses; pass++)
            {
                // いちばん被っているグループから、動かす(動かす物は、手で置いていない物を優先する)
                int worstIndex = -1;
                float worstRatio = _overlapRatio;
                for (int i = 0; i < squads.Count; i++)
                {
                    float ratio = MoveScore(squads, i);
                    if (ratio < worstRatio) continue;

                    worstRatio = ratio;
                    worstIndex = i;
                }

                int movedThisPass = 0;
                while (worstIndex >= 0)
                {
                    if (TryMoveSquadToGap(squads[worstIndex].target, squads[worstIndex].inField, squads[worstIndex].name, squads[worstIndex].offset, squads[worstIndex].rootY)) movedThisPass++;
                    else _report.AppendLine($"  動かせなかった: {squads[worstIndex].name}");

                    // 同じ回の中で、次に被っているグループを探す(動かせなかった物は、飛ばす)
                    squads[worstIndex] = (squads[worstIndex].target, squads[worstIndex].inField, squads[worstIndex].name, false, squads[worstIndex].offset, squads[worstIndex].rootY);
                    worstIndex = -1;
                    worstRatio = _overlapRatio;
                    for (int i = 0; i < squads.Count; i++)
                    {
                        float ratio = MoveScore(squads, i);
                        if (ratio < worstRatio) continue;

                        worstRatio = ratio;
                        worstIndex = i;
                    }
                }
                moved += movedThisPass;
                if (movedThisPass == 0) break;

                // 動かしたグループを、もう一度、動かす対象に戻す
                for (int i = 0; i < squads.Count; i++) squads[i] = (squads[i].target, squads[i].inField, squads[i].name, squads[i].name.StartsWith(_generatedSquadPrefix), squads[i].offset, squads[i].rootY);
            }
            _report.AppendLine($"被った位置にあったグループを、空いている所へ動かした {moved}個");
            ReportOverlaps(squads, "動かした後");

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
            string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "FieldPoliceOverlapReport.txt");
            File.WriteAllText(reportPath, _report.ToString(), new UTF8Encoding(false));
        }
        Debug.Log("CSED_FieldMultiLayoutGenerator: 被った位置にある警察のグループを動かしました");
    }

    /// <summary>
    /// グループごとの、別のグループに被っている割合を、報告に書くメソッド(0.3以上の物だけ)
    /// </summary>
    private static void ReportOverlaps(List<(Transform target, bool inField, string name, bool generated, Vector3 offset, float rootY)> squads, string label)
    {
        int total = 0;
        int high = 0;
        List<string> lines = new List<string>();
        for (int i = 0; i < squads.Count; i++)
        {
            float ratio = OverlapRatio(squads, i);
            total++;
            if (ratio >= _overlapRatio) high++;
            if (ratio >= 0.3f) lines.Add($"    {squads[i].name}: 被り {ratio * 100.0f:F0}% / Field.prefab側 {squads[i].inField}");
        }
        _report.AppendLine($"[{label}] 調べたグループ {total}個 / 被りが {_overlapRatio * 100.0f:F0}% 以上 {high}個");
        foreach (string line in lines) _report.AppendLine(line);
    }

    /// <summary>
    /// 巡回ルートの線の上の点(_overlapSampleStep おき)を返すメソッド(最後から最初への線も含む)
    /// </summary>
    private static List<Vector3> SampleRoute(List<Transform> points)
    {
        List<Vector3> samples = new List<Vector3>();
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 from = points[i].position;
            Vector3 to = points[(i + 1) % points.Count].position;
            float length = Vector3.Distance(from, to);
            for (float d = 0.0f; d < length; d += _overlapSampleStep) samples.Add(Vector3.Lerp(from, to, length > 0.01f ? d / length : 0.0f));
        }
        return samples;
    }

    /// <summary>
    /// グループを動かす必要の高さ(0〜1。動かさない物は 0)を返すメソッド
    /// 配置ツールが置いたグループは、ルートのうち別のグループに被っている割合。手で置いたグループは、前にある別のグループと、ほぼ同じ(_overlapDuplicate 以上被る)時だけ、その割合
    /// </summary>
    private static float MoveScore(List<(Transform target, bool inField, string name, bool generated, Vector3 offset, float rootY)> squads, int index)
    {
        if (squads[index].generated) return OverlapRatio(squads, index);
        if (squads[index].name.StartsWith(_generatedSquadPrefix)) return 0.0f;     // 動かせなかったグループは、同じ回の中では、もう動かさない

        List<Vector3> mine = SampleRoute(squads[index].target.GetComponent<CS_PoliceSquad>().GetRouteDrawPoints());
        if (mine.Count == 0) return 0.0f;

        for (int j = 0; j < index; j++)
        {
            List<Vector3> earlier = SampleRoute(squads[j].target.GetComponent<CS_PoliceSquad>().GetRouteDrawPoints());
            int overlapped = 0;
            foreach (Vector3 sample in mine)
            {
                if (IsNearAny(earlier, sample, _overlapNear)) overlapped++;
            }
            if ((float)overlapped / mine.Count >= _overlapDuplicate) return (float)overlapped / mine.Count;
        }
        return 0.0f;
    }

    /// <summary>
    /// グループ i の巡回ルートのうち、別のグループのルートに _overlapNear 以内で重なる割合(0〜1)を返すメソッド
    /// </summary>
    private static float OverlapRatio(List<(Transform target, bool inField, string name, bool generated, Vector3 offset, float rootY)> squads, int index)
    {
        List<Vector3> mine = SampleRoute(squads[index].target.GetComponent<CS_PoliceSquad>().GetRouteDrawPoints());
        if (mine.Count == 0) return 0.0f;

        List<Vector3> others = new List<Vector3>();
        for (int j = 0; j < squads.Count; j++)
        {
            if (j != index) others.AddRange(SampleRoute(squads[j].target.GetComponent<CS_PoliceSquad>().GetRouteDrawPoints()));
        }

        int overlapped = 0;
        foreach (Vector3 sample in mine)
        {
            if (IsNearAny(others, sample, _overlapNear)) overlapped++;
        }
        return (float)overlapped / mine.Count;
    }

    /// <summary>
    /// 元の場所(center)から _overlapMoveRadius 以内で、他のグループの巡回ポイントから遠い順に、地上の歩ける点を並べるメソッド(開始地点のそばは避け、近い候補は省く)
    /// </summary>
    private static List<Vector3> GetNearbyGapCandidates(Vector3 center)
    {
        List<(float distance, Vector3 position)> scored = new List<(float, Vector3)>();
        foreach (Vector3 candidate in _ground)
        {
            if (FlatDistance(candidate, center) > _overlapMoveRadius || IsNearAny(_startPoints, candidate, _siteMinFromStart)) continue;

            scored.Add((MinFlatDistance(_squadPoints, candidate), candidate));
        }
        scored.Sort((a, b) => b.distance.CompareTo(a.distance));

        List<Vector3> result = new List<Vector3>();
        foreach ((float distance, Vector3 position) entry in scored)
        {
            if (!IsNearAny(result, entry.position, 30.0f)) result.Add(entry.position);
            if (result.Count >= _gapCandidateTries) break;
        }
        return result;
    }

    /// <summary>
    /// グループを、他のグループの巡回ルートから離れた所の、歩ける道で、巡回ポイントを作り直して動かすメソッド
    /// </summary>
    private static bool TryMoveSquadToGap(Transform target, bool inField, string name, Vector3 offset, float rootY, int forceArea = -1)
    {
        CS_PoliceSquad targetSquad = target.GetComponent<CS_PoliceSquad>();
        List<Transform> oldPoints = targetSquad.GetRouteDrawPoints();
        if (oldPoints.Count == 0) return false;

        // 自分以外のグループの巡回ポイントを、重なりを避ける記録にする
        _squadPoints = new List<Vector3>();
        foreach (CS_PoliceSquad other in _multi.GetComponentsInChildren<CS_PoliceSquad>(true))
        {
            Transform otherTarget = FindInContents(other.transform, out _);
            if (otherTarget == target) continue;

            foreach (Transform point in (otherTarget != null ? otherTarget.GetComponent<CS_PoliceSquad>() : other).GetRouteDrawPoints()) _squadPoints.Add(point.position);
        }

        // すでにこの回の中で動かした(プレハブの中の新しい位置にある)グループのポイントも、重なりを避ける記録に足す
        _squadPoints.AddRange(_movedSquadPoints);

        // 元の場所の近く(_overlapMoveRadius 以内)で、他のグループから離れた所を先に試し、無ければ、エリア全体で一番空いている所を試す
        Vector3 oldCenter = Vector3.zero;
        foreach (Transform point in oldPoints) oldCenter += point.position;
        oldCenter /= oldPoints.Count;
        int area = NearestArea(oldCenter);
        List<Vector3> candidates = forceArea >= 0 ? GetGapCandidates(forceArea) : GetNearbyGapCandidates(oldCenter);
        if (forceArea < 0) candidates.AddRange(GetGapCandidates(area));
        List<Vector3> route = new List<Vector3>();
        foreach (Vector3 center in candidates)
        {
            route = BuildWideRoute(center, _minExtraRouteLength * 0.5f);
            route = FollowStreets(route);
            route.RemoveAll(point => CSED_FieldPlacementCheck.FindProblem(point, true) != null);
            if (route.Count >= 4) break;

            route = new List<Vector3>();
        }
        if (route.Count < 4) return false;

        Transform spawn = null;
        foreach (Transform child in target)
        {
            if (!oldPoints.Contains(child)) spawn = child;
        }
        foreach (Transform point in oldPoints) Object.DestroyImmediate(point.gameObject);

        if (spawn != null) spawn.position = route[0] - offset;
        else target.position = route[0] - offset;
        for (int i = 0; i < route.Count; i++)
        {
            GameObject point = new GameObject($"RoadPatrolPoint_{i:000}");
            point.transform.SetParent(target, false);
            point.transform.position = route[i] - offset;

            // シーンで、グループの高さが、別の値に上書きされている(rootY)物は、グループの高さからの差で、ポイントの高さを決める
            if (!float.IsNaN(rootY)) point.transform.localPosition = new Vector3(point.transform.localPosition.x, route[i].y - rootY, point.transform.localPosition.z);
        }
        _repairFieldChanged |= inField;
        foreach (Vector3 point in route) _movedSquadPoints.Add(point);
        _report.AppendLine($"  動かした {name}: ポイント {oldPoints.Count}個 → {route.Count}個 / 一周 {RouteLength(route):F0}m / 新しい中心 ({route[0].x:F0}, {route[0].z:F0})");
        return true;
    }
}
