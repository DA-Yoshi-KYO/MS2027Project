using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// C: BomExplosion_Burst
//  ・爆発は1点からドカンと出て、その場で固まる(散らばらない)
//  ・爆発後はオレンジの薄いもや(キャラや物が透けて見える)
//  ・白い煙は爆心の地面(物)からもくもく出て広がって消える
// メニュー: Tools > BomEffect > Build C (Burst)
public static class CSED_BomEffectBurst
{
    const string Root = "Assets/Programmer/Scene/Test/Effect/bomEffect";
    const string ScenePath = Root + "/bom_ffect.unity";
    const string CPath = Root + "/prefab/BomExplosion_Burst.prefab";
    const string HazeMatPath = Root + "/material/MT_BomGroundHaze.mat";
    static readonly string[] FireMats = { Root + "/material/MT_BomPuffFire_0.mat", Root + "/material/MT_BomPuffFire_1.mat", Root + "/material/MT_BomPuffFire_2.mat" };

    const float Life = 64f / 30f;
    const float Size = 9.317f;

    // 調整用(ここだけ触れば見た目が変わる)
    public static float FireAlpha = 0.16f;      // 爆発の不透明度(下げるほどキャラが見える)
    public static float FireCount = 6;        // 1マテリアルあたりの数
    public static float FireSpeed = 0.7f;
    public static float FireSize = 0.6f;       // 爆発の大きさ
    public static float FireGlow = 2.0f;       // 発光(ブルーム)の強さ     // 1点から押し出す強さ
    public static float HazeAlpha = 0.4f;     // 爆発後のオレンジもや
    public static float SmokeAlpha = 0.35f;     // 白煙

