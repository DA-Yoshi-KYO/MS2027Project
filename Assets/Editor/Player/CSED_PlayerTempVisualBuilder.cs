using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/*
 * 仮アセット(Mannequin)をPlayerの見た目として組み込むエディタ拡張
 * Tools/Player/仮の見た目を組み込む から実行する(何度実行しても同じ結果になる)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・通常の見た目(Mannequin_Medium)と、変身後の見た目(Mannequin_Large)の2体を組み込む
 *   ボーン構成(Rig_Medium / Rig_Large)が違い、アニメーションを共有できないため、見た目ごとに
 *   アニメーションとAnimator Controllerを分けている(RigSetupで見た目ごとの違いをまとめている)
 * ・やること
 *   1. 使うアニメーションのループ設定(FBXのImport設定)
 *   2. Animator Controller(PlayerTempAnimator.controller / PlayerTempAnimator_Transformed.controller)の生成
 *      パラメータ名はCS_PlayerAnimatorParamsの定数を使う(2つとも同じパラメータ・同じ状態の構成)
 *   3. マテリアル(MT_PlayerMannequin.mat)の生成(2体で共用)
 *   4. Player.prefabへModel / TransformedModelを追加し、CS_PlayerVisualに2つのAnimatorを設定する
 *      (カプセルの描画は外す。TransformedModelは非表示で置き、変身中だけCS_PlayerVisualが表示する)
 *   5. 変身途中のパーティクル(TransformingEffect)と、そのマテリアル(MT_PlayerTransformingParticle.mat)を作り、
 *      CS_PlayerVisualに設定する(足元から光の粒が立ち上る仮の演出)
 * ・Large用のアニメーションには後退・横移動・ジャンプが無いため、近いモーションで代用している
 * ・本番アセットに差し替えるときは、このツールは不要になる(ClaudeUsers/プレイヤー見た目の差し替えガイド.md)
 * ・攻撃モーションの再生速度は、CSO_AttackDataのDurationに収まるよう自動で決める
 */
// ========================================

public static class CSED_PlayerTempVisualBuilder
{
    // 見た目ごとに違う設定(モデル、アニメーション、使うモーション名)
    private class RigSetup
    {
        public string modelPath;
        public string modelName;            // Player.prefab内での子オブジェクト名
        public bool active;                 // Prefab上で表示しておくか
        public string controllerPath;
        public string animFolder;
        public string[] clipFiles;
        public string[] loopClips;

        public string idle;
        public string forward;
        public string backward;
        public string strafeLeft;
        public string strafeRight;
        public string jumpStart;
        public string jumpAir;
        public string jumpLand;
        public string dash;
        public string hit;
        public string death;
        public string[] attacks;            // 通常攻撃の各段(足りない段は最後を使い回す)
        public string special;
    }

    private const string _tempRoot = "Assets/Programmer/TempAssets/Player";
    private const string _texturePath = _tempRoot + "/Mannequin Character/Textures/mannequin_texture.png";
    private const string _outputFolder = "Assets/Programmer/TempAssets/Generated";
    private const string _materialPath = _outputFolder + "/MT_PlayerMannequin.mat";
    private const string _particleMaterialPath = _outputFolder + "/MT_PlayerTransformingParticle.mat";
    private const string _transformingEffectName = "TransformingEffect";
    private static readonly Color _transformingColor = new Color(0.3f, 0.8f, 1f);
    private const string _prefabPath = "Assets/Programmer/Prefab/Entity/Player/Player.prefab";
    private const float _transitionTime = 0.1f;

