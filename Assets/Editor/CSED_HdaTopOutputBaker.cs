#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using HoudiniEngineUnity;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HDAのTOP出力(「〇〇_OUTPUTS」配下の HEU_GeoSync)をまとめてベイクし、エリアごとにPrefab化するツール。
/// HDAルートの Bake ボタンは TOP の結果を対象にしないため、
/// 「Nothing to bake as no geometry available!」になる問題の回避用。
///
/// 出力先(エリア名は、HEU_GeoSync の親オブジェクトの名前):
///   Assets/Designer/Prefab/Background/エリア名/エリア名.prefab   ← エリアPrefab
///   Assets/Designer/Prefab/Background/エリア名/地区/地区.prefab  ← 地区Prefab(全地区をここに集める)
///
/// Houdini Engine の Bake() は保存先が固定(HoudiniEngineAssetCache/Baked)なので、
/// ベイク後にアセットを地区フォルダへ移動する(GUIDは変わらないので参照は壊れない)。
///
/// 使い方:
///   1. Hierarchy で「〇〇_OUTPUTS」(または HEU_GeoSync を子に持つ親)を選ぶ
///   2. メニュー Tools/HDA/TOP出力をベイクしてPrefab化 を実行
/// </summary>
public static class CSED_HdaTopOutputBaker
{
    private const string menuPath = "Tools/HDA/TOP出力をベイクしてPrefab化";

    // 出力先の基準フォルダ
    private const string baseFolder = "Assets/Designer/Prefab/Background";

    // エリアのフォルダの下に作る、地区Prefabの置き場所
    private const string districtFolderName = "Area_01";