    [MenuItem("Tools/BomEffect/Build C (Burst)")]
    public static void BuildC()
    {
        var haze = AssetDatabase.LoadAssetAtPath<Material>(HazeMatPath);
        seed = 0;
        var root = new GameObject("BomExplosion_Burst");

        // ---- 爆発: 1点からドカン。すぐ止まって1つの塊になる
        for (int i = 0; i < FireMats.Length; i++)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FireMats[i]);
            if (mat == null) continue;
            var ps = NewSystem(root, "Fire_" + i, new Vector3(0, Size * 0.01f, 0));
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(Size * FireSize * 0.75f, Size * FireSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(Size * FireSpeed * 0.5f, Size * FireSpeed);
            main.startLifetime = new ParticleSystem.MinMaxCurve(Life * 0.9f, Life);
            main.maxParticles = 40;
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)FireCount), new ParticleSystem.Burst(0.04f + 0.02f * i, (short)(FireCount * 0.5f)) });
            var shape = ps.shape; shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 38; shape.radius = Size * 0.04f; shape.rotation = new Vector3(-90, 0, 0); // 地面から上へ立ち上がる
            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.limit = 0; limit.dampen = 0.22f;
            var sizeOver = ps.sizeOverLifetime; sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.25f), new Keyframe(0.15f, 0.9f), new Keyframe(1, 1.3f)));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            { var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
              g.SetKeys(FireColors(),
                        new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(FireAlpha * 2.2f, 0.03f), new GradientAlphaKey(FireAlpha, 0.25f), new GradientAlphaKey(FireAlpha * 0.8f, 0.55f), new GradientAlphaKey(0, 0.85f) });
              col.color = g; } // 最初の閃光は濃く、すぐ透ける
            var sheet = Sheet(ps); sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 2f / 64f);
            Renderer(ps, mat, 0, true);
            mat.SetFloat("_EmissionIntensity", FireGlow); EditorUtility.SetDirty(mat);
            EmissionCurve(ps);
        }

        // ---- 足元: 地面沿いに全方向へ広がる炎(どの向きから見ても足元にかかる)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FireMats[1]);
            var ps = NewSystem(root, "GroundFire", new Vector3(0, Size * 0.01f, 0));
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(Size * FireSize * 0.5f, Size * FireSize * 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(Size * FireSpeed * 0.5f, Size * FireSpeed * 0.9f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(Life * 0.85f, Life);
            main.maxParticles = 30;
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.02f, 14), new ParticleSystem.Burst(0.07f, 8) });
            var shape = ps.shape; shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 82; shape.radius = Size * 0.03f; shape.rotation = new Vector3(-90, 0, 0);
            shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread; // 全方向に均等に出す(ムラ防止)
            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.limit = 0; limit.dampen = 0.25f;
            var sizeOver = ps.sizeOverLifetime; sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.15f, 0.9f), new Keyframe(1, 1.3f)));
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(FireColors(), new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(FireAlpha * 2.2f, 0.03f), new GradientAlphaKey(FireAlpha, 0.25f), new GradientAlphaKey(FireAlpha * 0.8f, 0.55f), new GradientAlphaKey(0, 0.85f) });
            col.color = g;
            var sheet = Sheet(ps); sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 2f / 64f);
            Renderer(ps, mat, 0, true);
            EmissionCurve(ps);
        }

        if (haze != null)
        {
            // ---- 爆発後のオレンジのもや(中も外も薄く残る。上に浮かない)
            {
                var ps = NewSystem(root, "OrangeHaze", new Vector3(0, Size * 0.04f, 0));
                var main = ps.main;
                main.startDelay = 0.1f;
                main.startSize = new ParticleSystem.MinMaxCurve(Size * 0.5f, Size * 0.75f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                main.startSpeed = new ParticleSystem.MinMaxCurve(Size * 0.01f, Size * 0.04f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 3.8f);
                main.duration = 3.8f; main.maxParticles = 20;
                var emission = ps.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 10), new ParticleSystem.Burst(0.2f, 6) });
                var shape = ps.shape; shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = Size * 0.18f; shape.rotation = new Vector3(-90, 0, 0);
                var sizeOver = ps.sizeOverLifetime; sizeOver.enabled = true;
                sizeOver.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1, 1.25f)));
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 0.5f, 0.3f), 0), new GradientColorKey(new Color(0.5f, 0.25f, 0.16f), 0.3f), new GradientColorKey(new Color(0.38f, 0.18f, 0.13f), 1) },
                          new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(HazeAlpha, 0.12f), new GradientAlphaKey(HazeAlpha * 0.8f, 0.55f), new GradientAlphaKey(0, 1) });
                col.color = g;
                Renderer(ps, GlowMat("MT_BomOrangeHaze", haze, 0f), 2, false);
            }
            // ---- 白煙: 爆心の地面(物)からもくもく出て、広がって消える
            {
                var ps = NewSystem(root, "WhiteSmoke", new Vector3(0, Size * 0.01f, 0));
                var main = ps.main;
                main.startDelay = 0.15f;
                main.duration = 1.6f;
                main.startSize = new ParticleSystem.MinMaxCurve(Size * 0.06f, Size * 0.11f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                main.startSpeed = new ParticleSystem.MinMaxCurve(Size * 0.05f, Size * 0.12f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
                main.maxParticles = 60;
                var emission = ps.emission; emission.rateOverTime = 0;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 40), new Keyframe(0.5f, 25), new Keyframe(1, 0)));
                emission.SetBursts(new[] { new ParticleSystem.Burst(0, 12) });
                var shape = ps.shape; shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 75; shape.radius = Size * 0.05f; shape.rotation = new Vector3(-90, 0, 0);
                var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.limit = 0; limit.dampen = 0.08f;
                var sizeOver = ps.sizeOverLifetime; sizeOver.enabled = true;
                sizeOver.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 2.0f)));
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                          new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(SmokeAlpha, 0.12f), new GradientAlphaKey(SmokeAlpha * 0.6f, 0.6f), new GradientAlphaKey(0, 1) });
                col.color = g;
                Renderer(ps, GlowMat("MT_BomWhiteSmoke", haze, 1.0f), -2, false);
            }
        }
        Save(root);
    }

    // 白煙・もや用: ふわっとした丸テクスチャを、くすまないように少し発光させたマテリアル
    static Material GlowMat(string name, Material src, float glow)
    {
        string path = Root + "/material/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(src); AssetDatabase.CreateAsset(mat, path); }
        else mat.CopyPropertiesFromMaterial(src);
        mat.SetFloat("_EmissionAreaType", (float)Nova.Editor.Core.Scripts.EmissionAreaType.All);
        mat.SetFloat("_EmissionColorType", (float)Nova.Editor.Core.Scripts.EmissionColorType.BaseColor);
        mat.SetFloat("_EmissionIntensity", glow);
        Nova.Editor.Core.Scripts.ParticlesUberUnlitMaterialPostProcessor.SetupMaterialKeywords(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // 爆発の色: 最初は白く光る → オレンジ → 赤黒く
    static GradientColorKey[] FireColors()
    {
        return new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, 0.85f, 0.7f), 0.2f),
                       new GradientColorKey(new Color(0.62f, 0.38f, 0.3f), 0.45f), new GradientColorKey(new Color(0.4f, 0.21f, 0.17f), 1f) };
    }

    static int seed = 0;
    static ParticleSystem NewSystem(GameObject parent, string name, Vector3 pos)
    {
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false); go.transform.localPosition = pos;
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(4211 + 97 * seed++); // 毎回同じ出方にする(ムラ防止)
        var main = ps.main;
        main.duration = Life; main.loop = false; main.playOnAwake = true;
        main.startLifetime = Life; main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission; emission.rateOverTime = 0;
        return ps;
    }

    // 寿命に対する透明度。最後は必ず0にする(残りカス防止)
    static void Fade(ParticleSystem ps, Color c, float a, float inT, float holdT, float outT)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(a, inT), new GradientAlphaKey(a * 0.85f, holdT), new GradientAlphaKey(0, outT) });
        col.color = g;
    }

    static ParticleSystem.TextureSheetAnimationModule Sheet(ParticleSystem ps)
    {
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 8; sheet.numTilesY = 8; sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 60f / 64f));
        sheet.cycleCount = 1;
        return sheet;
    }

    static ParticleSystemRenderer Renderer(ParticleSystem ps, Material mat, int sortingFudge, bool custom)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard; r.alignment = ParticleSystemRenderSpace.View;
        r.sortMode = ParticleSystemSortMode.Distance; r.sortingFudge = sortingFudge; r.maxParticleSize = 10;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        var streams = new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV };
        if (custom) { streams.Add(ParticleSystemVertexStream.UV2); streams.Add(ParticleSystemVertexStream.Custom1XYZW); streams.Add(ParticleSystemVertexStream.Custom2XYZW); }
        r.SetActiveVertexStreams(streams);
        return r;
    }

    static void EmissionCurve(ParticleSystem ps)
    {
        var custom = ps.customData; custom.enabled = true;
        custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 4);
        var curve = new AnimationCurve(new Keyframe(0, 6f), new Keyframe(0.1f, 4f), new Keyframe(0.25f, 1f), new Keyframe(0.4f, 0f), new Keyframe(1, 0)) // 爆発の瞬間だけ発光、その後は光らない(赤黒く);
        custom.SetVector(ParticleSystemCustomData.Custom1, 2, new ParticleSystem.MinMaxCurve(1, curve));
    }

    static GameObject FindRoot(string name, GameObject exclude = null)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == name && go != exclude) return go;
        return null;
    }

    static void Save(GameObject root)
    {
        var scene = EditorSceneManager.GetActiveScene();
        bool inTestScene = scene.path == ScenePath;
        var existing = inTestScene ? FindRoot(root.name, root) : null;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, CPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        if (inTestScene)
        {
            // 確認用: C を表示、A/B は非表示(シーンは保存しない)
            var refObj = existing != null ? existing : FindRoot("BomExplosion_Puffs");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (refObj != null) { inst.transform.position = refObj.transform.position; inst.transform.rotation = refObj.transform.rotation; inst.transform.localScale = refObj.transform.localScale; }
            Undo.RegisterCreatedObjectUndo(inst, "Place " + prefab.name);
            if (existing != null) Undo.DestroyObjectImmediate(existing); // 子の位置などの古いオーバーライドを消す
            inst.SetActive(true);
            foreach (var n in new[] { "BomExplosion_Puffs", "BomExplosion_Scatter" }) { var o = FindRoot(n); if (o != null) o.SetActive(false); }
            Selection.activeGameObject = inst;
            EditorSceneManager.MarkSceneDirty(scene);
        }
        Debug.Log("[BomEffect] Built: " + CPath);
    }
}
