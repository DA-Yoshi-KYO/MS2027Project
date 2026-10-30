using System.Linq;
using UnityEditor;
using UnityEngine;

// メニュー: Tools > HealEffect > Setup Ring Up
// 選択中の Particle System に「上昇リング」の設定を適用する。
// 何も選択していない / Particle System がない場合は新しく作る。
public static class HealRingSetup
{
    const string Dir = "Assets/Programmer/Scene/Test/Effect/heelEffect/";
    const string MeshPath = Dir + "mesh/RingCylinder.obj";
    const string MatPath = Dir + "material/MT_circle_up_ring.mat";

    // ---- 調整用パラメーター ----
    const float TotalTime = 3f;      // リングを出し続ける時間
    const float Lifetime = 1f;       // リング1個の寿命
    const float RiseHeight = 0.35f;  // 1個が上がる高さ
    const int Count = 9;             // 出すリングの数（TotalTime の間に等間隔）
    static readonly Vector3 Size = new Vector3(1.5f, 0.3f, 1.5f); // 直径 / 帯の太さ / 直径

    [MenuItem("Tools/HealEffect/Setup Ring Up")]
    static void Setup()
    {
        ParticleSystem ps = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<ParticleSystem>() : null;
        if (ps == null)
        {
            var go = new GameObject("HealRing_Up");
            Undo.RegisterCreatedObjectUndo(go, "Create HealRing_Up");
            if (Selection.activeTransform) go.transform.SetParent(Selection.activeTransform, false);
            ps = go.AddComponent<ParticleSystem>();
        }
        var r = ps.GetComponent<ParticleSystemRenderer>();
        Undo.RecordObjects(new Object[] { ps, r, ps.transform }, "Setup Ring Up");

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.transform.localRotation = Quaternion.identity;
        ps.transform.localScale = Vector3.one;

        // Main
        var main = ps.main;
        main.duration = TotalTime;
        main.loop = false;
        main.prewarm = false;
        main.startDelay = 0f;
        main.startLifetime = Lifetime;
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = Size.x; main.startSizeY = Size.y; main.startSizeZ = Size.z;
        main.startRotation3D = false;
        main.startRotation = 0f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.simulationSpeed = 1f;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.playOnAwake = true;
        main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
        main.maxParticles = 50;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; // 画面外判定で止まる→カクつき防止
        main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;

        // Emission: 等間隔の Burst
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.rateOverDistance = 0f;
        float interval = TotalTime / Count;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1, (short)1, Count, interval) });

        var shape = ps.shape; shape.enabled = false;

        // Velocity: 真上に一定速度（寿命の間にちょうど RiseHeight 上がる）
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f);
        vel.y = new ParticleSystem.MinMaxCurve(RiseHeight / Lifetime);
        vel.z = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalX = 0f; vel.orbitalY = 0f; vel.orbitalZ = 0f;
        vel.orbitalOffsetX = 0f; vel.orbitalOffsetY = 0f; vel.orbitalOffsetZ = 0f;
        vel.radial = 0f;
        vel.speedModifier = 1f;

        // Color: 加算なので色もアルファも 黒/0 → 白/1 → 黒/0
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.white, 0.2f),
                    new GradientColorKey(Color.white, 0.7f), new GradientColorKey(Color.black, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        // 揺れの原因になるモジュールは全部オフ
        var lv = ps.limitVelocityOverLifetime; lv.enabled = false;
        var iv = ps.inheritVelocity; iv.enabled = false;
        var fo = ps.forceOverLifetime; fo.enabled = false;
        var sz = ps.sizeOverLifetime; sz.enabled = false;
        var rot = ps.rotationOverLifetime; rot.enabled = false;
        var rbs = ps.rotationBySpeed; rbs.enabled = false;
        var noise = ps.noise; noise.enabled = false;
        var ext = ps.externalForces; ext.enabled = false;
        var tsa = ps.textureSheetAnimation; tsa.enabled = false;

        // Renderer
        r.renderMode = ParticleSystemRenderMode.Mesh;
        var mesh = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().FirstOrDefault();
        if (mesh) r.mesh = mesh; else Debug.LogWarning("RingCylinder mesh not found: " + MeshPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat) r.sharedMaterial = mat; else Debug.LogWarning("Material not found: " + MatPath);
        r.alignment = ParticleSystemRenderSpace.Local;
        // Nova は GPU Instancing 時に transform/color/Custom1/Custom2 の並びでデータを読むので合わせる
        r.enableGPUInstancing = true;
        r.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.Custom1XYZW, ParticleSystemVertexStream.Custom2XYZW });
        r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion; // TAA/モーションブラーのブレ防止
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.pivot = Vector3.zero;

        EditorUtility.SetDirty(ps);
        EditorUtility.SetDirty(r);
        Selection.activeGameObject = ps.gameObject;
        ps.Play(true);
        Debug.Log("[HealRingSetup] done: " + ps.name);
    }
}
