/*
+=====================================
 ファイル名 : CSE_ScriptSearch_AssetsResultJump.cs
 概要     : Prefab検索結果の表示と対象Prefabへのジャンプ処理
 作者     : ヨシモト リョウ
 履歴     : 2026/02/15 新規作成
           2026/09/06 GUI構成整理・検索結果表示調整
           2026/09/07 コメント・履歴・UI微調整
=====================================+
*/

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class CSE_ScriptSearch
{
    private Vector2 _assetsScroll;
    private int _assetsSelectedIndex = -1;

    /// <summary>
    /// Prefab検索結果をHierarchy結果と同じ形式で描画する。
    /// Pathクリックでコピー、ブロックのダブルクリックでPrefabを開く。
    /// </summary>
    private void DrawAssetsResultsView()
    {
        GUIStyle resultMessageStyle =
            new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };

        EditorGUILayout.LabelField(
            _assetsResultMessage,
            resultMessageStyle
        );

        GUILayout.Space(8.0f);

        if (_assetsHitPrefabPaths.Count <= 0)
        {
            EditorGUILayout.LabelField(
                "ヒットなし",
                EditorStyles.miniBoldLabel
            );

            return;
        }

        _assetsScroll =
            EditorGUILayout.BeginScrollView(
                _assetsScroll,
                GUILayout.Height(
                    GetResultScrollHeight()
                )
            );

        for (int i = 0; i < _assetsHitPrefabPaths.Count; i++)
        {
            string path =
                _assetsHitPrefabPaths[i];

            string prefabName =
                Path.GetFileNameWithoutExtension(
                    path
                );

            bool isSelected =
                i == _assetsSelectedIndex;

            GUIStyle valueStyle =
                CreateResultValueStyle(
                    isSelected
                );
DrawResultSeparator();

            GUILayout.Space(6.0f);

            const float blockHeight = 68.0f;
            const float labelWidth = 58.0f;
            const float rowHeight = 20.0f;
            const float rowGap = 2.0f;

            Rect blockRect =
                GUILayoutUtility.GetRect(
                    GUIContent.none,
                    GUIStyle.none,
                    GUILayout.Height(blockHeight),
                    GUILayout.ExpandWidth(true)
                );

            
            bool isHovered =
                blockRect.Contains(
                    Event.current.mousePosition
                );

            DrawResultBlockBackground(
                blockRect,
                isHovered,
                isSelected
            );

float contentX =
                blockRect.x + 8.0f;

            float valueX =
                contentX + labelWidth;

            float valueWidth =
                blockRect.width - labelWidth - 16.0f;

            Rect prefabLabelRect =
                new Rect(
                    contentX,
                    blockRect.y,
                    labelWidth,
                    rowHeight
                );

            Rect prefabValueRect =
                new Rect(
                    valueX,
                    blockRect.y,
                    valueWidth,
                    rowHeight
                );

            Rect pathLabelRect =
                new Rect(
                    contentX,
                    blockRect.y + rowHeight + rowGap,
                    labelWidth,
                    rowHeight
                );

            Rect pathValueRect =
                new Rect(
                    valueX,
                    blockRect.y + rowHeight + rowGap,
                    valueWidth,
                    rowHeight
                );

            Rect objectLabelRect =
                new Rect(
                    contentX,
                    blockRect.y + (rowHeight + rowGap) * 2.0f,
                    labelWidth,
                    rowHeight
                );

            Rect objectValueRect =
                new Rect(
                    valueX,
                    blockRect.y + (rowHeight + rowGap) * 2.0f,
                    valueWidth,
                    rowHeight
                );

            // Prefab
            EditorGUI.LabelField(
                prefabLabelRect,
                "Prefab :",
                EditorStyles.label
            );

            EditorGUI.LabelField(
                prefabValueRect,
                prefabName,
                valueStyle
            );

            // Path
            EditorGUI.LabelField(
                pathLabelRect,
                "Path :",
                EditorStyles.label
            );

            bool pathClicked =
                DrawCopyablePath(
                    pathValueRect,
                    path,
                    isSelected
                );

            // Object
            EditorGUI.LabelField(
                objectLabelRect,
                "Object :",
                EditorStyles.label
            );

            bool objectClicked =
                DrawHoverableObject(
                    objectValueRect,
                    prefabName,
                    isSelected
                );

            // ----------------------------
            // クリック成功時の選択処理
            // Pathコピー成功、またはObjectクリック成功時だけ
            // この検索結果を選択状態にする。
            // ----------------------------
            if (pathClicked)
            {
                _assetsSelectedIndex = i;
            }

            if (objectClicked)
            {
                Event e =
                    Event.current;

                _assetsSelectedIndex = i;

                if (e.clickCount == 2)
                {
                    JumpToAsset(
                        path
                    );
                }

                e.Use();
            }

            GUILayout.Space(6.0f);
        }

        DrawResultSeparator();

        EditorGUILayout.EndScrollView();
    }


    /// <summary>
    /// Prefabアセットへジャンプする。
    /// </summary>
    private static void JumpToAsset(
        string assetPath
    )
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        Object obj =
            AssetDatabase.LoadMainAssetAtPath(
                assetPath
            );

        if (obj == null)
        {
            return;
        }

        EditorUtility.FocusProjectWindow();

        Selection.activeObject = obj;

        EditorGUIUtility.PingObject(obj);

        AssetDatabase.OpenAsset(obj);
    }
}
#endif
