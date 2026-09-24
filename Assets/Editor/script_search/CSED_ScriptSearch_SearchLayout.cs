/* ================================================
 * ScriptSearchツールの検索設定GUI
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
/// ScriptSearchツールの検索設定GUI。
/// </summary>
public partial class CSED_ScriptSearch
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
