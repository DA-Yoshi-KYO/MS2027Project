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

/// <summary>
/// 配置ツールのうち、仕様書(Confluence「フィールド」)の、3エリアのリスク・リターンに合わせて、エリアごとの配置を整える部分
/// エリアの番号と区域: エリア0=インフラ区域(警察・悪人は中程度) / エリア1=生活区域(警備が緩く、悪人が少ない) / エリア2=エネルギー区域(警備が厳重で、悪人が多い)
/// 直す物は、Field_Solo1.prefab の中の物と、Field.prefab が Field_Solo1 に足している物の、どちらも対象にする(足す物は Field_Solo1.prefab に足す)
/// </summary>
public static partial class CSED_FieldMultiLayoutGenerator
{
    private const int _specLifeVillains = 5;                 // 生活区域(エリア1)の悪人の出現点の数
    private const int _specLifeArea = 1;                     // 生活区域のエリア番号
    private const int _specEnergyArea = 2;                   // エネルギー区域のエリア番号
    private const int _specSquadsFromLife = 3;               // 生活区域から、エネルギー区域へ移す警察のグループの数の上限
    private const int _specLifeSquadsTarget = 8;             // 生活区域の警察のグループの数(これより多い分だけ、移す。何度実行しても、増減しない)
    private const int _specEnergySquadsTarget = 20;          // エネルギー区域の警察のグループの数(これより少ない分だけ、足す)
    private const int _specEnergyDronesTarget = 16;          // エネルギー区域の巡回ドローンの数(これより少ない分だけ、足す。インフラ区域より多くする)
    private const int _specEmptyMinGround = 5;               // 何も無い升目に、グループを足す時の、升目の中の歩ける点の数の下限(マップの端を除く)
    private const float _specLightDuplicate = 0.8f;          // ライトの線が、この割合以上重なっていたら、同じ場所のライトとみなす
    private static readonly int[] _specLightTargets = { 5, 4, 10 };   // エリアごとのストリートライトの数の目安(重なっていたライトを動かす先を決める)

    /// <summary>
    /// 仕様書に合わせて、エリアごとの配置を整えるメソッド
    /// 1) 悪人: 生活区域(エリア1)が0個なので、エネルギー区域の位置にあるのにインフラ区域担当の物と、インフラ区域の地上の物を、生活区域へ移し、担当も CrimeArea_01 にする(出現点は増やさない)
    /// 2) 警察: 生活区域のグループを、エネルギー区域へ移し、エネルギー区域に、空いている所のグループを足す
    /// 3) ドローン: 悪人の近く(_policeCoverDistance以内)に警察陣営がいない所へ、ドローンか警察のグループを足す
    /// 4) ストリートライト: 動く線が重なっているライトを、足りないエリアの大通りへ動かす
    /// 5) 何も置かれていない場所(40mの升目で、近くに警察・悪人・防犯機器が無い所)へ、警察のグループを足す
    /// 防犯レーザーは、これとは別に「レーザーだけを配置する」で置き直す(悪人の位置が変わるため、シーンを読み込み直してから)
    /// 結果は Temp/FieldSpecBalanceReport.txt
    /// </summary>
    [MenuItem("Tools/Police/仕様書に合わせてエリアのバランスを直す")]
    private static void BalanceBySpec()
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
            _movedSquadPoints = new List<Vector3>();