    [MenuItem(menuPath, true)]
    private static bool ValidateBake()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem(menuPath)]
    private static void BakeSelectedOutputs()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null) return;

        // 選択した親の下にある HEU_GeoSync を全部集める(非アクティブも含む)
        HEU_GeoSync[] syncs = root.GetComponentsInChildren<HEU_GeoSync>(true);
        if (syncs.Length == 0)
        {
            Debug.LogWarning($"[HdaTopOutputBaker] {root.name} の下に HEU_GeoSync が見つかりません。「〇〇_OUTPUTS」を選んでください。");
            return;
        }

        // 同じ親(エリア)ごとにまとめる
        Dictionary<string, List<HEU_GeoSync>> areas = GroupByArea(syncs, root);

        int bakedCount = 0;
        Object originalSelection = Selection.activeObject;
        try
        {
            foreach (KeyValuePair<string, List<HEU_GeoSync>> area in areas)
            {
                bakedCount += BakeArea(area.Key, area.Value);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Selection.activeObject = originalSelection;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[HdaTopOutputBaker] {bakedCount}/{syncs.Length} 個の地区をベイクしました({baseFolder})。");
    }

    /// <summary>
    /// HEU_GeoSync を、親オブジェクトの名前(エリア名)ごとに分ける。親がいなければ選択中のオブジェクト名を使う。
    /// </summary>
    private static Dictionary<string, List<HEU_GeoSync>> GroupByArea(HEU_GeoSync[] syncs, GameObject root)
    {
        Dictionary<string, List<HEU_GeoSync>> areas = new Dictionary<string, List<HEU_GeoSync>>();
        foreach (HEU_GeoSync sync in syncs)
        {
            Transform parent = sync.transform.parent;
            string areaName = parent != null ? parent.name : root.name;
            if (!areas.ContainsKey(areaName)) areas[areaName] = new List<HEU_GeoSync>();
            areas[areaName].Add(sync);
        }
        return areas;
    }

    /// <summary>
    /// 1エリア分をベイクし、エリアPrefabを作る。ベイクできた地区の数を返す。
    /// </summary>
    private static int BakeArea(string areaName, List<HEU_GeoSync> syncs)
    {
        string areaFolder = baseFolder + "/" + areaName;
        string districtFolder = areaFolder + "/" + districtFolderName;
        EnsureFolder(districtFolder);

        // 前回のベイクで作ったアセット(エリアPrefabを保存してから削除する)
        List<string> oldAssetPaths = GetFilePaths(districtFolder);

        List<GameObject> districtPrefabs = new List<GameObject>();
        for (int i = 0; i < syncs.Count; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar(
                    "HDA ベイク中", $"{areaName}: {syncs[i].name} ({i + 1}/{syncs.Count})", (float)i / syncs.Count))
            {
                break;
            }

            // 同期中(読み込み中)のものは Bake() が何もしないのでスキップする
            if (syncs[i]._syncing)
            {
                Debug.LogWarning($"[HdaTopOutputBaker] {syncs[i].name} は読み込み中のためスキップしました。");
                continue;
            }

            Selection.activeObject = null;
            syncs[i].Bake();

            GameObject prefab = MoveBakedAssets(Selection.activeObject as GameObject, districtFolder);
            if (prefab != null)
            {
                districtPrefabs.Add(prefab);
            }
            else
            {
                Debug.LogWarning($"[HdaTopOutputBaker] {syncs[i].name} はPrefabを作れませんでした。");
            }
        }

        if (districtPrefabs.Count == 0) return 0;

        // エリアPrefabを保存してから、前回のアセットを消す(失敗したときに前回分を残すため)
        SaveAreaPrefab(areaName, areaFolder, districtPrefabs);
        foreach (string oldPath in oldAssetPaths)
        {
            AssetDatabase.DeleteAsset(oldPath);
        }

        return districtPrefabs.Count;
    }

    /// <summary>
    /// フォルダ内のファイル(フォルダは除く)のパスを返す。
    /// </summary>
    private static List<string> GetFilePaths(string folder)
    {
        List<string> paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.IsValidFolder(path)) paths.Add(path);
        }
        return paths;
    }

    /// <summary>
    /// 地区Prefabを1つの親にまとめて、エリアPrefabとして保存する。
    /// </summary>
    private static void SaveAreaPrefab(string areaName, string areaFolder, List<GameObject> districtPrefabs)
    {
        GameObject areaRoot = new GameObject(areaName);
        try
        {
            foreach (GameObject prefab in districtPrefabs)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(areaRoot.transform, false);
            }

            // 同じパスに上書きする(GUIDが変わらないので、シーンに置いたエリアPrefabが自動で更新される)
            string areaPrefabPath = areaFolder + "/" + areaName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(areaRoot, areaPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(areaRoot);
        }
    }

    /// <summary>
    /// ベイク結果のフォルダ内のアセットを、すべて地区フォルダへ移し、移動後のPrefabを返す。
    /// 空になったベイク結果のフォルダは削除する。
    /// </summary>
    private static GameObject MoveBakedAssets(GameObject bakedPrefab, string districtFolder)
    {
        if (bakedPrefab == null) return null;

        string prefabPath = AssetDatabase.GetAssetPath(bakedPrefab);
        if (string.IsNullOrEmpty(prefabPath)) return null;

        string bakedFolder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
        GameObject movedPrefab = null;

        foreach (string guid in AssetDatabase.FindAssets("", new[] { bakedFolder }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(assetPath)) continue;

            string destPath = AssetDatabase.GenerateUniqueAssetPath(districtFolder + "/" + Path.GetFileName(assetPath));
            string error = AssetDatabase.MoveAsset(assetPath, destPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"[HdaTopOutputBaker] 移動に失敗しました({assetPath}): {error}");
                continue;
            }

            if (assetPath == prefabPath) movedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
        }

        // 中身が空になったベイク結果のフォルダだけ削除する
        if (AssetDatabase.FindAssets("", new[] { bakedFolder }).Length == 0)
        {
            AssetDatabase.DeleteAsset(bakedFolder);
        }

        return movedPrefab;
    }

    /// <summary>
    /// 「Assets/A/B」のような階層を、無ければ順に作る。
    /// </summary>
    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
    }
}
#endif