    // 通常の見た目
    private static readonly RigSetup _normalRig = new RigSetup
    {
        modelPath = _tempRoot + "/Mannequin Character/characters/Mannequin_Medium.fbx",
        modelName = "Model",
        active = true,
        controllerPath = _outputFolder + "/PlayerTempAnimator.controller",
        animFolder = _tempRoot + "/Animations/fbx/Rig_Medium/",
        clipFiles = new[] { "Rig_Medium_General", "Rig_Medium_MovementBasic", "Rig_Medium_MovementAdvanced", "Rig_Medium_CombatMelee" },
        loopClips = new[] { "Idle_A", "Running_A", "Walking_Backwards", "Running_Strafe_Left", "Running_Strafe_Right", "Jump_Idle" },
        idle = "Idle_A",
        forward = "Running_A",
        backward = "Walking_Backwards",
        strafeLeft = "Running_Strafe_Left",
        strafeRight = "Running_Strafe_Right",
        jumpStart = "Jump_Start",
        jumpAir = "Jump_Idle",
        jumpLand = "Jump_Land",
        dash = "Dodge_Forward",
        hit = "Hit_A",
        death = "Death_A",
        attacks = new[] { "Melee_Unarmed_Attack_Punch_A", "Melee_Unarmed_Attack_Kick", "Melee_1H_Attack_Jump_Chop" },
        special = "Melee_2H_Attack_Spinning",
    };

    // 変身後の見た目(後退・横移動・ジャンプのモーションが無いので代用する)
    private static readonly RigSetup _transformedRig = new RigSetup
    {
        modelPath = _tempRoot + "/Mannequin Character/characters/Mannequin_Large.fbx",
        modelName = "TransformedModel",
        active = false,
        controllerPath = _outputFolder + "/PlayerTempAnimator_Transformed.controller",
        animFolder = _tempRoot + "/Animations/fbx/Rig_Large/",
        clipFiles = new[] { "Rig_Large_General", "Rig_Large_MovementBasic", "Rig_Large_MovementAdvanced", "Rig_Large_CombatMelee" },
        loopClips = new[] { "Idle_A", "Running_A", "Walking_A" },
        idle = "Idle_A",
        forward = "Running_A",
        backward = "Walking_A",
        strafeLeft = "Walking_A",
        strafeRight = "Walking_A",
        jumpStart = "Idle_A",
        jumpAir = "Idle_A",
        jumpLand = "Idle_A",
        dash = "Dodge_Forward",
        hit = "Hit_A",
        death = "Death_A",
        attacks = new[] { "Melee_Unarmed_Punch", "Melee_Unarmed_Kick", "Melee_Unarmed_Smash" },
        special = "Melee_2H_Attack",
    };

    [MenuItem("Tools/Player/仮の見た目を組み込む")]
    public static void Build()
    {
        EnsureFolder(_outputFolder);
        SetLoopSettings(_normalRig);
        SetLoopSettings(_transformedRig);

        Material material = CreateMaterial();
        Material particleMaterial = CreateParticleMaterial();
        ApplyToPrefab(material, particleMaterial);

        AssetDatabase.SaveAssets();
        Debug.Log("CSED_PlayerTempVisualBuilder: 仮の見た目を組み込みました");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    // 移動系のアニメーションだけループ再生にする
    private static void SetLoopSettings(RigSetup rig)
    {
        foreach (string file in rig.clipFiles)
        {
            string path = rig.animFolder + file + ".fbx";
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            ModelImporterClipAnimation[] animations = importer.clipAnimations;
            if (animations == null || animations.Length == 0) animations = importer.defaultClipAnimations;

            bool changed = false;
            foreach (ModelImporterClipAnimation animation in animations)
            {
                bool loop = System.Array.IndexOf(rig.loopClips, animation.name) >= 0;
                if (animation.loopTime == loop) continue;

                animation.loopTime = loop;
                changed = true;
            }

            if (!changed) continue;

            importer.clipAnimations = animations;
            importer.SaveAndReimport();
        }
    }

    private static Dictionary<string, AnimationClip> LoadClips(RigSetup rig)
    {
        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();

        foreach (string file in rig.clipFiles)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(rig.animFolder + file + ".fbx"))
            {
                AnimationClip clip = asset as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;

                clips[clip.name] = clip;
            }
        }

