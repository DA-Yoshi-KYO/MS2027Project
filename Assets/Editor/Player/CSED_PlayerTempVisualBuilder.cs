using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/*
 * 仮アセット(Mannequin)をPlayerの見た目として組み込むエディタ拡張
 * Tools/Player/仮の見た目を組み込む から実行する(何度実行しても同じ結果になる)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・やること
 *   1. 使うアニメーションのループ設定(FBXのImport設定)
 *   2. Animator Controller(PlayerTempAnimator.controller)の生成
 *      パラメータ名はCS_PlayerAnimatorParamsの定数を使う
 *   3. マテリアル(MT_PlayerMannequin.mat)の生成
 *   4. Player.prefabへModelを追加し、CS_PlayerVisualを付ける(カプセルの描画は外す)
 * ・本番アセットに差し替えるときは、このツールは不要になる(ClaudeUsers/プレイヤー見た目の差し替えガイド.md)
 * ・攻撃モーションの再生速度は、CSO_AttackDataのDurationに収まるよう自動で決める
 */
// ========================================

public static class CSED_PlayerTempVisualBuilder
{
    private const string _tempRoot = "Assets/Programmer/TempAssets/Player";
    private const string _modelPath = _tempRoot + "/Mannequin Character/characters/Mannequin_Medium.fbx";
    private const string _texturePath = _tempRoot + "/Mannequin Character/Textures/mannequin_texture.png";
    private const string _animFolder = _tempRoot + "/Animations/fbx/Rig_Medium/";
    private const string _outputFolder = "Assets/Programmer/TempAssets/Generated";
    private const string _controllerPath = _outputFolder + "/PlayerTempAnimator.controller";
    private const string _materialPath = _outputFolder + "/MT_PlayerMannequin.mat";
    private const string _prefabPath = "Assets/Programmer/Prefab/Entity/Player/Player.prefab";
    private const string _modelName = "Model";
    private const float _transitionTime = 0.1f;

    private static readonly string[] _loopClips =
    {
        "Idle_A", "Running_A", "Walking_Backwards", "Running_Strafe_Left", "Running_Strafe_Right", "Jump_Idle",
    };

    private static readonly string[] _clipFiles =
    {
        "Rig_Medium_General", "Rig_Medium_MovementBasic", "Rig_Medium_MovementAdvanced", "Rig_Medium_CombatMelee",
    };

    // 通常攻撃の各段に割り当てるモーション(足りない段は最後を使い回す)
    private static readonly string[] _attackClips =
    {
        "Melee_Unarmed_Attack_Punch_A", "Melee_Unarmed_Attack_Kick", "Melee_1H_Attack_Jump_Chop",
    };

    private const string _specialClip = "Melee_2H_Attack_Spinning";

