/*
+=====================================
 ファイル名 : CSE_ScriptSearch_ResultTabs.cs
 概要     : Hierarchy / Prefab 検索結果のタブ切り替えGUI
 作者     : ヨシモト リョウ
 履歴     : 2026/02/15 新規作成
           2026/09/06 GUI構成整理・検索結果表示調整
           2026/09/07 コメント・履歴・UI微調整
=====================================+
*/

#if UNITY_EDITOR
using UnityEngine;

public partial class CSE_ScriptSearch
{
    private enum ResultTab
    {
        Hierarchy = 0,
        Prefab = 1
    }

    private int _resultTabIndex = 0;

    /// <summary>
    /// Hierarchy / Prefab の切り替えタブを描画する。
    /// </summary>
    private void DrawResultTabBar()
    {
        int hierarchyCount =
            _hierarchyResults != null
                ? _hierarchyResults.Count
                : 0;

        int prefabCount =
            _assetsHitPrefabPaths != null
                ? _assetsHitPrefabPaths.Count
                : 0;

        string[] tabs =
        {
            $"Hierarchy ({hierarchyCount})",
            $"Prefab ({prefabCount})"
        };

        _resultTabIndex =
            GUILayout.Toolbar(
                _resultTabIndex,
                tabs
            );
    }

    private ResultTab GetCurrentResultTab()
    {
        return (ResultTab)_resultTabIndex;
    }
}
#endif
