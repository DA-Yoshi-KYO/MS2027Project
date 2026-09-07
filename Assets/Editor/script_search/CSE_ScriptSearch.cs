/*
+=====================================
 ファイル名 : CSE_ScriptSearch.cs
 概要     : ScriptSearchツールのEditorWindow本体
 作者     : ヨシモト リョウ
 履歴     : 2026/02/15 新規作成
           2026/09/06 GUI構成整理・検索結果表示調整
           2026/09/07 コメント・履歴・UI微調整
=====================================+
*/

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ScriptSearchツール本体。
/// GUIの詳細はpartial classで分割して管理する。
/// </summary>
public partial class CSE_ScriptSearch : EditorWindow
{
    [MenuItem("Tools/ScriptSearch")]
    public static void ShowWindow()
    {
        GetOrCreateWindow();
    }


    /// <summary>
    /// ScriptSearchウィンドウを取得して表示する。
    /// </summary>
    private static CSE_ScriptSearch GetOrCreateWindow()
    {
        CSE_ScriptSearch window =
            GetWindow<CSE_ScriptSearch>("ScriptSearch");

        window.minSize = new Vector2(
            500.0f,
            400.0f
        );

        window.Show();

        window.Focus();

        return window;
    }


    /// <summary>
    /// 指定ScriptをTarget Scriptへ設定して、そのまま検索を実行する。
    /// </summary>
    public static void OpenAndSearch(
        MonoScript targetScript
    )
    {
        if (targetScript == null)
        {
            return;
        }

        CSE_ScriptSearch window =
            GetOrCreateWindow();

        window._targetScript =
            targetScript;

        window.ExecuteSearch();
    }

    private void OnEnable()
    {
    }

    private void OnGUI()
    {
        DrawMainLayout();
    }
}
#endif
