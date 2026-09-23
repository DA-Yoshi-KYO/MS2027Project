/* ================================================
 * ScriptSearchツールのSearchボタンGUI
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
/// ScriptSearch：SearchボタンUI（partial）
/// </summary>
public partial class CSED_ScriptSearch
{
    /// <summary>
    /// Searchボタン押下時に呼ばれる処理（別CSで実装する）
    /// </summary>
    partial void OnClickSearchButton();

    /// <summary>
    /// Searchボタンを描画する。
    /// </summary>
    private void DrawSearchButtonField()
    {
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            // ちょい大きめにしたい場合は Height を調整
            if (GUILayout.Button("Search", GUILayout.Width(160.0f), GUILayout.Height(28.0f)))
            {
                // ここでは処理しない（処理は別ファイルで）
                OnClickSearchButton();
            }

            GUILayout.FlexibleSpace();
        }
    }
}
#endif

