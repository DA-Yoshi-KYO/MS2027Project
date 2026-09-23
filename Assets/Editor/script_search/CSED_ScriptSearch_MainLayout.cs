/* ================================================
 * ScriptSearchツール全体のGUIレイアウト
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
/// ScriptSearchツール全体のGUIレイアウト。
/// </summary>
public partial class CSED_ScriptSearch
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
