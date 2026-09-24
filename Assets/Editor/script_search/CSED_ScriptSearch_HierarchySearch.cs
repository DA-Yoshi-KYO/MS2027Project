/* ================================================
 * ScriptSearchツールのHierarchy検索（検索ロジック担当）
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
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ScriptSearchツールのHierarchy検索（検索ロジック担当）。
/// </summary>
public partial class CSED_ScriptSearch
{
    /// <summary>
    /// Scene内でScriptが見つかった結果
    /// </summary>
    private class HierarchySearchResult
    {
        public string SceneName;
        public string ScenePath;
        public string HierarchyPath;
    }

    private readonly List<HierarchySearchResult> _hierarchyResults
        = new List<HierarchySearchResult>(256);

    private string _hierarchyResultMessage = "まだ検索していません。";


    /// <summary>
    /// 検索結果メッセージを更新して再描画する。nullや空文字は空の表示へ置き換える。
    /// </summary>
    private void SetHierarchyResultMessage(string message)
    {
        _hierarchyResultMessage = string.IsNullOrEmpty(message)
            ? ""
            : message;

        Repaint();
    }


    /// <summary>
    /// Assets内に存在する全Sceneを検索
    /// </summary>
    private void ExecuteHierarchySearch()
    {
        _hierarchyResults.Clear();

        if (_targetScript == null)
        {
            SetHierarchyResultMessage("検索対象Scriptを指定してください。");
            return;
        }

        Type scriptType = _targetScript.GetClass();

        if (scriptType == null)
        {
            SetHierarchyResultMessage(
                "Scriptからクラス型を取得できませんでした。"
            );
            return;
        }

        if (!typeof(Component).IsAssignableFrom(scriptType))
        {
            SetHierarchyResultMessage(
                "MonoBehaviour系のScriptを指定してください。"
            );
            return;
        }


        // Assets以下に存在する全Scene
        string[] sceneGuids =
        AssetDatabase.FindAssets(
            "t:Scene",
            new[] { "Assets" }
        );

        Scene activeSceneBefore = SceneManager.GetActiveScene();

        try
        {
            for (int i = 0 ; i < sceneGuids.Length ; i++)
            {
                string scenePath =
                    AssetDatabase.GUIDToAssetPath(sceneGuids[i]);

                if (string.IsNullOrEmpty(scenePath))
                {
                    continue;
                }

                // Assets以外は絶対に検索しない
                if (!scenePath.StartsWith("Assets/"))
                {
                    continue;
                }


                EditorUtility.DisplayProgressBar(
                    "ScriptSearch",
                    $"Searching Scene... {i + 1}/{sceneGuids.Length}\n{scenePath}",
                    (float)(i + 1) / sceneGuids.Length
                );


                Scene targetScene = default;
                bool openedBySearch = false;


                // 既に開いているSceneか調べる
                for (int s = 0 ; s < SceneManager.sceneCount ; s++)
                {
                    Scene loadedScene =
                        SceneManager.GetSceneAt(s);

                    if (loadedScene.path == scenePath)
                    {
                        targetScene = loadedScene;
                        break;
                    }
                }


                try
                {
                    // 開いていなければAdditiveで一時的に開く
                    if (!targetScene.IsValid())
                    {
                        targetScene =
                            EditorSceneManager.OpenScene(
                                scenePath,
                                OpenSceneMode.Additive
                            );

                        openedBySearch = true;
                    }


                    SearchScene(
                        targetScene,
                        scenePath,
                        scriptType
                    );
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"ScriptSearch: Scene検索失敗\n" +
                        $"{scenePath}\n" +
                        $"{e.Message}"
                    );
                }
                finally
                {
                    // 検索用に開いたSceneだけ閉じる
                    if (openedBySearch &&
                        targetScene.IsValid())
                    {
                        EditorSceneManager.CloseScene(
                            targetScene,
                            true
                        );
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (activeSceneBefore.IsValid())
            {
                SceneManager.SetActiveScene(
                    activeSceneBefore
                );
            }
        }


        _hierarchyResults.Sort(
            (a, b) =>
            {
                int sceneCompare =
                    string.Compare(
                        a.ScenePath,
                        b.ScenePath,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (sceneCompare != 0)
                {
                    return sceneCompare;
                }

                return string.Compare(
                    a.HierarchyPath,
                    b.HierarchyPath,
                    StringComparison.OrdinalIgnoreCase
                );
            }
        );


        SetHierarchyResultMessage(
            $"Hit Objects: {_hierarchyResults.Count}"
        );
    }


    /// <summary>
    /// Scene単体を検索
    /// </summary>
    private void SearchScene(
        Scene scene,
        string scenePath,
        Type scriptType
    )
    {
        if (!scene.IsValid())
        {
            return;
        }

        GameObject[] roots =
            scene.GetRootGameObjects();

        HashSet<string> unique =
            new HashSet<string>(
                StringComparer.Ordinal
            );


        foreach (GameObject root in roots)
        {
            Component[] components =
                root.GetComponentsInChildren(
                    scriptType,
                    true
                );

            foreach (Component component in components)
            {
                if (component == null)
                {
                    continue;
                }

                string hierarchyPath =
                    BuildHierarchyPath(
                        component.gameObject
                    );

                if (!unique.Add(hierarchyPath))
                {
                    continue;
                }


                _hierarchyResults.Add(
                    new HierarchySearchResult
                    {
                        SceneName =
                            System.IO.Path
                                .GetFileNameWithoutExtension(
                                    scenePath
                                ),

                        ScenePath = scenePath,

                        HierarchyPath =
                            hierarchyPath
                    }
                );
            }
        }
    }


    /// <summary>
    /// 親をたどり、ルートから対象までの名前をスラッシュで連結したパスを返す。
    /// </summary>
    private static string BuildHierarchyPath(
        GameObject go
    )
    {
        if (go == null)
        {
            return "";
        }

        string path = go.name;

        Transform parent =
            go.transform.parent;

        while (parent != null)
        {
            path =
                parent.name + "/" + path;

            parent = parent.parent;
        }

        return path;
    }
}

#endif

