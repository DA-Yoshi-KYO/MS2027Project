/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-16 | 初回作成
 * ================================================ */

using UnityEditor;
using UnityEngine;

/// <summary>
/// 配置ツールの、Sceneビューでの表示と操作(マウスの位置の予告・クリックで置く・クリックで消す)
/// 道具が選ばれている間だけ、何も無い所のクリックを、このツールが受け取る(Unityの選択操作にならない)
/// 操作: 左クリック = 置く(消す) / Esc = 道具を外す(作業中は、取り消し・確定) / Enter = 巡回ポイントの確定
/// 置き方の種類: 1クリック(SAObject・ハイドポイント・信号機・グラップル・悪人) / 続けてクリック(動くSAポイント・警察・ドローン。Enterで確定) / 2クリック(レーザー・ライト。始点と終点)
/// </summary>
[InitializeOnLoad]
public static class CSED_PlacementSceneGUI
{
    private static readonly int _controlHash = "Placement".GetHashCode();
    private const float _minUpNormal = 0.5f;            // これより傾いた面(壁など)には、SAObject・ハイドポイント・信号機・ライトを置かない(法線の上向き成分)
    private const float _pickPixels = 28.0f;            // 消す時に、見えない物を選べる、マウスからの距離(ピクセル)
    private static readonly Color _okColor = new Color(0.3f, 0.9f, 1.0f);
    private static readonly Color _ngColor = new Color(1.0f, 0.3f, 0.3f);
    private static readonly Color _reachColor = new Color(1.0f, 0.8f, 0.2f);
    private static readonly Color _pathColor = new Color(0.4f, 1.0f, 0.5f);

    static CSED_PlacementSceneGUI()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        CSED_PlacementState.Tool tool = CSED_PlacementState.tool;
        if (tool == CSED_PlacementState.Tool.None) return;

        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(_controlHash, FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);

        DrawHud(tool);
        // 悪人・警察・ドローンの分布(足りている所・過疎の所)を、色で重ねる
        if (tool == CSED_PlacementState.Tool.Villain) CSED_PlacementOverlay.Draw(CSED_PlacementOverlay.Kind.Villain);
        else if (tool == CSED_PlacementState.Tool.Police) CSED_PlacementOverlay.Draw(CSED_PlacementOverlay.Kind.Police);
        else if (tool == CSED_PlacementState.Tool.Drone) CSED_PlacementOverlay.Draw(CSED_PlacementOverlay.Kind.Drone);
        if (HandleKeys(e)) return;

        DrawWorking();

        bool clicked = e.type == EventType.MouseDown && e.button == 0 && !e.alt && HandleUtility.nearestControl == controlId;
        if (tool == CSED_PlacementState.Tool.Delete)
        {
            HandleDelete(e, clicked);
        }
        else if (CSED_PlacementBuilder.TryRaycast(e.mousePosition, out RaycastHit hit))
        {
            bool placeable = CanPlace(tool, hit);
            DrawPreview(tool, hit, placeable);
            if (clicked && placeable) Place(tool, hit, sceneView);
        }

