using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/*
 * 仮アセット(プレイヤーと同じMannequin)を、色を変えて悪人の見た目として組み込むエディタ拡張
 * Tools/Villain/仮の見た目を組み込む から実行する(何度実行しても同じ結果になる)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・やること
 *   1. ループ再生するモーションを、悪人用に複製して保存する(Generated/Villain/Clips)
 *      FBXのループ設定はプレイヤー用のツール(CSED_PlayerTempVisualBuilder)が上書きするため、FBX側は変更しない
 *   2. Animator Controller(VillainTempAnimator.controller)の生成
 *      パラメータ名はCS_VillainAnimatorParamsの定数を使う
 *   3. マテリアル(MT_VillainMannequin.mat)の生成
 *      プレイヤーと同じテクスチャに色(_tintColor)を掛ける。犯罪完遂のフェードのため半透明(Transparent)にする
 *      色を掛けるのは新しく作る時だけ(Inspectorで変えた色は、実行し直しても戻さない)
 *   4. Villain.prefabへModelを追加し、CS_VillainVisualを設定する
 *      (カプセルの描画と、向き確認用の子のCubeは外す)
 * ・溜めと攻撃は同じパンチのモーションを使い、再生位置はCS_VillainVisualがAttackTimeで決める(Motion Time)
 *   → 溜め時間・攻撃判定の時間を変えても、実行し直す必要はない
 * ・Tools/Villain/攻撃モーションを解析
 *   パンチのモーションで手が一番後ろに来る位置(腕を引き切った)と、一番前に来る位置(腕が伸び切った)を求め、
 *   CS_VillainVisualのwindupTime・strikeTimeへ設定する(Villain・VillainBossの両方)
 *   あわせて、今あるAnimator Controllerの溜め・攻撃をMotion Timeで動くように直す(作り直さない)
 * ・Tools/Villain/死亡モーションを追加
 *   今あるAnimator Controllerに、死亡モーション(Death)とDeadパラメータを追加する(作り直さない。何度実行しても同じ結果)
 *   Deadがtrueになると、どの状態からでも死亡モーションへ移り、最後の姿勢のまま止まる(消えるのはCS_VillainHealthが行う)
 *   → Controllerを作り直すとVillainBossからの参照が切れるので、追加はこのメニューで行う
 * ・本番アセットに差し替えるときは、このツールは不要になる(CS_VillainVisualのメモを参照)
 */
// ========================================

public static class CSED_VillainTempVisualBuilder
{
    private const string _tempRoot = "Assets/Programmer/PlaceholderAsset/Player";
    private const string _modelPath = _tempRoot + "/Mannequin Character/characters/Mannequin_Medium.fbx";
    private const string _texturePath = _tempRoot + "/Mannequin Character/Textures/mannequin_texture.png";
    private const string _animFolder = _tempRoot + "/Animations/fbx/Rig_Medium/";
    private static readonly string[] _clipFiles = { "Rig_Medium_General", "Rig_Medium_MovementBasic", "Rig_Medium_MovementAdvanced", "Rig_Medium_CombatMelee" };

    private const string _outputFolder = "Assets/Programmer/PlaceholderAsset/Generated/Villain";
    private const string _clipFolder = _outputFolder + "/Clips";
    private const string _controllerPath = _outputFolder + "/VillainTempAnimator.controller";
    private const string _materialPath = _outputFolder + "/MT_VillainMannequin.mat";
    private const string _prefabPath = "Assets/Programmer/Prefab/Entity/Villain/Villain.prefab";
    private const string _bossPrefabPath = "Assets/Programmer/Prefab/Entity/Villain/VillainBoss.prefab";
    private const string _combatMeleeFile = "Rig_Medium_CombatMelee";
    private const int _analyzeSamples = 120;   // 攻撃モーションを解析する時の分割数
    private const string _modelName = "Model";
    private const string _directionMarkerName = "Cube";   // 向き確認用の仮オブジェクト(モデルで向きが分かるので外す)

    private static readonly Color _tintColor = new Color(0.85f, 0.25f, 0.25f);   // プレイヤーと見分けるための色
    private const float _transitionTime = 0.1f;

