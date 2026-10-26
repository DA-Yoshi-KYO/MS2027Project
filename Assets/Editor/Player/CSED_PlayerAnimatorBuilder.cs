using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/*
 * プレイヤー(Munnki)のアニメーションをAnimator Controllerへ組み込むエディタ拡張
 * Tools/Player/アニメーションを組み込む から実行する(何度実行しても同じ結果になる)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・アニメーションを差し替える・増やすときは、下の「どの動作にどのアニメーションを使うか」の表を書き換えて実行し直す
 *   (同じファイル名で上書きしただけなら、表はそのままで実行し直すだけでよい)
 * ・やること
 *   1. 表にあるアニメーションのFBXの読み込み設定(Humanoid、ループ、切り出す範囲)
 *      アニメーションのFBXはMunnkiとボーンの並びが少し違い、MunnkiのAvatarを共有できないため、
 *      それぞれのFBXからAvatarを作る(Humanoid同士なので、Munnkiにそのまま再生できる)
 *      ただしMixamoの「Without Skin」のFBXは基準の姿勢(T-ポーズ)を持っておらず、そのままだとモーションの最初の姿勢
 *      (走りなら膝を曲げた姿勢)が基準になり、足が変な方向に曲がる。そのため、ボーンの割り当てと基準の姿勢は
 *      Munnkiのモデルのものを写す(ボーン名はどちらもMixamoの名前なので、名前で対応が取れる)
 *   2. Animator Controller(PlayerMunnkiAnimator.controller)を作り直す
 *      ファイルは消さずに中身だけ作り直す(消して作り直すと、読み込み済みのPlayer.prefabがControllerを見失い、
 *      プレイしてもアニメーションしなくなるため)
 *      パラメータ名はCS_PlayerAnimatorParamsの定数を使う
 *   3. Player.prefabのModelのAnimatorに、作り直したControllerを設定する
 * ・表で空(null)の動作
 *   待機は仮の立ちポーズ(Munnki_TPose.anim)を使う
 *   それ以外(ダッシュ・被弾・死亡など)は状態を作らない(その間は移動のアニメーションのまま)
 * ・攻撃・必殺技・ダッシュ・パリー・奇襲は、ゲーム側の時間(CSO_AttackDataのDurationなど)に収まるよう再生速度を決める
 *   モーションの前後に余計な動きがある場合は、表のstart / end(0〜1)で使う範囲を切り出す
 * ・ルートモーションは使わない(移動はRigidbodyが行う)。高さ・向きはモーションの中に焼き込むので、ジャンプの高さは見た目に出る
 * ・以前のMannequin用の仮の見た目の組み込みツール(CSED_PlayerTempVisualBuilder)は、Munnkiへの差し替えで不要になったため、このツールに置き換えた
 */
// ========================================

public static class CSED_PlayerAnimatorBuilder
{
    // 1つの動作に使うアニメーション
    private class Motion
    {
        public string file;         // _animationFolderの中のFBXのファイル名(拡張子なし)
        public string clipName;     // 作るクリップの名前(同じFBXから複数切り出す時に区別する)
        public bool loop;
        public float start;         // 使う範囲の始まり(0〜1)
        public float end = 1f;      // 使う範囲の終わり(0〜1)
    }

    private const string _munnkiRoot = "Assets/Programmer/PlaceholderAsset/Player/Munnki";
    private const string _animationFolder = _munnkiRoot + "/Animation 1/";
    private const string _modelPath = _munnkiRoot + "/Model/FBX/Munkki_mixamo_rigged.fbx";
    private const string _controllerPath = _munnkiRoot + "/Animation/PlayerMunnkiAnimator.controller";
    private const string _fallbackClipPath = _munnkiRoot + "/Animation/Munnki_TPose.anim";
    private const string _prefabPath = "Assets/Programmer/Prefab/Entity/Player/Player.prefab";
    private const string _modelName = "Model";
    private const float _transitionTime = 0.1f;

    // ---- どの動作にどのアニメーションを使うか(アニメーションが変わったらここを書き換える) ----

