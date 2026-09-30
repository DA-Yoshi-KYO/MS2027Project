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
 *   4. Villain.prefabへModelを追加し、CS_VillainVisualを設定する
 *      (カプセルの描画と、向き確認用の子のCubeは外す)
 * ・攻撃モーションの再生速度は、CS_VillainCombatの攻撃判定の時間(hitActiveTime)に収まるよう自動で決める
 *   → hitActiveTimeを変えたら、このツールを実行し直す
 * ・本番アセットに差し替えるときは、このツールは不要になる(CS_VillainVisualのメモを参照)
 */
// ========================================

public static class CSED_VillainTempVisualBuilder
{
    private const string _tempRoot = "Assets/Programmer/TempAssets/Player";
    private const string _modelPath = _tempRoot + "/Mannequin Character/characters/Mannequin_Medium.fbx";
    private const string _texturePath = _tempRoot + "/Mannequin Character/Textures/mannequin_texture.png";
    private const string _animFolder = _tempRoot + "/Animations/fbx/Rig_Medium/";
    private static readonly string[] _clipFiles = { "Rig_Medium_General", "Rig_Medium_MovementBasic", "Rig_Medium_MovementAdvanced", "Rig_Medium_CombatMelee" };

    private const string _outputFolder = "Assets/Programmer/TempAssets/Generated/Villain";
    private const string _clipFolder = _outputFolder + "/Clips";
    private const string _controllerPath = _outputFolder + "/VillainTempAnimator.controller";
    private const string _materialPath = _outputFolder + "/MT_VillainMannequin.mat";
    private const string _prefabPath = "Assets/Programmer/Prefab/Entity/Villain/Villain.prefab";
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
    private const string _charge = "Melee_Blocking";                 // 溜め(構えて耐える)
    private const string _attack = "Melee_Unarmed_Attack_Punch_A";   // 攻撃判定中
    private const string _hit = "Hit_A";                             // ノックバック
    private static readonly string[] _loopClips = { _idle, _forward, _backward, _strafeLeft, _strafeRight, _charge };

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
            float hitActiveTime = serializedCombat != null ? serializedCombat.FindProperty("_hitActiveTime").floatValue : 1f;
            Object playerBaseStats = serializedCombat != null ? serializedCombat.FindProperty("_playerBaseStats").objectReferenceValue : null;

            Dictionary<string, AnimationClip> clips = LoadClips();
            AnimatorController controller = CreateController(clips, hitActiveTime);
            Material material = CreateMaterial();

            RemovePlaceholderVisuals(root);
            Animator animator = AttachModel(root, controller, material);
            AttachVisual(root, animator, playerBaseStats);

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
            AssetDatabase.CreateAsset(material, _materialPath);
        }

        material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(_texturePath));
        material.SetColor("_BaseColor", _tintColor);
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

    private static AnimatorController CreateController(Dictionary<string, AnimationClip> clips, float hitActiveTime)
    {
        AssetDatabase.DeleteAsset(_controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(_controllerPath);
        controller.AddParameter(CS_VillainAnimatorParams.speed, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_VillainAnimatorParams.moveX, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_VillainAnimatorParams.moveZ, AnimatorControllerParameterType.Float);
        controller.AddParameter(CS_VillainAnimatorParams.charging, AnimatorControllerParameterType.Bool);
        controller.AddParameter(CS_VillainAnimatorParams.attacking, AnimatorControllerParameterType.Bool);
        controller.AddParameter(CS_VillainAnimatorParams.hit, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = AddLocomotion(controller, machine, clips);
        machine.defaultState = locomotion;

        AddAttack(machine, locomotion, Clip(clips, _charge), Clip(clips, _attack), hitActiveTime);
        AddHit(machine, locomotion, Clip(clips, _hit));

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
    private static void AddAttack(AnimatorStateMachine machine, AnimatorState locomotion, AnimationClip chargeClip, AnimationClip attackClip, float hitActiveTime)
    {
        AnimatorState charge = AddClipState(machine, "Charge", chargeClip);
        AnimatorState attack = AddClipState(machine, "Attack", attackClip);

        // 攻撃判定の時間に収まるよう再生速度を決める
        if (attackClip != null && hitActiveTime > 0f) attack.speed = attackClip.length / hitActiveTime;

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
}