    // 使うモーション
    private const string _idle = "Idle_B";
    private const string _forward = "Running_B";
    private const string _backward = "Walking_Backwards";
    private const string _strafeLeft = "Running_Strafe_Left";
    private const string _strafeRight = "Running_Strafe_Right";
    private const string _attack = "Melee_Unarmed_Attack_Punch_A";   // 溜め(腕を引く)・攻撃判定中(腕を伸ばす)
    private const string _hit = "Hit_A";                             // ノックバック
    private const string _death = "Death_A";                         // 撃退されて倒れる
    private const string _generalFile = "Rig_Medium_General";
    private const string _deathStateName = "Death";
    private static readonly string[] _loopClips = { _idle, _forward, _backward, _strafeLeft, _strafeRight };

    [MenuItem("Tools/Villain/仮の見た目を組み込む")]
    public static void Build()
    {
        EnsureFolder(_outputFolder);
        EnsureFolder(_clipFolder);

        GameObject root = PrefabUtility.LoadPrefabContents(_prefabPath);
        try
        {
            CS_VillainCombat combat = root.GetComponent<CS_VillainCombat>();
            SerializedObject serializedCombat = combat != null ? new SerializedObject(combat) : null;
            Object playerBaseStats = serializedCombat != null ? serializedCombat.FindProperty("_playerBaseStats").objectReferenceValue : null;

            Dictionary<string, AnimationClip> clips = LoadClips();
            AnimatorController controller = CreateController(clips);
            Material material = CreateMaterial();

            RemovePlaceholderVisuals(root);
            Animator animator = AttachModel(root, controller, material);
            AttachVisual(root, animator, playerBaseStats);
            if (TryAnalyzeAttack(Clip(clips, _attack), out float windupTime, out float strikeTime))
            {
                SetAttackTimes(root, windupTime, strikeTime);
            }

            PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("CSED_VillainTempVisualBuilder: 悪人の仮の見た目を組み込みました");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    // FBXからモーションを読み込む。ループ再生するものは、ループ設定をした複製を使う
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

        foreach (string name in _loopClips)
        {
            if (!clips.TryGetValue(name, out AnimationClip source)) continue;

            clips[name] = CreateLoopCopy(source);
        }

        return clips;
    }

    private static AnimationClip CreateLoopCopy(AnimationClip source)
    {
        string path = _clipFolder + "/" + source.name + ".anim";
        AssetDatabase.DeleteAsset(path);

        AnimationClip copy = Object.Instantiate(source);
        copy.name = source.name;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(copy, settings);

        AssetDatabase.CreateAsset(copy, path);
        return copy;
    }

    private static AnimationClip Clip(Dictionary<string, AnimationClip> clips, string name)
    {
        if (clips.TryGetValue(name, out AnimationClip clip)) return clip;

        Debug.LogError("CSED_VillainTempVisualBuilder: アニメーションが見つかりません: " + name);
        return null;
    }

    // プレイヤーと同じテクスチャに色を掛けた、半透明にできるマテリアル
    private static Material CreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(_materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("HDRP/Lit"));
            material.SetColor("_BaseColor", _tintColor);   // 色は作る時だけ掛ける(Inspectorで変えた色を戻さない)
            AssetDatabase.CreateAsset(material, _materialPath);
        }

