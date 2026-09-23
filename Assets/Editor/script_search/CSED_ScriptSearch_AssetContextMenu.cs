/* ================================================
 * Project上のScript右クリックメニューからScriptSearchを実行
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
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project上のScript右クリックメニューからScriptSearchを実行。
/// </summary>
public partial class CSED_ScriptSearch
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
