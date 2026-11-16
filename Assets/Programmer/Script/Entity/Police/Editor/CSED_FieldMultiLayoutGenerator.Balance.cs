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
/// 配置ツールのうち、エリアごとの配置の数と、何も置かれていない場所を調べる部分
/// 仕様書(Confluence「フィールド」)の、3エリアのリスク・リターンと、見比べるために使う
/// </summary>
public static partial class CSED_FieldMultiLayoutGenerator
{
    private const float _balanceCell = 40.0f;            // 何も無い場所を調べる、升目の大きさ(m)
    private const float _balanceNear = 45.0f;            // 升目の中心から、この距離以内に、何かあれば、何かある場所とみなす(m)

    /// <summary>
    /// エリアごとの、警察・悪人・防犯機器・SAObjectの数と、何も置かれていない場所を調べて、報告に書くメソッド
    /// 結果は Temp/FieldBalanceReport.txt
    /// </summary>
    [MenuItem("Tools/Police/エリアごとの配置の数と、何も無い場所を調べる")]
    private static void ReportBalance()
    {
        _report = new StringBuilder();
        _multi = FindMulti();
        string path = _multi != null ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(_multi.gameObject)) : "";
        if (_multi == null || !path.EndsWith(_targetFile))
        {
            Debug.LogError("CSED_FieldMultiLayoutGenerator: シーンに Field/Field_Solo1 が見つかりません(MainSceneを開いてください)");
            return;
        }

