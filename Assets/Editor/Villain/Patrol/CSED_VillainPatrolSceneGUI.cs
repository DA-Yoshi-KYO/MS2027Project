using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/*
 * 巡回エディタのSceneビューの表示と操作
 * スポーン位置を選んでいる間、巡回パターンのルート・ポイント・悪人を描き、置く・動かす・選ぶ操作を受け付ける
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・表示(スポーン位置を選んでいる間だけ)
 *   ルートの線: NavMeshで実際に通る道をルートの色で描く。通れない区間は赤い直線
 *   ポイント: 番号付きの丸(選んでいるポイントは塗りつぶし)。やることがあれば番号の横に書く
 *   悪人: ポイント1に丸と向きの線、隊列で付いてくる人は小さな丸。プレビュー再生中は今の位置に描く
 *   選んでいない悪人のルートは薄く描く
 * ・操作
 *   道具(悪人を置く / ポイントを置く)が押されている時だけ、Sceneビューのクリックで置く(押されていない時は普段どおりのUnityの操作)
 *     悪人を置く: クリックした地面(NavMeshの上)にルートを作る。置いた所がポイント1。置いたら「ポイントを置く」に切り替わる
 *     ポイントを置く: 選んでいる悪人のルートの最後にポイントを足す(悪人がいなければ悪人を置く)
 *     Escで道具を外す
 *   ポイント・悪人・線をクリックすると、その悪人(とポイント)を選ぶ。ポイントはドラッグで動かせる(地面の上を動く)
 *   変更はUndo(Ctrl+Z)できる
 * ・保存: シーン・プレハブを保存した時に、使っている巡回パターンのアセットも保存する
 */
// ========================================

[InitializeOnLoad]
public static class CSED_VillainPatrolSceneGUI
{
    private const float _pointRadius = 0.35f;
    private const float _villainRadius = 0.5f;
    private const float _followerRadius = 0.35f;
    private const float _lineWidth = 4f;
    private const float _lineHeight = 0.05f;           // 線を地面から浮かせる高さ(m。地面に埋もれないように)
    private const float _groundSampleRadius = 2f;      // クリックした地面の近くでNavMeshを探す半径(m)
    private const float _unselectedAlpha = 0.35f;      // 選んでいない悪人のルートの濃さ
    private static readonly int _placeControlHash = "VillainPatrolPlace".GetHashCode();
    private static readonly int _pointControlHash = "VillainPatrolPoint".GetHashCode();
    private static readonly int _lineControlHash = "VillainPatrolLine".GetHashCode();

    private static CS_VillainSpawnPoint _lastSpawnPoint;

