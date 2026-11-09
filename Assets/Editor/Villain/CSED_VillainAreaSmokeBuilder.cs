using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

/*
 * 悪人エリアの入口に出す紫の煙(プレハブ)を作り、悪人の出現点に組み込むエディタ拡張
 * Tools/Villain/悪人エリアの煙を作る から実行する(何度実行しても同じ結果になる)
 *
 * 制作者：　Claude
 */

// ========================================
/*
 * メモ
 * ・使い方はClaudeDocs/悪人エリアの煙の使い方.md参照
 * ・やること
 *   1. プレハブ(VillainAreaSmoke.prefab)を作る。VisualEffect(VEG_VillainAreaSmoke.vfx) 1つだけ
 *      煙を出す・止めるのはCS_VillainAreaSmokeが行うので、起動時には何も出さない(Initial Eventを空にする)
 *   2. VillainSpawnPoint.prefabに組み込む。古いフォグ(VillainAreaFog)を消し、煙とCS_VillainAreaSmokeを付ける
 * ・瘴気の動きと見た目はVEG_VillainAreaSmoke.vfxに入っている。このスクリプトでは変えない
 */
// ========================================

public static class CSED_VillainAreaSmokeBuilder
{
    private const string _vfxPath = "Assets/Programmer/Effect/VEG_VillainAreaSmoke.vfx";
    private const string _smokePrefabPath = "Assets/Programmer/Prefab/Field/FieldElement/Villain/VillainAreaSmoke.prefab";
    private const string _spawnPointPrefabPath = "Assets/Programmer/Prefab/Field/FieldElement/Villain/VillainSpawnPoint.prefab";
    private const string _oldFogName = "VillainAreaFog";
    private static readonly string[] _oldChildNames = { "GroundLayer", "SmokeLeft", "SmokeRight" };

    [MenuItem("Tools/Villain/悪人エリアの煙を作る")]
    public static void Build()
    {
        VisualEffectAsset vfx = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(_vfxPath);
        if (vfx == null)
        {
            Debug.LogError("CSED_VillainAreaSmokeBuilder: VFXがありません: " + _vfxPath);
            return;
        }

        GameObject smokePrefab = CreateSmokePrefab(vfx);
        AddToSpawnPoint(smokePrefab);

        AssetDatabase.SaveAssets();
        Debug.Log("CSED_VillainAreaSmokeBuilder: 悪人エリアの煙を作り、VillainSpawnPointに組み込みました");
    }

    // 既にあるプレハブは作り直さず編集する(作り直すと、出現点からの参照やField.prefabの上書きが切れるため)
    private static GameObject CreateSmokePrefab(VisualEffectAsset vfx)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(_smokePrefabPath) != null;
        GameObject root = exists
            ? PrefabUtility.LoadPrefabContents(_smokePrefabPath)
            : new GameObject(Path.GetFileNameWithoutExtension(_smokePrefabPath));
        try
        {
            RemoveOldParticles(root);

            if (!root.TryGetComponent(out VisualEffect visualEffect)) visualEffect = root.AddComponent<VisualEffect>();
            visualEffect.visualEffectAsset = vfx;
            visualEffect.initialEventName = string.Empty;

            return PrefabUtility.SaveAsPrefabAsset(root, _smokePrefabPath);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    // 前の作りのパーティクル(ParticleSystemと左右の柱)を消す
    private static void RemoveOldParticles(GameObject root)
    {
        foreach (string childName in _oldChildNames)
        {
            Transform child = root.transform.Find(childName);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        if (root.TryGetComponent(out ParticleSystemRenderer renderer)) Object.DestroyImmediate(renderer);
        if (root.TryGetComponent(out ParticleSystem particle)) Object.DestroyImmediate(particle);
    }

    // VillainSpawnPoint.prefabの古いフォグを消し、煙とCS_VillainAreaSmokeを組み込む
    private static void AddToSpawnPoint(GameObject smokePrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_spawnPointPrefabPath);
        try
        {
            Transform oldFog = root.transform.Find(_oldFogName);
            if (oldFog != null) Object.DestroyImmediate(oldFog.gameObject);

            Transform smoke = root.transform.Find(smokePrefab.name);
            if (smoke == null)
            {
                smoke = ((GameObject)PrefabUtility.InstantiatePrefab(smokePrefab, root.transform)).transform;
            }

            if (!root.TryGetComponent(out CS_VillainAreaSmoke areaSmoke))
            {
                areaSmoke = root.AddComponent<CS_VillainAreaSmoke>();
            }

            SerializedObject serialized = new SerializedObject(areaSmoke);
            serialized.FindProperty("_smoke").objectReferenceValue = smoke.GetComponent<VisualEffect>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, _spawnPointPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
