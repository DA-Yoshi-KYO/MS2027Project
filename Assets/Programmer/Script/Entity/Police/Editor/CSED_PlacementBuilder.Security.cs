/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-16 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 配置ツールの、敵(悪人の出現点)・警察のグループ・巡回ドローン・防犯レーザー・ストリートライトを、作る・探す処理
/// 作りは、配置ツール(CSED_FieldMultiLayoutGenerator)が置く物と同じ
/// </summary>
public static partial class CSED_PlacementBuilder
{
    // ---- プレハブ ----
    private const string _villainPrefabPath = _prefabFolder + "Villain/VillainSpawnPoint.prefab";
    private const string _squadPrefabPath = _prefabFolder + "Police/PoliceSquad.prefab";
    private const string _dronePrefabPath = _prefabFolder + "Security/SecurityDroneSpawner.prefab";
    private const string _laserPrefabPath = _prefabFolder + "Security/SecurityLaser.prefab";
    private const string _lightPrefabPath = _prefabFolder + "Security/SecurityLight.prefab";

    // ---- 置く場所(親) ----
    private const string _villainGroupPath = "Villain/VillainSpawnPoints";
    private const string _policeGroup = "Police";
    private const string _droneGroup = "SecurityDrones";
    private const string _laserGroup = "SecurityLasers";
    private const string _lightGroup = "SecurityLights";

    // ---- 大きさ(配置ツールと同じ値) ----
    private const float _laserHeight = 1.0f;                 // レーザーの高さ(足元から。m)
    private const float _laserThickness = 0.3f;              // レーザーの太さ(m)
    private const float _laserExtra = 0.6f;                  // レーザーの長さを、歩ける幅より足す長さ(m)
    private const float _laserMaxDistance = 50.0f;           // レーザーが往復する距離の上限(m)
    private const float _lightHeightOffset = 0.25f;          // ストリートライトを、床から浮かせる高さ(m)
    private const float _lightMaxDistance = 100.0f;          // ストリートライトが往復する距離の上限(m)
    private const float _widthProbe = 30.0f;                 // 通路の幅を測る時に、左右へ光線を飛ばす距離(m)
    private const float _snapRadius = 3.0f;                  // 警察の巡回ポイントを、歩ける場所へ合わせる半径(m)

    /// <summary>
    /// 置いた物の根になる物(警察のグループ・巡回ドローン・レーザー・ライト・悪人の出現点)かを判定するメソッド
    /// </summary>
    private static bool IsSecurityRoot(Transform t)
    {
        return t.GetComponent<CS_PoliceSquad>() != null || t.GetComponent<CS_SecurityDroneSpawner>() != null || t.GetComponent<CS_SecurityLaser>() != null
            || t.GetComponent<CS_SecurityLight>() != null || t.GetComponent<CS_VillainSpawnPoint>() != null;
    }

