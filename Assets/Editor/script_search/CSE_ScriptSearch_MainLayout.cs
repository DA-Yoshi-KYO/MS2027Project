/*
+=====================================
 ファイル名 : CSE_ScriptSearch_MainLayout.cs
 概要     : ScriptSearchツール全体のGUIレイアウト
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
    /// <summary>
    /// ウィンドウ全体を描画する。
    /// </summary>
    private void DrawMainLayout()
    {
        GUILayout.Space(10.0f);

        DrawSearchLayout();

        GUILayout.Space(10.0f);

        DrawResultLayout();

        GUILayout.FlexibleSpace();

        DrawHelpLink();

        GUILayout.Space(8.0f);
    }
}
#endif