        return clips;
    }

    private static AnimationClip Clip(Dictionary<string, AnimationClip> clips, string name)
    {
        if (clips.TryGetValue(name, out AnimationClip clip)) return clip;

        Debug.LogError("CSED_PlayerTempVisualBuilder: アニメーションが見つかりません: " + name);
        return null;
    }

    private static Material CreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(_materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(material, _materialPath);
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(_texturePath);
        material.SetTexture("_BaseColorMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // 変身途中のパーティクル用。加算合成で光って見えるよう、露出の影響を受けない発光色を使う
    private static Material CreateParticleMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(_particleMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("HDRP/Unlit"));
            AssetDatabase.CreateAsset(material, _particleMaterialPath);
        }

        Texture2D texture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        material.SetTexture("_UnlitColorMap", texture);
        material.SetColor("_UnlitColor", _transformingColor);
        material.SetFloat("_BlendMode", 1f);      // 加算合成
        material.SetFloat("_EmissiveExposureWeight", 0f);
        HDMaterial.SetSurfaceType(material, true);
        HDMaterial.SetEmissiveColor(material, _transformingColor * 2f);
        HDMaterial.ValidateMaterial(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---- Animator Controller ----

    private static AnimatorController CreateController(
        RigSetup rig, IReadOnlyList<CSO_AttackData> steps, CSO_AttackData special)
    {
        Dictionary<string, AnimationClip> clips = LoadClips(rig);

        AssetDatabase.DeleteAsset(rig.controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(rig.controllerPath);
        AddParameters(controller);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = AddLocomotion(controller, machine, clips, rig);
        machine.defaultState = locomotion;

        AddAir(machine, locomotion, clips, rig);
        AddSimpleAction(machine, locomotion, Clip(clips, rig.dash), CS_PlayerAnimatorParams.dash, 0.5f);
        AddAttacks(machine, locomotion, clips, rig, steps);
        AddSpecial(machine, locomotion, Clip(clips, rig.special), special);
        AddSimpleAction(machine, locomotion, Clip(clips, rig.hit), CS_PlayerAnimatorParams.hit, 0.9f);
        AddDeath(machine, locomotion, Clip(clips, rig.death));

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddParameters(AnimatorController controller)
    {
        controller.AddParameter(CS_PlayerAnimatorParams.speed, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_PlayerAnimatorParams.moveX, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_PlayerAnimatorParams.moveZ, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_PlayerAnimatorParams.grounded, AnimatorControllerParameterType.Bool);
        controller.AddParameter(CS_PlayerAnimatorParams.dead, AnimatorControllerParameterType.Bool);
        controller.AddParameter(CS_PlayerAnimatorParams.jump, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.dash, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.attack, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.special, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.hit, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.comboStep, AnimatorControllerParameterType.Int);
    }

    private static AnimatorState AddLocomotion(
        AnimatorController controller, AnimatorStateMachine machine, Dictionary<string, AnimationClip> clips, RigSetup rig)
    {
        BlendTree tree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = CS_PlayerAnimatorParams.moveX,
            blendParameterY = CS_PlayerAnimatorParams.moveZ,
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(tree, controller);

        tree.AddChild(Clip(clips, rig.idle), new Vector2(0f, 0f));
        tree.AddChild(Clip(clips, rig.forward), new Vector2(0f, 1f));
        tree.AddChild(Clip(clips, rig.backward), new Vector2(0f, -1f));
        tree.AddChild(Clip(clips, rig.strafeLeft), new Vector2(-1f, 0f));
        tree.AddChild(Clip(clips, rig.strafeRight), new Vector2(1f, 0f));

        AnimatorState state = machine.AddState("Locomotion");
        state.motion = tree;
        return state;
    }

    // ジャンプ開始 → 空中 → 着地。ジャンプ以外の落下でも空中になる
    private static void AddAir(
        AnimatorStateMachine machine, AnimatorState locomotion, Dictionary<string, AnimationClip> clips, RigSetup rig)
    {
        AnimatorState start = AddClipState(machine, "JumpStart", Clip(clips, rig.jumpStart));
        AnimatorState air = AddClipState(machine, "Air", Clip(clips, rig.jumpAir));
        AnimatorState land = AddClipState(machine, "JumpLand", Clip(clips, rig.jumpLand));

        AddActionTransitions(machine, start, CS_PlayerAnimatorParams.jump, null);

        AnimatorStateTransition startToAir = start.AddTransition(air);
        SetupTransition(startToAir, true, 0.9f);

        AnimatorStateTransition fall = locomotion.AddTransition(air);
        SetupTransition(fall, false, 0f);
        fall.AddCondition(AnimatorConditionMode.IfNot, 0f, CS_PlayerAnimatorParams.grounded);

        AnimatorStateTransition landing = air.AddTransition(land);
        SetupTransition(landing, false, 0f);
        landing.AddCondition(AnimatorConditionMode.If, 0f, CS_PlayerAnimatorParams.grounded);

        AnimatorStateTransition landed = land.AddTransition(locomotion);
        SetupTransition(landed, true, 0.8f);
    }

    private static void AddSimpleAction(
        AnimatorStateMachine machine, AnimatorState locomotion, AnimationClip clip, string trigger, float exitTime)
    {
        AnimatorState state = AddClipState(machine, trigger, clip);
        AddActionTransitions(machine, state, trigger, null);
        AddExitTransition(state, locomotion, exitTime);
    }

    private static void AddAttacks(
        AnimatorStateMachine machine, AnimatorState locomotion, Dictionary<string, AnimationClip> clips,
        RigSetup rig, IReadOnlyList<CSO_AttackData> steps)
    {
        int count = steps != null ? steps.Count : rig.attacks.Length;

        for (int i = 0; i < count; i++)
        {
            AnimationClip clip = Clip(clips, rig.attacks[Mathf.Min(i, rig.attacks.Length - 1)]);
            AnimatorState state = AddClipState(machine, "Attack" + (i + 1), clip);
            FitSpeed(state, clip, steps != null ? steps[i] : null);
            AddActionTransitions(machine, state, CS_PlayerAnimatorParams.attack, i);
            AddExitTransition(state, locomotion, 0.95f);
        }
    }

    private static void AddSpecial(
        AnimatorStateMachine machine, AnimatorState locomotion, AnimationClip clip, CSO_AttackData special)
    {
        AnimatorState state = AddClipState(machine, "Special", clip);
        FitSpeed(state, clip, special);
        AddActionTransitions(machine, state, CS_PlayerAnimatorParams.special, null);
        AddExitTransition(state, locomotion, 0.95f);
    }

    private static void AddDeath(AnimatorStateMachine machine, AnimatorState locomotion, AnimationClip clip)
    {
        AnimatorState death = AddClipState(machine, "Death", clip);

        AnimatorStateTransition die = machine.AddAnyStateTransition(death);
        SetupTransition(die, false, 0f);
        die.AddCondition(AnimatorConditionMode.If, 0f, CS_PlayerAnimatorParams.dead);
        die.canTransitionToSelf = false;

        AnimatorStateTransition revive = death.AddTransition(locomotion);
        SetupTransition(revive, false, 0f);
        revive.AddCondition(AnimatorConditionMode.IfNot, 0f, CS_PlayerAnimatorParams.dead);
    }

    // どの状態からでも、トリガーでその状態へ入る(死亡中は入らない)
    private static void AddActionTransitions(AnimatorStateMachine machine, AnimatorState state, string trigger, int? comboStep)
    {
        AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
        SetupTransition(enter, false, 0f);
        enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        enter.AddCondition(AnimatorConditionMode.IfNot, 0f, CS_PlayerAnimatorParams.dead);
        enter.canTransitionToSelf = false;

        if (comboStep.HasValue)
        {
            enter.AddCondition(AnimatorConditionMode.Equals, comboStep.Value, CS_PlayerAnimatorParams.comboStep);
        }
    }

    // 再生が終わったらLocomotionへ戻る
    private static void AddExitTransition(AnimatorState state, AnimatorState locomotion, float exitTime)
    {
        AnimatorStateTransition exit = state.AddTransition(locomotion);
        SetupTransition(exit, true, exitTime);
    }

    private static void SetupTransition(AnimatorStateTransition transition, bool hasExitTime, float exitTime)
    {
        transition.hasExitTime = hasExitTime;
        transition.exitTime = exitTime;
        transition.hasFixedDuration = true;
        transition.duration = _transitionTime;
    }

    private static AnimatorState AddClipState(AnimatorStateMachine machine, string name, AnimationClip clip)
    {
        AnimatorState state = machine.AddState(name);
        state.motion = clip;
        return state;
    }

    // モーションがCSO_AttackDataのDurationに収まるように再生速度を決める
    private static void FitSpeed(AnimatorState state, AnimationClip clip, CSO_AttackData data)
    {
        if (clip == null || data == null || data.duration <= 0f) return;

        state.speed = clip.length / data.duration;
    }

    // ---- Prefab ----

    private static void ApplyToPrefab(Material material, Material particleMaterial)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_prefabPath);

        try
        {
            CS_PlayerAttack attack = root.GetComponent<CS_PlayerAttack>();
            CS_PlayerSpecialAttack special = root.GetComponent<CS_PlayerSpecialAttack>();
            IReadOnlyList<CSO_AttackData> steps = attack != null ? attack.attackSteps : null;
            CSO_AttackData specialData = special != null ? special.specialAttackData : null;

            RemoveCapsuleRenderer(root);
            Animator normal = AttachModel(root, _normalRig, CreateController(_normalRig, steps, specialData), material);
            Animator transformed = AttachModel(root, _transformedRig, CreateController(_transformedRig, steps, specialData), material);
            ParticleSystem effect = AttachTransformingEffect(root, particleMaterial);
            AttachVisual(root, normal, transformed, effect);

            PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveCapsuleRenderer(GameObject root)
    {
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        MeshFilter filter = root.GetComponent<MeshFilter>();
        if (renderer != null) Object.DestroyImmediate(renderer);
        if (filter != null) Object.DestroyImmediate(filter);
    }

    private static Animator AttachModel(GameObject root, RigSetup rig, AnimatorController controller, Material material)
    {
        Transform old = root.transform.Find(rig.modelName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(rig.modelPath);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
        model.name = rig.modelName;
        model.transform.localPosition = new Vector3(0f, -1f, 0f);   // カプセル(高さ2)の足元に合わせる
        model.transform.localRotation = Quaternion.identity;
        model.SetActive(rig.active);

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterial = material;
        }

        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        return animator;
    }

    // 足元から光の粒が立ち上る、変身途中の仮エフェクト
    private static ParticleSystem AttachTransformingEffect(GameObject root, Material material)
    {
        Transform old = root.transform.Find(_transformingEffectName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject effectObject = new GameObject(_transformingEffectName);
        effectObject.transform.SetParent(root.transform, false);
        effectObject.transform.localPosition = new Vector3(0f, -1f, 0f);        // カプセルの足元
        effectObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);  // 上向きに放出する

        ParticleSystem particle = effectObject.AddComponent<ParticleSystem>();
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particle.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = 1.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = _transformingColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 60f;

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 5f;
        shape.radius = 0.7f;

        ParticleSystem.SizeOverLifetimeModule size = particle.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer renderer = effectObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        return particle;
    }

    private static void AttachVisual(GameObject root, Animator normal, Animator transformed, ParticleSystem transformingEffect)
    {
        CS_PlayerVisual visual = root.GetComponent<CS_PlayerVisual>();
        if (visual == null) visual = root.AddComponent<CS_PlayerVisual>();

        SerializedObject serialized = new SerializedObject(visual);
        serialized.FindProperty("_animator").objectReferenceValue = normal;
        serialized.FindProperty("_transformedAnimator").objectReferenceValue = transformed;
        serialized.FindProperty("_transformingEffect").objectReferenceValue = transformingEffect;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
