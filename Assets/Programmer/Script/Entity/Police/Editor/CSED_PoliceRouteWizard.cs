/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-19 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 選んだ警察のグループ(CS_PoliceSquad)の巡回ポイントを、灰色の道の上に自動で作るエディタ専用ツール
/// 1. 巡回ルートを作る範囲を、XとZの座標で決める(Yは、無視するかどうかを選べる)
/// 2. 範囲の中の灰色の道(Road)の上を一周するルートを作る(路地裏(BackAlley)は、余白をあけて避ける)
/// 3. ルートの曲がり角ごとに巡回ポイントのオブジェクトを作り、グループの子にして、PatrolPointsに設定する
/// ツールの画面を開いている間は、入力した範囲をSceneビューに表示する(範囲を調整しながら確認できる)
/// Field_Multi.prefab・Field_Solo1.prefabをPrefab Modeで開いてグループを選んで使えば、プレハブにそのまま保存される
/// (シーン内のプレハブのインスタンスに使った場合は、Overridesから、プレハブへApplyする)
/// </summary>
public class CSED_PoliceRouteWizard : ScriptableWizard
{
    // 作った巡回ポイントの名前の先頭(作り直す時に、前に作ったものを見分けて消すのに使う)
    private const string _pointNamePrefix = "RoadPatrolPoint_";

    // 灰色の道・路地裏のプレハブの名前
    private const string _roadPrefabName = "/Road.prefab";
    private const string _alleyPrefabName = "/BackAlley.prefab";

    // 細長くない(幅に対する長さがこれ未満の)ものは、広場とみなして中心線にしない
    private const float _minRoadRatio = 1.5f;

    // Yを無視する時に使う、十分に広いYの範囲
    private const float _unlimitedHeight = 100000.0f;

    // 範囲の初期値(X・Zの片側の大きさ)
    private const float _defaultHalfSize = 120.0f;

    // Yを無視する時に、Sceneビューで範囲を表示する高さの余白と、枠の縦線の長さ(m)
    private const float _displayHeightOffset = 0.3f;
    private const float _displayWallHeight = 4.0f;

    // Sceneビューに表示する範囲の色
    private static readonly Color _areaFillColor = new Color(0.2f, 1.0f, 0.6f, 0.12f);
    private static readonly Color _areaLineColor = new Color(0.2f, 1.0f, 0.6f, 1.0f);

    [SerializeField]
    [Tooltip("巡回ルートを作る、選択中のグループ")]
    private CS_PoliceSquad _squad = null;

    [Header("巡回ルートを作る範囲(ワールド座標)")]
    [SerializeField] private float _minX = -_defaultHalfSize;
    [SerializeField] private float _maxX = _defaultHalfSize;
    [SerializeField] private float _minZ = -_defaultHalfSize;
    [SerializeField] private float _maxZ = _defaultHalfSize;

    [SerializeField]
    [Tooltip("オンにすると、Y(高さ)は見ない。オフにすると、下のYの範囲の中にある道だけを使う")]
    private bool _ignoreY = true;

    [SerializeField] private float _minY = -5.0f;
    [SerializeField] private float _maxY = 50.0f;

    [Header("作り方")]
    [SerializeField, Min(0f)]
    [Tooltip("路地裏の外側にあける余白(m)。大きいほど、路地裏から離れた道だけを通る")]
    private float _alleyMargin = 1.5f;

    [SerializeField, Range(0f, 90f)]
    [Tooltip("巡回ポイントを作る、道の曲がり角の最小の角度(度)。まっすぐ通り過ぎるだけの場所には、巡回ポイントを作らない")]
    private float _minTurnAngle = 10.0f;

    [SerializeField]
    [Tooltip("オフだと、範囲の中で一番大きくつながった道だけを回る(離れた道はSceneビューで橙色になる)。オンだと、離れた道も全部回る。離れた道へは、NavMeshの最短の道で移動するので、路地裏を通ることがある")]
    private bool _includeDisconnected = false;