        material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(_texturePath));
        material.SetFloat("_Smoothness", 0.2f);

        // 犯罪完遂のフェード(_BaseColorのアルファを下げる)が見えるよう半透明にする
        // 体の奥の面が透けて見えないよう、先に深度を書き込む(Transparent Depth Prepass)
        HDMaterial.SetSurfaceType(material, true);
        material.SetFloat("_TransparentDepthPrepassEnable", 1f);
        HDMaterial.ValidateMaterial(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---- Animator Controller ----

    private static AnimatorController CreateController(Dictionary<string, AnimationClip> clips)
    {
        AssetDatabase.DeleteAsset(_controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(_controllerPath);
        controller.AddParameter(CS_VillainAnimatorParams.speed, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_VillainAnimatorParams.moveX, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_VillainAnimatorParams.moveZ, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_VillainAnimatorParams.charging, AnimatorControllerParameterType.Bool);
        controller.AddParameter(CS_VillainAnimatorParams.attacking, AnimatorControllerParameterType.Bool);
        controller.AddParameter(CS_VillainAnimatorParams.attackTime, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_VillainAnimatorParams.hit, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(CS_VillainAnimatorParams.dead, AnimatorControllerParameterType.Bool);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = AddLocomotion(controller, machine, clips);
        machine.defaultState = locomotion;

        AddAttack(machine, locomotion, Clip(clips, _attack));
        AddHit(machine, locomotion, Clip(clips, _hit));
        AddDeath(controller, Clip(clips, _death));

        EditorUtility.SetDirty(controller);
        return controller;
    }

    // 待機・前進・後退・左右移動を、体から見た移動方向(MoveX, MoveZ)でブレンドする
    private static AnimatorState AddLocomotion(AnimatorController controller, AnimatorStateMachine machine, Dictionary<string, AnimationClip> clips)
    {
        BlendTree tree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = CS_VillainAnimatorParams.moveX,
            blendParameterY = CS_VillainAnimatorParams.moveZ,
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(tree, controller);

        tree.AddChild(Clip(clips, _idle), new Vector2(0f, 0f));
        tree.AddChild(Clip(clips, _forward), new Vector2(0f, 1f));
        tree.AddChild(Clip(clips, _backward), new Vector2(0f, -1f));
        tree.AddChild(Clip(clips, _strafeLeft), new Vector2(-1f, 0f));
        tree.AddChild(Clip(clips, _strafeRight), new Vector2(1f, 0f));

        AnimatorState state = machine.AddState("Locomotion");
        state.motion = tree;
        return state;
    }

    // 溜め(Charging) → 攻撃(Attacking) → 両方falseでLocomotionへ戻る
    // 溜め・攻撃とも同じモーションを、AttackTimeの位置で再生する(CS_VillainVisualが決める)
    private static void AddAttack(AnimatorStateMachine machine, AnimatorState locomotion, AnimationClip attackClip)
    {
        AnimatorState charge = AddClipState(machine, "Charge", attackClip);
        AnimatorState attack = AddClipState(machine, "Attack", attackClip);
        UseAttackTime(charge);
        UseAttackTime(attack);

        AddAnyStateTransition(machine, charge, AnimatorConditionMode.If, CS_VillainAnimatorParams.charging);
        AddAnyStateTransition(machine, attack, AnimatorConditionMode.If, CS_VillainAnimatorParams.attacking);

        AnimatorStateTransition chargeEnd = charge.AddTransition(locomotion);
        SetupTransition(chargeEnd, false, 0f);
        chargeEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, CS_VillainAnimatorParams.charging);
        chargeEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, CS_VillainAnimatorParams.attacking);

        AnimatorStateTransition attackEnd = attack.AddTransition(locomotion);
        SetupTransition(attackEnd, false, 0f);
        attackEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, CS_VillainAnimatorParams.attacking);
    }

    // 再生位置をAttackTimeで決める(Motion Time)
    private static void UseAttackTime(AnimatorState state)
    {
        state.speed = 1f;
        state.timeParameterActive = true;
        state.timeParameter = CS_VillainAnimatorParams.attackTime;
    }

    // ノックバック(Hit)で再生し、終わったらLocomotionへ戻る
    private static void AddHit(AnimatorStateMachine machine, AnimatorState locomotion, AnimationClip clip)
    {
        AnimatorState hit = AddClipState(machine, "Hit", clip);

        AnimatorStateTransition enter = machine.AddAnyStateTransition(hit);
        SetupTransition(enter, false, 0f);
        enter.AddCondition(AnimatorConditionMode.If, 0f, CS_VillainAnimatorParams.hit);
        enter.canTransitionToSelf = true;   // 連続で食らったら最初から再生し直す

        AnimatorStateTransition exit = hit.AddTransition(locomotion);
        SetupTransition(exit, true, 0.9f);
    }

    // Deadがtrueになったら、どの状態からでも死亡モーションへ移る(戻らない。最後の姿勢のまま止まる)
    // 今あるControllerにも使えるよう、既にあれば作り直さずに設定し直す
    private static void AddDeath(AnimatorController controller, AnimationClip clip)
    {
        if (!HasParameter(controller, CS_VillainAnimatorParams.dead))
        {
            controller.AddParameter(CS_VillainAnimatorParams.dead, AnimatorControllerParameterType.Bool);
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState death = FindState(machine, _deathStateName);
        if (death == null) death = machine.AddState(_deathStateName);
        death.motion = clip;

        // 死亡モーションへの遷移を作り直す
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            if (transition.destinationState == death) machine.RemoveAnyStateTransition(transition);
        }
        AddAnyStateTransition(machine, death, AnimatorConditionMode.If, CS_VillainAnimatorParams.dead);

        // 倒れた後は、くらい・溜め・攻撃などの他の状態へ移らない
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            if (transition.destinationState == death || HasCondition(transition, CS_VillainAnimatorParams.dead)) continue;

            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, CS_VillainAnimatorParams.dead);
        }

        EditorUtility.SetDirty(controller);
    }

    private static bool HasParameter(AnimatorController controller, string parameterName)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == parameterName) return true;
        }
        return false;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name == stateName) return child.state;
        }
        return null;
    }

    private static bool HasCondition(AnimatorStateTransition transition, string parameterName)
    {
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == parameterName) return true;
        }
        return false;
    }

    private static void AddAnyStateTransition(AnimatorStateMachine machine, AnimatorState state, AnimatorConditionMode mode, string parameter)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
        SetupTransition(transition, false, 0f);
        transition.AddCondition(mode, 0f, parameter);
        transition.canTransitionToSelf = false;
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

    // カプセルの描画と、向き確認用のCubeを外す(当たり判定のCapsuleColliderは残す)
    private static void RemovePlaceholderVisuals(GameObject root)
    {
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        MeshFilter filter = root.GetComponent<MeshFilter>();
        if (renderer != null) Object.DestroyImmediate(renderer);
        if (filter != null) Object.DestroyImmediate(filter);

        Transform marker = root.transform.Find(_directionMarkerName);
        if (marker != null) Object.DestroyImmediate(marker.gameObject);
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

    private static void AttachVisual(GameObject root, Animator animator, Object playerBaseStats)
    {
        CS_VillainVisual visual = root.GetComponent<CS_VillainVisual>();
        if (visual == null) visual = root.AddComponent<CS_VillainVisual>();

        SerializedObject serialized = new SerializedObject(visual);
        serialized.FindProperty("_animator").objectReferenceValue = animator;
        serialized.FindProperty("_playerBaseStats").objectReferenceValue = playerBaseStats;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- 攻撃モーションの解析 ----

    [MenuItem("Tools/Villain/死亡モーションを追加")]
    public static void AddDeathMotion()
    {
        AnimationClip clip = LoadFbxClip(_generalFile, _death);
        if (clip == null)
        {
            Debug.LogError("CSED_VillainTempVisualBuilder: アニメーションが見つかりません: " + _death);
            return;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
        if (controller == null)
        {
            Debug.LogWarning("CSED_VillainTempVisualBuilder: Animator Controllerが無いため、先に 仮の見た目を組み込む を実行してください");
            return;
        }

        AddDeath(controller, clip);
        AssetDatabase.SaveAssets();
        Debug.Log("CSED_VillainTempVisualBuilder: 死亡モーションを追加しました");
    }

    [MenuItem("Tools/Villain/攻撃モーションを解析")]
    public static void AnalyzeAttackMotion()
    {
        AnimationClip clip = LoadFbxClip(_combatMeleeFile, _attack);
        if (clip == null)
        {
            Debug.LogError("CSED_VillainTempVisualBuilder: アニメーションが見つかりません: " + _attack);
            return;
        }

        UpdateExistingController(clip);

        if (!TryAnalyzeAttack(clip, out float windupTime, out float strikeTime)) return;

        foreach (string path in new[] { _prefabPath, _bossPrefabPath })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (SetAttackTimes(root, windupTime, strikeTime)) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"CSED_VillainTempVisualBuilder: 攻撃モーションを解析しました(腕を引き切る: {windupTime:0.000} / 腕が伸び切る: {strikeTime:0.000})");
    }

    private static AnimationClip LoadFbxClip(string file, string clipName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(_animFolder + file + ".fbx"))
        {
            if (asset is AnimationClip clip && clip.name == clipName) return clip;
        }
        return null;
    }

    // 今あるControllerの溜め・攻撃を、攻撃モーションをAttackTimeの位置で再生するように直す(作り直さない)
    private static void UpdateExistingController(AnimationClip attackClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
        if (controller == null)
        {
            Debug.LogWarning("CSED_VillainTempVisualBuilder: Animator Controllerが無いため、先に 仮の見た目を組み込む を実行してください");
            return;
        }

        bool hasAttackTime = false;
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == CS_VillainAnimatorParams.attackTime) hasAttackTime = true;
        }
        if (!hasAttackTime) controller.AddParameter(CS_VillainAnimatorParams.attackTime, AnimatorControllerParameterType.Float);

        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
        {
            AnimatorState state = child.state;
            if (state.name != "Charge" && state.name != "Attack") continue;

            state.motion = attackClip;
            UseAttackTime(state);
            EditorUtility.SetDirty(state);
        }

        EditorUtility.SetDirty(controller);
    }

    // 攻撃モーションの中で、手が一番前に来る位置(腕が伸び切った)と、その前で一番後ろに来る位置(腕を引き切った)を求める
    // 左右の手のうち、前後に大きく動く方をパンチする手とみなす。位置は0〜1(モーション全体に対する割合)
    private static bool TryAnalyzeAttack(AnimationClip clip, out float windupTime, out float strikeTime)
    {
        windupTime = 0f;
        strikeTime = 0f;
        if (clip == null) return false;

        GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(_modelPath));
        model.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            List<Transform> hands = FindHands(model.transform);
            if (hands.Count == 0)
            {
                Debug.LogError("CSED_VillainTempVisualBuilder: モデルに手のボーンが見つかりません");
                return false;
            }

            float[,] forward = new float[hands.Count, _analyzeSamples + 1];
            for (int i = 0; i <= _analyzeSamples; i++)
            {
                clip.SampleAnimation(model, clip.length * i / _analyzeSamples);
                for (int h = 0; h < hands.Count; h++)
                {
                    forward[h, i] = model.transform.InverseTransformPoint(hands[h].position).z;
                }
            }

            int bestHand = 0;
            float bestRange = -1f;
            for (int h = 0; h < hands.Count; h++)
            {
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i <= _analyzeSamples; i++)
                {
                    min = Mathf.Min(min, forward[h, i]);
                    max = Mathf.Max(max, forward[h, i]);
                }
                if (max - min <= bestRange) continue;

                bestRange = max - min;
                bestHand = h;
            }

            int strike = 0;
            for (int i = 1; i <= _analyzeSamples; i++)
            {
                if (forward[bestHand, i] > forward[bestHand, strike]) strike = i;
            }
            int windup = 0;
            for (int i = 1; i < strike; i++)
            {
                if (forward[bestHand, i] < forward[bestHand, windup]) windup = i;
            }

            windupTime = (float)windup / _analyzeSamples;
            strikeTime = (float)strike / _analyzeSamples;
            Debug.Log($"CSED_VillainTempVisualBuilder: パンチする手 = {hands[bestHand].name}");
            return true;
        }
        finally
        {
            Object.DestroyImmediate(model);
        }
    }

    // 手首のボーン(名前にhandを含み、親はhandを含まない。指は除く)
    private static List<Transform> FindHands(Transform root)
    {
        List<Transform> hands = new List<Transform>();
        foreach (Transform bone in root.GetComponentsInChildren<Transform>())
        {
            if (!bone.name.ToLower().Contains("hand")) continue;
            if (bone.parent != null && bone.parent.name.ToLower().Contains("hand")) continue;

            hands.Add(bone);
        }
        return hands;
    }

    // CS_VillainVisualへ腕を引き切る位置・伸び切る位置を設定する。付いていなければfalse
    private static bool SetAttackTimes(GameObject root, float windupTime, float strikeTime)
    {
        CS_VillainVisual visual = root.GetComponent<CS_VillainVisual>();
        if (visual == null) return false;

        SerializedObject serialized = new SerializedObject(visual);
        serialized.FindProperty("_windupTime").floatValue = windupTime;
        serialized.FindProperty("_strikeTime").floatValue = strikeTime;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
