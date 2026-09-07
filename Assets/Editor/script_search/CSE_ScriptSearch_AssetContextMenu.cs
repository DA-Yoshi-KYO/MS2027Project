/*
+=====================================
 ファイル名 : CSE_ScriptSearch_AssetContextMenu.cs
 概要     : Project上のScript右クリックメニューからScriptSearchを実行
 作者     : ヨシモト リョウ
 履歴     : 2026/02/15 新規作成
           2026/09/06 GUI構成整理・検索結果表示調整
           2026/09/07 コメント・履歴・UI微調整
=====================================+
*/

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public partial class CSE_ScriptSearch
{
    private const string SearchAttachedScriptMenuPath =
        "Assets/アタッチしているスクリプトを検索";


    /// <summary>
    /// Projectウィンドウで選択中のScriptをTarget Scriptに設定し、
    /// ScriptSearchを開いてそのまま検索する。
    /// </summary>
    [MenuItem(
        SearchAttachedScriptMenuPath,
        false,
        2000
    )]
    private static void SearchAttachedScriptFromProject()
    {
        MonoScript script =
            Selection.activeObject as MonoScript;

        if (script == null)
        {
            return;
        }

        OpenAndSearch(
            script
        );
    }


    /// <summary>
    /// GameObjectへアタッチ可能なComponent系Scriptを選択している時だけ
    /// 右クリックメニューを有効にする。
    /// </summary>
    [MenuItem(
        SearchAttachedScriptMenuPath,
        true
    )]
    private static bool ValidateSearchAttachedScriptFromProject()
    {
        MonoScript script =
            Selection.activeObject as MonoScript;

        if (script == null)
        {
            return false;
        }

        Type scriptType =
            script.GetClass();

        if (scriptType == null)
        {
            return false;
        }

        return typeof(Component).IsAssignableFrom(
            scriptType
        );
    }
}

#endif