    [Header("表示")]
    [SerializeField]
    [Tooltip("Sceneビューに、道の使い分け(青:巡回する / 橙:離れていて外れる / 赤:路地裏にかかるので通らない)を表示する")]
    private bool _showRoads = true;

    // Sceneビューに表示するための、今の入力で作った道の網(入力を変えるたびに作り直す)
    [System.NonSerialized] private CSED_PoliceRoadGraph _preview = null;

    /// <summary>
    /// ツールの画面を開くメソッド(CS_PoliceSquadを選んだ状態で使う)
    /// </summary>
    [MenuItem("Tools/Police/選んだグループの巡回ポイントを、灰色の道から作る")]
    private static void Open()
    {
        CS_PoliceSquad squad = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<CS_PoliceSquad>() : null;
        if (squad == null)
        {
            EditorUtility.DisplayDialog("巡回ポイントを作る", "巡回ルートを作る CS_PoliceSquad(PoliceSquad)を選んでから、もう一度実行してください。", "OK");
            return;
        }

        CSED_PoliceRouteWizard wizard = DisplayWizard<CSED_PoliceRouteWizard>("灰色の道から巡回ポイントを作る", "作る");
        wizard._squad = squad;
        wizard.OnWizardUpdate();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawArea;
        RebuildPreview();
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawArea;
        SceneView.RepaintAll();
    }

    private void OnWizardUpdate()
    {
        helpString = "選んだグループの、指定した範囲の中の灰色の道の上を一周する巡回ポイントを作り直します。\n"
                   + "前に作った巡回ポイント(RoadPatrolPoint_)は消えます。手で置いた巡回ポイントは消えません。";

        if (_maxX <= _minX || _maxZ <= _minZ || (!_ignoreY && _maxY <= _minY))
        {
            errorString = "範囲の最大が、最小より大きくなるようにしてください。";
            isValid = false;
            _preview = null;
            SceneView.RepaintAll();
            return;
        }

        errorString = string.Empty;
        isValid = _squad != null;

        // 入力を変えたら、道の使い分けと、Sceneビューの表示も更新する
        RebuildPreview();
        if (_preview != null) helpString += "\n" + CreateRoadSummary(_preview);
        SceneView.RepaintAll();
    }

    private void OnWizardCreate()
    {
        if (_squad == null) return;

        if (!TryCreateRoute(_squad, CreateArea(), _alleyMargin, _minTurnAngle, _includeDisconnected, out string message))
        {
            EditorUtility.DisplayDialog("巡回ポイントを作る", message, "OK");
            return;
        }

        Debug.Log(message, _squad);
    }

    /// <summary>
    /// 今の入力で道の網を作り直し、Sceneビューに表示する道の使い分けを更新するメソッド
    /// </summary>
    private void RebuildPreview()
    {
        _preview = null;
        if (_squad == null || !_showRoads) return;

        _preview = BuildGraph(_squad, CreateArea(), _alleyMargin, _includeDisconnected, out int _, out int _);
    }

    /// <summary>
    /// 道の使い分けの本数を、説明の文章にするメソッド
    /// </summary>
    private static string CreateRoadSummary(CSED_PoliceRoadGraph graph)
    {
        string summary = $"巡回する道(青): {graph.usableEdges.Count} 本 / 路地裏にかかって通らない道(赤): {graph.blockedEdges.Count} 本";
        if (graph.excludedEdges.Count > 0)
        {
            summary += $"\n離れていて外れる道(橙): {graph.excludedEdges.Count} 本 (「離れた道も回る」をオンにすると回ります)";
        }
        return summary;
    }

