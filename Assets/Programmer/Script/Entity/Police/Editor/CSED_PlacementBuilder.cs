/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-16 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 配置ツールの、SAObject・ハイドポイント・信号機・グラップル・動くSAポイントを、作る・消す処理
/// 置く場所(親)は、Field_Solo1 の中の SAObjects / HidePoints / ActionObjects(無ければ作る)。Field_Solo1 が無ければ、シーンの「配置ツール」に置く
/// SAObjectとハイドポイントの作りは、配置ツール(CSED_FieldMultiLayoutGenerator)と同じ(ハイドポイントは、SAObjectの根元と同じ位置に置く)
/// すべてUndoできる
/// </summary>
public static partial class CSED_PlacementBuilder
{
    // ---- プレハブ・マテリアル ----
    private const string _prefabFolder = "Assets/Programmer/Prefab/Field/FieldElement/";
    public const string hidePointPrefabPath = _prefabFolder + "HidePoint/HidePoint.prefab";
    public const string trafficLightPrefabPath = _prefabFolder + "TrafficLight/TrafficLight.prefab";
    public const string grapplePrefabPath = _prefabFolder + "GrapplePoint/GrapplePoint.prefab";
    private const string _headMaterialPath = "Assets/Programmer/Material/MT_SAPoint.mat";                       // SAObjectの頭のマテリアル
    private const string _poleMaterialPath = "Assets/Programmer/PlaceholderAsset/Field/Material/Road.mat";      // SAObjectの柱のマテリアル
    public const string fieldPrefabPath = _prefabFolder + "Field_Solo1.prefab";

    // ---- 置く場所(親)の名前 ----
    private const string _fieldName = "Field_Solo1";
    public const string saObjectGroup = "SAObjects";
    public const string hidePointGroup = "HidePoints";
    public const string actionGroup = "ActionObjects";
    private const string _fallbackRootName = "配置ツール";

    // ---- SAObjectの大きさ・SAできる範囲の決め方(配置ツールと同じ値) ----
    private const float _headSize = 2.0f;               // SAObjectの頭(箱)の大きさ(m)
    private const float _poleWidth = 0.5f;              // SAObjectの柱の太さ(m)
    private const float _groundHeight = 3.0f;           // これより低い所は、地上とみなす(m)
    public const float groundPoleHeight = 8.0f;         // 地上のSAObjectの柱の高さ(m)
    public const float roofPoleHeight = 4.0f;           // 屋上のSAObjectの柱の高さ(m)
    private const float _maxFlightDistance = 22.0f;     // SAで飛んで着くまでの距離の上限(m)
    private const float _maxReachHalf = 18.0f;          // 足元の範囲の箱の、水平方向の半分の大きさの上限(m)
    private const float _samePosition = 0.05f;          // SAObjectとハイドポイントが、同じ位置とみなす差(m)

