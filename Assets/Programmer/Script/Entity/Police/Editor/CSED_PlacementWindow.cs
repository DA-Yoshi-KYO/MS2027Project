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
/// 配置ツールのウィンドウ。置く物(道具)を選び、置く物ごとの設定を変える
/// Sceneビューをクリックして、SAObject・ハイドポイント・信号機・グラップル・動くSAポイント・敵(悪人の出現点)・警察・ドローン・レーザー・ライトを置く(CSED_PlacementSceneGUI)
/// 開き方: Tools/配置/配置ツールを開く
/// </summary>
public class CSED_PlacementWindow : EditorWindow
{
    // 道具の並び(表示名と道具)。SAポイント系と、敵・警備系に分ける
    private static readonly (string label, CSED_PlacementState.Tool tool)[] _saTools =
    {
        ("SAObject + ハイドポイント", CSED_PlacementState.Tool.SaObject),
        ("ハイドポイントだけ", CSED_PlacementState.Tool.HidePoint),
        ("信号機", CSED_PlacementState.Tool.TrafficLight),
        ("グラップル", CSED_PlacementState.Tool.Grapple),
        ("動くSAポイント", CSED_PlacementState.Tool.Moving),
    };

    private static readonly (string label, CSED_PlacementState.Tool tool)[] _securityTools =
    {
        ("敵(悪人の出現点)", CSED_PlacementState.Tool.Villain),
        ("警察のグループ", CSED_PlacementState.Tool.Police),
        ("巡回ドローン", CSED_PlacementState.Tool.Drone),
        ("防犯レーザー", CSED_PlacementState.Tool.Laser),
        ("ストリートライト", CSED_PlacementState.Tool.Light),
    };

    private Vector2 _scroll;

    [MenuItem("Tools/配置/配置ツールを開く")]
    private static void Open()
    {
        GetWindow<CSED_PlacementWindow>("配置ツール");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneRepaint;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneRepaint;
        CSED_PlacementState.tool = CSED_PlacementState.Tool.None;
        CSED_PlacementState.ClearWorking();
    }