    /// <summary>
    /// 入力した範囲を、Sceneビューに表示するメソッド
    /// Yを無視する時は、グループの高さに平らな枠と縦線を、無視しない時は、Yの範囲の箱を描く
    /// グループの位置も表示する(巡回は、この位置に一番近い道から始まる)
    /// </summary>
    /// <param name="sceneView">表示先のSceneビュー</param>
    private void DrawArea(SceneView sceneView)
    {
        if (_maxX <= _minX || _maxZ <= _minZ || (!_ignoreY && _maxY <= _minY)) return;

        // 他の物に隠れても見えるようにする
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        float baseY = _squad != null ? _squad.transform.position.y : 0.0f;
        float bottom = _ignoreY ? baseY + _displayHeightOffset : _minY;
        float top = _ignoreY ? bottom + _displayWallHeight : _maxY;
        DrawBox(bottom, top);

        // 座標が分かるよう、4隅に値を表示する
        Handles.Label(new Vector3(_minX, bottom, _minZ), $"X {_minX:0.#} / Z {_minZ:0.#}");
        Handles.Label(new Vector3(_maxX, bottom, _minZ), $"X {_maxX:0.#} / Z {_minZ:0.#}");
        Handles.Label(new Vector3(_minX, bottom, _maxZ), $"X {_minX:0.#} / Z {_maxZ:0.#}");
        Handles.Label(new Vector3(_maxX, bottom, _maxZ), $"X {_maxX:0.#} / Z {_maxZ:0.#}");

        DrawSquadPosition();
        DrawRoads();
    }

    /// <summary>
    /// 道の使い分けを、Sceneビューに描くメソッド(青:巡回する / 橙:離れていて外れる / 赤:路地裏にかかるので通らない)
    /// </summary>
    private void DrawRoads()
    {
        if (_preview == null) return;

        DrawEdges(_preview, _preview.blockedEdges, Color.red, true);
        DrawEdges(_preview, _preview.excludedEdges, new Color(1.0f, 0.55f, 0.0f), false);
        DrawEdges(_preview, _preview.usableEdges, new Color(0.25f, 0.55f, 1.0f), false);
    }

    /// <summary>
    /// 同じ種類の道を、同じ色の線で描くメソッド
    /// </summary>
    /// <param name="graph">道の網</param>
    /// <param name="edges">描く道(両端のノードの組)</param>
    /// <param name="color">線の色</param>
    /// <param name="dotted">点線で描くか</param>
    private static void DrawEdges(CSED_PoliceRoadGraph graph, IReadOnlyList<Vector2Int> edges, Color color, bool dotted)
    {
        Handles.color = color;
        Vector3 lift = Vector3.up * _displayHeightOffset;
        foreach (Vector2Int edge in edges)
        {
            Vector3 from = graph.GetNode(edge.x) + lift;
            Vector3 to = graph.GetNode(edge.y) + lift;
            if (dotted) Handles.DrawDottedLine(from, to, 4.0f);
            else Handles.DrawAAPolyLine(6.0f, from, to);

            Handles.DrawSolidDisc(from, Vector3.up, 0.8f);
            Handles.DrawSolidDisc(to, Vector3.up, 0.8f);
        }
    }

    /// <summary>
    /// 範囲の箱(下面を塗り、枠線を描く)を描くメソッド
    /// </summary>
    /// <param name="bottom">下面のY</param>
    /// <param name="top">上面のY</param>
    private void DrawBox(float bottom, float top)
    {
        Vector3[] floor =
        {
            new Vector3(_minX, bottom, _minZ), new Vector3(_maxX, bottom, _minZ),
            new Vector3(_maxX, bottom, _maxZ), new Vector3(_minX, bottom, _maxZ),
        };
        Handles.DrawSolidRectangleWithOutline(floor, _areaFillColor, _areaLineColor);

        Handles.color = _areaLineColor;
        Vector3 center = new Vector3((_minX + _maxX) * 0.5f, (bottom + top) * 0.5f, (_minZ + _maxZ) * 0.5f);
        Handles.DrawWireCube(center, new Vector3(_maxX - _minX, top - bottom, _maxZ - _minZ));
    }

    /// <summary>
    /// グループの位置(巡回を始める場所の目安)を、範囲の中か外かが分かる色で描くメソッド
    /// </summary>
    private void DrawSquadPosition()
    {
        if (_squad == null) return;

        Vector3 position = _squad.transform.position;
        bool isInside = position.x >= _minX && position.x <= _maxX && position.z >= _minZ && position.z <= _maxZ;
        Handles.color = isInside ? _areaLineColor : Color.yellow;
        Handles.DrawWireDisc(position, Vector3.up, 2.0f);
        Handles.Label(position + Vector3.up * 2.0f, isInside ? "グループの位置(ここに一番近い道から巡回を始める)" : "グループの位置(範囲の外。範囲の中で一番近い道から巡回を始める)");
    }

