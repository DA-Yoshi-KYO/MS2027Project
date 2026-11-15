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
/// 配置ツールのうち、悪人の出現点を、区画分けの図(3つの区画)に合わせて、犯罪完遂エリア(CrimeArea_01〜03)の子に振り分ける部分
/// 区画 1(緑・北西) → CrimeArea_01 / 区画 2(赤・北東〜東) → CrimeArea_02 / 区画 3(黄・南) → CrimeArea_03
/// 位置は動かさず、親だけを付け替える
/// </summary>
public static partial class CSED_FieldMultiLayoutGenerator
{
    // 区画分けの図(Game/Sceneを真上から見た画像)の、ピクセルとワールドの対応
    // 図の中心(広場)がワールドの原点で、右が +X、上が +Z。エリアの開始地点の印(Area0〜2)から合わせた
    private const float _sectorPixelCenterU = 410.5f;
    private const float _sectorPixelCenterV = 399.2f;
    private const float _sectorPixelPerMeter = 1.0685f;

    // 図に描かれた線(ピクセル)。手書きなので、ざっくりした多角形
    private static readonly Vector2[] _sectorGreen =
    {
        new Vector2(55, 97), new Vector2(388, 79), new Vector2(397, 100), new Vector2(392, 150), new Vector2(405, 200), new Vector2(405, 300),
        new Vector2(388, 314), new Vector2(345, 320), new Vector2(322, 345), new Vector2(318, 385), new Vector2(332, 425), new Vector2(310, 460),
        new Vector2(240, 500), new Vector2(150, 555), new Vector2(95, 578), new Vector2(60, 592), new Vector2(22, 590), new Vector2(28, 440),
        new Vector2(42, 250), new Vector2(60, 160),
    };
    private static readonly Vector2[] _sectorRed =
    {
        new Vector2(432, 85), new Vector2(728, 86), new Vector2(740, 562), new Vector2(560, 505), new Vector2(503, 440), new Vector2(503, 380),
        new Vector2(482, 340), new Vector2(432, 312), new Vector2(428, 290), new Vector2(432, 150),
    };
    private static readonly Vector2[] _sectorYellow =
    {
        new Vector2(320, 470), new Vector2(380, 488), new Vector2(450, 502), new Vector2(487, 483), new Vector2(525, 497), new Vector2(680, 545),
        new Vector2(772, 615), new Vector2(765, 748), new Vector2(600, 745), new Vector2(420, 738), new Vector2(125, 766), new Vector2(108, 700),
        new Vector2(88, 630), new Vector2(100, 590), new Vector2(190, 545), new Vector2(280, 490),
    };

    // 線の外にある時の、原点からの角度(度)による区画: 区画1 [90, 207) / 区画3 [207, 333) / 区画2 それ以外
    private const float _sectorAngleGreenYellow = 207.0f;
    private const float _sectorAngleYellowRed = 333.0f;
    private const float _sectorBoundaryWarning = 8.0f;       // 境界の角度から、この範囲(度)にある出現点は、報告で注意する

    /// <summary>
    /// 悪人の出現点を、区画分けの図に合わせて振り分けた結果を、動かさずに、報告に書くメソッド(結果は Temp/FieldVillainSectorReport.txt)
    /// </summary>
    [MenuItem("Tools/Police/悪人を区画分けの図に合わせて振り分ける(動かさない)")]
    private static void CheckVillainSectors()
    {
        SortVillainsBySector(false);
    }

    /// <summary>
    /// 悪人の出現点を、区画分けの図に合わせて、犯罪完遂エリア(CrimeArea_01〜03)の子に振り分けるメソッド(位置は動かさない)
    /// </summary>
    [MenuItem("Tools/Police/悪人を区画分けの図に合わせて振り分ける")]
    private static void ApplyVillainSectors()
    {
        SortVillainsBySector(true);
    }