    private static T LoadPrefab<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogError($"CSED_PlacementBuilder: プレハブが見つかりません({path})");
        return asset;
    }

    // =====================================================================
    // 敵(悪人の出現点)
    // =====================================================================

    /// <summary>
    /// 悪人の出現点を置くメソッド。置く先の犯罪完遂エリア(CrimeArea_01〜03)は、区画分けの図から自動で決める(設定で、手動にもできる)
    /// 出現点は、地面から villainHeight 浮かせて置き、巡回パターンと最初のルート(最初のポイント = クリックした地面)も作る
    /// 巡回ポイントを足す操作は、既存の巡回エディタ(スポーン位置を選ぶと出るツールバー)へ引き継ぐ
    /// </summary>
    /// <param name="ground">クリックした地面の点</param>
    public static GameObject PlaceVillain(Vector3 ground)
    {
        GameObject prefab = LoadPrefab<GameObject>(_villainPrefabPath);
        if (prefab == null) return null;

        int area = CSED_PlacementState.villainAreaAuto ? CSED_FieldMultiLayoutGenerator.GetVillainSector(ground) : CSED_PlacementState.villainArea;
        Transform parent = GetGroup($"{_villainGroupPath}/CrimeArea_0{area}");
        string spawnName = NextVillainName();
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = spawnName;
        instance.transform.position = ground + Vector3.up * CSED_PlacementState.villainHeight;
        instance.transform.rotation = Quaternion.identity;

        // 巡回パターンと、最初のルートを作る(巡回エディタの「悪人を置く」と同じ)
        CS_VillainSpawnPoint spawnPoint = instance.GetComponent<CS_VillainSpawnPoint>();
        CS_VillainPatrolRoute route = CSED_VillainPatrolEditorState.EnsurePattern(spawnPoint);
        CSO_VillainPatrolPattern pattern = route.pattern;
        Undo.RecordObject(pattern, "巡回の悪人を置く");
        Color color = CSO_VillainPatrolPattern.palette[pattern.routes.Count % CSO_VillainPatrolPattern.palette.Length];
        pattern.routes.Add(new CSO_VillainPatrolPattern.Route(color, route.ToLocal(ground)));
        CSED_VillainPatrolEditorState.MarkPatternDirty(route);
        Finish(instance, "悪人の出現点を置く");

        // 選択が変わった後に、巡回エディタの「ポイントを置く」へ切り替える(選択の変更で、巡回エディタの道具が外れるため)
        int routeIndex = pattern.routes.Count - 1;
        EditorApplication.delayCall += () =>
        {
            if (instance == null) return;

            Selection.activeGameObject = instance;
            CSED_VillainPatrolEditorState.Select(routeIndex, 0);
            CSED_VillainPatrolEditorState.tool = CSED_VillainPatrolEditorState.Tool.PlacePoint;
        };
        return instance;
    }

    /// <summary>
    /// 今のフィールド(Field_Solo1)の中の、指定した種類の部品(警察のグループ・巡回ドローンなど)を、すべて返すメソッド(分布の表示に使う)
    /// </summary>
    public static List<T> GetInField<T>() where T : Component
    {
        Transform root = FindFieldRoot();
        return root != null ? new List<T>(root.GetComponentsInChildren<T>(true)) : new List<T>();
    }

    /// <summary>
    /// 今のフィールド(Field_Solo1)の中の、悪人の出現点を、すべて返すメソッド(分布の表示に使う)
    /// </summary>
    public static List<CS_VillainSpawnPoint> GetVillains()
    {
        Transform root = FindFieldRoot();
        return root != null ? new List<CS_VillainSpawnPoint>(root.GetComponentsInChildren<CS_VillainSpawnPoint>(true)) : new List<CS_VillainSpawnPoint>();
    }

    /// <summary>
    /// 悪人の出現点の、次の名前(VillainSpawnPoint_NN。今ある一番大きい番号の次)を返すメソッド
    /// </summary>
    private static string NextVillainName()
    {
        int max = 0;
        Transform root = FindFieldRoot();
        if (root != null)
        {
            foreach (CS_VillainSpawnPoint spawn in root.GetComponentsInChildren<CS_VillainSpawnPoint>(true))
            {
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(spawn.name, @"VillainSpawnPoint_(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int number)) max = Mathf.Max(max, number);
            }
        }
        return $"VillainSpawnPoint_{max + 1:00}";
    }

    // =====================================================================
    // 警察のグループ
    // =====================================================================

    /// <summary>
    /// 警察のグループを作り、最初の巡回ポイントを置くメソッド
    /// 人数・ステータスは、今あるグループ(ひな形)から写す。人数は設定の人数にし、離れて巡回する
    /// </summary>
    public static CS_PoliceSquad BeginSquad(Vector3 first)
    {
        GameObject prefab = LoadPrefab<GameObject>(_squadPrefabPath);
        if (prefab == null) return null;

        Transform group = GetGroup(_policeGroup);
        string squadName = NextName(group, "PoliceSquad_M");
        CS_PoliceSquad template = FindSquadTemplate();
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        instance.name = squadName;
        instance.transform.position = SnapToWalkable(first);

        CS_PoliceSquad squad = instance.GetComponent<CS_PoliceSquad>();
        if (template != null)
        {
            CopyScriptValues(template.gameObject, instance);
            ApplyMembers(squad, template);
        }
        else
        {
            Debug.LogWarning("CSED_PlacementBuilder: ひな形になる警察のグループが見つからないため、ステータスが空のままです(Inspectorで、メンバーのステータスを入れてください)");
        }

        Undo.RegisterCreatedObjectUndo(instance, "警察のグループを置く");
        Undo.SetCurrentGroupName("警察のグループを置く");
        CSED_PlacementState.RememberCreated(instance);
        AddSquadPoint(squad, first);
        Selection.activeGameObject = instance;
        return squad;
    }

    /// <summary>
    /// 警察のグループに、巡回ポイントを1つ足すメソッド
    /// </summary>
    public static void AddSquadPoint(CS_PoliceSquad squad, Vector3 position)
    {
        GameObject point = new GameObject($"RoadPatrolPoint_{CountPoints(squad.transform):000}");
        point.transform.SetParent(squad.transform, false);
        point.transform.position = SnapToWalkable(position);
        Undo.RegisterCreatedObjectUndo(point, "警察の巡回ポイントを足す");
        EditorUtility.SetDirty(squad);
    }

    /// <summary>
    /// 巡回ポイント(子オブジェクト)の数を返すメソッド
    /// </summary>
    public static int CountPoints(Transform parent)
    {
        return parent != null ? parent.childCount : 0;
    }

    private static CS_PoliceSquad FindSquadTemplate()
    {
        Transform root = FindFieldRoot();
        return root != null ? root.GetComponentInChildren<CS_PoliceSquad>(true) : null;
    }

    /// <summary>
    /// グループの人数を設定の人数にして、ステータスをひな形の先頭と同じにし、離れて巡回するようにするメソッド
    /// </summary>
    private static void ApplyMembers(CS_PoliceSquad squad, CS_PoliceSquad template)
    {
        SerializedProperty templateStatuses = new SerializedObject(template).FindProperty("_memberStatuses");
        if (templateStatuses == null || templateStatuses.arraySize == 0) return;

        Object status = templateStatuses.GetArrayElementAtIndex(0).objectReferenceValue;
        SerializedObject serialized = new SerializedObject(squad);
        SerializedProperty statuses = serialized.FindProperty("_memberStatuses");
        statuses.arraySize = CSED_PlacementState.policeMembers;
        for (int i = 0; i < statuses.arraySize; i++) statuses.GetArrayElementAtIndex(i).objectReferenceValue = status;
        serialized.FindProperty("_spreadPatrol").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 点を、近くの歩ける場所(NavMesh)へ合わせるメソッド(設定がオフ・近くに無ければ、そのまま)
    /// </summary>
    private static Vector3 SnapToWalkable(Vector3 position)
    {
        if (!CSED_PlacementState.policeSnapNavMesh) return position;

        return NavMesh.SamplePosition(position, out NavMeshHit hit, _snapRadius, NavMesh.AllAreas) ? hit.position : position;
    }

    // =====================================================================
    // 巡回ドローン
    // =====================================================================

    /// <summary>
    /// 巡回ドローン(出現クラス)を作り、最初の巡回ポイントを置くメソッド(プレハブに入っている、仮の巡回ポイントは消す)
    /// </summary>
    public static CS_SecurityDroneSpawner BeginDrone(Vector3 first)
    {
        GameObject prefab = LoadPrefab<GameObject>(_dronePrefabPath);
        if (prefab == null) return null;

        Transform group = GetGroup(_droneGroup);
        string droneName = NextName(group, "SecurityDroneSpawner_M");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        instance.name = droneName;
        instance.transform.position = first + Vector3.up * CSED_PlacementState.droneHeight;

        // プレハブの仮の巡回ポイントは消す(クリックした点だけにする)
        List<GameObject> defaults = new List<GameObject>();
        foreach (Transform child in instance.transform) defaults.Add(child.gameObject);
        foreach (GameObject child in defaults) Undo.DestroyObjectImmediate(child);

        CS_SecurityDroneSpawner drone = instance.GetComponent<CS_SecurityDroneSpawner>();
        Undo.RegisterCreatedObjectUndo(instance, "巡回ドローンを置く");
        Undo.SetCurrentGroupName("巡回ドローンを置く");
        CSED_PlacementState.RememberCreated(instance);
        AddDronePoint(drone, first);
        Selection.activeGameObject = instance;
        return drone;
    }

    /// <summary>
    /// 巡回ドローンに、巡回ポイントを1つ足すメソッド(クリックした面から、設定の高さだけ上に置く)
    /// </summary>
    public static void AddDronePoint(CS_SecurityDroneSpawner drone, Vector3 position)
    {
        GameObject point = new GameObject($"PatrolPoint_{CountPoints(drone.transform)}");
        point.transform.SetParent(drone.transform, false);
        point.transform.position = position + Vector3.up * CSED_PlacementState.droneHeight;
        Undo.RegisterCreatedObjectUndo(point, "ドローンの巡回ポイントを足す");
        EditorUtility.SetDirty(drone);
    }

    // =====================================================================
    // 防犯レーザー・ストリートライト(始点と終点の2クリック)
    // =====================================================================

    /// <summary>
    /// 始点から終点まで、通路の左右の壁までの幅(m)の、一番狭い値を測るメソッド(2mおきに、左右へ光線を飛ばす)
    /// どこかで左右のどちらかに壁が無ければ、測れないので 0 を返す
    /// </summary>
    public static float MeasureWidth(Vector3 start, Vector3 direction, float distance)
    {
        PhysicsScene physics = GetPhysicsScene();
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        float minWidth = float.MaxValue;
        for (float t = 0.0f; t <= distance + 0.01f; t += 2.0f)
        {
            Vector3 origin = start + direction * t + Vector3.up * _laserHeight;
            if (!physics.Raycast(origin, side, out RaycastHit left, _widthProbe, ~0, QueryTriggerInteraction.Ignore)
                || !physics.Raycast(origin, -side, out RaycastHit right, _widthProbe, ~0, QueryTriggerInteraction.Ignore)) return 0.0f;

            minWidth = Mathf.Min(minWidth, left.distance + right.distance);
        }
        return minWidth == float.MaxValue ? 0.0f : minWidth;
    }

    /// <summary>
    /// レーザーの長さ(m)を求めるメソッド(自動なら、通路の幅 + 余り。測れなければ、設定の長さ)
    /// </summary>
    public static float GetLaserLength(Vector3 start, Vector3 end)
    {
        if (!CSED_PlacementState.laserBeamAuto) return Mathf.Max(0.1f, CSED_PlacementState.laserBeamLength);

        Vector3 delta = Vector3.ProjectOnPlane(end - start, Vector3.up);
        float width = delta.sqrMagnitude > 0.01f ? MeasureWidth(start, delta.normalized, Mathf.Min(delta.magnitude, _laserMaxDistance)) : 0.0f;
        return width > 0.0f ? width + _laserExtra : Mathf.Max(0.1f, CSED_PlacementState.laserBeamLength);
    }

    /// <summary>
    /// ライトの円の直径(m)を求めるメソッド(自動なら、道幅 - 1m を 8〜20mに収める。測れなければ、設定の直径)
    /// </summary>
    public static float GetLightDiameter(Vector3 start, Vector3 end)
    {
        if (!CSED_PlacementState.lightDiameterAuto) return Mathf.Max(1.0f, CSED_PlacementState.lightDiameter);

        Vector3 delta = Vector3.ProjectOnPlane(end - start, Vector3.up);
        float width = delta.sqrMagnitude > 0.01f ? MeasureWidth(start, delta.normalized, Mathf.Min(delta.magnitude, _lightMaxDistance)) : 0.0f;
        return width > 0.0f ? Mathf.Clamp(width - 1.0f, 8.0f, 20.0f) : Mathf.Max(1.0f, CSED_PlacementState.lightDiameter);
    }

    /// <summary>
    /// 防犯レーザーを置くメソッド。始点を中心に、終点の向きへ、終点まで往復する(棒は、往復する向きに直角に伸びる)
    /// </summary>
    public static GameObject PlaceLaser(Vector3 start, Vector3 end)
    {
        Vector3 delta = Vector3.ProjectOnPlane(end - start, Vector3.up);
        if (delta.magnitude < 0.5f)
        {
            Debug.LogWarning("CSED_PlacementBuilder: 始点と終点が近すぎます(0.5m以上離してください)");
            return null;
        }

        GameObject prefab = LoadPrefab<GameObject>(_laserPrefabPath);
        if (prefab == null) return null;

        Vector3 direction = delta.normalized;
        float distance = Mathf.Min(delta.magnitude, _laserMaxDistance);
        float length = GetLaserLength(start, end);
        Transform group = GetGroup(_laserGroup);
        string laserName = NextName(group, "SecurityLaser");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        instance.name = laserName;
        instance.transform.SetPositionAndRotation(start + Vector3.up * _laserHeight, Quaternion.LookRotation(direction));

        SerializedObject serialized = new SerializedObject(instance.GetComponent<CS_SecurityLaser>());
        serialized.FindProperty("_beamLength").floatValue = length;
        serialized.FindProperty("_beamThickness").floatValue = _laserThickness;
        serialized.FindProperty("_moveDirection").vector3Value = Vector3.forward;
        serialized.FindProperty("_moveDistance").floatValue = distance;
        serialized.ApplyModifiedProperties();
        Finish(instance, "防犯レーザーを置く");
        return instance;
    }

    /// <summary>
    /// ストリートライトを作り、最初の経路の点を置くメソッド(プレハブに入っている経路の点は消す)
    /// 経路の点は、子の「RoutePoints」の、さらに子として並べる(クリックするたびに足す)
    /// </summary>
    public static CS_SecurityLight BeginLight(Vector3 first)
    {
        GameObject prefab = LoadPrefab<GameObject>(_lightPrefabPath);
        if (prefab == null) return null;

        Transform group = GetGroup(_lightGroup);
        string lightName = NextName(group, "SecurityLight");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        instance.name = lightName;
        instance.transform.SetPositionAndRotation(first + Vector3.up * _lightHeightOffset, Quaternion.identity);

        // プレハブに経路の入れ物があれば、中身を消して使い、無ければ作る
        Transform routeRoot = instance.transform.Find(CS_SecurityLight.routeRootName);
        if (routeRoot == null)
        {
            routeRoot = new GameObject(CS_SecurityLight.routeRootName).transform;
            routeRoot.SetParent(instance.transform, false);
        }
        List<GameObject> defaults = new List<GameObject>();
        foreach (Transform child in routeRoot) defaults.Add(child.gameObject);
        foreach (GameObject child in defaults) Undo.DestroyObjectImmediate(child);

        CS_SecurityLight light = instance.GetComponent<CS_SecurityLight>();
        SerializedObject serialized = new SerializedObject(light);
        serialized.FindProperty("_moveDirection").vector3Value = Vector3.forward;
        serialized.FindProperty("_moveDistance").floatValue = 0.0f;
        serialized.FindProperty("_diameter").floatValue = Mathf.Max(1.0f, CSED_PlacementState.lightDiameter);
        serialized.FindProperty("_loopRoute").boolValue = CSED_PlacementState.lightLoop;
        serialized.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(instance, "ストリートライトを置く");
        Undo.SetCurrentGroupName("ストリートライトを置く");
        CSED_PlacementState.RememberCreated(instance);
        AddLightPoint(light, first);
        Selection.activeGameObject = instance;
        return light;
    }

    /// <summary>
    /// ストリートライトに、経路の点を1つ足すメソッド(ライトと同じ高さ。足したあと、円の直径を測り直す)
    /// </summary>
    public static void AddLightPoint(CS_SecurityLight light, Vector3 position)
    {
        Transform routeRoot = light.transform.Find(CS_SecurityLight.routeRootName);
        if (routeRoot == null) return;

        GameObject point = new GameObject($"RoutePoint_{CountPoints(routeRoot)}");
        point.transform.SetParent(routeRoot, false);
        point.transform.position = position + Vector3.up * _lightHeightOffset;
        Undo.RegisterCreatedObjectUndo(point, "ライトの経路の点を足す");
        UpdateLightDiameter(light);
        EditorUtility.SetDirty(light);
    }

    /// <summary>
    /// ライトの円の直径を、経路のすべての区間で測った、一番狭い道幅に合わせるメソッド(自動がオフなら、設定の直径のまま)
    /// </summary>
    public static void UpdateLightDiameter(CS_SecurityLight light)
    {
        Transform routeRoot = light.transform.Find(CS_SecurityLight.routeRootName);
        float diameter = Mathf.Max(1.0f, CSED_PlacementState.lightDiameter);
        if (routeRoot != null && CSED_PlacementState.lightDiameterAuto && routeRoot.childCount >= 2)
        {
            float minimum = float.MaxValue;
            bool measured = true;
            for (int i = 1; i < routeRoot.childCount && measured; i++)
            {
                Vector3 from = routeRoot.GetChild(i - 1).position;
                Vector3 to = routeRoot.GetChild(i).position;
                Vector3 delta = Vector3.ProjectOnPlane(to - from, Vector3.up);
                float width = delta.sqrMagnitude > 0.01f ? MeasureWidth(from, delta.normalized, Mathf.Min(delta.magnitude, _lightMaxDistance)) : 0.0f;
                if (width <= 0.0f) measured = false;
                else minimum = Mathf.Min(minimum, width);
            }
            if (measured && minimum != float.MaxValue) diameter = Mathf.Clamp(minimum - 1.0f, 8.0f, 20.0f);
        }

        SerializedObject serialized = new SerializedObject(light);
        serialized.FindProperty("_diameter").floatValue = diameter;
        serialized.ApplyModifiedProperties();
    }

    // =====================================================================
    // 消す時の、見えない物の探し方
    // =====================================================================

    /// <summary>
    /// 画面上で、マウスの一番近くにある、見えない物(警察のグループ・巡回ドローン・悪人の出現点。巡回ポイントも含む)を探すメソッド
    /// これらは、シーンで描かれない・当たり判定が無いので、クリックでは選べないため、位置の近さで選ぶ
    /// </summary>
    /// <param name="maxPixels">この距離(画面上のピクセル)より遠い物は、選ばない</param>
    public static Transform FindNearestInvisible(Vector2 mousePosition, float maxPixels, out Vector3 nearPoint)
    {
        nearPoint = Vector3.zero;
        Transform field = FindFieldRoot();
        if (field == null) return null;

        Transform best = null;
        float bestDistance = maxPixels;
        foreach (Component component in field.GetComponentsInChildren<Component>(true))
        {
            if (!(component is CS_PoliceSquad) && !(component is CS_SecurityDroneSpawner) && !(component is CS_VillainSpawnPoint)) continue;

            List<Vector3> points = new List<Vector3> { component.transform.position };
            if (!(component is CS_VillainSpawnPoint)) foreach (Transform child in component.transform) points.Add(child.position);
            foreach (Vector3 point in points)
            {
                float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(point), mousePosition);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = component.transform;
                nearPoint = point;
            }
        }
        return best;
    }

    // =====================================================================
    // 設定値のコピー
    // =====================================================================

    /// <summary>
    /// 元のゲームオブジェクトのスクリプトの設定値を、コピーへ写すメソッド(参照は、アセットだけ写す)
    /// </summary>
    private static void CopyScriptValues(GameObject source, GameObject copy)
    {
        MonoBehaviour[] sourceScripts = source.GetComponents<MonoBehaviour>();
        MonoBehaviour[] copyScripts = copy.GetComponents<MonoBehaviour>();
        for (int i = 0; i < sourceScripts.Length && i < copyScripts.Length; i++)
        {
            if (sourceScripts[i] == null || copyScripts[i] == null || sourceScripts[i].GetType() != copyScripts[i].GetType()) continue;

            SerializedObject from = new SerializedObject(sourceScripts[i]);
            SerializedObject to = new SerializedObject(copyScripts[i]);
            SerializedProperty property = from.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.ObjectReference;
                if (property.name == "m_Script") continue;
                if (property.propertyType == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue != null && !EditorUtility.IsPersistent(property.objectReferenceValue)) continue;

                to.CopyFromSerializedProperty(property);
            }
            to.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
