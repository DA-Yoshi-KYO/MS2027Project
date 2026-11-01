using System.Collections.Generic;
using System.IO;
using Nova.Editor.Core.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 板ポリ感を減らす2案
//  A: BomExplosion_Scatter … 今の爆発テクスチャを小さい板に砕いて散らす(回転・大きさ・開始コマをばらつかせる)
//  B: BomExplosion_Puffs   … Houdini で作った「もこもこ1個分」のフリップブックを、中心から外へたくさん出して爆発を組む
// どちらも新しい Prefab として作る(BomExplosion はそのまま)。
// メニュー: Tools > BomEffect > Build A (Scatter) / Build B (Puffs)  ※初回は自動で1回実行
[InitializeOnLoad]
public static class CSED_BomEffectVariants
{
    const string Root = "Assets/Programmer/Scene/Test/Effect/bomEffect";
    const string ScenePath = Root + "/bom_ffect.unity";
    const string APath = Root + "/prefab/BomExplosion_Scatter.prefab";
    const string BPath = Root + "/prefab/BomExplosion_Puffs.prefab";
    const string FireMatPath = Root + "/material/MT_BomExplosion.mat";
    const string SmokeMatPath = Root + "/material/MT_BomSmoke.mat";
    const string HazeMatPath = Root + "/material/MT_BomGroundHaze.mat";
    static readonly string[] PuffFireTex = { Root + "/texture/BomPuff_Fire_0.png", Root + "/texture/BomPuff_Fire_1.png", Root + "/texture/BomPuff_Fire_2.png" };
    const string PuffSmokeTex = Root + "/texture/BomPuff_Smoke_0.png";

    const float Life = 64f / 30f;     // 64コマ / 30fps
    const float Size = 9.317f;        // BomExplosion の大きさに合わせる