    private static readonly Motion _idle = new Motion { file = "Standing Idle", clipName = "Idle", loop = true };   // 待機(nullにすると仮の立ちポーズ)
    private static readonly Motion _walk = new Motion { file = "Standard Walk", clipName = "Walk", loop = true };   // 通常の移動
    private static readonly Motion _run = new Motion { file = "Running", clipName = "Run", loop = true };          // 走り(ダッシュ中・ダッシュボタンを押し続けている間)

    // ジャンプは1つのFBXを、踏み切り・空中・着地の3つに切り出して使う
    private static readonly Motion _jumpStart = new Motion { file = "Jump", clipName = "JumpStart", start = 0f, end = 0.4f };
    private static readonly Motion _jumpAir = new Motion { file = "Jump", clipName = "JumpAir", start = 0.4f, end = 0.6f, loop = true };
    private static readonly Motion _jumpLand = new Motion { file = "Jump", clipName = "JumpLand", start = 0.6f, end = 1f };

    private static readonly Motion _dash = null;    // ダッシュ(まだ無い)

    // 通常攻撃の各段(5段コンボの4・5段目は先頭から使い回す。CS_PlayerVisualのAttack Motion Countもこの数に合わせる)
    private static readonly Motion[] _attacks =
    {
        new Motion { file = "Attack1", clipName = "Attack1" },
        new Motion { file = "Attack2", clipName = "Attack2" },
        new Motion { file = "Attack3", clipName = "Attack3" },
    };

    private static readonly Motion _special = new Motion { file = "SpecialAttack", clipName = "Special" };
    private static readonly Motion _parry = new Motion { file = "parry", clipName = "Parry" };
    private static readonly Motion _ambush = new Motion { file = "Surprise Uppercut", clipName = "Ambush" };
    private static readonly Motion _hit = null;     // 被弾(まだ無い)
    private static readonly Motion _death = new Motion { file = "defense", clipName = "Death" };    // 死亡(最後の姿勢のまま、復活するまで止まる)

    private const float _parryDuration = 0.6f;      // パリー(反撃)のモーションの長さ(秒。CS_PlayerParryの無敵時間と同じくらい)
    private const float _ambushDuration = 0.6f;     // 奇襲のモーションの長さ(秒。飛び込み+攻撃)

    // ----

    [MenuItem("Tools/Player/アニメーションを組み込む")]
    public static void Build()
    {
        Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(_modelPath);
        if (avatar == null || !avatar.isHuman)
        {
            Debug.LogError("CSED_PlayerAnimatorBuilder: MunnkiのモデルがHumanoidになっていません: " + _modelPath);
            return;
        }

        List<Motion> motions = CollectMotions();
        ImportMotions(motions, ((ModelImporter)AssetImporter.GetAtPath(_modelPath)).humanDescription);

        AnimatorController controller = CreateController();
        ApplyToPrefab(controller);

        AssetDatabase.SaveAssets();
        Debug.Log("CSED_PlayerAnimatorBuilder: アニメーションを組み込みました");
    }

    private static List<Motion> CollectMotions()
    {
        List<Motion> motions = new List<Motion> { _idle, _walk, _run, _jumpStart, _jumpAir, _jumpLand, _dash, _special, _parry, _ambush, _hit, _death };
        motions.AddRange(_attacks);
        motions.RemoveAll(motion => motion == null);
        return motions;
    }

    // ---- FBXの読み込み設定 ----

