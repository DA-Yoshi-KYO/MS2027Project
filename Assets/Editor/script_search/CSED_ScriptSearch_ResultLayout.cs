/* ================================================
 * ScriptSearchツールの検索結果専用GUIレイアウト
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-02-15 | 初回作成
 * 2026-09-06 | GUI構成整理・検索結果表示調整
 * 2026-09-07 | コメント・履歴・UI微調整
 * 2026-09-23 | CSED_へ改名・コメント形式を統一
 * ================================================ */

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ScriptSearchツールの検索結果専用GUIレイアウト。
/// </summary>
public partial class CSED_ScriptSearch
{
    /// <summary>
    /// 検索欄の直下に検索結果専用領域を描画する。
    /// </summary>
    private void DrawResultLayout()
    {
        using (new GUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField(
                "検索結果",
                EditorStyles.boldLabel
            );

            GUILayout.Space(6.0f);

            DrawResultTabBar();

            GUILayout.Space(8.0f);

            switch (GetCurrentResultTab())
            {
                case ResultTab.Hierarchy:
                    DrawHierarchyResultsView();
                    break;

                case ResultTab.Prefab:
                    DrawAssetsResultsView();
                    break;
            }

            GUILayout.Space(14.0f);
        }
    }

    /// <summary>
    /// Windowサイズに合わせて検索結果ScrollViewの高さを調整する。
    /// </summary>
    private float GetResultScrollHeight()
    {
        return Mathf.Max(
            180.0f,
            position.height - 285.0f
        );
    }
}
#endif