    [MenuItem("Tools/Player/仮の見た目を組み込む")]
    public static void Build()
    {
        EnsureFolder(_outputFolder);
        SetLoopSettings();

        Dictionary<string, AnimationClip> clips = LoadClips();
        Material material = CreateMaterial();
        ApplyToPrefab(clips, material);

        AssetDatabase.SaveAssets();
        Debug.Log("CSED_PlayerTempVisualBuilder: 仮の見た目を組み込みました");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    // 移動系のアニメーションだけループ再生にする
    private static void SetLoopSettings()
    {
        foreach (string file in _clipFiles)
        {
            string path = _animFolder + file + ".fbx";
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            ModelImporterClipAnimation[] animations = importer.clipAnimations;
            if (animations == null || animations.Length == 0) animations = importer.defaultClipAnimations;

            bool changed = false;
            foreach (ModelImporterClipAnimation animation in animations)
            {
                bool loop = System.Array.IndexOf(_loopClips, animation.name) >= 0;
                if (animation.loopTime == loop) continue;

                animation.loopTime = loop;
                changed = true;
            }

            if (!changed) continue;

            importer.clipAnimations = animations;
            importer.SaveAndReimport();
        }
    }

    private static Dictionary<string, AnimationClip> LoadClips()
    {
        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();

        foreach (string file in _clipFiles)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(_animFolder + file + ".fbx"))
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

    // ---- Animator Controller ----

    private static AnimatorController CreateController(
        Dictionary<string, AnimationClip> clips, IReadOnlyList<CSO_AttackData> steps, CSO_AttackData special)
    {
        AssetDatabase.DeleteAsset(_controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(_controllerPath);
        AddParameters(controller);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = AddLocomotion(controller, machine, clips);
        machine.defaultState = locomotion;

        AddAir(machine, locomotion, clips);
        AddSimpleAction(machine, locomotion, clips, "Dodge_Forward", CS_PlayerAnimatorParams.dash, 0.5f);
        AddAttacks(machine, locomotion, clips, steps);
        AddSpecial(machine, locomotion, clips, special);
        AddSimpleAction(machine, locomotion, clips, "Hit_A", CS_PlayerAnimatorParams.hit, 0.9f);
        AddDeath(machine, locomotion, clips);

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
        AnimatorController controller, AnimatorStateMachine machine, Dictionary<string, AnimationClip> clips)
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

        tree.AddChild(Clip(clips, "Idle_A"), new Vector2(0f, 0f));
        tree.AddChild(Clip(clips, "Running_A"), new Vector2(0f, 1f));
        tree.AddChild(Clip(clips, "Walking_Backwards"), new Vector2(0f, -1f));
        tree.AddChild(Clip(clips, "Running_Strafe_Left"), new Vector2(-1f, 0f));
        tree.AddChild(Clip(clips, "Running_Strafe_Right"), new Vector2(1f, 0f));

        AnimatorState state = machine.AddState("Locomotion");
        state.motion = tree;
        return state;
    }

    // ジャンプ開始 → 空中 → 着地。ジャンプ以外の落下でも空中になる
    private static void AddAir(AnimatorStateMachine machine, AnimatorState locomotion, Dictionary<string, AnimationClip> clips)
    {
        AnimatorState start = AddClipState(machine, "JumpStart", Clip(clips, "Jump_Start"));
        AnimatorState air = AddClipState(machine, "Air", Clip(clips, "Jump_Idle"));
        AnimatorState land = AddClipState(machine, "JumpLand", Clip(clips, "Jump_Land"));

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
        AnimatorStateMachine machine, AnimatorState locomotion, Dictionary<string, AnimationClip> clips,
        string clipName, string trigger, float exitTime)
    {
        AnimatorState state = AddClipState(machine, trigger, Clip(clips, clipName));
        AddActionTransitions(machine, state, trigger, null);
        AddExitTransition(state, locomotion, exitTime);
    }

    private static void AddAttacks(
        AnimatorStateMachine machine, AnimatorState locomotion, Dictionary<string, AnimationClip> clips,
        IReadOnlyList<CSO_AttackData> steps)
    {
        int count = steps != null ? steps.Count : _attackClips.Length;

        for (int i = 0; i < count; i++)
        {
            AnimationClip clip = Clip(clips, _attackClips[Mathf.Min(i, _attackClips.Length - 1)]);
            AnimatorState state = AddClipState(machine, "Attack" + (i + 1), clip);
            FitSpeed(state, clip, steps != null ? steps[i] : null);
            AddActionTransitions(machine, state, CS_PlayerAnimatorParams.attack, i);
            AddExitTransition(state, locomotion, 0.95f);
        }
    }

    private static void AddSpecial(
        AnimatorStateMachine machine, AnimatorState locomotion, Dictionary<string, AnimationClip> clips, CSO_AttackData special)
    {
        AnimationClip clip = Clip(clips, _specialClip);
        AnimatorState state = AddClipState(machine, "Special", clip);
        FitSpeed(state, clip, special);
        AddActionTransitions(machine, state, CS_PlayerAnimatorParams.special, null);
        AddExitTransition(state, locomotion, 0.95f);
    }

    private static void AddDeath(AnimatorStateMachine machine, AnimatorState locomotion, Dictionary<string, AnimationClip> clips)
    {
        AnimatorState death = AddClipState(machine, "Death", Clip(clips, "Death_A"));

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

    private static void ApplyToPrefab(Dictionary<string, AnimationClip> clips, Material material)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_prefabPath);

        try
        {
            CS_PlayerAttack attack = root.GetComponent<CS_PlayerAttack>();
            CS_PlayerSpecialAttack special = root.GetComponent<CS_PlayerSpecialAttack>();
            AnimatorController controller = CreateController(
                clips, attack != null ? attack.attackSteps : null, special != null ? special.specialAttackData : null);

            RemoveCapsuleRenderer(root);
            Animator animator = AttachModel(root, controller, material);
            AttachVisual(root, animator);

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

    private static Animator AttachModel(GameObject root, AnimatorController controller, Material material)
    {
        Transform old = root.transform.Find(_modelName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(_modelPath);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
        model.name = _modelName;
        model.transform.localPosition = new Vector3(0f, -1f, 0f);   // カプセル(高さ2)の足元に合わせる
        model.transform.localRotation = Quaternion.identity;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = material;
        }

        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        return animator;
    }

    private static void AttachVisual(GameObject root, Animator animator)
    {
        CS_PlayerVisual visual = root.GetComponent<CS_PlayerVisual>();
        if (visual == null) visual = root.AddComponent<CS_PlayerVisual>();

        SerializedObject serialized = new SerializedObject(visual);
        serialized.FindProperty("_animator").objectReferenceValue = animator;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