    private static void ImportMotions(List<Motion> motions, HumanDescription modelDescription)
    {
        Dictionary<string, List<Motion>> byFile = new Dictionary<string, List<Motion>>();
        foreach (Motion motion in motions)
        {
            if (!byFile.TryGetValue(motion.file, out List<Motion> list))
            {
                list = new List<Motion>();
                byFile[motion.file] = list;
            }
            list.Add(motion);
        }

        foreach (KeyValuePair<string, List<Motion>> pair in byFile)
        {
            string path = _animationFolder + pair.Key + ".fbx";
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("CSED_PlayerAnimatorBuilder: アニメーションが見つかりません: " + path);
                continue;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.clipAnimations = new ModelImporterClipAnimation[0];
            importer.SaveAndReimport();     // Rigを変えた後に読み込み直さないと、元のアニメーション(take)とボーンが読めない

            importer.humanDescription = CopyHumanDescription(modelDescription, importer.humanDescription);
            importer.SaveAndReimport();

            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (takes.Length == 0)
            {
                Debug.LogError("CSED_PlayerAnimatorBuilder: アニメーションが入っていません: " + path);
                continue;
            }

            ModelImporterClipAnimation take = takes[0];
            List<ModelImporterClipAnimation> clips = new List<ModelImporterClipAnimation>();
            foreach (Motion motion in pair.Value)
            {
                clips.Add(new ModelImporterClipAnimation
                {
                    name = motion.clipName,
                    takeName = take.takeName,
                    firstFrame = Mathf.Lerp(take.firstFrame, take.lastFrame, motion.start),
                    lastFrame = Mathf.Lerp(take.firstFrame, take.lastFrame, motion.end),
                    loopTime = motion.loop,
                    lockRootRotation = true,        // 向きはモーションの中に焼き込む(体の向きはゲーム側が決める)
                    keepOriginalOrientation = true,
                    lockRootHeightY = true,         // 高さもモーションの中に焼き込む(ジャンプの高さが見た目に出る)
                    keepOriginalPositionY = true,
                    lockRootPositionXZ = false,     // 前後左右の移動はルートモーション扱い(使わないので、その場で動く)
                });
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }
    }

    // ボーンの割り当てと基準の姿勢をMunnkiのモデルから写す。FBXに無いボーン(髪など)は除き、Munnkiに無いボーン(ルートなど)はFBXのものを残す
    private static HumanDescription CopyHumanDescription(HumanDescription model, HumanDescription target)
    {
        Dictionary<string, SkeletonBone> modelBones = new Dictionary<string, SkeletonBone>();
        foreach (SkeletonBone bone in model.skeleton)
        {
            modelBones[bone.name] = bone;
        }

        HashSet<string> targetNames = new HashSet<string>();
        SkeletonBone[] skeleton = target.skeleton;
        for (int i = 0; i < skeleton.Length; i++)
        {
            targetNames.Add(skeleton[i].name);
            if (i == 0) continue;   // 先頭はFBXの一番上のオブジェクト(名前がファイルごとに違う)なので、そのまま使う

            if (modelBones.TryGetValue(skeleton[i].name, out SkeletonBone source))
            {
                skeleton[i].position = source.position;
                skeleton[i].rotation = source.rotation;
                skeleton[i].scale = source.scale;
            }
        }

        List<HumanBone> human = new List<HumanBone>();
        foreach (HumanBone bone in model.human)
        {
            if (targetNames.Contains(bone.boneName)) human.Add(bone);
        }

        HumanDescription result = model;
        result.skeleton = skeleton;
        result.human = human.ToArray();
        return result;
    }

    private static AnimationClip LoadClip(Motion motion)
    {
        if (motion == null) return null;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(_animationFolder + motion.file + ".fbx"))
        {
            if (asset is AnimationClip clip && clip.name == motion.clipName) return clip;
        }

        Debug.LogError($"CSED_PlayerAnimatorBuilder: クリップが見つかりません: {motion.file} / {motion.clipName}");
        return null;
    }

    // ---- Animator Controller ----

