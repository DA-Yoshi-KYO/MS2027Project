/*
+=====================================
 ファイル名 : CSE_ScriptSearch_SearchLayout.cs
 概要     : ScriptSearchツールの検索設定GUI
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
    /// 検索対象ScriptとSearchボタンを描画する。
    /// </summary>
    private void DrawSearchLayout()
    {
        using (new GUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField(
                "検索設定",
                EditorStyles.boldLabel
            );

            GUILayout.Space(8.0f);

            DrawTargetScriptField();

            GUILayout.Space(10.0f);

            DrawSearchButtonField();
        }
    }
}
#endif
