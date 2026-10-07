using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

/*
 * Player.prefabにカメラの揺れ(Cinemachine)を組み込むエディタ拡張
 * Tools/Player/カメラの揺れを組み込む から実行する(何度実行しても同じ結果になる)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・やること
 *   1. プレイヤーのカメラ(Player Camera)にCinemachineBrainを付ける
 *   2. CinemachineCamera(PlayerVirtualCamera)とCinemachineImpulseListenerを作る
 *      CS_Playerがこのカメラの位置・向きを決め、BrainがPlayer Cameraへ反映する(レンズ設定は元のカメラからコピー)
 *   3. Player本体にCinemachineImpulseSourceとCS_PlayerCameraShakeを付ける
 * ・CinemachineCameraはPlayer Cameraの子にしない(Brainが動かすカメラの子にすると位置がずれ続けるため)
 */
// ========================================

public static class CSED_PlayerCameraShakeInstaller
{
    private const string _prefabPath = "Assets/Programmer/Prefab/Entity/Player/Player.prefab";
    private const string _virtualCameraName = "PlayerVirtualCamera";

    [MenuItem("Tools/Player/カメラの揺れを組み込む")]
    public static void Install()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_prefabPath);

        try
        {
            CS_Player player = root.GetComponent<CS_Player>();
            SerializedObject serializedPlayer = new SerializedObject(player);
            Camera playerCamera = ((Transform)serializedPlayer.FindProperty("_cameraTransform").objectReferenceValue).GetComponent<Camera>();

            AttachBrain(playerCamera);
            CinemachineCamera virtualCamera = CreateVirtualCamera(root, playerCamera);

            serializedPlayer.FindProperty("_virtualCameraTransform").objectReferenceValue = virtualCamera.transform;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<CinemachineImpulseSource>() == null) root.AddComponent<CinemachineImpulseSource>();
            if (root.GetComponent<CS_PlayerCameraShake>() == null) root.AddComponent<CS_PlayerCameraShake>();

            PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
            Debug.Log("CSED_PlayerCameraShakeInstaller: カメラの揺れを組み込みました");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AttachBrain(Camera playerCamera)
    {
        CinemachineBrain brain = playerCamera.GetComponent<CinemachineBrain>();
        if (brain == null) brain = playerCamera.gameObject.AddComponent<CinemachineBrain>();

        // CS_Playerが LateUpdate で位置を決めた後(Brainは実行順100)に反映させる
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
    }

    private static CinemachineCamera CreateVirtualCamera(GameObject root, Camera playerCamera)
    {
        Transform existing = root.transform.Find(_virtualCameraName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject virtualObject = new GameObject(_virtualCameraName);
        virtualObject.transform.SetParent(root.transform, false);

        CinemachineCamera virtualCamera = virtualObject.AddComponent<CinemachineCamera>();
        virtualCamera.Lens = LensSettings.FromCamera(playerCamera);
        virtualObject.AddComponent<CinemachineImpulseListener>();
        return virtualCamera;
    }
}
