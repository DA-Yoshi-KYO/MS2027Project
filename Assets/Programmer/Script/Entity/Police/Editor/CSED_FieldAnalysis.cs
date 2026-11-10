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
/// 開いているシーンのフィールドを調べて、配置を考えるための資料を書き出すエディタ専用ツール
/// ・3つのエリア(プレイヤーの開始地点のエリア)の位置、悪人の出現点、歩ける場所の高さごとの分布を出す
/// メニュー「Tools/Police/フィールドを分析する」で実行する(MainSceneを開いてから)。結果は Temp/FieldAnalysisReport.txt
/// </summary>
public static class CSED_FieldAnalysis
{
    // 高さの区切り(m)。地上・低い屋根・高所を分ける
    private static readonly float[] _heightEdges = { 3.0f, 8.0f, 15.0f, 30.0f, 60.0f, 120.0f };

    [MenuItem("Tools/Police/フィールドを分析する")]
    private static void Analyze()
    {
        StringBuilder report = new StringBuilder();
        List<Vector3> areaCenters = AppendAreas(report);
        AppendVillains(report, areaCenters);
        AppendNavMeshHeights(report, areaCenters);
        AppendAlleys(report, areaCenters);
        AppendRoofs(report, areaCenters);
        AppendVillainCandidates(report, areaCenters);

        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "FieldAnalysisReport.txt");
        File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
        Debug.Log($"CSED_FieldAnalysis: 分析しました。結果: {path}");
    }

    /// <summary>
    /// プレイヤーの開始エリア(CS_PlayerSpawnerのエリアの親)の位置を書き出すメソッド
    /// </summary>
    private static List<Vector3> AppendAreas(StringBuilder report)
    {
        List<Vector3> centers = new List<Vector3>();
        foreach (CS_PlayerSpawner spawner in Object.FindObjectsByType<CS_PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            report.AppendLine($"【PlayerSpawner】{spawner.transform.parent?.name}/{spawner.name} エリアの数 {spawner.areaSpawnRoots?.Count ?? 0}");
            if (spawner.areaSpawnRoots == null) continue;

            for (int i = 0; i < spawner.areaSpawnRoots.Count; i++)
            {
                Transform root = spawner.areaSpawnRoots[i];
                if (root == null) continue;

                Vector3 center = Vector3.zero;
                foreach (Transform child in root) center += child.position;
                if (root.childCount > 0) center /= root.childCount; else center = root.position;
                centers.Add(center);
                report.AppendLine($"  エリア{i}: {root.name} 中心 ({center.x:F1}, {center.y:F1}, {center.z:F1}) 開始地点 {root.childCount}個");
            }
        }
        return centers;
    }

    /// <summary>
    /// 悪人の出現点(どのエリアに近いか)と、悪人のスポナーが持っている出現点の数を書き出すメソッド
    /// </summary>
    private static void AppendVillains(StringBuilder report, List<Vector3> areaCenters)
    {
        report.AppendLine();
        foreach (CS_VillainSpawner spawner in Object.FindObjectsByType<CS_VillainSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject serialized = new SerializedObject(spawner);
            SerializedProperty property = serialized.GetIterator();
            report.AppendLine($"【VillainSpawner】{GetPath(spawner.transform)} active={spawner.gameObject.activeInHierarchy}");
            bool enter = true;
            while (property.NextVisible(enter))
            {
                enter = false;
                if (property.isArray && property.propertyType != SerializedPropertyType.String) report.AppendLine($"  {property.name}: 要素 {property.arraySize}");
                else if (property.propertyType == SerializedPropertyType.Integer || property.propertyType == SerializedPropertyType.Float)
                    report.AppendLine($"  {property.name}: {(property.propertyType == SerializedPropertyType.Integer ? property.intValue.ToString() : property.floatValue.ToString("F2"))}");
            }
        }

        report.AppendLine();
        CS_VillainSpawnPoint[] points = Object.FindObjectsByType<CS_VillainSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        report.AppendLine($"【悪人の出現点】{points.Length}個");
        foreach (CS_VillainSpawnPoint point in points)
        {
            Vector3 p = point.transform.position;
            report.AppendLine($"  {GetPath(point.transform)} ({p.x:F1}, {p.y:F1}, {p.z:F1}) active={point.gameObject.activeInHierarchy} 近いエリア={FindNearest(areaCenters, p)}");
        }
    }

    /// <summary>
    /// 歩ける場所(NavMesh)の頂点を、高さと、近いエリアごとに数えて書き出すメソッド
    /// </summary>
    private static void AppendNavMeshHeights(StringBuilder report, List<Vector3> areaCenters)
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        int areaCount = Mathf.Max(1, areaCenters.Count);
        int bandCount = _heightEdges.Length + 1;
        int[,] counts = new int[areaCount, bandCount];
        foreach (Vector3 vertex in triangulation.vertices)
        {
            int area = areaCenters.Count > 0 ? FindNearest(areaCenters, vertex) : 0;
            counts[area, GetBand(vertex.y)]++;
        }

        report.AppendLine();
        report.AppendLine("【歩ける場所(NavMesh)の頂点数: エリア × 高さ】");
        for (int area = 0; area < areaCount; area++)
        {
            StringBuilder line = new StringBuilder($"  エリア{area}:");
            for (int band = 0; band < bandCount; band++) line.Append($" {GetBandLabel(band)}={counts[area, band]}");
            report.AppendLine(line.ToString());
        }
    }

    /// <summary>
    /// 路地裏(NavMeshModifierVolume)の数を、近いエリアごとに数えて書き出すメソッド(悪人の出現点を置く候補)
    /// </summary>
    private static void AppendAlleys(StringBuilder report, List<Vector3> areaCenters)
    {
        Unity.AI.Navigation.NavMeshModifierVolume[] volumes =
            Object.FindObjectsByType<Unity.AI.Navigation.NavMeshModifierVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int[] counts = new int[Mathf.Max(1, areaCenters.Count)];
        List<string> samples = new List<string>();
        foreach (Unity.AI.Navigation.NavMeshModifierVolume volume in volumes)
        {
            Vector3 center = volume.transform.TransformPoint(volume.center);
            int area = areaCenters.Count > 0 ? FindNearest(areaCenters, center) : 0;
            counts[area]++;
            if (samples.Count < 12) samples.Add($"  {GetPath(volume.transform)} 中心 ({center.x:F0}, {center.y:F0}, {center.z:F0}) 大きさ {volume.size.x * volume.transform.lossyScale.x:F0}x{volume.size.z * volume.transform.lossyScale.z:F0}");
        }

        report.AppendLine();
        report.AppendLine($"【路地裏などのNavMeshModifierVolume】{volumes.Length}個 / エリアごと: {string.Join(", ", counts)}");
        foreach (string sample in samples) report.AppendLine(sample);
    }

    /// <summary>
    /// 上から下へ光線を撃って、建物の屋上(水平な面)の数を、高さとエリアごとに数えて書き出すメソッド(SAポイントを置く候補)
    /// </summary>
    private static void AppendRoofs(StringBuilder report, List<Vector3> areaCenters)
    {
        const float step = 8.0f;
        const float startHeight = 250.0f;
        int areaCount = Mathf.Max(1, areaCenters.Count);
        int bandCount = _heightEdges.Length + 1;
        int[,] counts = new int[areaCount, bandCount];
        int total = 0;
        for (float x = -380.0f; x <= 380.0f; x += step)
        {
            for (float z = -390.0f; z <= 380.0f; z += step)
            {
                if (!Physics.Raycast(new Vector3(x, startHeight, z), Vector3.down, out RaycastHit hit, startHeight + 5.0f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.95f || hit.point.y < 3.0f) continue;

                Vector3 point = hit.point;
                counts[areaCenters.Count > 0 ? FindNearest(areaCenters, point) : 0, GetBand(point.y)]++;
                total++;
            }
        }

        report.AppendLine();
        report.AppendLine($"【屋上など高所の水平な面(格子 {step}m ごとに、一番上の面が高さ3m以上で水平なもの)】{total}点");
        for (int area = 0; area < areaCount; area++)
        {
            StringBuilder line = new StringBuilder($"  エリア{area}:");
            for (int band = 1; band < bandCount; band++) line.Append($" {GetBandLabel(band)}={counts[area, band]}");
            report.AppendLine(line.ToString());
        }
    }

    /// <summary>
    /// 悪人を置く候補(路地裏の歩ける場所・狭い道・低い屋上の歩ける場所)が、エリアごとにどれだけあるかを書き出すメソッド
    /// </summary>
    private static void AppendVillainCandidates(StringBuilder report, List<Vector3> areaCenters)
    {
        int alleyArea = NavMesh.GetAreaFromName("Alley");
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        int areaCount = Mathf.Max(1, areaCenters.Count);
        int[] alley = new int[areaCount];
        int[] narrow = new int[areaCount];
        int[] wide = new int[areaCount];
        int[] roof = new int[areaCount];
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        for (int t = 0; t < triangulation.areas.Length; t++)
        {
            Vector3 center = (triangulation.vertices[triangulation.indices[t * 3]] + triangulation.vertices[triangulation.indices[t * 3 + 1]] + triangulation.vertices[triangulation.indices[t * 3 + 2]]) / 3.0f;
            if (!seen.Add(new Vector2Int(Mathf.RoundToInt(center.x / 4.0f), Mathf.RoundToInt(center.z / 4.0f) + 100000 * Mathf.RoundToInt(center.y / 2.0f)))) continue;

            int area = areaCenters.Count > 0 ? FindNearest(areaCenters, center) : 0;
            if (center.y >= 4.0f && center.y <= 14.0f) { roof[area]++; continue; }
            if (center.y >= 3.0f) continue;

            if (triangulation.areas[t] == alleyArea) { alley[area]++; continue; }
            if (NavMesh.FindClosestEdge(center, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance <= 4.0f) narrow[area]++; else wide[area]++;
        }

        report.AppendLine();
        report.AppendLine($"【悪人を置く候補(4m四方の升目ごと): Alley の番号 {alleyArea}】");
        for (int area = 0; area < areaCount; area++)
        {
            report.AppendLine($"  エリア{area}: 路地裏(Alley) {alley[area]} / 狭い道(縁から4m以内) {narrow[area]} / 広い道(大通り) {wide[area]} / 低い屋上(高さ4〜14m) {roof[area]}");
        }
    }

    private static int GetBand(float y)
    {
        for (int i = 0; i < _heightEdges.Length; i++)
        {
            if (y < _heightEdges[i]) return i;
        }
        return _heightEdges.Length;
    }

    private static string GetBandLabel(int band)
    {
        if (band == 0) return $"〜{_heightEdges[0]}m";
        if (band == _heightEdges.Length) return $"{_heightEdges[band - 1]}m〜";
        return $"{_heightEdges[band - 1]}〜{_heightEdges[band]}m";
    }

    /// <summary>
    /// 一番近いエリアの番号を返すメソッド(水平距離で比べる)
    /// </summary>
    private static int FindNearest(List<Vector3> centers, Vector3 position)
    {
        int nearest = -1;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < centers.Count; i++)
        {
            Vector3 offset = centers[i] - position;
            offset.y = 0.0f;
            if (offset.sqrMagnitude >= nearestSqr) continue;

            nearest = i;
            nearestSqr = offset.sqrMagnitude;
        }
        return nearest;
    }

    private static string GetPath(Transform target)
    {
        string path = target.name;
        for (Transform parent = target.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
        return path;
    }
}
