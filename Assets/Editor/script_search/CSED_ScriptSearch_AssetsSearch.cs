/* ================================================
 * ScriptSearchツールのPrefab検索（検索ロジック担当）
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-02-15 | 初回作成
 * 2026-09-06 | GUI構成整理・検索結果表示調整
 * 2026-09-07 | コメント・履歴・UI微調整
 * 2026-09-23 | CSED_へ改名・コメント形式を統一
 * ================================================ */

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ScriptSearchツールのPrefab検索（検索ロジック担当）。
/// </summary>
public partial class CSED_ScriptSearch
{
    // ----------------------------
    // Assets検索結果データ
    // ----------------------------
    private readonly List<string> _assetsHitPrefabPaths = new List<string>(256);
    private string _assetsResultMessage = "まだ検索していません。";

    /// <summary>
    /// Assets検索結果メッセージをセット
    /// </summary>
    private void SetAssetsResultMessage(string message)
    {
        _assetsResultMessage = string.IsNullOrEmpty(message) ? "" : message;
        Repaint();
    }

    /// <summary>
    /// Assets検索を実行する（Prefab内に指定Scriptが付いているPrefabを抽出）
    /// </summary>
    private void ExecuteAssetsSearch()
    {
        _assetsHitPrefabPaths.Clear();

        if (_targetScript == null)
        {
            SetAssetsResultMessage("検索対象Scriptが未設定だよ。");
            return;
        }

        Type scriptType = _targetScript.GetClass();
        if (scriptType == null)
        {
            SetAssetsResultMessage("Scriptからクラス型が取れなかったよ（コンパイルエラー等の可能性）。");
            return;
        }

        if (!typeof(Component).IsAssignableFrom(scriptType))
        {
            SetAssetsResultMessage("このScriptはComponentじゃないみたい（MonoBehaviour系のみAssets検索対象だよ）。");
            return;
        }

        // Prefabだけ検索（Assets内の対象）
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        if (guids == null || guids.Length <= 0)
        {
            SetAssetsResultMessage("Prefabが見つからなかったよ。");
            return;
        }

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;

                // 進捗（重いとき用）
                if ((i % 50) == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "ScriptSearch (Assets)",
                        $"Searching Prefabs... {i + 1}/{guids.Length}",
                        (float)(i + 1) / (float)guids.Length
                    );
                }

                // Prefabの中身をロードして調べる（Instantiateしない安全な方法）
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null) continue;

                    Component[] comps = root.GetComponentsInChildren(scriptType, true);
                    if (comps != null && comps.Length > 0)
                    {
                        _assetsHitPrefabPaths.Add(path);
                    }
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        _assetsHitPrefabPaths.Sort(StringComparer.OrdinalIgnoreCase);

        SetAssetsResultMessage(
            $"Script: {scriptType.Name}\n" +
            $"Hit Prefabs: {_assetsHitPrefabPaths.Count}\n" +
            $"（ダブルクリックでPrefabへ飛べるよ）"
        );
    }
}
#endif