            List<Vector3> finalVillains = BalanceVillains(roofNodes);
            BalancePolice(refs, soloContents.transform);
            BalanceDrones(refs, soloContents.transform, finalVillains);
            BalanceEnergyDrones(refs, soloContents.transform);
            BalanceLights();
            FillEmptyPlaces(refs, soloContents.transform, finalVillains);

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
            string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "FieldSpecBalanceReport.txt");
            File.WriteAllText(reportPath, _report.ToString(), new UTF8Encoding(false));
        }
        Debug.Log("CSED_FieldMultiLayoutGenerator: 仕様書に合わせて、エリアのバランスを直しました");
    }

    /// <summary>
    /// マルチのフィールドの、防犯レーザーだけを、配置し直すメソッド(ライト・警察・SAObject・ドローンは、そのまま。手で置いたレーザーは消す)
    /// 悪人の位置を動かした後は、シーンを読み込み直してから使う(悪人の近くにレーザーを置くため)
    /// </summary>
    [MenuItem("Tools/Police/レーザーだけを配置する")]
    private static void PlaceLasersOnly()
    {
        PlaceDevices(true, false);
    }

    // =====================================================================
    // 悪人
    // =====================================================================

    /// <summary>
    /// 生活区域(エリア1)の悪人の出現点を、_specLifeVillains 個にそろえるメソッド(新しい出現点は作らず、今ある物を移す)
    /// 移す物: エネルギー区域の位置にあるのに、インフラ区域(Lv2)担当の物 → 足りなければ、インフラ区域の位置の、地上の物(後ろの物から)
    /// </summary>
    /// <returns>動かした後の、悪人の出現点の位置(出現点の高さそのまま)</returns>
    private static List<Vector3> BalanceVillains(List<Vector3> roofNodes)
    {
        List<VillainEntry> entries = CollectVillainEntries();
        List<VillainEntry> chosen = new List<VillainEntry>();
        int lifeCount = 0;
        foreach (VillainEntry entry in entries)
        {
            CS_CrimeArea crimeArea = entry.scene.crimeArea;
            int area = NearestArea(entry.position);
            if (area == _specLifeArea) lifeCount++;
            else if (area == _specEnergyArea && crimeArea != null && crimeArea.level == 2 && entry.target != null && !entry.inField) chosen.Add(entry);
        }
        int need = _specLifeVillains - lifeCount - chosen.Count;
        for (int i = entries.Count - 1; i >= 0 && need > 0; i--)
        {
            VillainEntry entry = entries[i];
            if (chosen.Contains(entry) || entry.isRoof || entry.target == null || entry.inField || NearestArea(entry.position) != 0) continue;

            chosen.Add(entry);
            need--;
        }

        // 動かさない物の位置・ルートを、先に確定する
        List<Vector3> settledSpawns = new List<Vector3>();
        List<Vector3> settledSamples = new List<Vector3>();
        foreach (VillainEntry entry in entries)
        {
            if (!chosen.Contains(entry)) SettleVillain(entry, entry.position, entry.yaw, settledSpawns, settledSamples);
        }

        int moved = 0;
        foreach (VillainEntry entry in chosen)
        {
            if (!TryRelocateVillain(entry, roofNodes, settledSpawns, settledSamples, out Vector3 newPosition, out float newYaw, _specLifeArea))
            {
                _report.AppendLine($"  悪人を生活区域へ移せなかった: {entry.name} ({entry.position.x:F0}, {entry.position.y:F0}, {entry.position.z:F0})");
                SettleVillain(entry, entry.position, entry.yaw, settledSpawns, settledSamples);
                continue;
            }

            if (Mathf.Abs(Mathf.DeltaAngle(entry.yaw, newYaw)) > 0.5f) entry.target.rotation = Quaternion.Euler(0.0f, Mathf.DeltaAngle(entry.yaw, newYaw), 0.0f) * entry.target.rotation;
            bool reparented = TryReparentVillain(entry, "CrimeArea_01", newPosition);
            _repairFieldChanged |= entry.inField;
            SettleVillain(entry, newPosition, newYaw, settledSpawns, settledSamples);
            entry.position = newPosition;
            moved++;
            _report.AppendLine($"  悪人を生活区域へ移した: {entry.name} → ({newPosition.x:F0}, {newPosition.y:F1}, {newPosition.z:F0}) 向き {newYaw:F0}° / 担当を CrimeArea_01 に変えた: {reparented}");
        }
        _report.AppendLine($"悪人の出現点: 生活区域(エリア1)へ移した {moved}個(元から生活区域にあった物 {lifeCount}個)");

        List<Vector3> result = new List<Vector3>();
        foreach (VillainEntry entry in entries) result.Add(entry.position - Vector3.up * entry.heightOffset);
        return result;
    }

    /// <summary>
    /// 悪人の出現点を、別の犯罪完遂エリア(CrimeArea_xx)の子にして、シーン上で position の位置に来るように置くメソッド
    /// 犯罪完遂エリアは、シーンで位置をずらされていることがある(CrimeArea_01 は、約 x+160、z-178)ので、新しい親の、シーンとプレハブの中の位置のずれを引いて置く
    /// </summary>
    private static bool TryReparentVillain(VillainEntry entry, string crimeAreaName, Vector3 position)
    {
        Transform area = _repairSoloRoot != null ? _repairSoloRoot.Find("Villain/VillainSpawnPoints/" + crimeAreaName) : null;
        Transform sceneArea = _multi.Find("Villain/VillainSpawnPoints/" + crimeAreaName);
        if (entry.inField || area == null || sceneArea == null || (sceneArea.lossyScale - area.lossyScale).sqrMagnitude > 0.0001f || Quaternion.Angle(sceneArea.rotation, area.rotation) > 0.5f)
        {
            entry.target.position = position - entry.offset;
            return false;
        }

        entry.target.SetParent(area, true);
        entry.target.position = position - (sceneArea.position - area.position);
        return entry.target.parent == area;
    }

    // =====================================================================
    // 警察
    // =====================================================================

    /// <summary>
    /// 警察のグループを、エリアごとの割り当てに合わせるメソッド
    /// 生活区域のグループを _specSquadsFromLife 個、エネルギー区域へ移し(このツールが置いた物を先に)、エネルギー区域に、空いている所のグループを _specSquadsToEnergy 個足す
    /// </summary>
    private static void BalancePolice(References refs, Transform soloRoot)
    {
        Transform container = FindOrCreateGroup(soloRoot, _policeGroup);
        List<(Transform target, bool inField, string name, bool generated, Vector3 offset, float rootY, int area)> squads = new List<(Transform, bool, string, bool, Vector3, float, int)>();
        HashSet<Transform> matched = new HashSet<Transform>();
        foreach (CS_PoliceSquad squad in _multi.GetComponentsInChildren<CS_PoliceSquad>(true))
        {
            List<Transform> points = squad.GetRouteDrawPoints();
            if (points.Count == 0) continue;

            Vector3 center = Vector3.zero;
            foreach (Transform point in points) center += point.position;
            Transform target = FindInContents(squad.transform, out bool inField, out Vector3 offset, matched);
            if (target == null || target.GetComponent<CS_PoliceSquad>() == null) continue;

            matched.Add(target);
            if (!inField) ExcludeFieldCounterpart(target, matched);
            squads.Add((target, inField, squad.name, squad.name.StartsWith(_generatedSquadPrefix), offset, Mathf.Abs(offset.y) > 0.05f ? squad.transform.position.y : float.NaN, NearestArea(center / points.Count)));
        }

        // 生活区域のグループを、エネルギー区域へ移す(このツールが置いた物を先に)
        int moved = 0;
        int lifeSquads = squads.FindAll(squad => squad.area == _specLifeArea).Count;
        int toMove = Mathf.Clamp(lifeSquads - _specLifeSquadsTarget, 0, _specSquadsFromLife);
        foreach (bool generated in new[] { true, false })
        {
            for (int i = 0; i < squads.Count && moved < toMove; i++)
            {
                if (squads[i].area != _specLifeArea || squads[i].generated != generated) continue;

                if (TryMoveSquadToGap(squads[i].target, squads[i].inField, squads[i].name, squads[i].offset, squads[i].rootY, _specEnergyArea))
                {
                    moved++;
                    squads[i] = (squads[i].target, squads[i].inField, squads[i].name, squads[i].generated, squads[i].offset, squads[i].rootY, _specEnergyArea);
                }
                else _report.AppendLine($"  警察のグループをエネルギー区域へ移せなかった: {squads[i].name}");
            }
        }
        _report.AppendLine($"警察のグループ: 生活区域からエネルギー区域へ移した {moved}個");

        // 今ある警察陣営の位置を、記録し直す(足す物を、重ねないため)。移した物は、プレハブの中の新しい位置を使う
        _policeFaction = new List<Vector3>();
        _squadPoints = new List<Vector3>();
        foreach (var squad in squads)
        {
            foreach (Transform point in squad.target.GetComponent<CS_PoliceSquad>().GetRouteDrawPoints())
            {
                _squadPoints.Add(point.position + squad.offset);
                _policeFaction.Add(point.position + squad.offset);
            }
        }
        foreach (CS_SecurityDroneSpawner drone in _multi.GetComponentsInChildren<CS_SecurityDroneSpawner>(true))
        {
            foreach (Transform point in drone.transform) _policeFaction.Add(point.position);
        }
        foreach (CS_SecurityLaser laser in _multi.GetComponentsInChildren<CS_SecurityLaser>(true)) _policeFaction.Add(laser.transform.position);
        foreach (CS_SecurityLight light in _multi.GetComponentsInChildren<CS_SecurityLight>(true)) _policeFaction.Add(light.transform.position);

        // エネルギー区域に、空いている所のグループを足す
        int number = 0;
        int added = 0;
        int toAdd = Mathf.Max(0, _specEnergySquadsTarget - squads.FindAll(squad => squad.area == _specEnergyArea).Count);
        foreach (Vector3 center in GetGapCandidates(_specEnergyArea))
        {
            if (added >= toAdd) break;
            if (TryPlaceSquad(container, refs, center, UniqueSquadName(container, $"{_generatedSquadPrefix}{_specEnergyArea}_S", ref number), _minExtraRouteLength)) added++;
        }
        _report.AppendLine($"警察のグループ: エネルギー区域の空いている所へ足した {added}個");
    }

    // =====================================================================
    // ドローン
    // =====================================================================

    /// <summary>
    /// 悪人の近く(_policeCoverDistance以内)に警察陣営がいない所へ、ドローンか警察のグループを足すメソッド(悪人は、動かした後の位置で見る)
    /// </summary>
    private static void BalanceDrones(References refs, Transform soloRoot, List<Vector3> villains)
    {
        List<Site> sites = new List<Site>();
        foreach (Vector3 position in villains)
        {
            sites.Add(new Site { area = NearestArea(position), index = 50 + sites.Count, position = position, isRoof = position.y >= _roofVillainMinHeight });
        }
        EnsurePoliceCoverage(soloRoot.gameObject, refs, sites);
    }

    /// <summary>
    /// エネルギー区域(警備が厳重)の巡回ドローンを、_specEnergyDronesTarget 機まで足すメソッド(インフラ区域より多くする)
    /// 屋上の升目(水平 _balanceCell・高さ 20m ごと)のうち、今あるドローンから遠い所から順に、ドローンを置く
    /// </summary>
    private static void BalanceEnergyDrones(References refs, Transform soloRoot)
    {
        List<Vector3> drones = new List<Vector3>();
        int energyCount = 0;
        foreach (CS_SecurityDroneSpawner drone in _multi.GetComponentsInChildren<CS_SecurityDroneSpawner>(true))
        {
            drones.Add(drone.transform.position);
            if (NearestArea(drone.transform.position) == _specEnergyArea) energyCount++;
        }

        Dictionary<Vector3Int, List<Vector3>> roofCells = new Dictionary<Vector3Int, List<Vector3>>();
        foreach (Vector3 roof in _roofNavAll)
        {
            if (NearestArea(roof) != _specEnergyArea) continue;

            Vector3Int cell = new Vector3Int(Mathf.FloorToInt(roof.x / _balanceCell), Mathf.FloorToInt(roof.y / 20.0f), Mathf.FloorToInt(roof.z / _balanceCell));
            if (!roofCells.TryGetValue(cell, out List<Vector3> list)) roofCells[cell] = list = new List<Vector3>();
            list.Add(roof);
        }

        Transform container = FindOrCreateGroup(soloRoot, _droneGroup);
        int added = 0;
        int number = 0;
        while (energyCount + added < _specEnergyDronesTarget)
        {
            // 今あるドローンから、一番遠い屋上の升目を選ぶ
            Vector3Int bestCell = default;
            float bestDistance = -1.0f;
            foreach (KeyValuePair<Vector3Int, List<Vector3>> pair in roofCells)
            {
                if (pair.Value.Count < _specEmptyMinGround) continue;

                Vector3 center = Vector3.zero;
                foreach (Vector3 roof in pair.Value) center += roof;
                center /= pair.Value.Count;
                float nearest = float.MaxValue;
                foreach (Vector3 drone in drones)
                {
                    if (Mathf.Abs(drone.y - center.y) < 25.0f) nearest = Mathf.Min(nearest, FlatDistance(drone, center));
                }
                if (nearest <= bestDistance) continue;

                bestDistance = nearest;
                bestCell = pair.Key;
            }
            if (bestDistance < 0.0f) break;

            List<Vector3> points = roofCells[bestCell];
            roofCells.Remove(bestCell);
            Vector3 cellCenter = Vector3.zero;
            foreach (Vector3 roof in points) cellCenter += roof;
            cellCenter /= points.Count;
            if (!TryCreateDrone(container, refs, points, $"SecurityDroneSpawner_A{_specEnergyArea}_D{++number:00}")) continue;

            drones.Add(cellCenter);
            added++;
        }
        _report.AppendLine($"巡回ドローン: エネルギー区域に足した {added}機(今まで {energyCount}機 → 目安 {_specEnergyDronesTarget}機)");
    }

    // =====================================================================
    // ストリートライト
    // =====================================================================

    /// <summary>
    /// 動く線が、別のライトとほとんど重なっている(_specLightDuplicate 以上)ライトを、足りないエリアの大通りへ動かすメソッド
    /// </summary>
    private static void BalanceLights()
    {
        List<CS_SecurityLight> lights = new List<CS_SecurityLight>(_multi.GetComponentsInChildren<CS_SecurityLight>(true));
        List<List<Vector3>> samples = new List<List<Vector3>>();
        List<LightLine> lines = new List<LightLine>();
        foreach (CS_SecurityLight light in lights)
        {
            SerializedObject serialized = new SerializedObject(light);
            Vector3 direction = light.transform.rotation * serialized.FindProperty("_moveDirection").vector3Value.normalized;
            float distance = serialized.FindProperty("_moveDistance").floatValue;
            List<Vector3> points = new List<Vector3>();
            for (float d = 0.0f; d <= distance; d += 5.0f) points.Add(light.transform.position + direction * d);
            samples.Add(points);
            lines.Add(new LightLine { area = NearestArea(light.transform.position), origin = light.transform.position - Vector3.up * _lightHeightOffset, yaw = light.transform.eulerAngles.y, distance = distance });
        }

        List<int> duplicates = new List<int>();
        for (int j = 0; j < lights.Count; j++)
        {
            for (int i = 0; i < j; i++)
            {
                if (duplicates.Contains(i)) continue;

                int near = 0;
                foreach (Vector3 sample in samples[j])
                {
                    if (IsNearSameLevel(samples[i], sample, 6.0f)) near++;
                }
                if ((float)near / samples[j].Count < _specLightDuplicate) continue;

                duplicates.Add(j);
                break;
            }
        }

        int[] perArea = new int[_areaCenters.Count];
        List<LightLine> kept = new List<LightLine>();
        for (int i = 0; i < lights.Count; i++)
        {
            if (duplicates.Contains(i)) continue;

            perArea[lines[i].area]++;
            kept.Add(lines[i]);
        }

        List<LightLine> candidates = FindLightLines();
        HashSet<Transform> matched = new HashSet<Transform>();
        int moved = 0;
        foreach (int index in duplicates)
        {
            Transform target = FindInContents(lights[index].transform, out bool inField, out Vector3 offset, matched);
            if (target == null)
            {
                _report.AppendLine($"  重なっているライトが、プレハブに見つからない: {lights[index].name}");
                continue;
            }
            matched.Add(target);

            // 目安より少ないエリアから順に、点数の高い大通りを選ぶ
            LightLine chosen = null;
            List<int> areas = new List<int>();
            for (int a = 0; a < _areaCenters.Count; a++) areas.Add(a);
            areas.Sort((x, y) => (_specLightTargets[y % _specLightTargets.Length] - perArea[y]).CompareTo(_specLightTargets[x % _specLightTargets.Length] - perArea[x]));
            foreach (int area in areas)
            {
                chosen = candidates.Find(line => line.area == area && IsLightLineInside(line) && !IsLightLineNear(kept, line, _lightMinSpacing * 0.6f) && LightRoadCoverage(line) >= _lightCoverageMin);
                if (chosen != null) break;
            }
            if (chosen == null)
            {
                _report.AppendLine($"  重なっているライトの動かす先が無い: {lights[index].name}");
                continue;
            }

            Vector3 world = chosen.origin + Vector3.up * _lightHeightOffset;
            target.position = world - offset;
            target.rotation = Quaternion.Euler(0.0f, chosen.yaw, 0.0f);
            SerializedObject targetSerialized = new SerializedObject(target.GetComponent<CS_SecurityLight>());
            targetSerialized.FindProperty("_moveDirection").vector3Value = Vector3.forward;
            targetSerialized.FindProperty("_moveDistance").floatValue = Mathf.Clamp(chosen.distance, 0.0f, 100.0f);
            targetSerialized.FindProperty("_diameter").floatValue = chosen.diameter;
            targetSerialized.ApplyModifiedPropertiesWithoutUndo();
            _repairFieldChanged |= inField;

            kept.Add(chosen);
            perArea[chosen.area]++;
            moved++;
            _policeFaction.Add(chosen.origin);
            _report.AppendLine($"  重なっていたライトを動かした: {lights[index].name} → エリア{chosen.area} ({chosen.origin.x:F0}, {chosen.origin.z:F0}) 向き {chosen.yaw:F0}° 往復 {chosen.distance:F0}m");
        }
        _report.AppendLine($"ストリートライト: 重なっていた {duplicates.Count}本のうち、別の大通りへ動かした {moved}本(エリアごとの数: {string.Join(", ", perArea)})");
    }

    /// <summary>
    /// ライトの動く線の、始点と終点が、街の範囲の内側(端から _playAreaMargin 以上)かを判定するメソッド
    /// </summary>
    private static bool IsLightLineInside(LightLine line)
    {
        Vector3 end = line.origin + Quaternion.Euler(0.0f, line.yaw, 0.0f) * Vector3.forward * line.distance;
        return IsInsidePlayArea(line.origin) && IsInsidePlayArea(end);
    }

    // =====================================================================
    // 何も置かれていない場所
    // =====================================================================

    /// <summary>
    /// 何も置かれていない場所(歩ける地上を _balanceCell の升目にまとめ、_balanceNear 以内に警察・悪人・防犯機器が無い升目)に、警察のグループを足すメソッド
    /// マップの端(升目の中の歩ける点が _specEmptyMinGround 未満)は、除く
    /// </summary>
    private static void FillEmptyPlaces(References refs, Transform soloRoot, List<Vector3> villains)
    {
        Transform container = FindOrCreateGroup(soloRoot, _policeGroup);
        Dictionary<Vector2Int, (Vector3 sum, int count)> cells = new Dictionary<Vector2Int, (Vector3, int)>();
        foreach (Vector3 ground in _ground)
        {
            Vector2Int cell = new Vector2Int(Mathf.FloorToInt(ground.x / _balanceCell), Mathf.FloorToInt(ground.z / _balanceCell));
            cells.TryGetValue(cell, out (Vector3 sum, int count) value);
            cells[cell] = (value.sum + ground, value.count + 1);
        }

        int number = 0;
        int added = 0;
        int empty = 0;
        foreach (KeyValuePair<Vector2Int, (Vector3 sum, int count)> pair in cells)
        {
            Vector3 center = pair.Value.sum / pair.Value.count;
            if (pair.Value.count < _specEmptyMinGround || IsNearAny(_policeFaction, center, _balanceNear)) continue;

            empty++;
            if (!NavMeshSample(center, out Vector3 ground)) continue;

            int area = NearestArea(ground);
            if (TryPlaceSquad(container, refs, ground, UniqueSquadName(container, $"{_generatedSquadPrefix}{area}_E", ref number))) added++;
            else _report.AppendLine($"  何も置かれていない場所に、警察のグループを足せなかった: ({ground.x:F0}, {ground.z:F0})");
        }
        _report.AppendLine($"何も置かれていない場所(地上): {empty}か所 → 警察のグループを足した {added}個");

        // 屋上: 高さ _roofVillainMinHeight 以上の、歩ける屋上の升目(水平 _balanceCell・高さ 20m ごと)で、近くに何も無い所へ、ドローンを足す
        Transform drones = FindOrCreateGroup(soloRoot, _droneGroup);
        List<Vector3> roofThings = new List<Vector3>(_policeFaction);
        roofThings.AddRange(villains);
        roofThings.AddRange(_saBases);
        Dictionary<Vector3Int, List<Vector3>> roofCells = new Dictionary<Vector3Int, List<Vector3>>();
        foreach (Vector3 roof in _roofNavAll)
        {
            Vector3Int cell = new Vector3Int(Mathf.FloorToInt(roof.x / _balanceCell), Mathf.FloorToInt(roof.y / 20.0f), Mathf.FloorToInt(roof.z / _balanceCell));
            if (!roofCells.TryGetValue(cell, out List<Vector3> list)) roofCells[cell] = list = new List<Vector3>();
            list.Add(roof);
        }
        int roofEmpty = 0;
        int roofAdded = 0;
        foreach (KeyValuePair<Vector3Int, List<Vector3>> pair in roofCells)
        {
            if (pair.Value.Count < _specEmptyMinGround) continue;

            Vector3 center = Vector3.zero;
            foreach (Vector3 roof in pair.Value) center += roof;
            center /= pair.Value.Count;
            bool hasThing = false;
            foreach (Vector3 thing in roofThings)
            {
                if (FlatDistance(thing, center) < _balanceNear && Mathf.Abs(thing.y - center.y) < 15.0f) { hasThing = true; break; }
            }
            if (hasThing) continue;

            roofEmpty++;
            if (TryCreateDrone(drones, refs, pair.Value, $"SecurityDroneSpawner_A{NearestArea(center)}_E{++number:00}")) roofAdded++;
        }
        _report.AppendLine($"何も置かれていない場所(屋上): {roofEmpty}か所 → ドローンを足した {roofAdded}機");
    }

    private static bool _hasPlayArea;
    private static Vector3 _playAreaMin;
    private static Vector3 _playAreaMax;
    private const float _playAreaMargin = 35.0f;             // 街の端(NavMeshの端)から、この距離は、何も置かない(m)

    /// <summary>
    /// 街の水平の広さ(NavMeshの頂点の範囲)を調べるメソッド
    /// 遊べる範囲(CS_PlayArea)は、古い街の大きさ(±130m)のままで、今の街(NavMeshは±374m)に合わないため、使わない
    /// </summary>
    private static void CollectPlayArea()
    {
        _hasPlayArea = false;
        UnityEngine.AI.NavMeshTriangulation triangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();
        if (triangulation.vertices.Length == 0) return;

        _playAreaMin = triangulation.vertices[0];
        _playAreaMax = triangulation.vertices[0];
        foreach (Vector3 vertex in triangulation.vertices)
        {
            _playAreaMin = Vector3.Min(_playAreaMin, vertex);
            _playAreaMax = Vector3.Max(_playAreaMax, vertex);
        }
        _hasPlayArea = true;
        _report.AppendLine($"街の広さ(NavMesh): X {_playAreaMin.x:F0}〜{_playAreaMax.x:F0} / Z {_playAreaMin.z:F0}〜{_playAreaMax.z:F0}(端から {_playAreaMargin:F0}m は、何も置かない)");
    }

    private static bool IsInsidePlayArea(Vector3 position)
    {
        if (!_hasPlayArea) return true;

        return position.x >= _playAreaMin.x + _playAreaMargin && position.x <= _playAreaMax.x - _playAreaMargin
            && position.z >= _playAreaMin.z + _playAreaMargin && position.z <= _playAreaMax.z - _playAreaMargin;
    }

    private static bool NavMeshSample(Vector3 position, out Vector3 result)
    {
        result = position;
        if (!UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit, 6.0f, UnityEngine.AI.NavMesh.AllAreas) || hit.position.y >= _groundHeight) return false;

        result = hit.position;
        return true;
    }
}
