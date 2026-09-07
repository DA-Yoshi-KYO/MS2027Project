/*
+=====================================
 ファイル名 : CSE_ScriptSearch_ResultLayout.cs
 概要     : ScriptSearchツールの検索結果専用GUIレイアウト
 作者     : ヨシモト リョウ
 履歴     : 2026/02/15 新規作成
           2026/09/06 GUI構成整理・検索結果表示調整
           2026/09/07 コメント・履歴・UI微調整
=====================================+
*/

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class CSE_ScriptSearch
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