        if (clicked) e.Use();
        if (e.type == EventType.MouseMove) sceneView.Repaint();
    }

    // ---- キー ----

    /// <summary>
    /// Esc(取り消し・確定・道具を外す)と Enter(確定)を受け取るメソッド
    /// </summary>
    /// <returns>この回の処理を終えるか</returns>
    private static bool HandleKeys(Event e)
    {
        if (e.type != EventType.KeyDown) return false;

        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
        {
            FinishPath();
            e.Use();
            return false;
        }

        if (e.keyCode != KeyCode.Escape) return false;

        if (CSED_PlacementState.hasPendingStart)
        {
            CSED_PlacementState.hasPendingStart = false;
        }
        else if (CSED_PlacementState.activeFollower != null || CSED_PlacementState.activeSquad != null || CSED_PlacementState.activeDrone != null || CSED_PlacementState.activeLight != null)
        {
            FinishPath();
        }
        else
        {
            CSED_PlacementState.tool = CSED_PlacementState.Tool.None;
            EditorWindow.focusedWindow?.Repaint();
        }
        SceneView.RepaintAll();
        e.Use();
        return true;
    }

    /// <summary>
    /// 巡回ポイントを足すのを終える(確定する)メソッド(動くSAポイント・警察のグループ・巡回ドローン)
    /// </summary>
    public static void FinishPath()
    {
        if (CSED_PlacementState.activeFollower != null)
        {
            if (CSED_PlacementBuilder.CountMovingPoints(CSED_PlacementState.activeFollower) < 2) Debug.LogWarning("CSED_PlacementSceneGUI: 動くSAポイントの制御点が1つだけです(2つ以上ないと動きません)");
        }
        else if (CSED_PlacementState.activeSquad != null)
        {
            if (CSED_PlacementBuilder.CountPoints(CSED_PlacementState.activeSquad.transform) < 3) Debug.LogWarning("CSED_PlacementSceneGUI: 警察の巡回ポイントが3つ未満です(3つ以上を、周回するルートにしてください)");
        }
        else if (CSED_PlacementState.activeDrone != null)
        {
            if (CSED_PlacementBuilder.CountPoints(CSED_PlacementState.activeDrone.transform) < 3) Debug.LogWarning("CSED_PlacementSceneGUI: ドローンの巡回ポイントが3つ未満です(3つ以上を、周回するルートにしてください)");
        }
        else if (CSED_PlacementState.activeLight != null)
        {
            if (CSED_PlacementBuilder.CountPoints(CSED_PlacementState.activeLight.transform.Find(CS_SecurityLight.routeRootName)) < 2) Debug.LogWarning("CSED_PlacementSceneGUI: ライトの経路の点が1つだけです(2つ以上ないと、その場から動きません)");
        }

        CSED_PlacementState.activeFollower = null;
        CSED_PlacementState.activeSquad = null;
        CSED_PlacementState.activeDrone = null;
        CSED_PlacementState.activeLight = null;
        SceneView.RepaintAll();
    }

    // ---- 置く ----

    private static bool CanPlace(CSED_PlacementState.Tool tool, RaycastHit hit)
    {
        switch (tool)
        {
            case CSED_PlacementState.Tool.SaObject:
            case CSED_PlacementState.Tool.HidePoint:
            case CSED_PlacementState.Tool.TrafficLight:
            case CSED_PlacementState.Tool.Villain:
            case CSED_PlacementState.Tool.Laser:
            case CSED_PlacementState.Tool.Light:
                return hit.normal.y >= _minUpNormal;
            default:
                return true;
        }
    }

    private static void Place(CSED_PlacementState.Tool tool, RaycastHit hit, SceneView sceneView)
    {
        switch (tool)
        {
            case CSED_PlacementState.Tool.SaObject:
                CSED_PlacementBuilder.PlaceSaObject(hit.point);
                break;
            case CSED_PlacementState.Tool.HidePoint:
                CSED_PlacementBuilder.PlaceHidePointOnly(hit.point);
                break;
            case CSED_PlacementState.Tool.TrafficLight:
                CSED_PlacementBuilder.PlaceTrafficLight(hit.point);
                break;
            case CSED_PlacementState.Tool.Grapple:
                CSED_PlacementBuilder.PlaceGrapple(hit.point, sceneView.camera.transform.position);
                break;
            case CSED_PlacementState.Tool.Moving:
                if (CSED_PlacementState.activeFollower == null) CSED_PlacementState.activeFollower = CSED_PlacementBuilder.BeginMoving(hit.point);
                else CSED_PlacementBuilder.AddMovingPoint(CSED_PlacementState.activeFollower, hit.point);
                break;
            case CSED_PlacementState.Tool.Villain:
                // 置いた後は、既存の巡回エディタへ引き継ぐ(続けてクリックして、巡回ポイントを足す)
                CSED_PlacementBuilder.PlaceVillain(hit.point);
                CSED_PlacementState.tool = CSED_PlacementState.Tool.None;
                EditorWindow.focusedWindow?.Repaint();
                break;
            case CSED_PlacementState.Tool.Police:
                if (CSED_PlacementState.activeSquad == null) CSED_PlacementState.activeSquad = CSED_PlacementBuilder.BeginSquad(hit.point);
                else CSED_PlacementBuilder.AddSquadPoint(CSED_PlacementState.activeSquad, hit.point);
                break;
            case CSED_PlacementState.Tool.Drone:
                if (CSED_PlacementState.activeDrone == null) CSED_PlacementState.activeDrone = CSED_PlacementBuilder.BeginDrone(hit.point);
                else CSED_PlacementBuilder.AddDronePoint(CSED_PlacementState.activeDrone, hit.point);
                break;
            case CSED_PlacementState.Tool.Laser:
                PlaceLine(hit.point);
                break;
            case CSED_PlacementState.Tool.Light:
                if (CSED_PlacementState.activeLight == null) CSED_PlacementState.activeLight = CSED_PlacementBuilder.BeginLight(hit.point);
                else CSED_PlacementBuilder.AddLightPoint(CSED_PlacementState.activeLight, hit.point);
                break;
        }
        sceneView.Repaint();
    }

    // レーザー: 1回目のクリックで始点、2回目のクリックで終点を決めて置く
    private static void PlaceLine(Vector3 point)
    {
        if (!CSED_PlacementState.hasPendingStart)
        {
            CSED_PlacementState.pendingStart = point;
            CSED_PlacementState.hasPendingStart = true;
            return;
        }

        Vector3 start = CSED_PlacementState.pendingStart;
        CSED_PlacementState.hasPendingStart = false;
        CSED_PlacementBuilder.PlaceLaser(start, point);
    }

    // ---- 消す ----

    // 消す対象は、マウスを動かした時・クリックした時だけ探す(毎回の描き直しで探すと、重い)
    private static Transform _deleteTarget;

    private static void HandleDelete(Event e, bool clicked)
    {
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
        {
            GameObject picked = HandleUtility.PickGameObject(e.mousePosition, false);
            _deleteTarget = CSED_PlacementBuilder.FindPlacedRoot(picked);

            // 見えない物(警察のグループ・巡回ドローン・悪人の出現点)は、位置の近さで探す
            if (_deleteTarget == null) _deleteTarget = CSED_PlacementBuilder.FindNearestInvisible(e.mousePosition, _pickPixels, out _);
        }

        Transform root = _deleteTarget;
        if (root == null) return;

        if (e.type == EventType.Repaint)
        {
            Handles.color = _ngColor;
            Bounds bounds = GetBounds(root);
            Handles.DrawWireCube(bounds.center, bounds.size);
            Handles.SphereHandleCap(0, root.position, Quaternion.identity, 1.2f, EventType.Repaint);
            foreach (Transform child in root)
            {
                if (child.GetComponent<Renderer>() == null && child.childCount == 0) Handles.SphereHandleCap(0, child.position, Quaternion.identity, 0.8f, EventType.Repaint);
            }
        }
        if (clicked)
        {
            CSED_PlacementBuilder.Delete(root);
            _deleteTarget = null;
        }
    }

    private static Bounds GetBounds(Transform root)
    {
        Bounds bounds = new Bounds(root.position, Vector3.one * 2.0f);
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    // ---- 表示 ----

    private static void DrawHud(CSED_PlacementState.Tool tool)
    {
        string name;
        switch (tool)
        {
            case CSED_PlacementState.Tool.SaObject: name = "SAObject + ハイドポイント"; break;
            case CSED_PlacementState.Tool.HidePoint: name = "ハイドポイントだけ"; break;
            case CSED_PlacementState.Tool.TrafficLight: name = "信号機"; break;
            case CSED_PlacementState.Tool.Grapple: name = "グラップル"; break;
            case CSED_PlacementState.Tool.Moving: name = CSED_PlacementState.activeFollower != null ? "動くSAポイント(制御点を足している。Enter / Escで確定)" : "動くSAポイント(最初の点をクリック)"; break;
            case CSED_PlacementState.Tool.Villain: name = "敵(悪人の出現点。置いた後は巡回エディタで、巡回ポイントを足す)"; break;
            case CSED_PlacementState.Tool.Police: name = CSED_PlacementState.activeSquad != null ? "警察(巡回ポイントを足している。Enter / Escで確定)" : "警察(最初の巡回ポイントをクリック)"; break;
            case CSED_PlacementState.Tool.Drone: name = CSED_PlacementState.activeDrone != null ? "ドローン(巡回ポイントを足している。Enter / Escで確定)" : "ドローン(最初の巡回ポイントをクリック)"; break;
            case CSED_PlacementState.Tool.Laser: name = CSED_PlacementState.hasPendingStart ? "レーザー(終点をクリック。Escで取り消し)" : "レーザー(始点をクリック)"; break;
            case CSED_PlacementState.Tool.Light: name = CSED_PlacementState.activeLight != null ? "ライト(経路の点を足している。Enter / Escで確定)" : "ライト(最初の経路の点をクリック)"; break;
            default: name = "消す(置いた物をクリック)"; break;
        }

        Handles.BeginGUI();
        GUI.Label(new Rect(12.0f, 8.0f, 900.0f, 22.0f), $"配置: {name}  /  左クリック: {(tool == CSED_PlacementState.Tool.Delete ? "消す" : "置く")}  /  Esc: 取り消し・道具を外す", EditorStyles.boldLabel);
        Handles.EndGUI();
    }

    private static void DrawPreview(CSED_PlacementState.Tool tool, RaycastHit hit, bool placeable)
    {
        if (Event.current.type != EventType.Repaint) return;

        Handles.color = placeable ? _okColor : _ngColor;
        Handles.DrawWireDisc(hit.point, hit.normal, 0.7f);
        if (!placeable) return;

        switch (tool)
        {
            case CSED_PlacementState.Tool.SaObject:
                DrawSaObjectPreview(hit.point);
                break;
            case CSED_PlacementState.Tool.HidePoint:
                DrawReachPreview(hit.point, hit.point, hit.point.y - CSED_PlacementState.reachDown);
                break;
            case CSED_PlacementState.Tool.TrafficLight:
                Handles.DrawWireCube(hit.point + Vector3.up * 4.0f, new Vector3(0.4f, 8.0f, 0.4f));
                break;
            case CSED_PlacementState.Tool.Grapple:
                Handles.DrawWireDisc(hit.point, Vector3.up, 0.4f);
                Handles.DrawWireDisc(hit.point, Vector3.right, 0.4f);
                Handles.DrawWireDisc(hit.point, Vector3.forward, 0.4f);
                break;
            case CSED_PlacementState.Tool.Moving:
                if (CSED_PlacementState.activeFollower != null) DrawNextSegment(CSED_PlacementState.activeFollower.transform.Find("PathPoints"), hit.point + Vector3.up * CSED_PlacementState.movingHeight, false);
                break;
            case CSED_PlacementState.Tool.Villain:
                DrawVillainPreview(hit.point);
                break;
            case CSED_PlacementState.Tool.Police:
                if (CSED_PlacementState.activeSquad != null) DrawNextSegment(CSED_PlacementState.activeSquad.transform, hit.point, true);
                DrawNearestLabel(CSED_PlacementOverlay.Kind.Police, hit.point);
                break;
            case CSED_PlacementState.Tool.Drone:
                if (CSED_PlacementState.activeDrone != null) DrawNextSegment(CSED_PlacementState.activeDrone.transform, hit.point + Vector3.up * CSED_PlacementState.droneHeight, true);
                else Handles.DrawDottedLine(hit.point, hit.point + Vector3.up * CSED_PlacementState.droneHeight, 4.0f);
                DrawNearestLabel(CSED_PlacementOverlay.Kind.Drone, hit.point);
                break;
            case CSED_PlacementState.Tool.Laser:
                if (CSED_PlacementState.hasPendingStart) DrawLaserPreview(CSED_PlacementState.pendingStart, hit.point);
                break;
            case CSED_PlacementState.Tool.Light:
                DrawLightPreview(hit.point);
                break;
        }
    }

    // 置く前の予告に、一番近い同じ種類の物の影響範囲(ルート)までの距離を出す
    private static void DrawNearestLabel(CSED_PlacementOverlay.Kind kind, Vector3 point)
    {
        float nearest = CSED_PlacementOverlay.GetNearestDistance(kind, point);
        string text = nearest < 0.0f ? "-" : $"{nearest:F0}m";
        Handles.Label(point + Vector3.up * 2.0f, $"最寄りの{CSED_PlacementOverlay.GetKindName(kind)}のルートまで {text}");
    }

    private static void DrawSaObjectPreview(Vector3 basePosition)
    {
        float pole = CSED_PlacementBuilder.GetPoleHeight(basePosition);
        Handles.DrawWireCube(basePosition + Vector3.up * (pole * 0.5f), new Vector3(0.5f, pole, 0.5f));
        Handles.DrawWireCube(basePosition + Vector3.up * (pole + 1.0f), Vector3.one * 2.0f);
        DrawReachPreview(basePosition, basePosition + Vector3.up * (pole + 2.0f), basePosition.y - CSED_PlacementState.reachDown);
    }

    // SAできる範囲(足元の箱)の予告を描く
    private static void DrawReachPreview(Vector3 root, Vector3 stand, float feetLowY)
    {
        CSED_PlacementBuilder.GetReachBox(root, stand, feetLowY, out Vector3 center, out Vector3 size);
        Handles.color = _reachColor;
        Handles.DrawWireCube(center, size);
    }

    // 悪人の出現点の予告(地面から浮かせる高さの線と、置かれる犯罪完遂エリア)を描く
    private static void DrawVillainPreview(Vector3 ground)
    {
        Vector3 top = ground + Vector3.up * CSED_PlacementState.villainHeight;
        Handles.DrawDottedLine(ground, top, 4.0f);
        Handles.DrawWireDisc(top, Vector3.up, 1.5f);
        int area = CSED_PlacementState.villainAreaAuto ? CSED_FieldMultiLayoutGenerator.GetVillainSector(ground) : CSED_PlacementState.villainArea;
        float nearest = CSED_PlacementOverlay.GetNearestDistance(CSED_PlacementOverlay.Kind.Villain, ground);
        string nearestText = nearest < 0.0f ? "-" : $"{nearest:F0}m";
        Handles.Label(top + Vector3.up * 1.0f, $"CrimeArea_0{area}\n最寄りの悪人まで {nearestText}");
    }

    // 巡回ポイントの親の、最後のポイントから、次に置く点までの線(周回する物は、最初の点へ戻る線も)を描く
    private static void DrawNextSegment(Transform pointsParent, Vector3 next, bool closeLoop)
    {
        if (pointsParent == null || pointsParent.childCount == 0) return;

        Handles.color = _pathColor;
        Transform last = pointsParent.GetChild(pointsParent.childCount - 1);
        Handles.DrawDottedLine(last.position, next, 4.0f);
        if (closeLoop && pointsParent.childCount > 1) Handles.DrawDottedLine(next, pointsParent.GetChild(0).position, 2.0f);
    }

    // レーザーの予告(始点と終点に、棒を描く)
    private static void DrawLaserPreview(Vector3 start, Vector3 end)
    {
        Vector3 delta = Vector3.ProjectOnPlane(end - start, Vector3.up);
        if (delta.magnitude < 0.5f) return;

        Vector3 direction = delta.normalized;
        float distance = Mathf.Min(delta.magnitude, 50.0f);
        float length = CSED_PlacementBuilder.GetLaserLength(start, end);
        Vector3 side = Vector3.Cross(Vector3.up, direction) * (length * 0.5f);
        Vector3 from = start + Vector3.up;
        Vector3 to = start + direction * distance + Vector3.up;
        Handles.color = _okColor;
        Handles.DrawLine(from - side, from + side, 4.0f);
        Handles.DrawLine(to - side, to + side, 4.0f);
        Handles.DrawDottedLine(from, to, 4.0f);
        Handles.Label(to + Vector3.up, $"往復 {distance:F0}m / 長さ {length:F1}m");
    }

    // ライトの予告(次に置く点に、円を描く。足している途中なら、最後の点からの線と、道幅から測る直径も)
    private static void DrawLightPreview(Vector3 next)
    {
        Handles.color = _okColor;
        CS_SecurityLight light = CSED_PlacementState.activeLight;
        Transform routeRoot = light != null ? light.transform.Find(CS_SecurityLight.routeRootName) : null;
        float diameter = Mathf.Max(1.0f, CSED_PlacementState.lightDiameter);
        if (routeRoot != null && routeRoot.childCount > 0)
        {
            Vector3 last = routeRoot.GetChild(routeRoot.childCount - 1).position;
            DrawNextSegment(routeRoot, next + Vector3.up * 0.25f, CSED_PlacementState.lightLoop);
            if (CSED_PlacementState.lightDiameterAuto) diameter = CSED_PlacementBuilder.GetLightDiameter(last, next);
            Handles.Label(next + Vector3.up, $"直径 {diameter:F0}m / 区間 {Vector3.Distance(last, next):F0}m");
        }
        Handles.DrawWireDisc(next + Vector3.up * 0.25f, Vector3.up, diameter * 0.5f);
    }

    // 作業中の物(確定していない巡回ポイント・1回目のクリック)を描く
    private static void DrawWorking()
    {
        if (Event.current.type != EventType.Repaint) return;

        Handles.color = _pathColor;
        if (CSED_PlacementState.activeFollower != null)
        {
            CS_PathFollower follower = CSED_PlacementState.activeFollower;
            if (CSED_PlacementBuilder.CountMovingPoints(follower) >= 2)
            {
                follower.RefreshForEditor();
                var curve = follower.GetCurvePoints(12);
                for (int i = 1; i < curve.Count; i++) Handles.DrawLine(curve[i - 1], curve[i], 3.0f);
            }
            foreach (CS_PathFollowerPoint point in follower.GetPoints()) Handles.SphereHandleCap(0, point.transform.position, Quaternion.identity, 0.5f, EventType.Repaint);
        }
        DrawPointLoop(CSED_PlacementState.activeSquad != null ? CSED_PlacementState.activeSquad.transform : null);
        DrawPointLoop(CSED_PlacementState.activeDrone != null ? CSED_PlacementState.activeDrone.transform : null);
        if (CSED_PlacementState.activeLight != null) DrawLightRoute(CSED_PlacementState.activeLight);

        if (CSED_PlacementState.hasPendingStart) Handles.SphereHandleCap(0, CSED_PlacementState.pendingStart, Quaternion.identity, 0.6f, EventType.Repaint);
    }

    // ライトの経路を描く(点を順につなぎ、周回する設定なら、最初へ戻る線も)
    private static void DrawLightRoute(CS_SecurityLight light)
    {
        Transform routeRoot = light.transform.Find(CS_SecurityLight.routeRootName);
        if (routeRoot == null) return;

        for (int i = 0; i < routeRoot.childCount; i++)
        {
            Handles.SphereHandleCap(0, routeRoot.GetChild(i).position, Quaternion.identity, 0.5f, EventType.Repaint);
            if (i > 0) Handles.DrawLine(routeRoot.GetChild(i - 1).position, routeRoot.GetChild(i).position, 3.0f);
        }
        if (CSED_PlacementState.lightLoop && routeRoot.childCount > 2) Handles.DrawLine(routeRoot.GetChild(routeRoot.childCount - 1).position, routeRoot.GetChild(0).position, 3.0f);
    }

    // 子の巡回ポイントを、順につないで、最初へ戻る線を描く
    private static void DrawPointLoop(Transform parent)
    {
        if (parent == null) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Handles.SphereHandleCap(0, parent.GetChild(i).position, Quaternion.identity, 0.5f, EventType.Repaint);
            if (parent.childCount > 1) Handles.DrawLine(parent.GetChild(i).position, parent.GetChild((i + 1) % parent.childCount).position, 3.0f);
        }
    }
}
