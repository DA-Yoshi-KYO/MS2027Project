using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/*
 * 開いているシーンの全スポーン位置(CS_VillainSpawnPoint)に、犯罪完遂ゲージを組み込むエディタ拡張
 * Tools/Villain/犯罪完遂ゲージをスポーン位置に組み込む から実行する(何度実行しても同じ結果になる)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・スポーン位置ごとに行うこと(既に付いていれば何もしない)
 *   1. NetworkObjectを付ける(シーン配置のNetworkObjectは、サーバー起動時に自動でSpawnされる)
 *   2. CS_VillainCrimeGaugeを付ける
 *   3. 直下にゲージ(VillainCrimeGaugeCanvas.prefab)を置き、CS_VillainCrimeGaugeに設定する
 * ・ゲージはスポーン位置から gaugeHeight 上に置く(悪人の頭より上)。置いた後に位置を変えてもよい
 * ・スポーン位置を増やした時も、このメニューを実行し直せばよい
 * ・シーンは保存しないので、確認してから保存する
 */
// ========================================

public static class CSED_VillainCrimeGaugeInstaller
{
    private const string _gaugePrefabPath = "Assets/Programmer/Prefab/UI/InGame/VillainCrimeGaugeCanvas.prefab";
    private const string _gaugeName = "VillainCrimeGaugeCanvas";
    private const float _gaugeHeight = 2.3f;   // スポーン位置(体の中心の高さ)からの高さ(m)

    [MenuItem("Tools/Villain/犯罪完遂ゲージをスポーン位置に組み込む")]
    public static void Install()
    {
        GameObject gaugePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(_gaugePrefabPath);
        if (gaugePrefab == null)
        {
            Debug.LogError("CSED_VillainCrimeGaugeInstaller: ゲージのプレハブが見つかりません: " + _gaugePrefabPath);
            return;
        }

        CS_VillainSpawnPoint[] points = Object.FindObjectsByType<CS_VillainSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CS_VillainSpawnPoint point in points)
        {
            InstallTo(point, gaugePrefab);
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"CSED_VillainCrimeGaugeInstaller: {points.Length}か所のスポーン位置に犯罪完遂ゲージを組み込みました(シーンは未保存)");
    }

    private static void InstallTo(CS_VillainSpawnPoint point, GameObject gaugePrefab)
    {
        GameObject pointObject = point.gameObject;

        if (pointObject.GetComponent<NetworkObject>() == null) Undo.AddComponent<NetworkObject>(pointObject);

        CS_VillainCrimeGauge gauge = pointObject.GetComponent<CS_VillainCrimeGauge>();
        if (gauge == null) gauge = Undo.AddComponent<CS_VillainCrimeGauge>(pointObject);

        // ゲージのPresenterは親のTransformでModelを探すので、必ずスポーン位置の直下に置く
        Transform canvas = pointObject.transform.Find(_gaugeName);
        if (canvas == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(gaugePrefab, pointObject.transform);
            instance.name = _gaugeName;
            instance.transform.localPosition = new Vector3(0f, _gaugeHeight, 0f);
            instance.transform.localRotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(instance, "Add Villain Crime Gauge");
            canvas = instance.transform;
        }

        SerializedObject serialized = new SerializedObject(gauge);
        serialized.FindProperty("_gaugeCanvas").objectReferenceValue = canvas.gameObject;
        serialized.ApplyModifiedProperties();
    }
}