    /// <summary>
    /// 今開いているステージ(シーン・プレハブ編集中のプレハブ)の物理シーンを返すメソッド(クリックした先の地面・壁を調べるのに使う)
    /// </summary>
    public static PhysicsScene GetPhysicsScene()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        return stage != null ? stage.scene.GetPhysicsScene() : Physics.defaultPhysicsScene;
    }

    /// <summary>
    /// Sceneビューのマウス位置から、地面・壁などに当たる点を求めるメソッド(トリガーは無視する)
    /// </summary>
    public static bool TryRaycast(Vector2 mousePosition, out RaycastHit hit)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        return GetPhysicsScene().Raycast(ray.origin, ray.direction, out hit, 2000.0f, ~0, QueryTriggerInteraction.Ignore);
    }

    // =====================================================================
    // 置く場所
    // =====================================================================

    /// <summary>
    /// 置く先のフィールド(Field_Solo1)を探すメソッド。今のステージ(プレハブ編集中ならそのプレハブ、そうでなければ開いているシーン)の中で、アクティブなシーンを優先する。無ければ null
    /// </summary>
    private static Transform FindFieldRoot()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        Scene active = stage != null ? stage.scene : SceneManager.GetActiveScene();
        foreach (GameObject root in active.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == _fieldName) return candidate;
            }
        }

        foreach (Transform candidate in StageUtility.GetCurrentStageHandle().FindComponentsOfType<Transform>())
        {
            if (candidate.name == _fieldName) return candidate;
        }
        return null;
    }

    /// <summary>
    /// 置く先の親(Field_Solo1 の中の groupName。「Villain/VillainSpawnPoints」のように、/ で区切った入れ子も可。無ければ作る)を返すメソッド
    /// </summary>
    public static Transform GetGroup(string groupName)
    {
        Transform root = FindFieldRoot();
        if (root == null)
        {
            // Field_Solo1 が無い時は、シーンの「配置ツール」の中に置く
            GameObject fallback = null;
            foreach (Transform candidate in StageUtility.GetCurrentStageHandle().FindComponentsOfType<Transform>())
            {
                if (candidate.name == _fallbackRootName && candidate.parent == null) fallback = candidate.gameObject;
            }
            if (fallback == null)
            {
                fallback = new GameObject(_fallbackRootName);
                StageUtility.PlaceGameObjectInCurrentStage(fallback);
                Undo.RegisterCreatedObjectUndo(fallback, "配置ツールの親を作る");
            }
            root = fallback.transform;
        }

        Transform group = root;
        foreach (string part in groupName.Split('/'))
        {
            Transform next = group.Find(part);
            if (next == null)
            {
                GameObject created = new GameObject(part);
                created.transform.SetParent(group, false);
                Undo.RegisterCreatedObjectUndo(created, $"{part}を作る");
                next = created.transform;
            }
            group = next;
        }
        return group;
    }

    /// <summary>
    /// 親の中の、次の名前(1つ目は「名前」、2つ目から「名前 (1)」「名前 (2)」…。今ある一番大きい番号の次)を返すメソッド
    /// </summary>
    private static string NextName(Transform parent, string baseName)
    {
        int max = -1;
        foreach (Transform child in parent)
        {
            if (child.name == baseName) max = Mathf.Max(max, 0);
            else if (child.name.StartsWith(baseName + " (") && child.name.EndsWith(")") && int.TryParse(child.name.Substring(baseName.Length + 2, child.name.Length - baseName.Length - 3), out int number)) max = Mathf.Max(max, number);
        }
        return max < 0 ? baseName : $"{baseName} ({max + 1})";
    }

    private static void Finish(GameObject created, string undoName)
    {
        Undo.RegisterCreatedObjectUndo(created, undoName);
        Undo.SetCurrentGroupName(undoName);
        CSED_PlacementState.RememberCreated(created);
        Selection.activeGameObject = created;
        Physics.SyncTransforms();
    }

    // =====================================================================
    // SAObject + ハイドポイント
    // =====================================================================

    /// <summary>
    /// 根元の高さから、柱の高さを決めるメソッド(設定が自動なら、地上 8m / 屋上 4m)
    /// </summary>
    public static float GetPoleHeight(Vector3 basePosition)
    {
        if (!CSED_PlacementState.autoPoleHeight) return CSED_PlacementState.manualPoleHeight;

        return basePosition.y < _groundHeight ? groundPoleHeight : roofPoleHeight;
    }

    /// <summary>
    /// 足元の範囲の箱(SAReachBox)の、中心と大きさを求めるメソッド
    /// 箱は、飛び立つ足元の一番低い高さ(feetLowY)から、着く位置(stand)の少し上までを含み、水平方向は、箱のどこからでも飛んで着ける広さにする
    /// </summary>
    public static void GetReachBox(Vector3 root, Vector3 stand, float feetLowY, out Vector3 center, out Vector3 size)
    {
        float heightDifference = Mathf.Max(0.0f, stand.y - feetLowY);
        float half = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.0f, _maxFlightDistance * _maxFlightDistance - heightDifference * heightDifference)), 4.0f, _maxReachHalf);
        center = new Vector3(root.x, (feetLowY + stand.y) * 0.5f, root.z);
        size = new Vector3(half * 2.0f, heightDifference + 2.0f, half * 2.0f);
    }

    /// <summary>
    /// SAObject(柱 + 頭の箱)と、その頭の上へ飛ぶハイドポイントを、セットで置くメソッド
    /// </summary>
    /// <param name="basePosition">柱の根元(クリックした面の点)</param>
    public static GameObject PlaceSaObject(Vector3 basePosition)
    {
        Transform saGroup = GetGroup(saObjectGroup);
        Transform hideGroup = GetGroup(hidePointGroup);
        float poleHeight = GetPoleHeight(basePosition);

        // SAObject: 頭(Cube)、柱(Cube (1))の順(配置ツールと同じ)
        GameObject root = new GameObject(NextName(saGroup, "SAObject"));
        root.transform.SetParent(saGroup, false);
        root.transform.position = basePosition;
        int layer = FindSaLayer(saGroup);
        CreateCube(root.transform, "Cube", basePosition + Vector3.up * (poleHeight + _headSize * 0.5f), Vector3.one * _headSize, AssetDatabase.LoadAssetAtPath<Material>(_headMaterialPath), layer);
        CreateCube(root.transform, "Cube (1)", basePosition + Vector3.up * (poleHeight * 0.5f), new Vector3(_poleWidth, poleHeight, _poleWidth), AssetDatabase.LoadAssetAtPath<Material>(_poleMaterialPath), layer);
        Undo.RegisterCreatedObjectUndo(root, "SAObjectを置く");

        // ハイドポイント: SAObjectの根元と同じ位置に置き、頭の上へ飛ぶ
        Physics.SyncTransforms();
        Vector3 stand = basePosition + Vector3.up * (poleHeight + _headSize);
        GameObject hide = CreateHidePoint(hideGroup, basePosition, stand, basePosition.y - CSED_PlacementState.reachDown, CSED_PlacementState.keepLight);
        if (hide != null) Undo.RegisterCreatedObjectUndo(hide, "ハイドポイントを置く");

        Undo.SetCurrentGroupName("SAObjectとハイドポイントを置く");
        CSED_PlacementState.RememberCreated(root);
        if (hide != null) CSED_PlacementState.RememberCreated(hide);
        Selection.activeGameObject = root;
        Physics.SyncTransforms();
        return root;
    }

    /// <summary>
    /// ハイドポイントだけを、クリックした面の上に置くメソッド(その面の上に立って隠れる)
    /// </summary>
    /// <param name="standPosition">クリックした面の点(着く足元の位置)</param>
    public static GameObject PlaceHidePointOnly(Vector3 standPosition)
    {
        Transform hideGroup = GetGroup(hidePointGroup);
        GameObject hide = CreateHidePoint(hideGroup, standPosition, standPosition, standPosition.y - CSED_PlacementState.reachDown, CSED_PlacementState.keepLight);
        if (hide == null) return null;

        Finish(hide, "ハイドポイントを置く");
        return hide;
    }

    /// <summary>
    /// ハイドポイントのプレハブを置いて、着く位置・隠れられる箱・SAできる範囲の箱・ライトを合わせるメソッド
    /// </summary>
    /// <param name="root">ハイドポイントの位置(SAObjectの根元)</param>
    /// <param name="stand">飛んで着く足元の位置</param>
    /// <param name="feetLowY">飛び立つ足元の一番低い高さ</param>
    private static GameObject CreateHidePoint(Transform parent, Vector3 root, Vector3 stand, float feetLowY, bool keepLight)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(hidePointPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"CSED_PlacementBuilder: ハイドポイントのプレハブが見つかりません({hidePointPrefabPath})");
            return null;
        }

        string instanceName = NextName(parent, "HidePoint");     // 名前は、プレハブを置く前に決める(置いた物自身を数えないため)
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = instanceName;
        instance.transform.position = root;
        instance.transform.rotation = Quaternion.identity;

        Transform standPoint = instance.transform.Find("StandPoint");
        Transform hideBox = instance.transform.Find("HideBox");
        Transform reachBox = instance.transform.Find("SAReachBox");
        Transform pointLight = instance.transform.Find("Point Light");
        Vector3 localStand = stand + Vector3.up * 0.05f;
        standPoint.position = localStand;
        standPoint.rotation = Quaternion.LookRotation(FindWallDirection(stand));
        hideBox.position = localStand + Vector3.up * 1.65f;

        GetReachBox(root, stand, feetLowY, out Vector3 reachCenter, out Vector3 reachSize);
        reachBox.position = reachCenter;
        reachBox.localScale = reachSize;
        if (pointLight != null)
        {
            pointLight.position = localStand + Vector3.up * 0.35f;
            pointLight.gameObject.SetActive(keepLight);
        }
        return instance;
    }

    /// <summary>
    /// 着いた時に向く向き(隠れる物の方)を、一番近い壁の方向から決めるメソッド(8方向を調べる。どこにも壁が無ければ前向き)
    /// </summary>
    private static Vector3 FindWallDirection(Vector3 stand)
    {
        PhysicsScene physics = GetPhysicsScene();
        Vector3 best = Vector3.forward;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0.0f, i * 45.0f, 0.0f) * Vector3.forward;
            if (!physics.Raycast(stand + Vector3.up * 1.0f, direction, out RaycastHit hit, 8.0f, ~0, QueryTriggerInteraction.Ignore) || hit.distance >= bestDistance) continue;

            bestDistance = hit.distance;
            best = direction;
        }
        return best;
    }

    private static void CreateCube(Transform parent, string cubeName, Vector3 center, Vector3 size, Material material, int layer)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = cubeName;
        cube.transform.SetParent(parent, false);
        cube.transform.position = center;
        cube.transform.localScale = size;
        cube.layer = layer;
        if (material != null) cube.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    /// <summary>
    /// 今あるSAObjectの柱のレイヤーを調べるメソッド(同じレイヤーにそろえる。無ければデフォルト)
    /// </summary>
    private static int FindSaLayer(Transform saGroup)
    {
        foreach (Transform sa in saGroup)
        {
            Transform pole = sa.Find("Cube (1)");
            if (pole != null) return pole.gameObject.layer;
        }
        return 0;
    }

    // =====================================================================
    // 信号機・グラップル
    // =====================================================================

    /// <summary>
    /// 信号機(飛ぶだけのSAポイント)を置くメソッド
    /// </summary>
    public static GameObject PlaceTrafficLight(Vector3 basePosition)
    {
        GameObject instance = InstantiateAction(trafficLightPrefabPath, "TrafficLight", basePosition, Quaternion.identity);
        if (instance != null) Finish(instance, "信号機を置く");
        return instance;
    }

    /// <summary>
    /// グラップル(的)を置くメソッド。引っ張られる位置(StandPoint)は、的の手前(プレハブの-Z側)にあるので、飛んで来る側(カメラ側)に向ける
    /// </summary>
    /// <param name="targetPosition">的の位置(クリックした面の点)</param>
    /// <param name="cameraPosition">Sceneビューのカメラの位置(手前の向きを決める)</param>
    public static GameObject PlaceGrapple(Vector3 targetPosition, Vector3 cameraPosition)
    {
        Quaternion rotation = Quaternion.identity;
        if (CSED_PlacementState.grappleFaceCamera)
        {
            Vector3 away = Vector3.ProjectOnPlane(targetPosition - cameraPosition, Vector3.up);
            if (away.sqrMagnitude > 0.01f) rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        GameObject instance = InstantiateAction(grapplePrefabPath, "GrapplePoint", targetPosition, rotation);
        if (instance == null) return null;

        ApplyGrappleValues(instance);
        Finish(instance, "グラップルを置く");
        return instance;
    }

    /// <summary>
    /// 設定のグラップルの値(引っ張る速さ・放す距離の割合・放した後の減速)を、置いたグラップルへ入れるメソッド
    /// </summary>
    public static void ApplyGrappleValues(GameObject grapple)
    {
        CS_GrappleSAPoint point = grapple.GetComponent<CS_GrappleSAPoint>();
        if (point == null) return;

        SerializedObject serialized = new SerializedObject(point);
        serialized.FindProperty("_pullSpeed").floatValue = Mathf.Max(0.1f, CSED_PlacementState.pullSpeed);
        serialized.FindProperty("_releaseDistanceRate").floatValue = Mathf.Clamp(CSED_PlacementState.releaseDistanceRate, 0.05f, 1.0f);
        serialized.FindProperty("_releaseDrag").floatValue = Mathf.Max(0.0f, CSED_PlacementState.releaseDrag);
        serialized.ApplyModifiedProperties();
    }

    private static GameObject InstantiateAction(string prefabPath, string baseName, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"CSED_PlacementBuilder: プレハブが見つかりません({prefabPath})");
            return null;
        }

        Transform group = GetGroup(actionGroup);
        string instanceName = NextName(group, baseName);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        instance.name = instanceName;
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    // =====================================================================
    // 動くSAポイント(CS_PathFollower)
    // =====================================================================

    /// <summary>
    /// 動くSAポイント(CS_PathFollower + 動かすSAポイント + 制御点)を作り、最初の制御点を置くメソッド
    /// 構造: 動くSAポイント(CS_PathFollower。動かない) ├ SAポイント(これが動く) └ PathPoints(制御点の親)
    /// </summary>
    public static CS_PathFollower BeginMoving(Vector3 firstPoint)
    {
        string saPath = CSED_PlacementState.movingKind == CSED_PlacementState.MovingKind.Grapple ? grapplePrefabPath
            : (CSED_PlacementState.movingKind == CSED_PlacementState.MovingKind.HidePoint ? hidePointPrefabPath : trafficLightPrefabPath);
        GameObject saPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(saPath);
        if (saPrefab == null)
        {
            Debug.LogError($"CSED_PlacementBuilder: プレハブが見つかりません({saPath})");
            return null;
        }

        Transform group = GetGroup(actionGroup);
        GameObject root = new GameObject(NextName(group, "MovingSAPoint"));
        root.transform.SetParent(group, false);
        root.transform.position = firstPoint;
        CS_PathFollower follower = root.AddComponent<CS_PathFollower>();

        GameObject sa = (GameObject)PrefabUtility.InstantiatePrefab(saPrefab, root.transform);
        sa.transform.localPosition = Vector3.zero;
        sa.transform.localRotation = Quaternion.identity;
        if (CSED_PlacementState.movingKind == CSED_PlacementState.MovingKind.Grapple) ApplyGrappleValues(sa);

        GameObject pathPoints = new GameObject("PathPoints");
        pathPoints.transform.SetParent(root.transform, false);

        SerializedObject serialized = new SerializedObject(follower);
        serialized.FindProperty("_saPoint").objectReferenceValue = sa.GetComponent<CS_SAPoint>();
        serialized.FindProperty("_mode").enumValueIndex = (int)CSED_PlacementState.movingMode;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(root, "動くSAポイントを置く");
        Undo.SetCurrentGroupName("動くSAポイントを置く");
        CSED_PlacementState.RememberCreated(root);
        AddMovingPoint(follower, firstPoint);
        Selection.activeGameObject = root;
        return follower;
    }

    /// <summary>
    /// 動くSAポイントに、制御点を1つ足すメソッド(設定の待ち時間・速さを入れる)
    /// </summary>
    public static void AddMovingPoint(CS_PathFollower follower, Vector3 position)
    {
        Transform pathPoints = follower.transform.Find("PathPoints");
        if (pathPoints == null) return;

        GameObject point = new GameObject($"Point_{pathPoints.childCount}");
        point.transform.SetParent(pathPoints, false);
        point.transform.position = position + Vector3.up * CSED_PlacementState.movingHeight;
        CS_PathFollowerPoint component = point.AddComponent<CS_PathFollowerPoint>();
        SerializedObject serialized = new SerializedObject(component);
        serialized.FindProperty("_waitTime").floatValue = Mathf.Max(0.0f, CSED_PlacementState.movingWait);
        serialized.FindProperty("_speedToNext").floatValue = Mathf.Max(0.1f, CSED_PlacementState.movingSpeed);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Undo.RegisterCreatedObjectUndo(point, "動くSAポイントの制御点を足す");
        if (CountMovingPoints(follower) >= 2) follower.RefreshForEditor();     // 制御点が1つだけの時は、CS_PathFollowerが読み込めない(2つ以上必要)ので、呼ばない
        EditorUtility.SetDirty(follower);
    }

    /// <summary>
    /// 動くSAポイントの、制御点の数を返すメソッド
    /// </summary>
    public static int CountMovingPoints(CS_PathFollower follower)
    {
        Transform pathPoints = follower != null ? follower.transform.Find("PathPoints") : null;
        return pathPoints != null ? pathPoints.childCount : 0;
    }

    // =====================================================================
    // 消す
    // =====================================================================

    /// <summary>
    /// クリックした物(の親をたどって)から、置いた物の根(消す対象)を探すメソッド
    /// 動くSAポイント(CS_PathFollower)・SAObject・ハイドポイント・信号機・グラップルが対象
    /// </summary>
    public static Transform FindPlacedRoot(GameObject picked)
    {
        if (picked == null) return null;

        Transform saPoint = null;
        for (Transform t = picked.transform; t != null; t = t.parent)
        {
            if (t.GetComponent<CS_PathFollower>() != null) return t;
            if (IsSecurityRoot(t)) return t;
            if (saPoint == null && t.GetComponent<CS_SAPoint>() != null) saPoint = t;
            if (t.parent != null && t.parent.name == saObjectGroup) return t;
        }
        return saPoint;
    }

    /// <summary>
    /// 置いた物を消すメソッド。SAObjectを消す時は、同じ根元にあるハイドポイントも、ハイドポイントを消す時は、同じ根元のSAObjectも、一緒に消す
    /// </summary>
    /// <returns>消した物の数</returns>
    public static int Delete(Transform root)
    {
        if (root == null) return 0;

        List<GameObject> targets = new List<GameObject> { root.gameObject };
        Transform parent = root.parent;
        if (parent != null && parent.name == saObjectGroup)
        {
            Transform paired = FindSameBase(GetGroupOrNull(hidePointGroup), root.position);
            if (paired != null) targets.Add(paired.gameObject);
        }
        else if (root.GetComponent<CS_HidePoint>() != null && parent != null && parent.name == hidePointGroup)
        {
            Transform paired = FindSameBase(GetGroupOrNull(saObjectGroup), root.position);
            if (paired != null) targets.Add(paired.gameObject);
        }

        int count = 0;
        foreach (GameObject target in targets)
        {
            if (target == null) continue;

            Undo.DestroyObjectImmediate(target);
            count++;
        }
        Undo.SetCurrentGroupName("置いた物を消す");
        return count;
    }

    private static Transform GetGroupOrNull(string groupName)
    {
        Transform root = FindFieldRoot();
        return root != null ? root.Find(groupName) : null;
    }

    private static Transform FindSameBase(Transform group, Vector3 position)
    {
        if (group == null) return null;

        foreach (Transform child in group)
        {
            Vector3 difference = child.position - position;
            if (new Vector2(difference.x, difference.z).magnitude < _samePosition && Mathf.Abs(difference.y) < _samePosition) return child;
        }
        return null;
    }

    // =====================================================================
    // プレハブへ反映
    // =====================================================================

    /// <summary>
    /// このツールで置いた物のうち、Field_Solo1 のインスタンスに足された物(オーバーライド)を、Field_Solo1.prefab へ反映する(Apply)メソッド
    /// プレハブを開いて編集している間に置いた物は、すでにプレハブの中なので、対象にしない
    /// </summary>
    /// <returns>反映した数</returns>
    public static int ApplyCreatedToPrefab(out string message)
    {
        int applied = 0;
        int skipped = 0;
        foreach (GameObject created in CSED_PlacementState.GetCreated())
        {
            if (!PrefabUtility.IsAddedGameObjectOverride(created))
            {
                skipped++;
                continue;
            }

            try
            {
                PrefabUtility.ApplyAddedGameObject(created, fieldPrefabPath, InteractionMode.UserAction);
                applied++;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"CSED_PlacementBuilder: {created.name} を Field_Solo1.prefab へ反映できませんでした({exception.Message})");
                skipped++;
            }
        }

        CSED_PlacementState.ClearCreated();
        message = $"Field_Solo1.prefab へ反映した物 {applied}個 / 反映しなかった物(すでにプレハブの中・反映できなかった) {skipped}個";
        return applied;
    }
}
