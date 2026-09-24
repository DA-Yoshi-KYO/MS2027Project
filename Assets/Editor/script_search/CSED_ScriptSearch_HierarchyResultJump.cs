/* ================================================
 * Hierarchy検索結果の表示と対象GameObjectへのジャンプ処理
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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hierarchy検索結果の表示と対象GameObjectへのジャンプ処理。
/// </summary>
public partial class CSED_ScriptSearch
{
    private Vector2 _hierarchyScroll;
    private int _hierarchySelectedIndex = -1;

    /// <summary>
    /// Hierarchy検索結果を描画する。
    /// </summary>
    private void DrawHierarchyResultsView()
    {
        GUIStyle resultMessageStyle =
            new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };

        EditorGUILayout.LabelField(
            _hierarchyResultMessage,
            resultMessageStyle
        );

        GUILayout.Space(8.0f);

        if (_hierarchyResults.Count <= 0)
        {
            EditorGUILayout.LabelField(
                "ヒットなし",
                EditorStyles.miniBoldLabel
            );

            return;
        }

        _hierarchyScroll =
            EditorGUILayout.BeginScrollView(
                _hierarchyScroll,
                GUILayout.Height(
                    GetResultScrollHeight()
                )
            );

        for (int i = 0; i < _hierarchyResults.Count; i++)
        {
            HierarchySearchResult result =
                _hierarchyResults[i];

            DrawResultSeparator();

            GUILayout.Space(6.0f);

            string objectName =
                GetObjectNameFromHierarchyPath(
                    result.HierarchyPath
                );

            bool isSelected =
                i == _hierarchySelectedIndex;

            GUIStyle valueStyle =
                CreateResultValueStyle(
                    isSelected
                );
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

            Rect sceneLabelRect =
                new Rect(
                    contentX,
                    blockRect.y,
                    labelWidth,
                    rowHeight
                );

            Rect sceneValueRect =
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

            // Scene
            EditorGUI.LabelField(
                sceneLabelRect,
                "Scene :",
                EditorStyles.label
            );

            EditorGUI.LabelField(
                sceneValueRect,
                result.SceneName,
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
                    result.HierarchyPath,
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
                    objectName,
                    isSelected
                );

            // ----------------------------
            // クリック成功時の選択処理
            // Pathコピー成功、またはObjectクリック成功時だけ
            // この検索結果を選択状態にする。
            // ----------------------------
            if (pathClicked)
            {
                _hierarchySelectedIndex = i;
            }

            if (objectClicked)
            {
                Event e =
                    Event.current;

                _hierarchySelectedIndex = i;

                if (e.clickCount == 2)
                {
                    JumpToHierarchyObject(
                        result.ScenePath,
                        result.HierarchyPath
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
    /// HierarchyPathの最後からObject名を取得する。
    /// </summary>
    private static string GetObjectNameFromHierarchyPath(
        string hierarchyPath
    )
    {
        if (string.IsNullOrEmpty(hierarchyPath))
        {
            return "";
        }

        string[] parts =
            hierarchyPath.Split('/');

        if (parts.Length <= 0)
        {
            return "";
        }

        return parts[parts.Length - 1];
    }


    /// <summary>
    /// Sceneを開き、対象GameObjectへジャンプする。
    /// </summary>
    private void JumpToHierarchyObject(
        string scenePath,
        string hierarchyPath
    )
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            return;
        }

        Scene targetScene =
            FindLoadedSceneByPath(
                scenePath
            );

        if (!targetScene.IsValid())
        {
            targetScene =
                EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Single
                );
        }

        if (!targetScene.IsValid())
        {
            EditorUtility.DisplayDialog(
                "ScriptSearch",
                $"Sceneを開けませんでした。\n{scenePath}",
                "OK"
            );

            return;
        }

        SceneManager.SetActiveScene(
            targetScene
        );

        GameObject go =
            FindGameObjectByHierarchyPath(
                targetScene,
                hierarchyPath
            );

        if (go == null)
        {
            EditorUtility.DisplayDialog(
                "ScriptSearch",
                $"GameObjectが見つかりませんでした。\n{hierarchyPath}",
                "OK"
            );

            return;
        }

        Selection.activeGameObject = go;

        EditorGUIUtility.PingObject(go);

        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }


    /// <summary>
    /// ロード済みSceneをPathから取得する。
    /// </summary>
    private static Scene FindLoadedSceneByPath(
        string scenePath
    )
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene =
                SceneManager.GetSceneAt(i);

            if (
                !string.IsNullOrEmpty(scene.path) &&
                scene.path == scenePath
            )
            {
                return scene;
            }
        }

        return default;
    }


    /// <summary>
    /// HierarchyPathからGameObjectを取得する。
    /// </summary>
    private static GameObject FindGameObjectByHierarchyPath(
        Scene scene,
        string hierarchyPath
    )
    {
        if (
            !scene.IsValid() ||
            string.IsNullOrEmpty(hierarchyPath)
        )
        {
            return null;
        }

        string[] parts =
            hierarchyPath.Split('/');

        if (parts.Length <= 0)
        {
            return null;
        }

        GameObject[] roots =
            scene.GetRootGameObjects();

        GameObject current = null;

        for (int i = 0; i < roots.Length; i++)
        {
            if (
                roots[i] != null &&
                roots[i].name == parts[0]
            )
            {
                current = roots[i];
                break;
            }
        }

        if (current == null)
        {
            return null;
        }

        for (int i = 1; i < parts.Length; i++)
        {
            string targetName =
                parts[i];

            Transform found = null;

            Transform parent =
                current.transform;

            for (
                int c = 0;
                c < parent.childCount;
                c++
            )
            {
                Transform child =
                    parent.GetChild(c);

                if (
                    child != null &&
                    child.name == targetName
                )
                {
                    found = child;
                    break;
                }
            }

            if (found == null)
            {
                return null;
            }

            current =
                found.gameObject;
        }

        return current;
    }
}
#endif