    private static void SortVillainsBySector(bool apply)
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
        List<(string newName, Vector3 position, int[] sourcePath, string label)> deferred = new List<(string, Vector3, int[], string)>();
        try
        {
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
            int[] before = new int[4];
            int[] after = new int[4];
            int already = 0;
            int changed = 0;
            int skipped = 0;
            List<string> lines = new List<string>();
            foreach (VillainEntry entry in entries)
            {
                Vector3 position = entry.position;
                int sector = ClassifySector(position, out bool byPolygon, out int byAngle, out float angle, out float boundaryGap);
                string currentName = entry.scene.transform.parent != null ? entry.scene.transform.parent.name : "(親なし)";
                string newName = $"CrimeArea_0{sector}";
                int currentNumber = currentName.StartsWith("CrimeArea_0") && currentName.Length == 12 ? currentName[11] - '0' : 0;
                before[currentNumber]++;

                string note = "";
                if (!byPolygon) note += " / 図の線の外(角度で判定)";
                else if (byAngle != sector) note += $" / 角度で見ると区画{byAngle}(図の線を優先)";
                if (boundaryGap < _sectorBoundaryWarning) note += $" / 境界まで {boundaryGap:F0}°(要確認)";
                if (Mathf.Sqrt(position.x * position.x + position.z * position.z) < 90.0f) note += " / 中央の広場の中";

                string status;
                if (currentName == newName)
                {
                    already++;
                    after[sector]++;
                    status = "そのまま";
                }
                else if (entry.target == null)
                {
                    skipped++;
                    after[currentNumber]++;
                    status = $"付け替えられない(プレハブに見つからない。MainSceneの上書き) → {newName}";
                }
                else if (!apply)
                {
                    changed++;
                    after[sector]++;
                    status = $"付け替える {currentName} → {newName} ({(entry.inField ? "Field.prefab側" : "Field_Solo1側")}{(entry.inField ? $" / 足した物 {PrefabUtility.IsAddedGameObjectOverride(entry.target.gameObject)} / 引き継ぎ {PrefabUtility.IsPartOfPrefabInstance(entry.target.gameObject)}" : "")})";
                }
                else if (IsInheritedInField(entry))
                {
                    // Field.prefab が Field_Solo1 から引き継いだ物は、Field.prefab の側では親を付け替えられないので、元の Field_Solo1.prefab で付け替える(あとで)
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(entry.target.gameObject);
                    int[] sourcePath = source != null && AssetDatabase.GetAssetPath(source).EndsWith(_targetFile) ? GetIndexPath(source.transform) : null;
                    if (sourcePath != null)
                    {
                        deferred.Add((newName, position, sourcePath, entry.name));
                        changed++;
                        after[sector]++;
                        status = $"付け替える(Field.prefab の上書きがある物。Field_Solo1.prefab で付け替える) {currentName} → {newName}";
                    }
                    else
                    {
                        skipped++;
                        after[currentNumber]++;
                        status = $"付け替えられなかった(元の物が見つからない) → {newName}";
                    }
                }
                else if (TryReparentVillain(entry, newName, position))
                {
                    changed++;
                    after[sector]++;
                    status = $"付け替えた {currentName} → {newName}";
                }
                else
                {
                    skipped++;
                    after[currentNumber]++;
                    status = $"付け替えられなかった(Field.prefab側の引き継ぎ物など) → {newName}";
                }
                lines.Add($"  {entry.name} ({position.x:F0}, {position.y:F0}, {position.z:F0}) 原点から {angle:F0}° / 区画{sector} / {status}{note}");
            }

            _report.AppendLine($"【悪人の出現点を区画分けの図に合わせて{(apply ? "振り分けた" : "振り分ける(動かしていない)")}】{entries.Count}個 / そのまま {already}個 / 付け替え {changed}個 / 付け替えられない {skipped}個");
            _report.AppendLine($"  CrimeArea_01(区画1・緑): {before[1]} → {after[1]}個 / CrimeArea_02(区画2・赤): {before[2]} → {after[2]}個 / CrimeArea_03(区画3・黄): {before[3]} → {after[3]}個");
            foreach (string line in lines) _report.AppendLine(line);

            if (!apply) return;

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
            if (apply && deferred.Count > 0) ReparentInheritedVillains(path, deferred);
            string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "FieldVillainSectorReport.txt");
            File.WriteAllText(reportPath, _report.ToString(), new UTF8Encoding(false));
        }
        Debug.Log(apply ? "CSED_FieldMultiLayoutGenerator: 悪人を区画分けの図に合わせて振り分けました" : "CSED_FieldMultiLayoutGenerator: 悪人の振り分けを調べました");
    }

    /// <summary>
    /// Field.prefab の中の Field_Solo1 から引き継いだ(Field.prefab で足した物ではない)物かを判定するメソッド
    /// </summary>
    private static bool IsInheritedInField(VillainEntry entry)
    {
        return entry.inField && PrefabUtility.IsPartOfPrefabInstance(entry.target.gameObject) && !PrefabUtility.IsAddedGameObjectOverride(entry.target.gameObject);
    }

    /// <summary>
    /// ルートからの、子の番号の並び(ルートは含まない)を返すメソッド
    /// </summary>
    private static int[] GetIndexPath(Transform target)
    {
        List<int> path = new List<int>();
        for (Transform t = target; t.parent != null; t = t.parent) path.Insert(0, t.GetSiblingIndex());
        return path.ToArray();
    }

    private static Transform FollowIndexPath(Transform root, int[] path)
    {
        Transform current = root;
        foreach (int index in path)
        {
            if (index < 0 || index >= current.childCount) return null;

            current = current.GetChild(index);
        }
        return current;
    }

    /// <summary>
    /// Field.prefab が引き継いでいる悪人の出現点を、元の Field_Solo1.prefab で、別の犯罪完遂エリアの子に付け替えるメソッド
    /// 付け替えると、Field.prefab の、位置の上書き(親からの相対位置)の意味が変わるので、Field.prefab の側で、シーン上の位置が変わらないように、位置を直す
    /// </summary>
    private static void ReparentInheritedVillains(string soloPath, List<(string newName, Vector3 position, int[] sourcePath, string label)> deferred)
    {
        // 1) Field_Solo1.prefab で、親を付け替える(世界の位置は、そのまま)
        List<int[]> newPaths = new List<int[]>();
        GameObject solo = PrefabUtility.LoadPrefabContents(soloPath);
        try
        {
            // 付け替えると、同じ親の子の番号がずれるので、先に、対象を全部見つけておく
            List<Transform> targets = new List<Transform>();
            foreach (var item in deferred) targets.Add(FollowIndexPath(solo.transform, item.sourcePath));

            for (int index = 0; index < deferred.Count; index++)
            {
                var item = deferred[index];
                Transform target = targets[index];
                Transform area = solo.transform.Find("Villain/VillainSpawnPoints/" + item.newName);
                if (target == null || area == null)
                {
                    newPaths.Add(null);
                    _report.AppendLine($"  元の Field_Solo1.prefab で見つからない: {item.label}");
                    continue;
                }

                target.SetParent(area, true);
                newPaths.Add(GetIndexPath(target));
            }
            PrefabUtility.SaveAsPrefabAsset(solo, soloPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(solo);
        }

        // 2) Field.prefab で、付け替わった物の位置を、シーン上の位置が変わらないように直す
        GameObject soloAsset = AssetDatabase.LoadAssetAtPath<GameObject>(soloPath);
        GameObject field = PrefabUtility.LoadPrefabContents(_fieldPrefabPath);
        try
        {
            Transform fieldSolo = null;
            foreach (Transform child in field.transform)
            {
                if (child.name == _targetName) fieldSolo = child;
            }

            int fixedCount = 0;
            for (int i = 0; i < deferred.Count; i++)
            {
                if (newPaths[i] == null || fieldSolo == null) continue;

                Transform sourceAsset = FollowIndexPath(soloAsset.transform, newPaths[i]);
                Transform fieldTarget = null;
                foreach (Transform candidate in fieldSolo.GetComponentsInChildren<Transform>(true))
                {
                    if (sourceAsset != null && PrefabUtility.GetCorrespondingObjectFromSource(candidate.gameObject) == sourceAsset.gameObject) fieldTarget = candidate;
                }
                Transform sceneArea = _multi.Find("Villain/VillainSpawnPoints/" + deferred[i].newName);
                if (fieldTarget == null || sceneArea == null || fieldTarget.parent == null)
                {
                    _report.AppendLine($"  Field.prefab で見つからない(位置を直せなかった): {deferred[i].label}");
                    continue;
                }

                fieldTarget.position = deferred[i].position - (sceneArea.position - fieldTarget.parent.position);
                fixedCount++;
                _report.AppendLine($"  付け替えた(Field_Solo1.prefab で付け替え、Field.prefab の位置を直した): {deferred[i].label} → {deferred[i].newName}");
            }
            PrefabUtility.SaveAsPrefabAsset(field, _fieldPrefabPath);
            _report.AppendLine($"Field.prefab が引き継いでいる物の付け替え: {fixedCount}/{deferred.Count}個");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(field);
        }
    }

    /// <summary>
    /// ワールドの位置が、区画分けの図の、どの区画(1=緑 / 2=赤 / 3=黄)かを返すメソッド
    /// 図に描かれた線の中にあれば、その区画。どの線の中にも無ければ、原点からの角度で決める
    /// </summary>
    /// <param name="byPolygon">図の線の中にあったか</param>
    /// <param name="byAngle">原点からの角度で決めた区画</param>
    /// <param name="angle">原点からの角度(度。0〜360。+Xが0度、+Zが90度)</param>
    /// <param name="boundaryGap">区画の境界の角度までの差(度)</param>
    private static int ClassifySector(Vector3 world, out bool byPolygon, out int byAngle, out float angle, out float boundaryGap)
    {
        angle = Mathf.Repeat(Mathf.Atan2(world.z, world.x) * Mathf.Rad2Deg, 360.0f);
        byAngle = angle >= 90.0f && angle < _sectorAngleGreenYellow ? 1 : (angle >= _sectorAngleGreenYellow && angle < _sectorAngleYellowRed ? 3 : 2);
        boundaryGap = Mathf.Min(Mathf.Abs(Mathf.DeltaAngle(angle, 90.0f)), Mathf.Abs(Mathf.DeltaAngle(angle, _sectorAngleGreenYellow)), Mathf.Abs(Mathf.DeltaAngle(angle, _sectorAngleYellowRed)));

        Vector2 pixel = new Vector2(_sectorPixelCenterU + world.x * _sectorPixelPerMeter, _sectorPixelCenterV - world.z * _sectorPixelPerMeter);
        byPolygon = true;
        if (IsInsidePolygon(pixel, _sectorGreen)) return 1;
        if (IsInsidePolygon(pixel, _sectorRed)) return 2;
        if (IsInsidePolygon(pixel, _sectorYellow)) return 3;

        byPolygon = false;
        return byAngle;
    }

    private static bool IsInsidePolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if ((polygon[i].y > point.y) == (polygon[j].y > point.y)) continue;

            float crossX = (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x;
            if (point.x < crossX) inside = !inside;
        }
        return inside;
    }
}
