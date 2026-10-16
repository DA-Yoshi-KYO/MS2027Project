using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/*
 * 悪人の攻撃の予告エフェクト(頭の上で赤くぴかっと光る)を作り、悪人に組み込むエディタ拡張
 * Tools/Villain/攻撃予告エフェクトを作る から実行する(何度実行しても同じ結果になる)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・やること
 *   1. 光のテクスチャ(中心が明るい円と、十字のきらめき)を作って保存する(TX_VillainAttackWarning.png)
 *   2. マテリアル(MT_VillainAttackWarning.mat)を作る。HDRP/Unlitの半透明・加算(重なるほど明るくなる)
 *   3. プレハブ(VillainAttackWarning.prefab)を作る。Quad1枚 + CS_VillainAttackWarningEffect
 *   4. Villain.prefab・VillainBoss.prefabのCS_VillainAttackVisualのWarning Effect Prefabに設定する
 * ・光の色・明るさ・点滅の速さは、プレハブのCS_VillainAttackWarningEffectで変える
 *   大きさはプレハブ(Quad)のScale、高さはCS_VillainAttackVisualのWarning Effect Heightで変える
 */
// ========================================

public static class CSED_VillainWarningEffectBuilder
{
    private const string _assetFolder = "Assets/Programmer/PlaceholderAsset/Generated/Villain";
    private const string _texturePath = _assetFolder + "/TX_VillainAttackWarning.png";
    private const string _materialPath = _assetFolder + "/MT_VillainAttackWarning.mat";
    private const string _effectPrefabPath = "Assets/Programmer/Prefab/Entity/Villain/VillainAttackWarning.prefab";
    private static readonly string[] _villainPrefabPaths =
    {
        "Assets/Programmer/Prefab/Entity/Villain/Villain.prefab",
        "Assets/Programmer/Prefab/Entity/Villain/VillainBoss.prefab",
    };

    private const int _textureSize = 128;
    private const float _effectSize = 0.8f;   // 板の大きさ(m)
    private const int _blendModeAdditive = 1; // HDRPのBlend Mode(0: Alpha, 1: Additive)

    [MenuItem("Tools/Villain/攻撃予告エフェクトを作る")]
    public static void Build()
    {
        Texture2D texture = CreateTexture();
        Material material = CreateMaterial(texture);
        GameObject effectPrefab = CreatePrefab(material);

        foreach (string path in _villainPrefabPaths)
        {
            AssignToVillain(path, effectPrefab);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("CSED_VillainWarningEffectBuilder: 攻撃予告エフェクトを作り、悪人に組み込みました");
    }

    // 中心が明るい円に、十字のきらめきを重ねたテクスチャ(色は白。色はマテリアル側で付ける)
    private static Texture2D CreateTexture()
    {
        Texture2D texture = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, false);
        float half = (_textureSize - 1) * 0.5f;
        for (int y = 0; y < _textureSize; y++)
        {
            for (int x = 0; x < _textureSize; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float glow = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.5f);
                float core = Mathf.Clamp01(1f - distance / 0.25f);
                float horizontal = Mathf.Clamp01(1f - Mathf.Abs(dy) / 0.06f) * Mathf.Clamp01(1f - Mathf.Abs(dx));
                float vertical = Mathf.Clamp01(1f - Mathf.Abs(dx) / 0.06f) * Mathf.Clamp01(1f - Mathf.Abs(dy));
                float alpha = Mathf.Clamp01(glow + core + horizontal + vertical);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();

        File.WriteAllBytes(_texturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(_texturePath);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(_texturePath);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(_texturePath);
    }

    // HDRP/Unlitの半透明・加算のマテリアル(影は落とさない)
    private static Material CreateMaterial(Texture2D texture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(_materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("HDRP/Unlit"));
            AssetDatabase.CreateAsset(material, _materialPath);
        }

        material.SetTexture("_UnlitColorMap", texture);
        material.SetColor("_UnlitColor", Color.red);
        HDMaterial.SetSurfaceType(material, true);
        material.SetFloat("_BlendMode", _blendModeAdditive);
        HDMaterial.ValidateMaterial(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreatePrefab(Material material)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Quad);
        try
        {
            root.name = Path.GetFileNameWithoutExtension(_effectPrefabPath);
            Object.DestroyImmediate(root.GetComponent<Collider>());
            root.transform.localScale = Vector3.one * _effectSize;

            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            root.AddComponent<CS_VillainAttackWarningEffect>();
            return PrefabUtility.SaveAsPrefabAsset(root, _effectPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void AssignToVillain(string path, GameObject effectPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            CS_VillainAttackVisual visual = root.GetComponent<CS_VillainAttackVisual>();
            if (visual == null)
            {
                Debug.LogWarning("CSED_VillainWarningEffectBuilder: CS_VillainAttackVisualが付いていません: " + path);
                return;
            }

            SerializedObject serialized = new SerializedObject(visual);
            serialized.FindProperty("_warningEffectPrefab").objectReferenceValue = effectPrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