    // Sceneビューの操作(Escで道具を外した時など)に、このウィンドウの表示を合わせる
    private void OnSceneRepaint(SceneView sceneView)
    {
        if (Event.current.type == EventType.KeyDown) Repaint();
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("置く物を選んで、Sceneビューをクリックします", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("SAポイント", EditorStyles.miniBoldLabel);
        DrawToolGrid(_saTools);
        EditorGUILayout.LabelField("敵・警備", EditorStyles.miniBoldLabel);
        DrawToolGrid(_securityTools);

        using (new EditorGUILayout.HorizontalScope())
        {
            bool isDelete = CSED_PlacementState.tool == CSED_PlacementState.Tool.Delete;
            if (GUILayout.Toggle(isDelete, "消す", "Button") != isDelete) SelectTool(isDelete ? CSED_PlacementState.Tool.None : CSED_PlacementState.Tool.Delete);
            using (new EditorGUI.DisabledScope(CSED_PlacementState.tool == CSED_PlacementState.Tool.None))
            {
                if (GUILayout.Button("道具を外す(Esc)")) SelectTool(CSED_PlacementState.Tool.None);
            }
        }

        EditorGUILayout.Space();
        DrawSettings();

        EditorGUILayout.Space();
        DrawApply();
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolGrid((string label, CSED_PlacementState.Tool tool)[] tools)
    {
        string[] labels = new string[tools.Length];
        int current = -1;
        for (int i = 0; i < tools.Length; i++)
        {
            labels[i] = tools[i].label;
            if (tools[i].tool == CSED_PlacementState.tool) current = i;
        }

        int selected = GUILayout.SelectionGrid(current, labels, 2);
        if (selected != current && selected >= 0) SelectTool(tools[selected].tool);
    }

    private static void SelectTool(CSED_PlacementState.Tool tool)
    {
        CSED_PlacementState.tool = tool;
        CSED_PlacementState.ClearWorking();
        SceneView.RepaintAll();
    }

    private void DrawSettings()
    {
        switch (CSED_PlacementState.tool)
        {
            case CSED_PlacementState.Tool.SaObject:
                EditorGUILayout.HelpBox("クリックした面の上に、柱 + 頭の箱(SAObject)と、その頭の上へ飛ぶハイドポイントを、セットで置きます。壁(傾いた面)には置けません(赤く表示)", MessageType.Info);
                DrawSaObjectSettings(true);
                break;
            case CSED_PlacementState.Tool.HidePoint:
                EditorGUILayout.HelpBox("クリックした面の上に立って隠れる、ハイドポイントだけを置きます(建物の屋上・屋根など、すでにある物の上に置く時)", MessageType.Info);
                DrawSaObjectSettings(false);
                break;
            case CSED_PlacementState.Tool.TrafficLight:
                EditorGUILayout.HelpBox("クリックした面の上に、信号機(飛ぶだけのSAポイント。高さ8mの柱)を置きます", MessageType.Info);
                break;
            case CSED_PlacementState.Tool.Grapple:
                EditorGUILayout.HelpBox("クリックした場所に、グラップルの的を置きます。引っ張られる位置は、的の手前(Sceneビューのカメラ側)に向けて置きます", MessageType.Info);
                DrawGrappleSettings();
                break;
            case CSED_PlacementState.Tool.Moving:
                EditorGUILayout.HelpBox("最初のクリックで、動くSAポイントを作り、クリックするたびに、制御点を足します。Enter または Esc で確定します(制御点は2つ以上必要)", MessageType.Info);
                DrawMovingSettings();
                break;
            case CSED_PlacementState.Tool.Villain:
                EditorGUILayout.HelpBox("クリックした地面に、悪人の出現点を置きます。置く先の犯罪完遂エリア(CrimeArea_01〜03)は、区画分けの図から自動で決まります。置くと、巡回パターンと最初のポイントができ、巡回エディタ(出現点を選ぶと出るツールバー)に引き継ぎます。続けてクリックすると、巡回ポイントが足せます", MessageType.Info);
                DrawVillainSettings();
                break;
            case CSED_PlacementState.Tool.Police:
                EditorGUILayout.HelpBox("最初のクリックで警察のグループを作り、クリックするたびに、巡回ポイントを足します(周回するルート)。Enter または Esc で確定します(3つ以上)。ステータスは、今あるグループから写します", MessageType.Info);
                DrawPoliceSettings();
                break;
            case CSED_PlacementState.Tool.Drone:
                EditorGUILayout.HelpBox("最初のクリックで巡回ドローンを作り、クリックするたびに、巡回ポイントを足します(周回するルート)。Enter または Esc で確定します(3つ以上)。ポイントは、クリックした面から、設定の高さだけ上に置きます", MessageType.Info);
                CSED_PlacementState.droneHeight = Mathf.Max(0.0f, EditorGUILayout.FloatField(new GUIContent("ポイントの高さ(m)", "クリックした面から何m上に、巡回ポイントを置くか(屋上の上空は4m)"), CSED_PlacementState.droneHeight));
                DrawOverlaySettings(CSED_PlacementOverlay.Kind.Drone);
                break;
            case CSED_PlacementState.Tool.Laser:
                EditorGUILayout.HelpBox("始点をクリックし、終点をクリックします。始点を中心に、終点まで往復します。棒は、往復する向きに直角に、通路の端から端まで伸びます(Escで、始点を取り消し)", MessageType.Info);
                CSED_PlacementState.laserBeamAuto = EditorGUILayout.Toggle(new GUIContent("長さを自動", "通路の左右の壁までの幅(一番狭い所)に合わせる。壁が無ければ、下の長さ"), CSED_PlacementState.laserBeamAuto);
                CSED_PlacementState.laserBeamLength = Mathf.Max(0.1f, EditorGUILayout.FloatField("長さ(m)", CSED_PlacementState.laserBeamLength));
                break;
            case CSED_PlacementState.Tool.Light:
                EditorGUILayout.HelpBox("最初のクリックでライトを作り、クリックするたびに、経路の点を足します(2つ以上)。Enter または Esc で確定します。ライトは、点を順につないだ線の上を動いて、床を円で照らします(曲がり角も通れます)", MessageType.Info);
                CSED_PlacementState.lightLoop = EditorGUILayout.Toggle(new GUIContent("最後から最初へ戻って周回", "オフなら、最後の点で折り返して往復する。オンなら、最後の点から最初の点へ戻って周回する"), CSED_PlacementState.lightLoop);
                CSED_PlacementState.lightDiameterAuto = EditorGUILayout.Toggle(new GUIContent("直径を自動", "全区間の道幅の一番狭い所から1mを引いた値(8〜20m)。測れなければ、下の直径"), CSED_PlacementState.lightDiameterAuto);
                CSED_PlacementState.lightDiameter = Mathf.Max(1.0f, EditorGUILayout.FloatField("直径(m)", CSED_PlacementState.lightDiameter));
                break;
            case CSED_PlacementState.Tool.Delete:
                EditorGUILayout.HelpBox("置いた物をクリックすると消します(Ctrl+Zで戻せます)。SAObjectを消すと、同じ根元のハイドポイントも、ハイドポイントを消すと、同じ根元のSAObjectも、一緒に消えます。警察のグループ・ドローン・悪人の出現点は、見えないので、マウスの近くの物(赤く光る)を消します", MessageType.Info);
                break;
            default:
                EditorGUILayout.HelpBox("上の道具を選ぶと、Sceneビューのクリックで置けます。Field.prefab のインスタンス(MainScene)の上に置くと、シーンの上書きとして残ります。「プレハブへ反映」で Field_Solo1.prefab に入れてください", MessageType.None);
                break;
        }
        DrawParentPaths();
    }

    /// <summary>
    /// 選んでいる道具が置く先の親を、Hierarchyのオブジェクトで選ぶメソッド(ドラッグ・または「◎」から選ぶ)
    /// 親がまだ無い時は、欄が空になり、置く時に、下に出す標準のパスで作る
    /// </summary>
    private static void DrawParentPaths()
    {
        CSED_PlacementState.GroupKind[] kinds = GetParentKinds(CSED_PlacementState.tool);
        if (kinds.Length == 0) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("置く先の親(Hierarchyから選ぶ)", EditorStyles.boldLabel);
        foreach (CSED_PlacementState.GroupKind kind in kinds)
        {
            string path = CSED_PlacementState.GetParentPath(kind);
            Transform existing = CSED_PlacementBuilder.FindGroup(kind);
            GameObject current = existing != null ? existing.gameObject : null;

            EditorGUILayout.BeginHorizontal();
            Object picked = EditorGUILayout.ObjectField(new GUIContent(GetParentKindLabel(kind), "Hierarchyのオブジェクトを、ここへドラッグ(または「◎」から選ぶ)すると、そのオブジェクトの下に置く。親がまだ無い時は、置く時に、下のパスで作る"), current, typeof(GameObject), true);
            if (picked != null && picked != current) UseHierarchyObject(kind, picked as GameObject);
            using (new EditorGUI.DisabledScope(path == CSED_PlacementState.GetDefaultParentPath(kind)))
            {
                if (GUILayout.Button(new GUIContent("標準", "標準の親に戻す: " + CSED_PlacementState.GetDefaultParentPath(kind)), GUILayout.Width(44.0f))) CSED_PlacementState.SetParentPath(kind, string.Empty);
            }
            EditorGUILayout.EndHorizontal();

            // 今のパス(親がまだ無ければ、置く時に作る旨も)
            EditorGUILayout.LabelField(existing != null ? path : path + "  (まだ無い。置く時に作る)", EditorStyles.miniLabel);
        }

        if (CSED_PlacementState.tool == CSED_PlacementState.Tool.Villain) EditorGUILayout.HelpBox("敵は、置いた場所の区画(区画分けの図から自動。手動にもできる)に合う欄の親に置きます", MessageType.None);
    }

    /// <summary>
    /// Hierarchyで選んだオブジェクトを、種類の親にするメソッド(プロジェクトのアセットは、シーンの物ではないので使えない)
    /// </summary>
    private static void UseHierarchyObject(CSED_PlacementState.GroupKind kind, GameObject picked)
    {
        if (picked == null) return;

        if (EditorUtility.IsPersistent(picked))
        {
            Debug.LogWarning("CSED_PlacementWindow: 親には、Hierarchy(シーン・開いているプレハブ)のオブジェクトを選んでください(Projectのアセットは使えません)");
            return;
        }
        CSED_PlacementState.SetParentPath(kind, CSED_PlacementBuilder.GetHierarchyPath(picked.transform));
    }

    private static CSED_PlacementState.GroupKind[] GetParentKinds(CSED_PlacementState.Tool tool)
    {
        switch (tool)
        {
            case CSED_PlacementState.Tool.SaObject: return new[] { CSED_PlacementState.GroupKind.SaObject, CSED_PlacementState.GroupKind.HidePoint };
            case CSED_PlacementState.Tool.HidePoint: return new[] { CSED_PlacementState.GroupKind.HidePoint };
            case CSED_PlacementState.Tool.TrafficLight:
            case CSED_PlacementState.Tool.Grapple:
            case CSED_PlacementState.Tool.Moving: return new[] { CSED_PlacementState.GroupKind.Action };
            case CSED_PlacementState.Tool.Villain: return new[] { CSED_PlacementState.GroupKind.VillainArea1, CSED_PlacementState.GroupKind.VillainArea2, CSED_PlacementState.GroupKind.VillainArea3 };
            case CSED_PlacementState.Tool.Police: return new[] { CSED_PlacementState.GroupKind.Police };
            case CSED_PlacementState.Tool.Drone: return new[] { CSED_PlacementState.GroupKind.Drone };
            case CSED_PlacementState.Tool.Laser: return new[] { CSED_PlacementState.GroupKind.Laser };
            case CSED_PlacementState.Tool.Light: return new[] { CSED_PlacementState.GroupKind.Light };
            default: return new CSED_PlacementState.GroupKind[0];
        }
    }

    private static string GetParentKindLabel(CSED_PlacementState.GroupKind kind)
    {
        switch (kind)
        {
            case CSED_PlacementState.GroupKind.SaObject: return "SAObject";
            case CSED_PlacementState.GroupKind.HidePoint: return "ハイドポイント";
            case CSED_PlacementState.GroupKind.Action: return "アクション";
            case CSED_PlacementState.GroupKind.VillainArea1: return "敵 区画1";
            case CSED_PlacementState.GroupKind.VillainArea2: return "敵 区画2";
            case CSED_PlacementState.GroupKind.VillainArea3: return "敵 区画3";
            case CSED_PlacementState.GroupKind.Police: return "警察";
            case CSED_PlacementState.GroupKind.Drone: return "ドローン";
            case CSED_PlacementState.GroupKind.Laser: return "レーザー";
            default: return "ライト";
        }
    }

    private static void DrawSaObjectSettings(bool withPole)
    {
        if (withPole)
        {
            CSED_PlacementState.autoPoleHeight = EditorGUILayout.Toggle(new GUIContent("柱の高さを自動", "地上(高さ3m未満)は8m、屋上は4m"), CSED_PlacementState.autoPoleHeight);
            using (new EditorGUI.DisabledScope(CSED_PlacementState.autoPoleHeight))
            {
                CSED_PlacementState.manualPoleHeight = Mathf.Max(0.5f, EditorGUILayout.FloatField("柱の高さ(m)", CSED_PlacementState.manualPoleHeight));
            }
        }
        CSED_PlacementState.reachDown = Mathf.Max(0.0f, EditorGUILayout.FloatField(new GUIContent("SAできる範囲を下へ(m)", "SAできる範囲の箱の下端を、置いた根元より何m下にするか。屋上のSAObjectに、下の道から飛んで来させたい時は大きくする(飛べる距離は22mまで)"), CSED_PlacementState.reachDown));
        CSED_PlacementState.keepLight = EditorGUILayout.Toggle(new GUIContent("ポイントライトを点ける", "ハイドポイントの目印の青い光"), CSED_PlacementState.keepLight);
    }

    private static void DrawGrappleSettings()
    {
        CSED_PlacementState.pullSpeed = Mathf.Max(0.1f, EditorGUILayout.FloatField(new GUIContent("PullSpeed(m/秒)", "引っ張られる速さ"), CSED_PlacementState.pullSpeed));
        CSED_PlacementState.releaseDistanceRate = EditorGUILayout.Slider(new GUIContent("ReleaseDistanceRate", "飛び始めの距離のこの割合まで引っ張られるか、着いたら放す(1にすると着いた時に放す)"), CSED_PlacementState.releaseDistanceRate, 0.05f, 1.0f);
        CSED_PlacementState.releaseDrag = Mathf.Max(0.0f, EditorGUILayout.FloatField(new GUIContent("ReleaseDrag", "放した後の水平の勢いの減速率(0なら減速せず着地まで)"), CSED_PlacementState.releaseDrag));
        CSED_PlacementState.grappleFaceCamera = EditorGUILayout.Toggle(new GUIContent("カメラ側から飛ぶ向きに置く", "的の手前(引っ張られる位置のある側)を、Sceneビューのカメラ側に向ける"), CSED_PlacementState.grappleFaceCamera);

        // 選んでいるグラップルへ、今の値を入れる
        GameObject selected = Selection.activeGameObject;
        CS_GrappleSAPoint point = selected != null ? selected.GetComponentInParent<CS_GrappleSAPoint>() : null;
        using (new EditorGUI.DisabledScope(point == null))
        {
            if (GUILayout.Button("選んでいるグラップルへ、この値を入れる"))
            {
                Undo.RecordObject(point, "グラップルの値を変える");
                CSED_PlacementBuilder.ApplyGrappleValues(point.gameObject);
            }
        }
    }

    private static void DrawMovingSettings()
    {
        CSED_PlacementState.movingKind = (CSED_PlacementState.MovingKind)EditorGUILayout.EnumPopup(new GUIContent("動かすSAポイント", "パスに沿って動かす、SAポイントの種類"), CSED_PlacementState.movingKind);
        CSED_PlacementState.movingMode = (CSE_PathFollowMode)EditorGUILayout.EnumPopup(new GUIContent("動き方(Mode)", "Once: 一方向 / Loop: 周回 / PingPong: 往復"), CSED_PlacementState.movingMode);
        CSED_PlacementState.movingSpeed = Mathf.Max(0.1f, EditorGUILayout.FloatField(new GUIContent("次の点までの速さ(m/秒)", "新しい制御点に入れる値"), CSED_PlacementState.movingSpeed));
        CSED_PlacementState.movingWait = Mathf.Max(0.0f, EditorGUILayout.FloatField(new GUIContent("待ち時間(秒)", "新しい制御点に入れる値"), CSED_PlacementState.movingWait));
        CSED_PlacementState.movingHeight = EditorGUILayout.FloatField(new GUIContent("制御点の高さ(m)", "クリックした面から何m上に制御点を置くか"), CSED_PlacementState.movingHeight);

        CS_PathFollower active = CSED_PlacementState.activeFollower;
        if (active != null)
        {
            EditorGUILayout.LabelField($"制御点を足している: {active.name}(制御点 {CSED_PlacementBuilder.CountMovingPoints(active)}個)");
            if (GUILayout.Button("確定する(Enter)")) CSED_PlacementSceneGUI.FinishPath();
        }
    }

    private static void DrawVillainSettings()
    {
        CSED_PlacementState.villainAreaAuto = EditorGUILayout.Toggle(new GUIContent("犯罪完遂エリアを自動", "区画分けの図の、区画1 → CrimeArea_01 / 区画2 → CrimeArea_02 / 区画3 → CrimeArea_03"), CSED_PlacementState.villainAreaAuto);
        using (new EditorGUI.DisabledScope(CSED_PlacementState.villainAreaAuto))
        {
            CSED_PlacementState.villainArea = EditorGUILayout.IntSlider("置く先(CrimeArea_0N)", CSED_PlacementState.villainArea, 1, 3);
        }
        CSED_PlacementState.villainHeight = Mathf.Max(0.0f, EditorGUILayout.FloatField(new GUIContent("出現点の高さ(m)", "出現点を、地面から何m浮かせて置くか(犯罪完遂ゲージを見えるように。既存の出現点は6.2m)。巡回ポイントは、そこから地面へ下りた位置に作られる"), CSED_PlacementState.villainHeight));

        DrawOverlaySettings(CSED_PlacementOverlay.Kind.Villain);
    }

    /// <summary>
    /// 種類ごとの、分布の表示(Sceneビュー)の設定と、区画ごとの一覧を出すメソッド
    /// </summary>
    private static void DrawOverlaySettings(CSED_PlacementOverlay.Kind kind)
    {
        string kindName = CSED_PlacementOverlay.GetKindName(kind);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("分布の表示(Sceneビュー)", EditorStyles.boldLabel);
        bool enabled = EditorGUILayout.Toggle(new GUIContent("分布を色で表示", $"地面を、最寄りの{kindName}のルートまでの距離で、緑(足りている)〜赤(過疎)に塗る。ルートの線・空いている場所(白い輪)も出す"), CSED_PlacementState.GetOverlayEnabled(kind));
        CSED_PlacementState.SetOverlayEnabled(kind, enabled);
        using (new EditorGUI.DisabledScope(!enabled))
        {
            CSED_PlacementState.SetCoverRadius(kind, EditorGUILayout.Slider(new GUIContent("足りている半径(m)", $"{kindName}のルートからこの距離以内の地面を、緑にする(この1.6倍より遠い所は、赤)"), CSED_PlacementState.GetCoverRadius(kind), 10.0f, 300.0f));
            if (kind == CSED_PlacementOverlay.Kind.Villain)
            {
                CSED_PlacementState.villainUseRoutes = EditorGUILayout.Toggle(new GUIContent("巡回ルートで測る", "影響範囲を、悪人グループの巡回ルート(巡回パターンのルートの線。パターンが無ければ往復の線)からの距離で測る。オフなら、出現点からの半径"), CSED_PlacementState.villainUseRoutes);
            }
            CSED_PlacementState.villainOverlayBorders = EditorGUILayout.Toggle(new GUIContent("区画の境界線と個数", "区画分けの図の線と、区画ごとの個数・密度を出す"), CSED_PlacementState.villainOverlayBorders);
            DrawStats(kind);
        }
    }

    // 区画ごとの、個数・密度・過疎を、一覧にする
    private static void DrawStats(CSED_PlacementOverlay.Kind kind)
    {
        CSED_PlacementOverlay.SectorStats[] stats = CSED_PlacementOverlay.GetStats(kind);
        string[] names = { "", "区画1(緑) CrimeArea_01", "区画2(赤) CrimeArea_02", "区画3(黄) CrimeArea_03" };
        string unit = kind == CSED_PlacementOverlay.Kind.Villain ? "個" : (kind == CSED_PlacementOverlay.Kind.Police ? "グループ" : "機");
        EditorGUILayout.LabelField($"{CSED_PlacementOverlay.GetKindName(kind)} {CSED_PlacementOverlay.GetCount(kind)}{unit}", EditorStyles.miniBoldLabel);
        for (int s = 1; s <= 3; s++)
        {
            string line = $"{names[s]}: {stats[s].count}{unit} / 1万㎡あたり {stats[s].density:F1}{(stats[s].sparse ? "  ← 過疎" : "")}{(stats[s].mismatch > 0 ? $"  / 親が合わない {stats[s].mismatch}個" : "")}";
            EditorGUILayout.LabelField(line, stats[s].sparse ? EditorStyles.boldLabel : EditorStyles.label);
        }
    }

    private static void DrawPoliceSettings()
    {
        CSED_PlacementState.policeMembers = EditorGUILayout.IntSlider(new GUIContent("グループの人数", "離れて巡回する人数(1〜3人)"), CSED_PlacementState.policeMembers, 1, 3);
        CSED_PlacementState.policeSnapNavMesh = EditorGUILayout.Toggle(new GUIContent("巡回ポイントを歩ける場所へ", "クリックした点の近く(3m以内)の、NavMeshの上へ合わせる"), CSED_PlacementState.policeSnapNavMesh);
        DrawOverlaySettings(CSED_PlacementOverlay.Kind.Police);

        CS_PoliceSquad active = CSED_PlacementState.activeSquad;
        if (active != null)
        {
            EditorGUILayout.LabelField($"巡回ポイントを足している: {active.name}(ポイント {CSED_PlacementBuilder.CountPoints(active.transform)}個)");
            if (GUILayout.Button("確定する(Enter)")) CSED_PlacementSceneGUI.FinishPath();
        }
    }

    private void DrawApply()
    {
        EditorGUILayout.LabelField("プレハブへ反映", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("MainScene(Field.prefab のインスタンス)の上に置いた物は、シーンの上書きです。Field_Solo1.prefab に入れると、他のシーンでも使えます(このツールで置いた物だけが対象)", MessageType.None);
        if (GUILayout.Button("置いた物を Field_Solo1.prefab へ反映する"))
        {
            CSED_PlacementBuilder.ApplyCreatedToPrefab(out string message);
            Debug.Log($"CSED_PlacementWindow: {message}");
            ShowNotification(new GUIContent(message));
        }
    }
}
