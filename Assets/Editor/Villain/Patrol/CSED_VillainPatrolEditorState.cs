using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/*
 * 巡回エディタの状態(今の道具・選んでいる悪人とポイント・プレビューの再生)と、共通の処理
 * ツールバー(CSED_VillainPatrolOverlay)・Sceneビュー(CSED_VillainPatrolSceneGUI)・Inspector(CSED_VillainPatrolRouteEditor)が共有する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・編集の対象は、Hierarchyで選んでいるスポーン位置(CS_VillainSpawnPoint)の巡回パターン
 * ・パターンのアセットは Assets/Programmer/Database/Villain/Patrol に作る(「悪人を置く」で無ければ自動で作る)
 * ・選んでいる悪人・ポイント・道具は、スポーン位置の選択が変わったら外す
 * ・状態が変わったら onChanged を呼び、Sceneビュー・ツールバー・Inspectorを描き直す
 */
// ========================================

public static class CSED_VillainPatrolEditorState
{
    public enum Tool
    {
        None,           // 何も押されていない(Sceneビューは普段どおりのUnityの操作)
        PlaceVillain,   // 悪人を置く
        PlacePoint,     // ポイントを置く
    }

    private const string _patternFolder = "Assets/Programmer/Database/Villain/Patrol";

    private static Tool _tool;
    private static int _selectedRoute = -1;
    private static int _selectedPoint = -1;
    private static bool _isPlaying;
    private static double _playStartTime;

    public static event Action onChanged;

    public static Tool tool
    {
        get => _tool;
        set { _tool = value; NotifyChanged(); }
    }
    public static int selectedRoute => _selectedRoute;
    public static int selectedPoint => _selectedPoint;
    public static bool isPlaying => _isPlaying;

    // プレビューの再生時間(秒)
    public static float playTime => _isPlaying ? (float)(EditorApplication.timeSinceStartup - _playStartTime) : 0f;

    // Hierarchyで選んでいるスポーン位置。選んでいなければnull
    public static CS_VillainSpawnPoint GetSpawnPoint()
    {
        GameObject selected = Selection.activeGameObject;
        return selected != null ? selected.GetComponent<CS_VillainSpawnPoint>() : null;
    }

    // 選んでいるスポーン位置の巡回(CS_VillainPatrolRoute)。付いていなければnull
    public static CS_VillainPatrolRoute GetRoute()
    {
        CS_VillainSpawnPoint spawnPoint = GetSpawnPoint();
        return spawnPoint != null ? spawnPoint.GetComponent<CS_VillainPatrolRoute>() : null;
    }

    public static void Select(int routeIndex, int pointIndex)
    {
        _selectedRoute = routeIndex;
        _selectedPoint = pointIndex;
        NotifyChanged();
    }

    // 選択・道具・再生を外す(スポーン位置の選択が変わった時)
    public static void Reset()
    {
        _tool = Tool.None;
        _selectedRoute = -1;
        _selectedPoint = -1;
        _isPlaying = false;
        NotifyChanged();
    }

    public static void SetPlaying(bool isPlaying)
    {
        _isPlaying = isPlaying;
        _playStartTime = EditorApplication.timeSinceStartup;
        NotifyChanged();
    }

    public static void NotifyChanged()
    {
        onChanged?.Invoke();
        SceneView.RepaintAll();
    }

    // スポーン位置に巡回とパターンが無ければ作る(「悪人を置く」で最初に置く時)
    public static CS_VillainPatrolRoute EnsurePattern(CS_VillainSpawnPoint spawnPoint)
    {
        CS_VillainPatrolRoute route = spawnPoint.GetComponent<CS_VillainPatrolRoute>();
        if (route == null) route = Undo.AddComponent<CS_VillainPatrolRoute>(spawnPoint.gameObject);

        if (route.pattern == null)
        {
            Undo.RecordObject(route, "巡回パターンを作る");
            route.pattern = CreatePatternAsset(spawnPoint.name, null);
            MarkDirty(route);
        }
        return route;
    }

    // 新しいパターンのアセットを作る(source があればその写し)
    public static CSO_VillainPatrolPattern CreatePatternAsset(string baseName, CSO_VillainPatrolPattern source)
    {
        if (!AssetDatabase.IsValidFolder(_patternFolder))
        {
            Directory.CreateDirectory(_patternFolder);
            AssetDatabase.Refresh();
        }

        string path = AssetDatabase.GenerateUniqueAssetPath($"{_patternFolder}/DB_VillainPatrol_{baseName}.asset");
        if (source != null)
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path);
            return AssetDatabase.LoadAssetAtPath<CSO_VillainPatrolPattern>(path);
        }

        CSO_VillainPatrolPattern pattern = ScriptableObject.CreateInstance<CSO_VillainPatrolPattern>();
        AssetDatabase.CreateAsset(pattern, path);
        return pattern;
    }

    // 今のシーン(プレハブ編集中ならプレハブ)で、同じパターンを使っているスポーン位置の数
    public static int CountUsage(CSO_VillainPatrolPattern pattern)
    {
        if (pattern == null) return 0;

        int count = 0;
        foreach (CS_VillainPatrolRoute route in StageUtility.GetCurrentStageHandle().FindComponentsOfType<CS_VillainPatrolRoute>())
        {
            if (route.pattern == pattern) count++;
        }
        return count;
    }

    // スポーン位置(シーン・プレハブ)の変更を保存対象にする
    public static void MarkDirty(UnityEngine.Object target)
    {
        EditorUtility.SetDirty(target);
        if (target is Component component) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }

    // パターンの変更を保存対象にし、スポーン位置のシーン(プレハブ)も変更ありにする(シーンの保存と一緒に保存するため)
    public static void MarkPatternDirty(CS_VillainPatrolRoute route)
    {
        if (route == null || route.pattern == null) return;

        EditorUtility.SetDirty(route.pattern);
        EditorSceneManager.MarkSceneDirty(route.gameObject.scene);
    }
}