    static CSED_BomEffectVariants()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(APath) == null && File.Exists(FireMatPath)) BuildA();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BPath) == null && File.Exists(PuffFireTex[0]) && File.Exists(PuffSmokeTex)) BuildB();
        };
    }

    // ---------------------------------------------------------------- A
    [MenuItem("Tools/BomEffect/Build A (Scatter)")]
    public static void BuildA()
    {
        var fire = AssetDatabase.LoadAssetAtPath<Material>(FireMatPath);
        var smoke = AssetDatabase.LoadAssetAtPath<Material>(SmokeMatPath);
        var haze = AssetDatabase.LoadAssetAtPath<Material>(HazeMatPath);
        var root = new GameObject("BomExplosion_Scatter");
        // 爆発: 小さめの板を半球の中にばらまく。開始コマを少しずつずらすので、全部が同じ絵にならない
        var f = Cards(root, "ExplosionCards", fire, 8, 30, 0.08f, Size * 0.18f, Size * 0.36f, Size * 0.14f, Size * 0.12f, 3);
        EmissionCurve(f);
        Cards(root, "SmokeCards", smoke, 9, 12, 0.12f, Size * 0.2f, Size * 0.36f, Size * 0.1f, Size * 0.12f, -1);
        if (haze != null) Haze(root, haze, Size);
        Save(root, APath);
    }

    static ParticleSystem Cards(GameObject parent, string name, Material mat, int queueDummy, int count, float spread,
                                float sizeMin, float sizeMax, float radius, float centerY, int sortingFudge)
    {
        var ps = NewSystem(parent, name, new Vector3(0, centerY, 0));
        var main = ps.main;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);   // ±25度
        main.startSpeed = 0;
        main.maxParticles = count;
        main.flipRotation = 0.5f;                                            // 半分は左右反転
        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)(count * 0.6f)), new ParticleSystem.Burst(spread, (short)(count * 0.4f)) });
        var shape = ps.shape; shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = radius;
        shape.rotation = new Vector3(-90, 0, 0);
        var sheet = Sheet(ps);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 3f / 64f);       // 開始コマを0〜3コマずらす
        var r = Renderer(ps, mat, sortingFudge);
        r.flip = new Vector3(0.5f, 0, 0);
        return ps;
    }

    // ---------------------------------------------------------------- B
    [MenuItem("Tools/BomEffect/Build B (Puffs)")]
    public static void BuildB()
    {
        var fireMats = new List<Material>();
        for (int i = 0; i < PuffFireTex.Length; i++)
        {
            if (!File.Exists(PuffFireTex[i])) continue;
            Texture(PuffFireTex[i]);
            fireMats.Add(Mat(Root + "/material/MT_BomPuffFire_" + i + ".mat", PuffFireTex[i], 0, true));
        }
        Texture(PuffSmokeTex);
        var smokeMat = Mat(Root + "/material/MT_BomPuffSmoke.mat", PuffSmokeTex, 1, false);
        var haze = AssetDatabase.LoadAssetAtPath<Material>(HazeMatPath);

        var root = new GameObject("BomExplosion_Puffs");
        // 爆発: もこもこを中心から外へ飛ばし、すぐ減速 → 寄り集まって1つの爆発の形になる
        for (int i = 0; i < fireMats.Count; i++)
        {
            var ps = NewSystem(root, "FirePuffs_" + i, new Vector3(0, Size * 0.08f, 0));
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(Size * 0.16f, Size * 0.28f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);   // 下が平らな形なので回しすぎない
            main.startSpeed = new ParticleSystem.MinMaxCurve(Size * 0.04f, Size * 0.18f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(Life * 0.85f, Life);
            main.maxParticles = 20;
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 8), new ParticleSystem.Burst(0.05f + 0.02f * i, 6), new ParticleSystem.Burst(0.12f, 4) });
            var shape = ps.shape; shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = Size * 0.13f;
            shape.rotation = new Vector3(-90, 0, 0);
            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.limit = 0; limit.dampen = 0.2f; // すぐ止まる
            var sizeOver = ps.sizeOverLifetime; sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.45f), new Keyframe(0.25f, 0.9f), new Keyframe(1, 1.25f)));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var sheet = Sheet(ps); sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 2f / 64f);
            Renderer(ps, fireMats[i], 0).flip = new Vector3(0.5f, 0, 0);
            EmissionCurve(ps);
        }
        // 煙: 中心から少し遅れてもくもく出て、広がって消える
        {
            var ps = NewSystem(root, "SmokePuffs", new Vector3(0, Size * 0.06f, 0));
            var main = ps.main;
            main.startDelay = 0.25f;
            main.startSize = new ParticleSystem.MinMaxCurve(Size * 0.2f, Size * 0.32f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);   // 下が平らな形なので回しすぎない
            main.startSpeed = new ParticleSystem.MinMaxCurve(Size * 0.05f, Size * 0.2f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(Life * 0.8f, Life);
            main.maxParticles = 12;
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 5), new ParticleSystem.Burst(0.15f, 5) });
            var shape = ps.shape; shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = Size * 0.04f; shape.rotation = new Vector3(-90, 0, 0);
            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.limit = 0; limit.dampen = 0.05f;
            var sizeOver = ps.sizeOverLifetime; sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(1, 1.6f)));
            Sheet(ps);
            Renderer(ps, smokeMat, -1).flip = new Vector3(0.5f, 0, 0);
        }
        if (haze != null) Haze(root, haze, Size);
        Save(root, BPath);
    }

    // ---------------------------------------------------------------- 共通
    static ParticleSystem NewSystem(GameObject parent, string name, Vector3 pos)
    {
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false); go.transform.localPosition = pos;
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = Life; main.loop = false; main.playOnAwake = true;
        main.startLifetime = Life; main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission; emission.rateOverTime = 0;
        return ps;
    }

    static ParticleSystem.TextureSheetAnimationModule Sheet(ParticleSystem ps)
    {
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 8; sheet.numTilesY = 8; sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 60f / 64f)); // 開始コマずらし分で1周を超えて最初のコマに戻らないように
        sheet.cycleCount = 1;
        return sheet;
    }

    static ParticleSystemRenderer Renderer(ParticleSystem ps, Material mat, int sortingFudge)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard; r.alignment = ParticleSystemRenderSpace.View;
        r.sortMode = ParticleSystemSortMode.Distance; r.sortingFudge = sortingFudge; r.maxParticleSize = 10;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2,
            ParticleSystemVertexStream.Custom1XYZW, ParticleSystemVertexStream.Custom2XYZW });
        return r;
    }

    static void EmissionCurve(ParticleSystem ps)
    {
        var custom = ps.customData; custom.enabled = true;
        custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 4);
        var curve = new AnimationCurve(new Keyframe(0, 3f), new Keyframe(0.12f, 2f), new Keyframe(0.35f, 0.6f), new Keyframe(1, 0));
        custom.SetVector(ParticleSystemCustomData.Custom1, 2, new ParticleSystem.MinMaxCurve(1, curve));
    }

    static void Haze(GameObject parent, Material mat, float size)
    {
        var ps = NewSystem(parent, "GroundHaze", new Vector3(0, 0.05f, 0));
        var main = ps.main; main.startSize = size * 1.3f; main.maxParticles = 1; main.startColor = new Color(1f, 0.45f, 0.25f, 0.75f);
        var emission = ps.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
        var shape = ps.shape; shape.enabled = false;
        var sizeOver = ps.sizeOverLifetime; sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.3f, 0.85f), new Keyframe(1, 1.2f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.6f), 0), new GradientColorKey(new Color(1f, 0.5f, 0.3f), 0.3f), new GradientColorKey(new Color(0.9f, 0.4f, 0.35f), 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.HorizontalBillboard; r.sortingFudge = 5;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
    }

    static void Texture(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;
        importer.sRGBTexture = true; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    static Material Mat(string path, string tex, int queueOffset, bool emissive)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = mat == null;
        if (create) mat = new Material(Shader.Find("Nova/Particles/UberUnlit"));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        mat.SetFloat("_RenderType", 2); mat.SetFloat("_TransparentBlendMode", 0);
        mat.SetFloat("_BlendSrc", 5); mat.SetFloat("_BlendDst", 10); mat.SetFloat("_ZWrite", 0); mat.SetFloat("_Cull", 0);
        mat.SetFloat("_SoftParticlesEnabled", 1); mat.SetFloat("_SoftParticlesIntensity", 0.25f);
        if (emissive)
        {
            mat.SetFloat("_EmissionAreaType", (float)EmissionAreaType.All);
            mat.SetFloat("_EmissionColorType", (float)EmissionColorType.BaseColor);
            mat.SetFloat("_EmissionIntensity", 0.4f);
            mat.SetFloat("_EmissionIntensityCoord", (float)CustomCoord.Coord1Z);
        }
        mat.renderQueue = 3000 + queueOffset;
        ParticlesUberUnlitMaterialPostProcessor.SetupMaterialKeywords(mat);
        if (create) AssetDatabase.CreateAsset(mat, path); else EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject FindIncludingInactive(string name, GameObject exclude = null)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == name && go != exclude) return go;
        return null;
    }

    static void Save(GameObject root, string path)
    {
        var scene0 = EditorSceneManager.GetActiveScene();
        var existing = scene0.path == ScenePath ? FindIncludingInactive(root.name, root) : null;
        Vector3 keepScale = existing != null ? existing.transform.localScale : Vector3.one;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        // 確認用シーンが開いていれば、BomExplosion と同じ位置に「非表示」で置く(切り替えて見比べる用)。保存はしない
        var scene = EditorSceneManager.GetActiveScene();
        var placed = scene.path == ScenePath ? FindIncludingInactive(prefab.name) : null;
        if (placed != null) placed.transform.localScale = keepScale.sqrMagnitude > 0 ? keepScale : placed.transform.localScale;
        if (scene.path == ScenePath && placed == null)
        {
            var baseObj = GameObject.Find("BomExplosion");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (baseObj != null) { inst.transform.position = baseObj.transform.position; inst.transform.rotation = baseObj.transform.rotation; inst.transform.localScale = baseObj.transform.localScale; }
            inst.SetActive(false);
            Undo.RegisterCreatedObjectUndo(inst, "Place " + prefab.name);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        Debug.Log("[BomEffect] Built: " + path);
    }
}
