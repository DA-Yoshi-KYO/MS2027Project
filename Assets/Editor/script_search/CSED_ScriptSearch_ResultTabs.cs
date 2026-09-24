/* ================================================
 * Hierarchy / Prefab 検索結果のタブ切り替えGUI
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-02-15 | 初回作成
 * 2026-09-06 | GUI構成整理・検索結果表示調整
 * 2026-09-07 | コメント・履歴・UI微調整
 * 2026-09-23 | CSED_へ改名・コメント形式を統一
 * ================================================ */

#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Hierarchy / Prefab 検索結果のタブ切り替えGUI。
/// </summary>
public partial class CSED_ScriptSearch
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

    /// <summary>
    /// 現在のタブ選択番号を、結果表示先の列挙値として返す。
    /// </summary>
    private ResultTab GetCurrentResultTab()
    {
        return (ResultTab)_resultTabIndex;
    }
}
#endif
