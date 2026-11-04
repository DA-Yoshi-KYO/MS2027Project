using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

/*
 * 巡回エディタのSceneビューのツールバー(Overlay)
 * スポーン位置(CS_VillainSpawnPoint)を選んでいる間だけ、Sceneビューに自動で出る
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・ボタン
 *   悪人を置く / ポイントを置く: 押している間だけ、Sceneビューのクリックで置く(もう一度押す・Escで外す)
 *   今ポイントを足している悪人を、ルートの色付きで表示する(悪人・ポイント・線をクリックすると切り替わる)
 *   ▶ 再生 / ■ 停止: プレイせずにSceneビューで悪人を動かす(CSED_VillainPatrolPreview)
 * ・パターン(アセット): 使っているパターン、同じパターンを使っているスポーン位置の数
 *   複製: 今のパターンを写した新しいアセットにする(このスポーン位置だけ変えたい時)
 * ・表示は IMGUIContainer で描く。状態が変わったら CSED_VillainPatrolEditorState.onChanged で描き直す
 */
// ========================================

[Overlay(typeof(SceneView), "VillainPatrolOverlay", "巡回", defaultDisplay = true)]
public class CSED_VillainPatrolOverlay : Overlay, ITransientOverlay
{
    private static readonly List<CSED_VillainPatrolOverlay> _overlays = new List<CSED_VillainPatrolOverlay>();

    private IMGUIContainer _container;

    // スポーン位置を選んでいる間だけ出す
    public bool visible => CSED_VillainPatrolEditorState.GetSpawnPoint() != null;

    public override void OnCreated()
    {
        _overlays.Add(this);
        CSED_VillainPatrolEditorState.onChanged += Repaint;
    }

    public override void OnWillBeDestroyed()
    {
        _overlays.Remove(this);
        CSED_VillainPatrolEditorState.onChanged -= Repaint;
    }

    public override VisualElement CreatePanelContent()
    {
        _container = new IMGUIContainer(OnGUI);
        _container.style.minWidth = 260f;
        return _container;
    }

    public static void RepaintAll()
    {
        foreach (CSED_VillainPatrolOverlay overlay in _overlays)
        {
            overlay.Repaint();
        }
    }

    private void Repaint()
    {
        _container?.MarkDirtyRepaint();
    }

    private void OnGUI()
    {
        CS_VillainSpawnPoint spawnPoint = CSED_VillainPatrolEditorState.GetSpawnPoint();
        if (spawnPoint == null) return;

        CS_VillainPatrolRoute route = spawnPoint.GetComponent<CS_VillainPatrolRoute>();
        CSO_VillainPatrolPattern pattern = route != null ? route.pattern : null;

        DrawPattern(spawnPoint, route, pattern);
        DrawTools();
        DrawCurrentRoute(pattern);
        DrawPlayback(pattern);
    }

    // 使っているパターンと、同じパターンを使っているスポーン位置の数・複製
    private static void DrawPattern(CS_VillainSpawnPoint spawnPoint, CS_VillainPatrolRoute route, CSO_VillainPatrolPattern pattern)
    {
        if (pattern == null)
        {
            EditorGUILayout.LabelField("「悪人を置く」で巡回を作ります", EditorStyles.miniLabel);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(pattern.name, EditorStyles.miniBoldLabel);
        if (GUILayout.Button("複製", EditorStyles.miniButton, GUILayout.Width(40f)))
        {
            Undo.RecordObject(route, "巡回パターンを複製");
            route.pattern = CSED_VillainPatrolEditorState.CreatePatternAsset(spawnPoint.name, pattern);
            CSED_VillainPatrolEditorState.MarkDirty(route);
            CSED_VillainPatrolEditorState.NotifyChanged();
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();

        int usage = CSED_VillainPatrolEditorState.CountUsage(pattern);
        if (usage > 1) EditorGUILayout.HelpBox($"このパターンを {usage} か所のスポーン位置で使っています。変更は全部に反映されます", MessageType.Info);
    }

    private static void DrawTools()
    {
        EditorGUILayout.BeginHorizontal();
        DrawToolButton("悪人を置く", CSED_VillainPatrolEditorState.Tool.PlaceVillain);
        DrawToolButton("ポイントを置く", CSED_VillainPatrolEditorState.Tool.PlacePoint);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawToolButton(string label, CSED_VillainPatrolEditorState.Tool tool)
    {
        bool isOn = CSED_VillainPatrolEditorState.tool == tool;
        bool pressed = GUILayout.Toggle(isOn, label, EditorStyles.miniButton);
        if (pressed == isOn) return;

        CSED_VillainPatrolEditorState.tool = pressed ? tool : CSED_VillainPatrolEditorState.Tool.None;
    }

    // 今ポイントを足している悪人(ルートの色付き)
    private static void DrawCurrentRoute(CSO_VillainPatrolPattern pattern)
    {
        int index = CSED_VillainPatrolEditorState.selectedRoute;
        bool hasRoute = pattern != null && index >= 0 && index < pattern.routes.Count;

        Rect rect = EditorGUILayout.GetControlRect();
        if (!hasRoute)
        {
            EditorGUI.LabelField(rect, "悪人なし", EditorStyles.miniLabel);
            return;
        }

        Rect swatch = new Rect(rect.x, rect.y + 3f, 12f, rect.height - 6f);
        EditorGUI.DrawRect(swatch, pattern.routes[index].color);
        rect.xMin += 18f;
        EditorGUI.LabelField(rect, $"{CSED_VillainPatrolLabels.GetRouteName(index)} に追加", EditorStyles.miniBoldLabel);
    }

    private static void DrawPlayback(CSO_VillainPatrolPattern pattern)
    {
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(pattern == null || pattern.routes.Count == 0))
        {
            bool isPlaying = CSED_VillainPatrolEditorState.isPlaying;
            if (GUILayout.Button(isPlaying ? "■ 停止" : "▶ 再生", EditorStyles.miniButton, GUILayout.Width(70f)))
            {
                CSED_VillainPatrolEditorState.SetPlaying(!isPlaying);
            }
        }
        EditorGUILayout.LabelField($"{CSED_VillainPatrolEditorState.playTime:0.0}秒", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        if (CSED_VillainPatrolEditorState.tool != CSED_VillainPatrolEditorState.Tool.None)
        {
            EditorGUILayout.LabelField("Sceneビューをクリックして置く(Escで外す)", EditorStyles.miniLabel);
        }
    }
}
