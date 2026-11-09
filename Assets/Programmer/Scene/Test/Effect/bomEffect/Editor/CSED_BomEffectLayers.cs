using System.Collections.Generic;
using Nova.Editor.Core.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 爆発エフェクトの「中にいる感じ」を足す追加セットアップ
//  ・爆発の板を手前/横/奥に重ねて、足元やキャラの前にもかかるようにする
//  ・地面を這うオレンジのもや(GroundHaze)を足す
//  ・爆発の発光(Emission)でブルームを乗せる。最初ほど強く光る
//  ・地面との境目のぼかし(Soft Particles)を狭くして、足元が消えないようにする
// 既存の Prefab を「その場で」編集するので、シーンで変えた大きさなどはそのまま残る
// メニュー: Tools > BomEffect > Add Layers (初回だけ自動で実行)
[InitializeOnLoad]
public static class CSED_BomEffectLayers
{
    const string Root = "Assets/Programmer/Scene/Test/Effect/bomEffect";
    const string PrefabPath = Root + "/prefab/BomExplosion.prefab";
    const string FireMatPath = Root + "/material/MT_BomExplosion.mat";
    const string SmokeMatPath = Root + "/material/MT_BomSmoke.mat";
    const string HazeMatPath = Root + "/material/MT_BomGroundHaze.mat";
    const string GlowTexPath = "Assets/Programmer/min/YosimotoRyo/NovaHDRPEffects/Textures/SoftGlow.png";

    static CSED_BomEffectLayers()
    {
        EditorApplication.delayCall += () =>
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null && prefab.transform.Find("GroundHaze") == null) AddLayers();
        };
    }

    [MenuItem("Tools/BomEffect/Add Layers")]
    public static void AddLayers()
    {
        var fireMat = AssetDatabase.LoadAssetAtPath<Material>(FireMatPath);
        var smokeMat = AssetDatabase.LoadAssetAtPath<Material>(SmokeMatPath);
        if (fireMat == null || smokeMat == null) { Debug.LogError("[BomEffect] Build を先に実行してください"); return; }

        // ---- マテリアル: 足元のぼかしを狭く / 爆発を発光させる
        foreach (var m in new[] { fireMat, smokeMat }) m.SetFloat("_SoftParticlesIntensity", 0.25f);
        fireMat.SetFloat("_EmissionAreaType", (float)EmissionAreaType.All);
        fireMat.SetFloat("_EmissionColorType", (float)EmissionColorType.BaseColor);
        fireMat.SetFloat("_EmissionIntensity", 0.4f);                       // 常に少し光る
        fireMat.SetFloat("_EmissionIntensityCoord", (float)CustomCoord.Coord1Z); // + Custom Data で最初に強く
        ParticlesUberUnlitMaterialPostProcessor.SetupMaterialKeywords(fireMat);
        ParticlesUberUnlitMaterialPostProcessor.SetupMaterialKeywords(smokeMat);
        EditorUtility.SetDirty(fireMat); EditorUtility.SetDirty(smokeMat);
        var hazeMat = HazeMaterial();

        // ---- Prefab をその場で編集
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var fire = root.transform.Find("Explosion").GetComponent<ParticleSystem>();
            var smoke = root.transform.Find("Smoke").GetComponent<ParticleSystem>();
            float size = fire.main.startSize.constant;
            float baseY = fire.transform.localPosition.y;

            foreach (var name in new[] { "ExplosionLayerFront", "ExplosionLayerLeft", "ExplosionLayerRight", "SmokeLayerFront", "GroundHaze" })
            {
                var old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            SetupEmissionCurve(fire);

            // 爆発の板を3枚追加(120度ずつ回り込むように配置 → どの角度から見ても手前に1枚くる)
            Layer(fire, "ExplosionLayerFront", new Vector3(0, -0.12f, -0.2f), 0.62f, baseY, size, 0.00f, false);
            Layer(fire, "ExplosionLayerLeft",  new Vector3(-0.18f, -0.08f, 0.12f), 0.7f, baseY, size, 0.04f, true);
            Layer(fire, "ExplosionLayerRight", new Vector3(0.18f, -0.1f, 0.12f), 0.66f, baseY, size, 0.07f, true);
            // 煙も手前に1枚
            Layer(smoke, "SmokeLayerFront", new Vector3(0.05f, -0.1f, -0.16f), 0.7f, baseY, size, 0.05f, true);

            GroundHaze(root.transform, hazeMat, size);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BomEffect] Layers added: " + PrefabPath);
    }

    static void SetupEmissionCurve(ParticleSystem ps)
    {
        // Custom1.z = 発光の強さの上乗せ。爆発直後に強く → すぐ落ちる
        var custom = ps.customData;
        custom.enabled = true;
        custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 4);
        var curve = new AnimationCurve(new Keyframe(0, 3f), new Keyframe(0.12f, 2f), new Keyframe(0.35f, 0.6f), new Keyframe(1, 0));
        custom.SetVector(ParticleSystemCustomData.Custom1, 2, new ParticleSystem.MinMaxCurve(1, curve));
        Streams(ps.GetComponent<ParticleSystemRenderer>());
    }

    static void Streams(ParticleSystemRenderer r)
    {
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2,
            ParticleSystemVertexStream.Custom1XYZW, ParticleSystemVertexStream.Custom2XYZW });
    }

    static void Layer(ParticleSystem src, string name, Vector3 offset, float sizeRate, float baseY, float size, float delay, bool flip)
    {
        var go = Object.Instantiate(src.gameObject, src.transform.parent);
        go.name = name;
        go.transform.localPosition = new Vector3(offset.x * size, baseY + offset.y * size, offset.z * size);
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main; main.startSize = size * sizeRate; main.startDelay = delay;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.flip = flip ? new Vector3(1, 0, 0) : Vector3.zero;
        Streams(r);
    }

    static void GroundHaze(Transform parent, Material mat, float size)
    {
        var go = new GameObject("GroundHaze");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, 0.05f, 0);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        float life = 64f / 30f;
        var main = ps.main;
        main.duration = life; main.loop = false; main.playOnAwake = true;
        main.startLifetime = life; main.startSpeed = 0; main.startSize = size * 1.3f; main.maxParticles = 1;
        main.startColor = new Color(1f, 0.45f, 0.25f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
        var shape = ps.shape; shape.enabled = false;
        var sizeOver = ps.sizeOverLifetime; sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.3f, 0.85f), new Keyframe(1, 1.2f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.6f), 0), new GradientColorKey(new Color(1f, 0.5f, 0.3f), 0.3f), new GradientColorKey(new Color(0.9f, 0.4f, 0.35f), 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        r.sortingFudge = 5; // 爆発より奥に描く
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
    }

    static Material HazeMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(HazeMatPath);
        bool create = mat == null;
        if (create) mat = new Material(Shader.Find("Nova/Particles/UberUnlit"));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexPath));
        mat.SetFloat("_RenderType", 2); mat.SetFloat("_TransparentBlendMode", 0);
        mat.SetFloat("_BlendSrc", 5); mat.SetFloat("_BlendDst", 10);
        mat.SetFloat("_ZWrite", 0); mat.SetFloat("_Cull", 0);
        mat.SetFloat("_SoftParticlesEnabled", 1); mat.SetFloat("_SoftParticlesIntensity", 0.5f);
        mat.renderQueue = 2999;
        ParticlesUberUnlitMaterialPostProcessor.SetupMaterialKeywords(mat);
        if (create) AssetDatabase.CreateAsset(mat, HazeMatPath); else EditorUtility.SetDirty(mat);
        return mat;
    }
}
