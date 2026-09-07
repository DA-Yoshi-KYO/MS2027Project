/*
+=====================================
 ファイル名 : CSE_ScriptSearch_ResultStyle.cs
 概要     : 検索結果表示で共通利用するGUIスタイルとクリック処理
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
    /// 通常の検索結果Value表示スタイル。
    /// </summary>
    private static GUIStyle CreateResultValueStyle(
        bool bold = false
    )
    {
        GUIStyle style =
            new GUIStyle(
                bold
                    ? EditorStyles.boldLabel
                    : EditorStyles.label
            );

        style.fontSize = 12;

        return style;
    }


    /// <summary>
    /// Object名表示用スタイル。
    /// </summary>
    private static GUIStyle CreateObjectValueStyle(
        bool bold = false
    )
    {
        GUIStyle style =
            CreateResultValueStyle(
                bold
            );

        style.normal.textColor =
            new Color(
                0.30f,
                0.65f,
                1.00f,
                1.00f
            );

        style.hover.textColor =
            style.normal.textColor;

        style.active.textColor =
            style.normal.textColor;

        return style;
    }


    /// <summary>
    /// クリック可能なPath表示用スタイル。
    /// </summary>
    private static GUIStyle CreatePathButtonStyle(
        bool bold = false
    )
    {
        GUIStyle style =
            new GUIStyle(
                bold
                    ? EditorStyles.boldLabel
                    : EditorStyles.label
            );

        style.fontSize = 12;
        style.alignment = TextAnchor.MiddleLeft;

        style.normal.textColor =
            new Color(
                1.00f,
                0.82f,
                0.20f,
                1.00f
            );

        style.hover.textColor =
            new Color(
                1.00f,
                0.92f,
                0.45f,
                1.00f
            );

        style.active.textColor =
            new Color(
                1.00f,
                0.70f,
                0.10f,
                1.00f
            );

        return style;
    }



    /// <summary>
    /// Object名を青色で描画する。
    /// Hover / Click判定は実際の文字列上だけに限定する。
    /// </summary>
    private static bool DrawHoverableObject(
        Rect rect,
        string objectName,
        bool bold = false
    )
    {
        string displayText =
            objectName ?? "";

        GUIStyle style =
            CreateResultValueStyle(
                bold
            );

        Vector2 textSize =
            style.CalcSize(
                new GUIContent(displayText)
            );

        Rect textRect =
            new Rect(
                rect.x,
                rect.y,
                Mathf.Min(
                    textSize.x + 4.0f,
                    rect.width
                ),
                rect.height
            );

        bool isHovered =
            textRect.Contains(
                Event.current.mousePosition
            );

        style.normal.textColor =
            isHovered
                ? new Color(
                    0.50f,
                    0.80f,
                    1.00f,
                    1.00f
                )
                : new Color(
                    0.30f,
                    0.65f,
                    1.00f,
                    1.00f
                );

        EditorGUI.LabelField(
            textRect,
            displayText,
            style
        );

        return
            Event.current.type == EventType.MouseDown &&
            textRect.Contains(
                Event.current.mousePosition
            );
    }


    /// <summary>
    /// Pathを黄色のクリック可能テキストとして描画する。
    /// 実際のPath文字列上をクリックした場合のみコピーする。
    /// 戻り値はPath文字列がクリックされた場合true。
    /// </summary>
    private static bool DrawCopyablePath(
        Rect rect,
        string path,
        bool bold = false
    )
    {
        string displayText =
            path ?? "";

        GUIStyle pathStyle =
            CreatePathButtonStyle(
                bold
            );

        Vector2 textSize =
            pathStyle.CalcSize(
                new GUIContent(displayText)
            );

        Rect clickableRect =
            new Rect(
                rect.x,
                rect.y,
                Mathf.Min(
                    textSize.x + 4.0f,
                    rect.width
                ),
                rect.height
            );

        EditorGUIUtility.AddCursorRect(
            clickableRect,
            MouseCursor.Link
        );

        bool clicked =
            GUI.Button(
                clickableRect,
                displayText,
                pathStyle
            );

        if (clicked)
        {
            EditorGUIUtility.systemCopyBuffer =
                displayText;
        }

        return clicked;
    }


    /// <summary>
    /// 検索結果ブロックのHover / Selected背景を描画する。
    /// </summary>
    private static void DrawResultBlockBackground(
        Rect blockRect,
        bool isHovered,
        bool isSelected
    )
    {
        if (isSelected)
        {
            EditorGUI.DrawRect(
                blockRect,
                new Color(
                    0.18f,
                    0.32f,
                    0.48f,
                    0.55f
                )
            );

            return;
        }

        if (isHovered)
        {
            EditorGUI.DrawRect(
                blockRect,
                new Color(
                    1.00f,
                    1.00f,
                    1.00f,
                    0.06f
                )
            );
        }
    }


    /// <summary>
    /// 検索結果同士を区切る横線を描画する。
    /// </summary>
    private static void DrawResultSeparator()
    {
        Rect rect =
            EditorGUILayout.GetControlRect(
                false,
                1.0f
            );

        EditorGUI.DrawRect(
            rect,
            new Color(
                0.35f,
                0.35f,
                0.35f,
                1.0f
            )
        );
    }
}

#endif
