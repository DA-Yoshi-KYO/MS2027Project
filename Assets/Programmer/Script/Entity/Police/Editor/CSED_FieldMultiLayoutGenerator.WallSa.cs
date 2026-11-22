/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-16 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 配置ツールのうち、ビルの壁に着けた(壁から腕でつないだ)SAObjectを、まとめて探す・削除する部分
/// 壁に着けたSAObjectは、壁と柱をつなぐ腕(子の「Cube (2)」。コライダー無し)が付いている物
/// 削除する時は、同じ足元にあるハイドポイントも一緒に削除する
/// </summary>
public static partial class CSED_FieldMultiLayoutGenerator
{
    private const string _wallArmName = "Cube (2)";     // 壁と柱をつなぐ腕(AddWallArm で作る)の名前

    /// <summary>
    /// 壁に着けたSAObjectを、削除せずに、数えて報告に書くメソッド(結果は Temp/WallSaObjectReport.txt)
    /// </summary>
    [MenuItem("Tools/Police/壁に着けているSAObjectを調べる(消さない)")]
    private static void CheckWallSaObjects()
    {
        RemoveWallSaObjects(false);
    }

    /// <summary>
    /// 壁に着けたSAObjectを、ハイドポイントごと、すべて削除するメソッド
    /// </summary>
    [MenuItem("Tools/Police/壁に着けているSAObjectをすべて削除する")]
    private static void DeleteWallSaObjects()
    {
        RemoveWallSaObjects(true);
    }

    private static void RemoveWallSaObjects(bool apply)
    {
        _multi = FindMulti();
        Transform saGroup = _multi != null ? _multi.Find(_saObjectGroup) : null;
        Transform hideGroup = _multi != null ? _multi.Find(_hideGroup) : null;
        string path = _multi != null ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(_multi.gameObject)) : "";
        if (saGroup == null || hideGroup == null || !path.EndsWith(_targetFile))
        {
            Debug.LogError("CSED_FieldMultiLayoutGenerator: Field_Solo1 のSAObjectsまたはHidePointsが見つかりません(MainSceneを開いてください)");
            return;
        }

        Dictionary<string, int> childNames = new Dictionary<string, int>();
        List<(string name, Vector3 from)> deletes = new List<(string, Vector3)>();
        List<string> lines = new List<string>();
        int total = 0;
        foreach (Transform sa in saGroup)
        {
            total++;
            foreach (Transform child in sa) childNames[child.name] = childNames.TryGetValue(child.name, out int count) ? count + 1 : 1;

            Transform arm = sa.Find(_wallArmName);
            if (arm == null || arm.GetComponent<Collider>() != null) continue;

            deletes.Add((sa.name, sa.position));
            lines.Add($"  {sa.name} ({sa.position.x:F1}, {sa.position.y:F1}, {sa.position.z:F1})");
        }

