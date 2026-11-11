using System.Collections.Generic;
using System.IO;
using Nova.Editor.Core.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 爆発エフェクト(Houdini で作ったフリップブック)を組み立てるエディター用スクリプト
// メニュー: Tools > BomEffect > Build
// 初回だけ、Unity がこのスクリプトを読み込んだ時に自動で1回実行される(Prefab が無い時のみ)
[InitializeOnLoad]
public static class CSED_BomEffectBuilder
{
    const string Root = "Assets/Programmer/Scene/Test/Effect/bomEffect";
    const string FireTex = Root + "/texture/BomExplosion_8x8.png";
    const string SmokeTex = Root + "/texture/BomSmoke_8x8.png";
    const string FireMat = Root + "/material/MT_BomExplosion.mat";
    const string SmokeMat = Root + "/material/MT_BomSmoke.mat";
    const string PrefabPath = Root + "/prefab/BomExplosion.prefab";
    const string ScenePath = Root + "/bom_ffect.unity";

    // Houdini 側のフリップブック情報(bom_flipbook_info.json と同じ値)
    const int Grid = 8;          // 8x8
    const int Frames = 64;       // 64コマ
    const float Fps = 30f;       // 30fps(Houdini のシーンと同じ)
    const float HoudiniSpan = 26.62f;   // 1コマに写っている範囲(m)
    const float HoudiniCenterY = 4.8f;  // 1コマの中心の高さ(m)。地面=0
    const float Scale = 0.35f;          // ゲーム内の大きさ(Houdini の何倍か)

    static CSED_BomEffectBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null && File.Exists(FireTex) && File.Exists(SmokeTex))
                Build();
        };
    }

    [MenuItem("Tools/BomEffect/Build")]
    public static void Build()
    {
        SetupTexture(FireTex);
        SetupTexture(SmokeTex);
        var fireMat = MakeMaterial(FireMat, FireTex, 0);
        var smokeMat = MakeMaterial(SmokeMat, SmokeTex, 1);   // 煙は爆発より手前に描く

        var root = new GameObject("BomExplosion");
        float size = HoudiniSpan * Scale;
        float centerY = HoudiniCenterY * Scale;
        Flipbook(root, "Explosion", fireMat, size, centerY, 0);
        Flipbook(root, "Smoke", smokeMat, size, centerY, -1);

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        // 確認用シーン(bom_ffect)が開いていれば置く。保存はしない(確認してから保存してもらう)
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path == ScenePath && GameObject.Find("BomExplosion") == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(instance, "Place BomExplosion");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = instance;
        }
        Debug.Log("[BomEffect] Build done: " + PrefabPath);
    }

    static void SetupTexture(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;          // フリップブックのコマ境界がにじまないように
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ; // BC7
        importer.SaveAndReimport();
    }

    static Material MakeMaterial(string path, string texPath, int queueOffset)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = mat == null;
        if (create) mat = new Material(Shader.Find("Nova/Particles/UberUnlit"));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetFloat("_RenderType", 2);            // Transparent
        mat.SetFloat("_TransparentBlendMode", 0);  // Alpha
        mat.SetFloat("_BlendSrc", 5);              // SrcAlpha
        mat.SetFloat("_BlendDst", 10);             // OneMinusSrcAlpha
        mat.SetFloat("_ZWrite", 0);
        mat.SetFloat("_Cull", 0);
        mat.SetFloat("_SoftParticlesEnabled", 1);  // 地面との境目をぼかす
        mat.SetFloat("_SoftParticlesIntensity", 1);
        mat.renderQueue = 3000 + queueOffset;
        ParticlesUberUnlitMaterialPostProcessor.SetupMaterialKeywords(mat);
        if (create) AssetDatabase.CreateAsset(mat, path); else EditorUtility.SetDirty(mat);
        return mat;
    }

    static void Flipbook(GameObject parent, string name, Material mat, float size, float centerY, int sortingFudge)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(0, centerY, 0);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float life = Frames / Fps;
        var main = ps.main;
        main.duration = life; main.loop = false; main.playOnAwake = true;
        main.startLifetime = life; main.startSpeed = 0; main.startSize = size;
        main.startColor = Color.white; main.maxParticles = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission; emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
        var shape = ps.shape; shape.enabled = false;

        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = Grid; sheet.numTilesY = Grid;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 1));
        sheet.cycleCount = 1;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
        r.alignment = ParticleSystemRenderSpace.View;
        r.sortingFudge = sortingFudge;
        r.maxParticleSize = 10;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
    }
}