    /// <summary>
    /// 入力した範囲から、巡回ルートを作る範囲の箱を作るメソッド(Yを無視する時は、Yを十分に広くする)
    /// </summary>
    private Bounds CreateArea()
    {
        float minY = _ignoreY ? -_unlimitedHeight : _minY;
        float maxY = _ignoreY ? _unlimitedHeight : _maxY;

        Bounds area = new Bounds();
        area.SetMinMax(new Vector3(_minX, minY, _minZ), new Vector3(_maxX, maxY, _maxZ));
        return area;
    }

    /// <summary>
    /// グループの巡回ポイントを、指定した範囲の中の灰色の道の上を一周するように、作り直すメソッド
    /// </summary>
    /// <param name="squad">巡回ポイントを作るグループ</param>
    /// <param name="area">巡回ルートを作る範囲(ワールド座標の箱)</param>
    /// <param name="alleyMargin">路地裏の外側にあける余白(m)</param>
    /// <param name="minTurnAngle">巡回ポイントを作る、道の曲がり角の最小の角度(度)</param>
    /// <param name="includeDisconnected">trueなら、他の道とつながっていない離れた道も回る(falseなら、一番大きくつながった道だけ)</param>
    /// <param name="message">結果の説明(作れなかった時は、その理由)</param>
    /// <returns>作れたらtrue</returns>
    public static bool TryCreateRoute(CS_PoliceSquad squad, Bounds area, float alleyMargin, float minTurnAngle, bool includeDisconnected, out string message)
    {
        CSED_PoliceRoadGraph graph = BuildGraph(squad, area, alleyMargin, includeDisconnected, out int roadCount, out int alleyCount);
        int start = graph.FindNearest(squad.transform.position);
        if (start < 0)
        {
            message = $"指定した範囲の中に、使える灰色の道がありません(道 {roadCount} 本・路地裏 {alleyCount} か所を読み取りました)。\n範囲と、路地裏の余白を確認してください。";
            return false;
        }

        List<int> route = graph.RemoveStraightNodes(graph.CreateLoop(start), minTurnAngle);
        CreatePatrolPoints(squad, graph, route);
        message = $"[PoliceRoute] 灰色の道 {roadCount} 本・路地裏 {alleyCount} か所から、巡回ポイントを {route.Count} 個作りました";
        if (graph.excludedEdges.Count > 0)
        {
            message += $"\n範囲の中に、他の道とつながっていない離れた道が {graph.excludedEdges.Count} 本あり、巡回に入っていません(Sceneビューの橙色)。「離れた道も回る」をオンにすると回ります。";
        }
        return true;
    }

    /// <summary>
    /// シーン(またはPrefab Mode)の灰色の道と路地裏を読み取り、指定した範囲の道の網を作るメソッド
    /// </summary>
    /// <param name="squad">グループ(このグループがあるシーンを読み取る)</param>
    /// <param name="area">巡回ルートを作る範囲</param>
    /// <param name="alleyMargin">路地裏の外側にあける余白(m)</param>
    /// <param name="includeDisconnected">離れた道も使うか</param>
    /// <param name="roadCount">読み取った灰色の道の数</param>
    /// <param name="alleyCount">読み取った路地裏の数</param>
    /// <returns>作った道の網</returns>
    private static CSED_PoliceRoadGraph BuildGraph(CS_PoliceSquad squad, Bounds area, float alleyMargin, bool includeDisconnected, out int roadCount, out int alleyCount)
    {
        List<CSED_PoliceRoadGraph.Road> roads = new List<CSED_PoliceRoadGraph.Road>();
        List<CSED_PoliceRoadGraph.Alley> alleys = new List<CSED_PoliceRoadGraph.Alley>();
        CollectRoadsAndAlleys(squad.gameObject.scene, roads, alleys);

        roadCount = roads.Count;
        alleyCount = alleys.Count;
        return CSED_PoliceRoadGraph.Build(roads, alleys, alleyMargin, area, !includeDisconnected);
    }

