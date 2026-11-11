using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/*
+=====================================
 ファイル名 : CSE_MayaPreviewMaterialCatalogBuilder
 概要       : CS_MayaPreviewMaterialCatalog を自動生成・更新する
              ・メニュー Tools/Maya Preview/Material Catalog/Rebuild で手動更新
              ・Playerビルド前に自動で更新（ビルドに最新の一覧を含める）
 作者       : ヨシモト リョウ
 履歴       : 2026/10/10 新規作成
=====================================+
*/

public class CSE_MayaPreviewMaterialCatalogBuilder : IPreprocessBuildWithReport
{
    // =========================================================
    // 定数
    // =========================================================

    private const string ASSET_PATH =
        "Assets/Programmer/MayaPreview/Resources/" +
        CS_MayaPreviewMaterialCatalog.RESOURCES_PATH + ".asset";


    public int callbackOrder => 0;


    // =========================================================
    // ビルド前に自動更新
    // =========================================================

    public void OnPreprocessBuild(BuildReport report)
    {
        Rebuild(false);
    }


    // =========================================================
    // メニュー
    // =========================================================

    [MenuItem("Tools/Maya Preview/Material Catalog/Rebuild")]
    private static void RebuildFromMenu()
    {
        CS_MayaPreviewMaterialCatalog catalog = Rebuild(true);

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
    }


    // =========================================================
    // 一覧の生成・更新
    // =========================================================

    public static CS_MayaPreviewMaterialCatalog Rebuild(bool verbose)
    {
        CS_MayaPreviewMaterialCatalog catalog = LoadOrCreate();

        string[] folders = (catalog.searchFolders ?? Array.Empty<string>())
            .Where(folder => !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
            .ToArray();

        if (folders.Length == 0)
        {
            folders = new[] { "Assets" };
        }

        var entries = new List<CS_MayaPreviewMaterialCatalog.Entry>();
        var usedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<string>();

        // パス順に並べて、同名があっても毎回同じものが選ばれるようにする
        IEnumerable<string> paths = AssetDatabase.FindAssets("t:Material", folders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Replace('\\', '/').Contains("/Editor/"))
            .Distinct()
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (string path in paths)
        {
            string name = Path.GetFileNameWithoutExtension(path);

            if (!string.IsNullOrEmpty(catalog.namePrefix) &&
                !name.StartsWith(catalog.namePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (usedNames.TryGetValue(name, out string firstPath))
            {
                duplicates.Add(name + "\n    使用 : " + firstPath + "\n    無視 : " + path);
                continue;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                continue;
            }

            usedNames.Add(name, path);

            entries.Add(new CS_MayaPreviewMaterialCatalog.Entry
            {
                name = name,
                guid = AssetDatabase.AssetPathToGUID(path),
                material = material
            });
        }

        // 変わった時だけ保存（無駄な差分を出さない）
        if (!SameEntries(catalog.entries, entries))
        {
            catalog.entries = entries;
            catalog.InvalidateLookup();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        if (duplicates.Count > 0)
        {
            Debug.LogWarning(
                "Maya Preview Material Catalog : 同じ名前のMaterialがあります。先に見つかった方を使います。\n" +
                string.Join("\n", duplicates),
                catalog
            );
        }

        if (verbose || entries.Count == 0)
        {
            Debug.Log(
                "Maya Preview Material Catalog : " + entries.Count + " 件登録 (" +
                string.Join(", ", folders) + " / 接頭辞 \"" + catalog.namePrefix + "\")",
                catalog
            );
        }

        return catalog;
    }


    // =========================================================
    // 内部処理
    // =========================================================

    private static CS_MayaPreviewMaterialCatalog LoadOrCreate()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CS_MayaPreviewMaterialCatalog>(ASSET_PATH);

        if (catalog != null)
        {
            return catalog;
        }

        EnsureFolder(Path.GetDirectoryName(ASSET_PATH).Replace('\\', '/'));

        catalog = ScriptableObject.CreateInstance<CS_MayaPreviewMaterialCatalog>();
        AssetDatabase.CreateAsset(catalog, ASSET_PATH);

        return catalog;
    }


    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }


    private static bool SameEntries(
        List<CS_MayaPreviewMaterialCatalog.Entry> a,
        List<CS_MayaPreviewMaterialCatalog.Entry> b)
    {
        if (a == null || a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] == null || a[i].name != b[i].name ||
                a[i].guid != b[i].guid || a[i].material != b[i].material)
            {
                return false;
            }
        }

        return true;
    }
}
