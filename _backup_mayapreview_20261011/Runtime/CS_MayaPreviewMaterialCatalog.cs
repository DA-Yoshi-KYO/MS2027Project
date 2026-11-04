using System;
using System.Collections.Generic;

using UnityEngine;

/*
+=====================================
 ファイル名 : CS_MayaPreviewMaterialCatalog
 概要       : Mayaから届いたマテリアル名をUnityのMaterialに引き当てる一覧
              ・中身はEditorの CSE_MayaPreviewMaterialCatalogBuilder が自動生成する
              ・Resources配下に置き、ビルド後のPreviewHost.exeからも読めるようにする
 作者       : ヨシモト リョウ
 履歴       : 2026/10/10 新規作成
=====================================+
*/

public class CS_MayaPreviewMaterialCatalog : ScriptableObject
{
    // =========================================================
    // 定数
    // =========================================================

    // Resources.Load 用パス（拡張子なし）
    public const string RESOURCES_PATH =
        "MayaPreview/MayaPreviewMaterialCatalog";


    // =========================================================
    // データ
    // =========================================================

    [Serializable]
    public class Entry
    {
        // Mayaから届く名前（= Unity Material名）
        public string name;

        // .mat の GUID（書き戻し時に保存先を特定するため）
        public string guid;

        public Material material;
    }


    [Tooltip("Materialを探すフォルダ（Assets/ から始まるパス）")]
    public string[] searchFolders = { "Assets" };

    [Tooltip("この接頭辞で始まるMaterialだけを登録する。空なら全て")]
    public string namePrefix = "MT_";

    public List<Entry> entries = new List<Entry>();


    // 名前 → Entry（大文字小文字は区別しない）
    [NonSerialized]
    private Dictionary<string, Entry> _lookup;


    // =========================================================
    // 読み込み
    // =========================================================

    public static CS_MayaPreviewMaterialCatalog Load()
    {
        return Resources.Load<CS_MayaPreviewMaterialCatalog>(
            RESOURCES_PATH
        );
    }


    // =========================================================
    // 名前検索
    // =========================================================

    public static string NormalizeName(string materialName)
    {
        if (string.IsNullOrEmpty(materialName))
        {
            return string.Empty;
        }

        // Mayaのネームスペース（ns:MT_xxx）を外す
        int colon = materialName.LastIndexOf(':');

        return (colon >= 0 ? materialName.Substring(colon + 1) : materialName).Trim();
    }


    public Entry FindEntry(string materialName)
    {
        string key = NormalizeName(materialName);

        if (key.Length == 0)
        {
            return null;
        }

        if (_lookup == null)
        {
            _lookup = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

            foreach (Entry entry in entries)
            {
                if (entry != null && entry.material != null &&
                    !string.IsNullOrEmpty(entry.name) &&
                    !_lookup.ContainsKey(entry.name))
                {
                    _lookup.Add(entry.name, entry);
                }
            }
        }

        return _lookup.TryGetValue(key, out Entry found) ? found : null;
    }


    public Material Find(string materialName)
    {
        return FindEntry(materialName)?.material;
    }


    // Editorで一覧を作り直した時に検索表を捨てる
    public void InvalidateLookup()
    {
        _lookup = null;
    }


    private void OnEnable()
    {
        _lookup = null;
    }
}