    /// <summary>
    /// シーン(またはPrefab Mode)の中にある、灰色の道と路地裏を集めるメソッド
    /// ステージのプレハブには何も足さず、プレハブの元(Road.prefab / BackAlley.prefab)で見分ける
    /// </summary>
    private static void CollectRoadsAndAlleys(UnityEngine.SceneManagement.Scene scene, List<CSED_PoliceRoadGraph.Road> roads, List<CSED_PoliceRoadGraph.Alley> alleys)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform target in root.GetComponentsInChildren<Transform>(true))
            {
                // プレハブを置いた1つ分の、一番上のオブジェクトだけを調べる
                if (PrefabUtility.GetNearestPrefabInstanceRoot(target.gameObject) != target.gameObject) continue;

                GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(target.gameObject);
                if (source == null) continue;

                string path = AssetDatabase.GetAssetPath(source);
                if (path.EndsWith(_roadPrefabName)) TryAddRoad(target, roads);
                else if (path.EndsWith(_alleyPrefabName)) alleys.Add(CreateAlley(target));
            }
        }
    }

    /// <summary>
    /// 灰色の道を、細長ければ中心線のデータにして加えるメソッド
    /// </summary>
    private static void TryAddRoad(Transform target, List<CSED_PoliceRoadGraph.Road> roads)
    {
        Vector3 scale = target.lossyScale;
        float length = Mathf.Max(scale.x, scale.z);
        float width = Mathf.Min(scale.x, scale.z);
        if (width <= 0.0f || length / width < _minRoadRatio) return;

        // 長い方の軸が、道の向き
        Vector3 axis = scale.x >= scale.z ? target.right : target.forward;
        axis.y = 0.0f;
        axis.Normalize();

        Vector3 center = target.position;
        roads.Add(new CSED_PoliceRoadGraph.Road(center - axis * length * 0.5f, center + axis * length * 0.5f, width));
    }

    /// <summary>
    /// 路地裏のオブジェクトから、水平方向の向きのある長方形のデータを作るメソッド
    /// </summary>
    private static CSED_PoliceRoadGraph.Alley CreateAlley(Transform target)
    {
        Vector3 scale = target.lossyScale;
        Quaternion rotation = Quaternion.Euler(0.0f, target.eulerAngles.y, 0.0f);
        return new CSED_PoliceRoadGraph.Alley(target.position, rotation, new Vector2(scale.x, scale.z) * 0.5f);
    }

    /// <summary>
    /// ルートの各ノードに巡回ポイントを作り、グループの子にして、PatrolPointsに設定するメソッド
    /// 前に作った巡回ポイント(RoadPatrolPoint_)は、先に消す
    /// </summary>
    private static void CreatePatrolPoints(CS_PoliceSquad squad, CSED_PoliceRoadGraph graph, List<int> route)
    {
        Undo.IncrementCurrentGroup();
        RemoveOldPoints(squad.transform);

        List<Transform> points = new List<Transform>();
        for (int i = 0; i < route.Count; i++)
        {
            GameObject point = new GameObject(_pointNamePrefix + i.ToString("000"));
            Undo.RegisterCreatedObjectUndo(point, "巡回ポイントを作る");
            point.transform.SetParent(squad.transform, false);
            point.transform.position = graph.GetNode(route[i]);
            points.Add(point.transform);
        }

        SerializedObject serialized = new SerializedObject(squad);
        SerializedProperty property = serialized.FindProperty("_patrolPoints");
        property.arraySize = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(squad);
    }

    /// <summary>
    /// 前に作った巡回ポイント(RoadPatrolPoint_)を、グループの子から消すメソッド
    /// </summary>
    private static void RemoveOldPoints(Transform squadTransform)
    {
        for (int i = squadTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = squadTransform.GetChild(i);
            if (child.name.StartsWith(_pointNamePrefix)) Undo.DestroyObjectImmediate(child.gameObject);
        }
    }
}