        // 名前が同じSAObjectが複数あると、名前だけでは、プレハブの中の物を、取り違えるので、重複を調べておく
        HashSet<string> seen = new HashSet<string>();
        int duplicated = 0;
        foreach ((string name, Vector3 from) delete in deletes)
        {
            if (!seen.Add(delete.name)) duplicated++;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine($"【壁に着けているSAObject】SAObject {total}本のうち {deletes.Count}本(名前が重複する物 {duplicated}本) / {(apply ? "削除した" : "削除していない")}");
        report.AppendLine("SAObjectの子の名前の内訳: " + string.Join(", ", ToList(childNames)));
        foreach (string line in lines) report.AppendLine(line);

        if (apply && deletes.Count > 0 && duplicated == 0) ApplySaFix(path, hideGroup, new List<(string, Vector3, Vector3)>(), deletes);
        else if (apply && duplicated > 0) report.AppendLine("名前が重複する物があるため、削除しませんでした");

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "WallSaObjectReport.txt"), report.ToString(), new UTF8Encoding(false));
        Debug.Log(apply ? "CSED_FieldMultiLayoutGenerator: 壁に着けているSAObjectを削除しました" : "CSED_FieldMultiLayoutGenerator: 壁に着けているSAObjectを調べました");
    }

    private static List<string> ToList(Dictionary<string, int> counts)
    {
        List<string> result = new List<string>();
        foreach (KeyValuePair<string, int> pair in counts) result.Add($"{pair.Key} {pair.Value}");
        return result;
    }

    // =====================================================================
    // 外周のSAObject
    // =====================================================================

    private const float _perimeterPlazaRadius = 110.0f;     // 中央の広場とみなす、原点からの距離(m)。この内側は、外周にしない

    /// <summary>
    /// 外周(区画分けの図の3つの線のどれの中でもなく、中央の広場でもない所)のSAObjectを、削除せずに、数えて報告に書くメソッド(結果は Temp/PerimeterSaObjectReport.txt)
    /// </summary>
    [MenuItem("Tools/Police/外周のSAObjectを調べる(消さない)")]
    private static void CheckPerimeterSaObjects()
    {
        RemovePerimeterSaObjects(false);
    }

    /// <summary>
    /// 外周のSAObjectを、ハイドポイントごと、すべて削除するメソッド
    /// </summary>
    [MenuItem("Tools/Police/外周のSAObjectをすべて削除する")]
    private static void DeletePerimeterSaObjects()
    {
        RemovePerimeterSaObjects(true);
    }

    private static void RemovePerimeterSaObjects(bool apply)
    {
        _multi = FindMulti();
        Transform saGroup = _multi != null ? _multi.Find(_saObjectGroup) : null;
        Transform hideGroup = _multi != null ? _multi.Find(_hideGroup) : null;
        string path = _multi != null ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(_multi.gameObject)) : "";
        if (saGroup == null || hideGroup == null || !path.EndsWith(_targetFile))
        {
            Debug.LogError("CSED_FieldMultiLayoutGenerator: Field_Solo1 のSAObjectsまたはHidePointsが見つかりません(MainSceneを開いてください)");
            return;
        }

        List<(string name, Vector3 from)> deletes = new List<(string, Vector3)>();
        int[] insideCount = new int[4];
        int plaza = 0;
        int total = 0;
        float minDistance = float.MaxValue;
        Bounds bounds = new Bounds();
        foreach (Transform sa in saGroup)
        {
            total++;
            Vector3 position = sa.position;
            float distance = Mathf.Sqrt(position.x * position.x + position.z * position.z);
            Vector2 pixel = WorldToSectorPixel(position);
            int inside = IsInsidePolygon(pixel, _sectorGreen) ? 1 : (IsInsidePolygon(pixel, _sectorRed) ? 2 : (IsInsidePolygon(pixel, _sectorYellow) ? 3 : 0));
            insideCount[inside]++;
            if (inside != 0) continue;
            if (distance < _perimeterPlazaRadius)
            {
                plaza++;
                continue;
            }

            if (deletes.Count == 0) bounds = new Bounds(position, Vector3.zero);
            else bounds.Encapsulate(position);
            minDistance = Mathf.Min(minDistance, distance);
            deletes.Add((sa.name, position));
        }

        HashSet<string> seen = new HashSet<string>();
        int duplicated = 0;
        foreach ((string name, Vector3 from) delete in deletes)
        {
            if (!seen.Add(delete.name)) duplicated++;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine($"【外周のSAObject】SAObject {total}本のうち {deletes.Count}本(名前が重複する物 {duplicated}本) / {(apply ? "削除した" : "削除していない")}");
        report.AppendLine($"  図の線の中: 区画1 {insideCount[1]}本 / 区画2 {insideCount[2]}本 / 区画3 {insideCount[3]}本 / 線の外 {insideCount[0]}本(うち中央の広場 {plaza}本、外周 {deletes.Count}本)");
        if (deletes.Count > 0) report.AppendLine($"  外周の範囲: X {bounds.min.x:F0}〜{bounds.max.x:F0} / Z {bounds.min.z:F0}〜{bounds.max.z:F0} / 原点から一番近い物 {minDistance:F0}m");
        foreach ((string name, Vector3 from) delete in deletes) report.AppendLine($"  {delete.name} ({delete.from.x:F1}, {delete.from.y:F1}, {delete.from.z:F1})");

        if (apply && deletes.Count > 0 && duplicated == 0) ApplySaFix(path, hideGroup, new List<(string, Vector3, Vector3)>(), deletes);
        else if (apply && duplicated > 0) report.AppendLine("名前が重複する物があるため、削除しませんでした");

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "PerimeterSaObjectReport.txt"), report.ToString(), new UTF8Encoding(false));
        Debug.Log(apply ? "CSED_FieldMultiLayoutGenerator: 外周のSAObjectを削除しました" : "CSED_FieldMultiLayoutGenerator: 外周のSAObjectを調べました");
    }
}
