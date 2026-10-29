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
 * ・当たる瞬間を合わせる(表のimpact。モーションの中で当たる瞬間の位置、0〜1。負の値なら合わせない)
 *   通常攻撃・必殺技: 当たる瞬間が、ゲーム側の判定が出る時間(Hit Delay)ちょうどに来るよう、再生速度と使う範囲を計算し直す
 *     速度 = 「判定までに当たる瞬間まで再生できる」と「残りの時間で終わりまで再生できる」のうち遅い方
 *     はみ出す側(当たる前の構え、または当たった後の戻り)は切り詰める
 *   奇襲: 飛び込みの距離で当たるまでの時間が毎回変わるため、Animatorの速さはゲーム中に決める(AmbushSpeed)
 *     飛び込みを始めた瞬間に構えから再生し、当たる瞬間に打撃が来るよう、CS_PlayerVisualが速さを計算する
 *     そのために、クリップの頭から当たる瞬間までの秒数をCS_PlayerVisualのAmbush Impact Timeへ書き込む
 *   当たる瞬間の位置は、FBXのアニメーションで動きが一番速いフレーム(振り抜く瞬間)を目安にした
 * ・ジャンプ: 踏み切りは沈み込みの一番下から、空中は最高点からにして、ボタンを押した瞬間に体が浮き始める見た目にする
 *   踏み切り(最高点まで)と空中(最高点から着地まで)は、ジャンプ力と重力から計算した上昇の時間に合わせて再生する
 *   着地は、着地した時に移動していれば(Speed)すぐ移動のアニメーションへ戻る(走りながら着地して止まらないように)
 * ・足・頭のIK(CS_PlayerIK)のため、レイヤーのIK Passをオンにする
 * ・ダッシュのモーションが無い間は、ダッシュした瞬間にどの状態からでも移動へ戻す(攻撃の途中でダッシュで中断できるため)
 * ・通常攻撃の各段に使うモーションは、段ごとのデータ(Hit Delay・Duration)が一番近いモーションを選び、
 *   CS_PlayerVisualのAttack Motion For Stepへ書き込む(5段コンボの4・5段目も、タイミングの合うモーションを使う)
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
        public float impact = -1f;  // 当たる瞬間の位置(0〜1。負の値なら合わせない)

        // ゲーム側のタイミング(ResolveTimingsで入れる)と、それに合わせて計算した結果(ImportMotionsで入れる)
        public float hitDelay;              // 判定が出るまでの時間(秒)
        public float duration;              // 動作の時間(秒)
        public float playFramesPerSecond;   // 当たる瞬間を合わせた再生の速さ(元のフレーム/秒。0なら合わせていない)
        public float impactFrames;          // 切り出したクリップの頭から当たる瞬間までのフレーム数
    }

    private const string _munnkiRoot = "Assets/Programmer/PlaceholderAsset/Player/Munnki";
    private const string _animationFolder = _munnkiRoot + "/Animation 1/";
    private const string _modelPath = _munnkiRoot + "/Model/FBX/Munkki_mixamo_rigged.fbx";
    private const string _controllerPath = _munnkiRoot + "/Animation/PlayerMunnkiAnimator.controller";
    private const string _fallbackClipPath = _munnkiRoot + "/Animation/Munnki_TPose.anim";
    private const string _prefabPath = "Assets/Programmer/Prefab/Entity/Player/Player.prefab";
    private const string _statsPath = "Assets/Programmer/Database/Player/DB_PlayerStats.asset";
    private const string _modelName = "Model";
    private const float _transitionTime = 0.1f;

    // ---- どの動作にどのアニメーションを使うか(アニメーションが変わったらここを書き換える) ----

    private static readonly Motion _idle = new Motion { file = "Standing Idle", clipName = "Idle", loop = true };   // 待機(nullにすると仮の立ちポーズ)
    private static readonly Motion _walk = new Motion { file = "Standard Walk", clipName = "Walk", loop = true };   // 通常の移動
    private static readonly Motion _run = new Motion { file = "Running", clipName = "Run", loop = true };          // 走り(ダッシュ中・ダッシュボタンを押し続けている間)

    // ジャンプは1つのFBXを、踏み切り・空中・着地の3つに切り出して使う(Jumping: 全57フレーム)
    //   沈み込みの一番下 11 / 最高点 22 / 着地 33 / 立ち直り 51(腰の高さの動きから読み取った)
    //   踏み切りは沈み込みの一番下から始める(ボタンを押した瞬間に体が浮くため、しゃがむ構えは使わない)
    //   空中はループさせず、最後の姿勢(着地の直前)のまま着地を待つ
    private static readonly Motion _jumpStart = new Motion { file = "Jumping", clipName = "JumpStart", start = 11f / 57f, end = 22f / 57f };
    private static readonly Motion _jumpAir = new Motion { file = "Jumping", clipName = "JumpAir", start = 22f / 57f, end = 31f / 57f };
    private static readonly Motion _jumpLand = new Motion { file = "Jumping", clipName = "JumpLand", start = 33f / 57f, end = 51f / 57f };

    private static readonly Motion _dash = null;    // ダッシュ(まだ無い)

    // 通常攻撃の各段(5段コンボの4・5段目は先頭から使い回す。CS_PlayerVisualのAttack Motion Countもこの数に合わせる)
    // impactは当たる瞬間(Attack1: 27/69フレーム、Attack2: 37/89、Attack3: 53/98)
    private static readonly Motion[] _attacks =
    {
        new Motion { file = "Attack1", clipName = "Attack1", impact = 27f / 69f },
        new Motion { file = "Attack2", clipName = "Attack2", impact = 37f / 89f },
        new Motion { file = "Attack3", clipName = "Attack3", impact = 53f / 98f },
    };

    private static readonly Motion _special = new Motion { file = "SpecialAttack", clipName = "Special", impact = 33f / 95f };
    private static readonly Motion _parry = new Motion { file = "parry", clipName = "Parry" };
    // 奇襲: 沈み込んで(〜25フレーム)から伸び上がってアッパー(32フレーム)、戻り(〜68フレーム)。その後の立ち止まりは使わない
    private static readonly Motion _ambush = new Motion { file = "Surprise Uppercut", clipName = "Ambush", end = 68f / 105f, impact = 32f / 105f };
    private static readonly Motion _hit = null;     // 被弾(まだ無い)
    private static readonly Motion _death = new Motion { file = "defense", clipName = "Death" };    // 死亡(最後の姿勢のまま、復活するまで止まる)

    private const float _parryDuration = 0.6f;      // パリー(反撃)のモーションの長さ(秒。CS_PlayerParryの無敵時間と同じくらい)
    private const float _landInterruptSpeed = 0.3f; // 着地した時、Speedがこれより大きければ(移動していれば)すぐ移動のアニメーションへ戻る

    private static float _ambushImpactTime;         // 奇襲のクリップの頭から当たる瞬間までの秒数(CS_PlayerVisualへ書き込む)

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
        ResolveTimings();
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

    // 通常攻撃・必殺技に、ゲーム側の判定のタイミング(Hit Delay・Duration)を入れる
    private static void ResolveTimings()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
        CS_PlayerAttack attack = prefab != null ? prefab.GetComponent<CS_PlayerAttack>() : null;
        CS_PlayerSpecialAttack special = prefab != null ? prefab.GetComponent<CS_PlayerSpecialAttack>() : null;
        IReadOnlyList<CSO_AttackData> steps = attack != null ? attack.attackSteps : null;

        for (int i = 0; i < _attacks.Length; i++)
        {
            SetTiming(_attacks[i], steps != null && i < steps.Count ? steps[i] : null);
        }
        SetTiming(_special, special != null ? special.specialAttackData : null);
    }

    private static void SetTiming(Motion motion, CSO_AttackData data)
    {
        if (motion == null) return;

        motion.hitDelay = data != null ? data.hitDelay : 0f;
        motion.duration = data != null ? data.duration : 0f;
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
                float firstFrame = Mathf.Lerp(take.firstFrame, take.lastFrame, motion.start);
                float lastFrame = Mathf.Lerp(take.firstFrame, take.lastFrame, motion.end);
                FitToHitTiming(motion, take, ref firstFrame, ref lastFrame);

                clips.Add(new ModelImporterClipAnimation
                {
                    name = motion.clipName,
                    takeName = take.takeName,
                    firstFrame = firstFrame,
                    lastFrame = lastFrame,
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

    // 当たる瞬間(impact)が、判定が出る時間(hitDelay)ちょうどに来るよう、使う範囲と再生の速さを決める
    // 速さは「判定までに当たる瞬間まで再生できる」と「残りの時間で終わりまで再生できる」のうち遅い方にし、はみ出す側を切り詰める
    private static void FitToHitTiming(Motion motion, ModelImporterClipAnimation take, ref float firstFrame, ref float lastFrame)
    {
        motion.playFramesPerSecond = 0f;
        motion.impactFrames = 0f;
        if (motion.impact < 0f) return;

        float impactFrame = Mathf.Lerp(take.firstFrame, take.lastFrame, motion.impact);
        if (motion.hitDelay > 0f && motion.duration > motion.hitDelay)
        {
            float framesPerSecond = Mathf.Min(
                (impactFrame - firstFrame) / motion.hitDelay,
                (lastFrame - impactFrame) / (motion.duration - motion.hitDelay));

            if (framesPerSecond > 0f)
            {
                firstFrame = impactFrame - motion.hitDelay * framesPerSecond;
                lastFrame = impactFrame + (motion.duration - motion.hitDelay) * framesPerSecond;
                motion.playFramesPerSecond = framesPerSecond;
            }
        }

        motion.impactFrames = Mathf.Max(0f, impactFrame - firstFrame);
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

        // 足・頭のIK(CS_PlayerIK)を使うため、レイヤーのIK Passをオンにする(layersは複製が返るので、書き換えて戻す)
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = AddLocomotion(controller, machine, out AnimatorState sprint);
        machine.defaultState = locomotion;

        AddJump(machine, locomotion, sprint);
        if (AddAction(machine, locomotion, _dash, CS_PlayerAnimatorParams.dash, player != null ? player.dashDuration : 0f, null) == null)
        {
            AddDashCancel(machine, locomotion);
        }

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
        AddAmbush(machine, locomotion);
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

        AnimatorControllerParameter ambushSpeed = new AnimatorControllerParameter
        {
            name = CS_PlayerAnimatorParams.ambushSpeed,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        };
        controller.AddParameter(ambushSpeed);
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
    // 踏み切り(最高点まで)と空中(最高点から着地の直前まで)は、上昇にかかる時間(ジャンプ力 ÷ 重力)で再生する
    private static void AddJump(AnimatorStateMachine machine, AnimatorState locomotion, AnimatorState sprint)
    {
        AnimationClip airClip = LoadClip(_jumpAir);
        if (airClip == null) return;

        float riseTime = GetJumpRiseTime();
        AnimatorState air = AddClipState(machine, "Air", airClip);
        if (riseTime > 0f) air.speed = airClip.length / riseTime;

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
            if (riseTime > 0f) start.speed = startClip.length / riseTime;
            AddEnterTransition(machine, start, CS_PlayerAnimatorParams.jump, null);
            SetupTransition(start.AddTransition(air), true, 0.9f);
        }

        AnimationClip landClip = LoadClip(_jumpLand);
        AnimatorState landTarget = locomotion;
        if (landClip != null)
        {
            AnimatorState land = AddClipState(machine, "JumpLand", landClip);
            SetupTransition(land.AddTransition(locomotion), true, 0.8f);

            // 移動しながら着地した時は、着地の動きを待たずに移動へ戻る
            AnimatorStateTransition keepMoving = land.AddTransition(locomotion);
            SetupTransition(keepMoving, false, 0f);
            keepMoving.AddCondition(AnimatorConditionMode.Greater, _landInterruptSpeed, CS_PlayerAnimatorParams.speed);
            landTarget = land;
        }

        AnimatorStateTransition landing = air.AddTransition(landTarget);
        SetupTransition(landing, false, 0f);
        landing.AddCondition(AnimatorConditionMode.If, 0f, CS_PlayerAnimatorParams.grounded);
    }

    // ジャンプして最高点に着くまでの時間(秒)。ジャンプ力(初速)÷ 重力。分からなければ0
    private static float GetJumpRiseTime()
    {
        CSO_PlayerStats stats = AssetDatabase.LoadAssetAtPath<CSO_PlayerStats>(_statsPath);
        float gravity = Mathf.Abs(Physics.gravity.y);
        if (stats == null || stats.jumpPower <= 0f || gravity <= 0f) return 0f;

        return stats.jumpPower / gravity;
    }

    // トリガーで入り、再生し終わったら移動へ戻る動作。アニメーションが無ければ状態を作らない
    private static AnimatorState AddAction(
        AnimatorStateMachine machine, AnimatorState locomotion, Motion motion, string trigger, float duration, int? comboStep)
    {
        AnimationClip clip = LoadClip(motion);
        if (clip == null) return null;

        AnimatorState state = AddClipState(machine, motion.clipName, clip);
        if (motion.playFramesPerSecond > 0f && clip.frameRate > 0f)
        {
            state.speed = motion.playFramesPerSecond / clip.frameRate;   // 当たる瞬間を判定に合わせた速さ(FitToHitTiming)
        }
        else if (clip.length > 0f && duration > 0f)
        {
            state.speed = clip.length / duration;
        }

        AddEnterTransition(machine, state, trigger, comboStep);
        SetupTransition(state.AddTransition(locomotion), true, 0.95f);
        return state;
    }

    // 奇襲。当たるまでの時間が飛び込む距離で変わるため、速さはゲーム中にAmbushSpeedで決める(CS_PlayerVisual)
    private static void AddAmbush(AnimatorStateMachine machine, AnimatorState locomotion)
    {
        _ambushImpactTime = 0f;

        AnimatorState state = AddAction(machine, locomotion, _ambush, CS_PlayerAnimatorParams.ambush, 0f, null);
        if (state == null) return;

        AnimationClip clip = (AnimationClip)state.motion;
        state.speed = 1f;
        state.speedParameter = CS_PlayerAnimatorParams.ambushSpeed;
        state.speedParameterActive = true;
        _ambushImpactTime = clip.frameRate > 0f ? _ambush.impactFrames / clip.frameRate : 0f;
    }

    // ダッシュのモーションが無い時、ダッシュした瞬間に移動のアニメーションへ戻す
    // (ゲーム側では攻撃の途中でもダッシュで中断できるので、攻撃のモーションが続いて滑って見えないように)
    private static void AddDashCancel(AnimatorStateMachine machine, AnimatorState locomotion)
    {
        AnimatorStateTransition cancel = machine.AddAnyStateTransition(locomotion);
        SetupTransition(cancel, false, 0f);
        cancel.AddCondition(AnimatorConditionMode.If, 0f, CS_PlayerAnimatorParams.dash);
        cancel.AddCondition(AnimatorConditionMode.IfNot, 0f, CS_PlayerAnimatorParams.dead);
        cancel.canTransitionToSelf = false;
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
                if (_ambushImpactTime > 0f) serialized.FindProperty("_ambushImpactTime").floatValue = _ambushImpactTime;

                int[] motionForStep = GetAttackMotionForStep(root.GetComponent<CS_PlayerAttack>());
                SerializedProperty motionForStepProperty = serialized.FindProperty("_attackMotionForStep");
                motionForStepProperty.arraySize = motionForStep.Length;
                for (int i = 0; i < motionForStep.Length; i++)
                {
                    motionForStepProperty.GetArrayElementAtIndex(i).intValue = motionForStep[i];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 通常攻撃の各段に使うモーションの番号。モーションの数までの段はそのまま、それより後の段は、
    // Hit Delay・Durationが一番近い段のモーションを使う(その段のタイミングに合わせて作ったモーションなので、当たる瞬間も合う)
    private static int[] GetAttackMotionForStep(CS_PlayerAttack attack)
    {
        IReadOnlyList<CSO_AttackData> steps = attack != null ? attack.attackSteps : null;
        if (steps == null) return new int[0];

        int motionCount = Mathf.Min(_attacks.Length, steps.Count);
        int[] result = new int[steps.Count];
        for (int i = 0; i < steps.Count; i++)
        {
            if (i < motionCount || steps[i] == null)
            {
                result[i] = motionCount > 0 ? i % motionCount : 0;
                continue;
            }

            float best = float.MaxValue;
            for (int j = 0; j < motionCount; j++)
            {
                if (steps[j] == null) continue;

                float difference = Mathf.Abs(steps[i].hitDelay - steps[j].hitDelay) + Mathf.Abs(steps[i].duration - steps[j].duration);
                if (difference >= best) continue;

                best = difference;
                result[i] = j;
            }
        }
        return result;
    }
}
