/*
+=====================================
 ファイル名 : CSE_ScriptSearch_HelpLink.cs
 概要     : ScriptSearchツール左下の使い方URLリンク表示
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
    private const string HelpUrl =
        "https://summervacationgamejam.atlassian.net/wiki/spaces/20272/pages/13860865/Unity";


    /// <summary>
    /// Tool左下に「使い方URL」リンクを表示する。
    /// </summary>
    private void DrawHelpLink()
    {
        GUIStyle linkStyle =
            new GUIStyle(EditorStyles.label);

        linkStyle.normal.textColor =
            new Color(
                0.25f,
                0.60f,
                1.00f,
                1.00f
            );

        linkStyle.hover.textColor =
            new Color(
                0.45f,
                0.75f,
                1.00f,
                1.00f
            );

        linkStyle.active.textColor =
            new Color(
                0.15f,
                0.45f,
                0.90f,
                1.00f
            );

        linkStyle.fontSize = 13;

        GUIContent content =
            new GUIContent(
                "使い方URL"
            );

        Vector2 textSize =
            linkStyle.CalcSize(
                content
            );

        using (new GUILayout.HorizontalScope())
        {
            // 左側に少し余白を入れて、ウィンドウ端へ寄りすぎないようにする。
            GUILayout.Space(16.0f);

            Rect linkRect =
                GUILayoutUtility.GetRect(
                    textSize.x + 4.0f,
                    20.0f,
                    GUILayout.ExpandWidth(false)
                );

            EditorGUIUtility.AddCursorRect(
                linkRect,
                MouseCursor.Link
            );

            if (
                GUI.Button(
                    linkRect,
                    content,
                    linkStyle
                )
            )
            {
                Application.OpenURL(
                    HelpUrl
                );
            }

            GUILayout.FlexibleSpace();
        }
    }
}

#endif