    private static AnimatorController CreateController()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
        CS_Player player = prefab.GetComponent<CS_Player>();
        CS_PlayerAttack attack = prefab.GetComponent<CS_PlayerAttack>();
        CS_PlayerSpecialAttack special = prefab.GetComponent<CS_PlayerSpecialAttack>();
        IReadOnlyList<CSO_AttackData> steps = attack != null ? attack.attackSteps : null;

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(_controllerPath);
        }
        ClearController(controller);
        AddParameters(controller);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = AddLocomotion(controller, machine, out AnimatorState sprint);
        machine.defaultState = locomotion;

        AddJump(machine, locomotion, sprint);
        AddAction(machine, locomotion, _dash, CS_PlayerAnimatorParams.dash, player != null ? player.dashDuration : 0f, null);

        for (int i = 0; i < _attacks.Length; i++)
        {
            float duration = steps != null && i < steps.Count && steps[i] != null ? steps[i].duration : 0f;
            AnimatorState state = AddAction(machine, locomotion, _attacks[i], CS_PlayerAnimatorParams.attack, duration, i);
            if (state == null) continue;

            // 暗躍ボーナスで攻撃が速くなった分だけ、モーションも速くする
            state.speedParameter = CS_PlayerAnimatorParams.attackSpeed;
            state.speedParameterActive = true;
        }

        AddAction(machine, locomotion, _special, CS_PlayerAnimatorParams.special, special != null && special.specialAttackData != null ? special.specialAttackData.duration : 0f, null);
        AddAction(machine, locomotion, _parry, CS_PlayerAnimatorParams.parry, _parryDuration, null);
        AddAction(machine, locomotion, _ambush, CS_PlayerAnimatorParams.ambush, _ambushDuration, null);
        AddAction(machine, locomotion, _hit, CS_PlayerAnimatorParams.hit, 0f, null);
        AddDeath(machine, locomotion);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    // パラメータ・状態・遷移・ブレンドツリーを全部消す(Controllerのファイル自体は残す)
    private static void ClearController(AnimatorController controller)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            controller.RemoveParameter(parameter);
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            machine.RemoveAnyStateTransition(transition);
        }
        foreach (ChildAnimatorState child in machine.states)
        {
            machine.RemoveState(child.state);
        }

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(_controllerPath))
        {
            if (asset is BlendTree) Object.DestroyImmediate(asset, true);
        }
    }

    private static void AddParameters(AnimatorController controller)
    {
        controller.AddParameter(CS_PlayerAnimatorParams.speed, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_PlayerAnimatorParams.moveX, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_PlayerAnimatorParams.moveZ, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_PlayerAnimatorParams.grounded, AnimatorControllerParameterType.Bool);
        controller.AddParameter(CS_PlayerAnimatorParams.dead, AnimatorControllerParameterType.Bool);
        controller.AddParameter(CS_PlayerAnimatorParams.sprint, AnimatorControllerParameterType.Bool);
        controller.AddParameter(CS_PlayerAnimatorParams.jump, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.dash, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.attack, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.special, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.hit, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.parry, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.ambush, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_PlayerAnimatorParams.comboStep, AnimatorControllerParameterType.Int);

        AnimatorControllerParameter attackSpeed = new AnimatorControllerParameter
        {
            name = CS_PlayerAnimatorParams.attackSpeed,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        };
        controller.AddParameter(attackSpeed);
    }

    // 移動。体から見た移動の向き・速さ(MoveX / MoveZ。通常の移動の速さで1)で、待機と歩きを混ぜる
    // 通常の移動より速い間(Sprint。ダッシュ中・ダッシュボタンを押し続けている間)は、走りの状態にする
    // 体は基本的に進む向きを向くので、後ろ・横は狙っている間(攻撃の向きを向いている間)くらいしか使わない
    private static AnimatorState AddLocomotion(AnimatorController controller, AnimatorStateMachine machine, out AnimatorState sprint)
    {
        AnimationClip idle = _idle != null ? LoadClip(_idle) : AssetDatabase.LoadAssetAtPath<AnimationClip>(_fallbackClipPath);
        AnimationClip walk = LoadClip(_walk);
        AnimationClip run = LoadClip(_run);

        AnimatorState locomotion = machine.AddState("Locomotion");
        locomotion.motion = CreateMoveTree(controller, "Locomotion", idle, walk ?? run);

        sprint = null;
        if (run == null || walk == null) return locomotion;

        sprint = machine.AddState("Sprint");
        sprint.motion = CreateMoveTree(controller, "Sprint", idle, run);

        AnimatorStateTransition toSprint = locomotion.AddTransition(sprint);
        SetupTransition(toSprint, false, 0f);
        toSprint.AddCondition(AnimatorConditionMode.If, 0f, CS_PlayerAnimatorParams.sprint);

        AnimatorStateTransition toWalk = sprint.AddTransition(locomotion);
        SetupTransition(toWalk, false, 0f);
        toWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, CS_PlayerAnimatorParams.sprint);
        return locomotion;
    }

    // 中央が待機、前後左右が移動のモーションのブレンドツリー
    private static BlendTree CreateMoveTree(AnimatorController controller, string name, AnimationClip idle, AnimationClip move)
    {
        BlendTree tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = CS_PlayerAnimatorParams.moveX,
            blendParameterY = CS_PlayerAnimatorParams.moveZ,
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(tree, controller);

        tree.AddChild(idle, new Vector2(0f, 0f));
        tree.AddChild(move, new Vector2(0f, 1f));
        tree.AddChild(move, new Vector2(0f, -1f));
        tree.AddChild(move, new Vector2(-1f, 0f));
        tree.AddChild(move, new Vector2(1f, 0f));
        return tree;
    }

    // ジャンプ開始 → 空中 → 着地。ジャンプ以外の落下でも空中になる
    private static void AddJump(AnimatorStateMachine machine, AnimatorState locomotion, AnimatorState sprint)
    {
        AnimationClip airClip = LoadClip(_jumpAir);
        if (airClip == null) return;

        AnimatorState air = AddClipState(machine, "Air", airClip);

        foreach (AnimatorState ground in new[] { locomotion, sprint })
        {
            if (ground == null) continue;

            AnimatorStateTransition fall = ground.AddTransition(air);
            SetupTransition(fall, false, 0f);
            fall.AddCondition(AnimatorConditionMode.IfNot, 0f, CS_PlayerAnimatorParams.grounded);
        }

        AnimationClip startClip = LoadClip(_jumpStart);
        if (startClip != null)
        {
            AnimatorState start = AddClipState(machine, "JumpStart", startClip);
            AddEnterTransition(machine, start, CS_PlayerAnimatorParams.jump, null);
            SetupTransition(start.AddTransition(air), true, 0.9f);
        }

        AnimationClip landClip = LoadClip(_jumpLand);
        AnimatorState landTarget = locomotion;
        if (landClip != null)
        {
            AnimatorState land = AddClipState(machine, "JumpLand", landClip);
            SetupTransition(land.AddTransition(locomotion), true, 0.8f);
            landTarget = land;
        }

        AnimatorStateTransition landing = air.AddTransition(landTarget);
        SetupTransition(landing, false, 0f);
        landing.AddCondition(AnimatorConditionMode.If, 0f, CS_PlayerAnimatorParams.grounded);
    }

    // トリガーで入り、再生し終わったら移動へ戻る動作。アニメーションが無ければ状態を作らない
    private static AnimatorState AddAction(
        AnimatorStateMachine machine, AnimatorState locomotion, Motion motion, string trigger, float duration, int? comboStep)
    {
        AnimationClip clip = LoadClip(motion);
        if (clip == null) return null;

        AnimatorState state = AddClipState(machine, motion.clipName, clip);
        if (clip.length > 0f && duration > 0f) state.speed = clip.length / duration;

        AddEnterTransition(machine, state, trigger, comboStep);
        SetupTransition(state.AddTransition(locomotion), true, 0.95f);
        return state;
    }

    private static void AddDeath(AnimatorStateMachine machine, AnimatorState locomotion)
    {
        AnimationClip clip = LoadClip(_death);
        if (clip == null) return;

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
    private static void AddEnterTransition(AnimatorStateMachine machine, AnimatorState state, string trigger, int? comboStep)
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

    // ---- Prefab ----

    private static void ApplyToPrefab(AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_prefabPath);

        try
        {
            Transform model = root.transform.Find(_modelName);
            Animator animator = model != null ? model.GetComponent<Animator>() : null;
            if (animator == null)
            {
                Debug.LogError("CSED_PlayerAnimatorBuilder: Player.prefabのModelにAnimatorがありません");
                return;
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            CS_PlayerVisual visual = root.GetComponent<CS_PlayerVisual>();
            if (visual != null)
            {
                SerializedObject serialized = new SerializedObject(visual);
                serialized.FindProperty("_animator").objectReferenceValue = animator;
                serialized.FindProperty("_attackMotionCount").intValue = _attacks.Length;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