    static CSED_VillainPatrolSceneGUI()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += HandleSelectionChanged;
        EditorApplication.update += UpdatePlayback;
        Undo.undoRedoPerformed += CSED_VillainPatrolEditorState.NotifyChanged;
        EditorSceneManager.sceneSaved += _ => SavePatterns();
        PrefabStage.prefabSaved += _ => SavePatterns();
    }

    // スポーン位置の選択が変わったら、選択・道具・再生を外す
    private static void HandleSelectionChanged()
    {
        CS_VillainSpawnPoint spawnPoint = CSED_VillainPatrolEditorState.GetSpawnPoint();
        if (spawnPoint == _lastSpawnPoint) return;

        _lastSpawnPoint = spawnPoint;
        CSED_VillainPatrolEditorState.Reset();
    }

    // 再生中はSceneビューを描き直し続ける
    private static void UpdatePlayback()
    {
        if (!CSED_VillainPatrolEditorState.isPlaying) return;

        SceneView.RepaintAll();
        CSED_VillainPatrolOverlay.RepaintAll();
    }

    // シーン・プレハブの保存と一緒に、使っている巡回パターンのアセットを保存する
    private static void SavePatterns()
    {
        foreach (CS_VillainPatrolRoute route in Resources.FindObjectsOfTypeAll<CS_VillainPatrolRoute>())
        {
            if (route.pattern != null) AssetDatabase.SaveAssetIfDirty(route.pattern);
        }
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        CS_VillainSpawnPoint spawnPoint = CSED_VillainPatrolEditorState.GetSpawnPoint();
        if (spawnPoint == null) return;

        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && CSED_VillainPatrolEditorState.tool != CSED_VillainPatrolEditorState.Tool.None)
        {
            CSED_VillainPatrolEditorState.tool = CSED_VillainPatrolEditorState.Tool.None;
            e.Use();
            return;
        }

        CS_VillainPatrolRoute route = spawnPoint.GetComponent<CS_VillainPatrolRoute>();
        CSO_VillainPatrolPattern pattern = route != null ? route.pattern : null;

        // 道具が押されている時は、何も無い所のクリックをこちらで受け取る(Unityの選択操作にしない)
        int placeControl = GUIUtility.GetControlID(_placeControlHash, FocusType.Passive);
        if (CSED_VillainPatrolEditorState.tool != CSED_VillainPatrolEditorState.Tool.None && e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(placeControl);
        }

        if (pattern != null) DrawAndHandleRoutes(route, pattern);

        if (CSED_VillainPatrolEditorState.tool != CSED_VillainPatrolEditorState.Tool.None
            && e.type == EventType.MouseDown && e.button == 0 && !e.alt
            && HandleUtility.nearestControl == placeControl)
        {
            if (TryGetGround(e.mousePosition, out Vector3 ground)) Place(spawnPoint, ground);
            e.Use();
        }
    }

    // ---- 置く ----

    private static void Place(CS_VillainSpawnPoint spawnPoint, Vector3 ground)
    {
        CS_VillainPatrolRoute route = CSED_VillainPatrolEditorState.EnsurePattern(spawnPoint);
        CSO_VillainPatrolPattern pattern = route.pattern;
        Vector3 local = route.ToLocal(ground);

        bool hasSelectedRoute = CSED_VillainPatrolEditorState.selectedRoute >= 0 && CSED_VillainPatrolEditorState.selectedRoute < pattern.routes.Count;
        if (CSED_VillainPatrolEditorState.tool == CSED_VillainPatrolEditorState.Tool.PlacePoint && hasSelectedRoute)
        {
            Undo.RecordObject(pattern, "巡回のポイントを置く");
            List<CSO_VillainPatrolPattern.Point> points = pattern.routes[CSED_VillainPatrolEditorState.selectedRoute].points;
            points.Add(new CSO_VillainPatrolPattern.Point(local));
            CSED_VillainPatrolEditorState.MarkPatternDirty(route);
            CSED_VillainPatrolEditorState.Select(CSED_VillainPatrolEditorState.selectedRoute, points.Count - 1);
            return;
        }

        // 悪人を置く(ポイントを置く時に悪人がいなければ、ここも悪人を置く)。置いた所がポイント1
        Undo.RecordObject(pattern, "巡回の悪人を置く");
        Color color = CSO_VillainPatrolPattern.palette[pattern.routes.Count % CSO_VillainPatrolPattern.palette.Length];
        pattern.routes.Add(new CSO_VillainPatrolPattern.Route(color, local));
        CSED_VillainPatrolEditorState.MarkPatternDirty(route);
        CSED_VillainPatrolEditorState.Select(pattern.routes.Count - 1, 0);
        CSED_VillainPatrolEditorState.tool = CSED_VillainPatrolEditorState.Tool.PlacePoint;
    }

    // マウスの下の地面(NavMeshの上に合わせる)。地面が無ければスポーン位置の高さの水平面
    private static bool TryGetGround(Vector2 mousePosition, out Vector3 ground)
    {
        if (!HandleUtility.PlaceObject(mousePosition, out ground, out _))
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane plane = new Plane(Vector3.up, CSED_VillainPatrolEditorState.GetSpawnPoint().transform.position);
            if (!plane.Raycast(ray, out float distance)) return false;
            ground = ray.GetPoint(distance);
        }

        if (NavMesh.SamplePosition(ground, out NavMeshHit hit, _groundSampleRadius, NavMesh.AllAreas)) ground = hit.position;
        return true;
    }

    // ---- 描く・選ぶ・動かす ----

    private static void DrawAndHandleRoutes(CS_VillainPatrolRoute route, CSO_VillainPatrolPattern pattern)
    {
        // 選んでいる悪人を最後に描く(上に重ねる)
        int selected = CSED_VillainPatrolEditorState.selectedRoute;
        for (int i = 0; i < pattern.routes.Count; i++)
        {
            if (i != selected) DrawAndHandleRoute(route, pattern, i);
        }
        if (selected >= 0 && selected < pattern.routes.Count) DrawAndHandleRoute(route, pattern, selected);
    }

    private static void DrawAndHandleRoute(CS_VillainPatrolRoute owner, CSO_VillainPatrolPattern pattern, int routeIndex)
    {
        CSO_VillainPatrolPattern.Route route = pattern.routes[routeIndex];
        if (route.points.Count == 0) return;

        bool isSelected = routeIndex == CSED_VillainPatrolEditorState.selectedRoute;
        Color color = route.color;
        color.a = isSelected ? 1f : _unselectedAlpha;
        Vector3[] points = CSED_VillainPatrolPreview.GetPointPositions(owner, route);

        HandleLines(owner, route, routeIndex, points, color);
        for (int i = 0; i < points.Length; i++)
        {
            HandlePoint(owner, pattern, routeIndex, i, points[i], color, isSelected);
        }
        DrawVillain(owner, route, color);
    }

    // ルートの線(NavMeshで通る道。通れない区間は赤)。線をクリックすると、その悪人を選ぶ
    private static void HandleLines(CS_VillainPatrolRoute owner, CSO_VillainPatrolPattern.Route route, int routeIndex, Vector3[] points, Color color)
    {
        if (points.Length < 2) return;

        Event e = Event.current;
        int control = GUIUtility.GetControlID(_lineControlHash, FocusType.Passive);
        List<Vector3> all = new List<Vector3>();

        int count = route.loopMode == CSE_VillainPatrolLoopMode.Loop && points.Length > 2 ? points.Length : points.Length - 1;
        for (int i = 0; i < count; i++)
        {
            CSED_VillainPatrolPreview.Leg leg = CSED_VillainPatrolPreview.CalculateLeg(points[i], points[(i + 1) % points.Length]);
            Vector3[] corners = Lift(leg.corners);
            all.AddRange(corners);

            if (e.type != EventType.Repaint) continue;
            Color lineColor = leg.isBlocked ? new Color(1f, 0.15f, 0.15f, color.a) : color;
            Handles.color = lineColor;
            if (leg.isBlocked) Handles.DrawDottedLine(corners[0], corners[corners.Length - 1], 4f);
            Handles.DrawAAPolyLine(_lineWidth, corners);
        }

        if (e.type == EventType.Layout) HandleUtility.AddControl(control, HandleUtility.DistanceToPolyLine(all.ToArray()));
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && HandleUtility.nearestControl == control)
        {
            CSED_VillainPatrolEditorState.Select(routeIndex, -1);
            e.Use();
        }
    }

    // ポイント(番号付きの丸)。クリックで選び、ドラッグで地面の上を動かす
    private static void HandlePoint(CS_VillainPatrolRoute owner, CSO_VillainPatrolPattern pattern, int routeIndex, int pointIndex, Vector3 position, Color color, bool isRouteSelected)
    {
        Event e = Event.current;
        int control = GUIUtility.GetControlID(_pointControlHash, FocusType.Passive);
        float size = HandleUtility.GetHandleSize(position) * 0.1f;
        float radius = Mathf.Max(_pointRadius, size);

        switch (e.GetTypeForControl(control))
        {
            case EventType.Layout:
                HandleUtility.AddControl(control, HandleUtility.DistanceToCircle(position, radius));
                break;

            case EventType.MouseDown:
                if (e.button != 0 || e.alt || HandleUtility.nearestControl != control) break;
                GUIUtility.hotControl = control;
                Undo.RecordObject(pattern, "巡回のポイントを動かす");
                CSED_VillainPatrolEditorState.Select(routeIndex, pointIndex);
                e.Use();
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != control) break;
                if (TryGetGround(e.mousePosition, out Vector3 ground))
                {
                    pattern.routes[routeIndex].points[pointIndex].localPosition = owner.ToLocal(ground);
                    CSED_VillainPatrolEditorState.MarkPatternDirty(owner);
                }
                e.Use();
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl != control) break;
                GUIUtility.hotControl = 0;
                CSED_VillainPatrolEditorState.NotifyChanged();
                e.Use();
                break;

            case EventType.Repaint:
                bool isSelected = isRouteSelected && pointIndex == CSED_VillainPatrolEditorState.selectedPoint;
                Vector3 lifted = position + Vector3.up * _lineHeight;
                Handles.color = isSelected ? color : new Color(0.12f, 0.12f, 0.12f, color.a);
                Handles.DrawSolidDisc(lifted, Vector3.up, radius);
                Handles.color = color;
                Handles.DrawWireDisc(lifted, Vector3.up, radius, pointIndex == 0 ? 3f : 2f);
                Handles.Label(lifted, GetPointLabel(pattern.routes[routeIndex].points[pointIndex], pointIndex), GetLabelStyle(color));
                break;
        }
    }

    // 悪人(ポイント1、再生中は今の位置)と、隊列で付いてくる人
    private static void DrawVillain(CS_VillainPatrolRoute owner, CSO_VillainPatrolPattern.Route route, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;

        CSED_VillainPatrolPreview.Pose pose = CSED_VillainPatrolEditorState.isPlaying
            ? CSED_VillainPatrolPreview.Evaluate(owner, route, CSED_VillainPatrolEditorState.playTime)
            : new CSED_VillainPatrolPreview.Pose { position = owner.GetPointWorldPosition(route, 0), yaw = owner.GetStartRotation(route).eulerAngles.y };

        DrawBody(pose.position, pose.yaw, _villainRadius, color, true);
        if (CSED_VillainPatrolEditorState.isPlaying && !string.IsNullOrEmpty(pose.label))
        {
            Handles.Label(pose.position + Vector3.up * 1.2f, pose.label, GetLabelStyle(color));
        }

        for (int i = 0; i < route.formation.followerCount; i++)
        {
            DrawBody(CSED_VillainPatrolPreview.GetFollowerPosition(route, pose, i), pose.yaw, _followerRadius, color, false);
        }
    }

    private static void DrawBody(Vector3 position, float yaw, float radius, Color color, bool isLeader)
    {
        Vector3 lifted = position + Vector3.up * (_lineHeight * 2f);
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        Handles.color = new Color(color.r, color.g, color.b, color.a * (isLeader ? 0.85f : 0.5f));
        Handles.DrawSolidDisc(lifted, Vector3.up, radius);
        Handles.color = new Color(1f, 1f, 1f, color.a);
        Handles.DrawAAPolyLine(3f, lifted, lifted + forward * radius * 1.8f);
    }

    // 番号と、やることの短い説明(例: 「2 見回し・停止」)
    private static string GetPointLabel(CSO_VillainPatrolPattern.Point point, int pointIndex)
    {
        string label = pointIndex == 0 ? "1 スタート" : (pointIndex + 1).ToString();
        if (point.actions.Count == 0) return label;

        List<string> names = new List<string>();
        foreach (CSO_VillainPatrolPattern.Action action in point.actions)
        {
            names.Add(CSED_VillainPatrolLabels.GetActionName(action.type));
        }
        return label + " " + string.Join("・", names);
    }

    private static GUIStyle GetLabelStyle(Color color)
    {
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = new Color(color.r, color.g, color.b, Mathf.Max(0.6f, color.a));
        style.contentOffset = new Vector2(12f, -8f);
        return style;
    }

    private static Vector3[] Lift(Vector3[] corners)
    {
        Vector3[] lifted = new Vector3[corners.Length];
        for (int i = 0; i < corners.Length; i++)
        {
            lifted[i] = corners[i] + Vector3.up * _lineHeight;
        }
        return lifted;
    }
}