        try
        {
            CollectGeometry();
            int areaCount = _areaCenters.Count;
            for (int i = 0; i < areaCount; i++) _report.AppendLine($"エリア{i}: 中心 ({_areaCenters[i].x:F0}, {_areaCenters[i].z:F0})");

            // 置かれている物(種類ごとに、位置の一覧)
            Dictionary<string, List<Vector3>> things = new Dictionary<string, List<Vector3>>();
            things["警察のグループ"] = new List<Vector3>();
            things["悪人の出現点"] = new List<Vector3>();
            things["防犯レーザー"] = new List<Vector3>();
            things["ストリートライト"] = new List<Vector3>();
            things["巡回ドローン"] = new List<Vector3>();
            things["SAObject"] = new List<Vector3>();
            things["アイテムの出現点"] = new List<Vector3>();
            List<Vector3> policePoints = new List<Vector3>();
            foreach (CS_PoliceSquad squad in _multi.GetComponentsInChildren<CS_PoliceSquad>(true))
            {
                List<Transform> points = squad.GetRouteDrawPoints();
                if (points.Count == 0) continue;

                Vector3 center = Vector3.zero;
                foreach (Transform point in points)
                {
                    center += point.position;
                    policePoints.Add(point.position);
                }
                things["警察のグループ"].Add(center / points.Count);
            }
            foreach (CS_VillainSpawnPoint spawn in _multi.GetComponentsInChildren<CS_VillainSpawnPoint>(true)) things["悪人の出現点"].Add(spawn.transform.position);
            foreach (CS_SecurityLaser laser in _multi.GetComponentsInChildren<CS_SecurityLaser>(true)) things["防犯レーザー"].Add(laser.transform.position);
            foreach (CS_SecurityLight light in _multi.GetComponentsInChildren<CS_SecurityLight>(true)) things["ストリートライト"].Add(light.transform.position);
            foreach (CS_SecurityDroneSpawner drone in _multi.GetComponentsInChildren<CS_SecurityDroneSpawner>(true)) things["巡回ドローン"].Add(drone.transform.position);
            foreach (Transform root in _multi.GetComponentsInChildren<Transform>(true))
            {
                if (root.name.StartsWith("SAObject") && root.parent != null && root.parent.name == "SAObjects") things["SAObject"].Add(root.position);
            }

            // 悪人の出現点を、担当のエリア(犯罪完遂エリア。Lv1 生活 / Lv2 インフラ / Lv3 エネルギー)ごとに数える
            Dictionary<string, int> crimeAreas = new Dictionary<string, int>();
            foreach (CS_VillainSpawnPoint spawn in _multi.GetComponentsInChildren<CS_VillainSpawnPoint>(true))
            {
                CS_CrimeArea crimeArea = spawn.crimeArea;
                string key = (crimeArea != null ? $"Lv{crimeArea.level} {crimeArea.areaName}" : "エリア無し") + $" / 位置はエリア{NearestArea(spawn.transform.position)}";
                crimeAreas[key] = crimeAreas.TryGetValue(key, out int count) ? count + 1 : 1;
            }
            _report.AppendLine();
            _report.AppendLine("【悪人の出現点(担当エリアごと)】");
            foreach (KeyValuePair<string, int> pair in crimeAreas) _report.AppendLine($"  {pair.Key}: {pair.Value}個");

            _report.AppendLine();
            _report.AppendLine("【エリアごとの数(位置が一番近いエリア)】");
            foreach (KeyValuePair<string, List<Vector3>> pair in things)
            {
                int[] perArea = new int[areaCount];
                foreach (Vector3 position in pair.Value) perArea[NearestArea(position)]++;
                _report.AppendLine($"  {pair.Key}: 全部 {pair.Value.Count} / エリアごと {string.Join(", ", perArea)}");
            }

            // 歩ける地上の広さ(エリアごと)
            int[] groundPerArea = new int[areaCount];
            foreach (Vector3 ground in _ground) groundPerArea[NearestArea(ground)]++;
            _report.AppendLine($"  歩ける地上の点(広さの目安): エリアごと {string.Join(", ", groundPerArea)}");

            // 何も置かれていない場所(地上の歩ける点を、升目にまとめ、近くに何も無い升目を数える)
            List<Vector3> everything = new List<Vector3>(policePoints);
            foreach (string key in new[] { "悪人の出現点", "防犯レーザー", "ストリートライト", "巡回ドローン" }) everything.AddRange(things[key]);
            Dictionary<Vector2Int, (Vector3 sum, int count)> cells = new Dictionary<Vector2Int, (Vector3, int)>();
            foreach (Vector3 ground in _ground)
            {
                Vector2Int cell = new Vector2Int(Mathf.FloorToInt(ground.x / _balanceCell), Mathf.FloorToInt(ground.z / _balanceCell));
                cells.TryGetValue(cell, out (Vector3 sum, int count) value);
                cells[cell] = (value.sum + ground, value.count + 1);
            }
            int[] emptyPerArea = new int[areaCount];
            int[] cellPerArea = new int[areaCount];
            List<(int count, Vector3 center)> empties = new List<(int, Vector3)>();
            foreach (KeyValuePair<Vector2Int, (Vector3 sum, int count)> pair in cells)
            {
                Vector3 center = pair.Value.sum / pair.Value.count;
                int area = NearestArea(center);
                cellPerArea[area]++;
                if (IsNearAny(everything, center, _balanceNear)) continue;

                emptyPerArea[area]++;
                empties.Add((pair.Value.count, center));
            }
            _report.AppendLine();
            _report.AppendLine($"【何も置かれていない場所(升目 {_balanceCell:F0}m のうち、{_balanceNear:F0}m 以内に警察・悪人・防犯機器が無い物)】");
            _report.AppendLine($"  歩ける升目 エリアごと {string.Join(", ", cellPerArea)} / 何も無い升目 エリアごと {string.Join(", ", emptyPerArea)}");
            empties.Sort((a, b) => b.count.CompareTo(a.count));
            for (int i = 0; i < empties.Count && i < 40; i++) _report.AppendLine($"  エリア{NearestArea(empties[i].center)} ({empties[i].center.x:F0}, {empties[i].center.z:F0}) 歩ける点 {empties[i].count}個");

            // 屋上(高さ _roofVillainMinHeight 以上の歩ける場所)で、何も置かれていない場所(升目 _balanceCell・高さ20mごと)
            Dictionary<Vector3Int, (Vector3 sum, int count)> roofCells = new Dictionary<Vector3Int, (Vector3, int)>();
            foreach (Vector3 roof in _roofNavAll)
            {
                Vector3Int cell = new Vector3Int(Mathf.FloorToInt(roof.x / _balanceCell), Mathf.FloorToInt(roof.y / 20.0f), Mathf.FloorToInt(roof.z / _balanceCell));
                roofCells.TryGetValue(cell, out (Vector3 sum, int count) value);
                roofCells[cell] = (value.sum + roof, value.count + 1);
            }
            List<Vector3> roofThings = new List<Vector3>(everything);
            roofThings.AddRange(things["SAObject"]);
            int[] roofCellPerArea = new int[areaCount];
            int[] roofEmptyPerArea = new int[areaCount];
            foreach (KeyValuePair<Vector3Int, (Vector3 sum, int count)> pair in roofCells)
            {
                if (pair.Value.count < 5) continue;

                Vector3 center = pair.Value.sum / pair.Value.count;
                int area = NearestArea(center);
                roofCellPerArea[area]++;
                bool hasThing = false;
                foreach (Vector3 thing in roofThings)
                {
                    if (FlatDistance(thing, center) < _balanceNear && Mathf.Abs(thing.y - center.y) < 15.0f) { hasThing = true; break; }
                }
                if (!hasThing) roofEmptyPerArea[area]++;
            }
            _report.AppendLine($"  屋上の升目(歩ける点5個以上) エリアごと {string.Join(", ", roofCellPerArea)} / 何も無い屋上の升目 エリアごと {string.Join(", ", roofEmptyPerArea)}");

            AppendDeviceOverlaps();
        }
        finally
        {
            string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "FieldBalanceReport.txt");
            File.WriteAllText(reportPath, _report.ToString(), new UTF8Encoding(false));
        }
        Debug.Log("CSED_FieldMultiLayoutGenerator: エリアごとの配置の数を調べました");
    }

    /// <summary>
    /// ドローン・ライト・レーザーの、動く線(巡回ルート・往復する線)の上の点を、集めるメソッド
    /// </summary>
    private static List<(string name, List<Vector3> samples)> CollectDeviceLines()
    {
        List<(string, List<Vector3>)> lines = new List<(string, List<Vector3>)>();
        foreach (CS_SecurityDroneSpawner drone in _multi.GetComponentsInChildren<CS_SecurityDroneSpawner>(true))
        {
            List<Vector3> points = new List<Vector3>();
            foreach (Transform point in drone.transform) points.Add(point.position);
            List<Vector3> samples = new List<Vector3>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 from = points[i];
                Vector3 to = points[(i + 1) % points.Count];
                float length = Vector3.Distance(from, to);
                for (float d = 0.0f; d < length; d += 5.0f) samples.Add(Vector3.Lerp(from, to, length > 0.01f ? d / length : 0.0f));
            }
            if (samples.Count == 0) samples.Add(drone.transform.position);
            lines.Add(($"ドローン {drone.name}", samples));
        }
        foreach (CS_SecurityLight light in _multi.GetComponentsInChildren<CS_SecurityLight>(true))
        {
            SerializedObject serialized = new SerializedObject(light);
            Vector3 direction = light.transform.rotation * serialized.FindProperty("_moveDirection").vector3Value.normalized;
            float distance = serialized.FindProperty("_moveDistance").floatValue;
            List<Vector3> samples = new List<Vector3>();
            for (float d = 0.0f; d <= distance; d += 5.0f) samples.Add(light.transform.position + direction * d);
            lines.Add(($"ライト {light.name}", samples));
        }
        foreach (CS_SecurityLaser laser in _multi.GetComponentsInChildren<CS_SecurityLaser>(true))
        {
            SerializedObject serialized = new SerializedObject(laser);
            Vector3 direction = laser.transform.rotation * serialized.FindProperty("_moveDirection").vector3Value.normalized;
            float distance = serialized.FindProperty("_moveDistance").floatValue;
            List<Vector3> samples = new List<Vector3>();
            for (float d = 0.0f; d <= distance; d += 5.0f) samples.Add(laser.transform.position + direction * d);
            lines.Add(($"レーザー {laser.name}", samples));
        }
        return lines;
    }

    /// <summary>
    /// 同じ種類の防犯機器(ドローン同士・ライト同士・レーザー同士)で、動く線が 6m 以内に重なっている組み合わせを、報告に書くメソッド(高さが 4m 以上違う物は、別とみなす)
    /// </summary>
    private static void AppendDeviceOverlaps()
    {
        List<(string name, List<Vector3> samples)> lines = CollectDeviceLines();
        _report.AppendLine();
        _report.AppendLine("【同じ種類の防犯機器で、動く線が 6m 以内に重なっている組み合わせ】");
        int total = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            for (int j = i + 1; j < lines.Count; j++)
            {
                if (lines[i].name.Split(' ')[0] != lines[j].name.Split(' ')[0]) continue;

                int near = 0;
                foreach (Vector3 sample in lines[i].samples)
                {
                    if (IsNearSameLevel(lines[j].samples, sample, 6.0f)) near++;
                }
                float ratio = (float)near / lines[i].samples.Count;
                if (ratio < 0.3f) continue;

                total++;
                _report.AppendLine($"  {lines[i].name} と {lines[j].name}: {lines[i].name.Split(' ')[0]}の線の {ratio * 100.0f:F0}% が重なる");
            }
        }
        _report.AppendLine($"  合計 {total}組");
    }
}
