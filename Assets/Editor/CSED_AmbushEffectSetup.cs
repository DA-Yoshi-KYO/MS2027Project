using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

/*
 * 奇襲の演出に使うアセット(Prefab・Material・Volume Profile)を作り、DB・Player・UICanvasに組み込む一時的なエディタスクリプト
 * YAMLを手で書かずに済ませるためのもの。組み込みが終わったら消してよい(メニュー Tools/AmbushSetup)
 *
 * 制作者：　吉田京志郎
 */
internal static class CSED_AmbushEffectSetup
{
    private const string _starPath = "Assets/Programmer/PlaceholderAsset/UI/AmbushMark/AmbushStar.png";
    private const string _fxFolder = "Assets/Programmer/Effect/Placeholder";
    private const string _volumeFolder = "Assets/Programmer/Volume/Postprocess";
    private const string _novaFolder = "Assets/Programmer/min/YosimotoRyo/NovaHDRPEffects";

    private const string _starMaterialPath = _fxFolder + "/MT_AmbushStar.mat";
    private const string _afterimageMaterialPath = _fxFolder + "/MT_AmbushAfterimage.mat";
    private const string _trailPath = _fxFolder + "/FX_PlayerAmbushTrail.prefab";
    private const string _afterimagePath = _fxFolder + "/FX_PlayerAmbushAfterimage.prefab";
    private const string _burstPath = _fxFolder + "/FX_PlayerAmbushBurst.prefab";

    private const string _windupProfilePath = _volumeFolder + "/VO_AmbushWindupScreenEffect.asset";
    private const string _lungeProfilePath = _volumeFolder + "/VO_AmbushLungeScreenEffect.asset";
    private const string _hitProfilePath = _volumeFolder + "/VO_AmbushHitScreenEffect.asset";

    private const string _dbPath = "Assets/Programmer/Database/Effect/DB_EffectDataBase.asset";
    private const string _playerPath = "Assets/Programmer/Prefab/Entity/Player/Player.prefab";
    private const string _uiCanvasPath = "Assets/Programmer/Prefab/UI/InGame/UICanvas.prefab";

    // ========================================
    // 1. 星の画像をSpriteにする・マテリアルを作る
    // ========================================

    [MenuItem("Tools/AmbushSetup/1 Texture and Materials")]
    private static void SetupTextureAndMaterials()
    {
        AssetDatabase.ImportAsset(_starPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(_starPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        Texture2D star = AssetDatabase.LoadAssetAtPath<Texture2D>(_starPath);

        CopyMaterial(_novaFolder + "/Materials/Spark_Additive.mat", _starMaterialPath);
        Material starMaterial = AssetDatabase.LoadAssetAtPath<Material>(_starMaterialPath);
        starMaterial.SetTexture("_BaseMap", star);
        EditorUtility.SetDirty(starMaterial);

        CopyMaterial(_novaFolder + "/Materials/Glow_Additive.mat", _afterimageMaterialPath);
        Material afterimageMaterial = AssetDatabase.LoadAssetAtPath<Material>(_afterimageMaterialPath);
        afterimageMaterial.SetTexture("_BaseMap", null);   // 白一色の影にする
        EditorUtility.SetDirty(afterimageMaterial);

        AssetDatabase.SaveAssets();
        Debug.Log("[AmbushSetup] 1 完了: 星のSprite化、MT_AmbushStar、MT_AmbushAfterimage");
    }

    private static void CopyMaterial(string from, string to)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(to) != null) return;
        if (!AssetDatabase.CopyAsset(from, to)) Debug.LogError($"[AmbushSetup] コピーに失敗: {from}");
    }

    // ========================================
    // 2. Prefab
    // ========================================

    [MenuItem("Tools/AmbushSetup/2 Prefabs")]
    private static void SetupPrefabs()
    {
        MakeTrailPrefab();
        MakeAfterimagePrefab();
        MakeBurstPrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("[AmbushSetup] 2 完了: FX_PlayerAmbushTrail / Afterimage / Burst");
    }

    private static void MakeTrailPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(_trailPath) == null)
        {
            AssetDatabase.CopyAsset(_fxFolder + "/FX_PlayerPunchTrailR.prefab", _trailPath);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(_trailPath);
        CS_EffectBoneTrail trail = root.GetComponent<CS_EffectBoneTrail>();
        SerializedObject so = new SerializedObject(trail);
        so.FindProperty("_boneName").stringValue = "mixamorig:Hips";
        so.FindProperty("_fallbackOffset").vector3Value = new Vector3(0f, 1f, 0f);
        so.FindProperty("_color").colorValue = new Color(1f, 0.15f, 0.25f, 1f);
        so.FindProperty("_width").floatValue = 0.7f;
        so.FindProperty("_trailTime").floatValue = 0.3f;
        so.FindProperty("_emitTime").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, _trailPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void MakeAfterimagePrefab()
    {
        GameObject root = new GameObject("FX_PlayerAmbushAfterimage");
        CS_EffectAfterimage afterimage = root.AddComponent<CS_EffectAfterimage>();

        SerializedObject so = new SerializedObject(afterimage);
        so.FindProperty("_material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(_afterimageMaterialPath);
        so.FindProperty("_color").colorValue = new Color(1.6f, 0.15f, 0.25f, 0.8f);
        so.FindProperty("_interval").floatValue = 0.04f;
        so.FindProperty("_fadeTime").floatValue = 0.3f;
        so.FindProperty("_emitTime").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, _afterimagePath);
        Object.DestroyImmediate(root);
    }

    private static void MakeBurstPrefab()
    {
        GameObject root = new GameObject("FX_PlayerAmbushBurst");

        // 衝撃波(Novaのサンプルを入れ子にして使う)
        GameObject shockwave = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(_novaFolder + "/Prefabs/03_Shockwave.prefab"));
        shockwave.name = "Shockwave";
        shockwave.transform.SetParent(root.transform, false);
        shockwave.transform.localScale = Vector3.one * 2f;

        // 火花
        GameObject sparks = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(_novaFolder + "/Prefabs/04_Sparks.prefab"));
        sparks.name = "Sparks";
        sparks.transform.SetParent(root.transform, false);

        // 星の破片
        GameObject shards = new GameObject("StarShards");
        shards.transform.SetParent(root.transform, false);
        MakeStarShards(shards.AddComponent<ParticleSystem>());

        PrefabUtility.SaveAsPrefabAsset(root, _burstPath);
        Object.DestroyImmediate(root);
    }

    // 星の形の破片が四方へ飛び散って、小さく薄くなって消える
    private static void MakeStarShards(ParticleSystem particle)
    {
        ParticleSystem.MainModule main = particle.main;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(2f, 1.6f, 0.5f, 1f);
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        ParticleSystem.RotationOverLifetimeModule rotation = particle.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

        ParticleSystem.SizeOverLifetimeModule size = particle.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(_starMaterialPath);
    }

    // ========================================
    // 3. Volume Profile
    // ========================================

    [MenuItem("Tools/AmbushSetup/3 Volume Profiles")]
    private static void SetupVolumeProfiles()
    {
        // 溜め: ビネットを強く、彩度を少し落とす(1回だけ)
        VolumeProfile windup = NewProfile(_windupProfilePath);
        Vignette windupVignette = AddComponent<Vignette>(windup, false);
        windupVignette.color.Override(Color.black);
        windupVignette.intensity.Override(0.55f);
        windupVignette.smoothness.Override(0.45f);
        ColorAdjustments windupColor = AddComponent<ColorAdjustments>(windup, false);
        windupColor.saturation.Override(-35f);
        CSV_ScreenEffectTiming windupTiming = AddComponent<CSV_ScreenEffectTiming>(windup, true);
        windupTiming.priority.value = 100f;
        windupTiming.playDuration.value = 0.25f;
        windupTiming.playWeightCurve.value = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.5f, 0.8f), new Keyframe(1f, 0f));
        SaveProfile(windup);

        // 飛び込み: 濃い赤系の集中線・ビネット・周りのぼかし(飛び込んでいる間)
        VolumeProfile lunge = NewProfile(_lungeProfilePath);
        CSV_SpeedLines lines = AddComponent<CSV_SpeedLines>(lunge, true);
        lines.intensity.value = 1f;
        lines.color.value = new Color(1f, 0.1f, 0.15f, 0.6f);
        lines.lineCount.value = 140;
        lines.density.value = 0.55f;
        lines.lineWidth.value = 0.2f;
        lines.streakLength.value = 0.35f;
        lines.innerRadius.value = 0.5f;
        lines.softness.value = 0.24f;
        lines.flickerRate.value = 12f;
        lines.flowSpeed.value = 4f;
        Vignette lungeVignette = AddComponent<Vignette>(lunge, false);
        lungeVignette.color.Override(new Color(0.25f, 0f, 0.03f, 1f));
        lungeVignette.intensity.Override(0.4f);
        lungeVignette.smoothness.Override(0.5f);
        CSV_EdgeBlur blur = AddComponent<CSV_EdgeBlur>(lunge, true);
        blur.intensity.value = 1f;
        blur.blurRadius.value = 0.02f;
        blur.innerRadius.value = 0.55f;
        blur.softness.value = 0.25f;
        CSV_ScreenEffectTiming lungeTiming = AddComponent<CSV_ScreenEffectTiming>(lunge, true);
        lungeTiming.priority.value = 101f;
        lungeTiming.fadeInTime.value = 0.04f;
        lungeTiming.fadeOutTime.value = 0.12f;
        SaveProfile(lunge);

        // 命中: モーションブラーを強く、色収差を一瞬(1回だけ)
        VolumeProfile hit = NewProfile(_hitProfilePath);
        MotionBlur motionBlur = AddComponent<MotionBlur>(hit, false);
        motionBlur.intensity.Override(20f);
        ChromaticAberration chromatic = AddComponent<ChromaticAberration>(hit, false);
        chromatic.intensity.Override(0.7f);
        CSV_ScreenEffectTiming hitTiming = AddComponent<CSV_ScreenEffectTiming>(hit, true);
        hitTiming.priority.value = 102f;
        hitTiming.playDuration.value = 0.18f;
        hitTiming.playWeightCurve.value = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        SaveProfile(hit);

        AssetDatabase.SaveAssets();
        Debug.Log("[AmbushSetup] 3 完了: VO_AmbushWindup/Lunge/HitScreenEffect");
    }

    private static VolumeProfile NewProfile(string path)
    {
        VolumeProfile existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);   // 作り直す(このスクリプトが作ったものだけ)

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    // Profileに効果を足して、サブアセットとして保存する
    private static T AddComponent<T>(VolumeProfile profile, bool overrideAll) where T : VolumeComponent
    {
        T component = profile.Add<T>(overrideAll);
        component.name = typeof(T).Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static void SaveProfile(VolumeProfile profile)
    {
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
    }

    // ========================================
    // 4. DBに登録する
    // ========================================

    [MenuItem("Tools/AmbushSetup/4 Register Database")]
    private static void RegisterDatabase()
    {
        Object db = AssetDatabase.LoadAssetAtPath<Object>(_dbPath);
        SerializedObject so = new SerializedObject(db);
        SerializedProperty list = so.FindProperty("_effectList");

        AddEntry(list, CSE_EffectId.PlayerAmbushTrail, _trailPath, 0.6f, 4);
        AddEntry(list, CSE_EffectId.PlayerAmbushAfterimage, _afterimagePath, 0.6f, 4);
        AddEntry(list, CSE_EffectId.PlayerAmbushBurst, _burstPath, 0.3f, 8);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log("[AmbushSetup] 4 完了: DB_EffectDataBaseに3件登録");
    }

    private static void AddEntry(SerializedProperty list, CSE_EffectId id, string prefabPath, float lifetime, int maxConcurrent)
    {
        // 登録済みなら足さない
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("_id").intValue == (int)id) return;
        }

        int index = list.arraySize;
        list.arraySize++;
        SerializedProperty entry = list.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("_id").intValue = (int)id;
        entry.FindPropertyRelative("_prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        entry.FindPropertyRelative("_lifetime").floatValue = lifetime;
        entry.FindPropertyRelative("_scale").floatValue = 1f;
        entry.FindPropertyRelative("_overrideColor").boolValue = false;
        entry.FindPropertyRelative("_color").colorValue = Color.white;
        entry.FindPropertyRelative("_maxConcurrent").intValue = maxConcurrent;
    }

    // ========================================
    // 5. Player.prefabに演出を足す
    // ========================================

    [MenuItem("Tools/AmbushSetup/5 Player Prefab")]
    private static void SetupPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_playerPath);
        CS_PlayerAmbushEffect effect = root.GetComponent<CS_PlayerAmbushEffect>();
        if (effect == null) effect = root.AddComponent<CS_PlayerAmbushEffect>();

        SerializedObject so = new SerializedObject(effect);
        so.FindProperty("_windupScreenEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VolumeProfile>(_windupProfilePath);
        so.FindProperty("_lungeScreenEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VolumeProfile>(_lungeProfilePath);
        so.FindProperty("_hitScreenEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VolumeProfile>(_hitProfilePath);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, _playerPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[AmbushSetup] 5 完了: Player.prefabにCS_PlayerAmbushEffectを追加");
    }

    // ========================================
    // 7. 緩急を強める(溜めを長く、飛び込みを速く。溜めの間はモノクロ)
    // ========================================

    [MenuItem("Tools/AmbushSetup/7 Retune Timing")]
    private static void RetuneTiming()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_playerPath);
        SerializedObject ambush = new SerializedObject(root.GetComponent<CS_PlayerAmbush>());
        ambush.FindProperty("_windupTime").floatValue = 0.5f;
        ambush.FindProperty("_dashSpeed").floatValue = 60f;
        ambush.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject ambushEffect = new SerializedObject(root.GetComponent<CS_PlayerAmbushEffect>());
        ambushEffect.FindProperty("_windupAnimatorSpeed").floatValue = 0.2f;   // 溜めの間はスローモーション
        ambushEffect.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, _playerPath);
        PrefabUtility.UnloadPrefabContents(root);

        VolumeProfile windup = AssetDatabase.LoadAssetAtPath<VolumeProfile>(_windupProfilePath);
        windup.TryGet(out ColorAdjustments color);
        color.saturation.Override(-50f);   // 色を少し残して、見やすく
        windup.TryGet(out CSV_ScreenEffectTiming timing);
        timing.fadeInTime.value = 0.12f;    // 溜めの間だけかけ続ける(SetHeld)。急に切り替わるとチカチカするので、じわっと出入りする
        timing.fadeOutTime.value = 0.15f;
        EditorUtility.SetDirty(windup);
        AssetDatabase.SaveAssets();
        Debug.Log("[AmbushSetup] 7 完了: 溜め0.2秒 / 飛び込み60m/秒 / 溜めの間モノクロ");
    }

    // ========================================
    // 8. Player.prefabにフィニッシュカメラを足す
    // ========================================

    [MenuItem("Tools/AmbushSetup/8 Finish Camera")]
    private static void SetupFinishCamera()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_playerPath);
        CS_PlayerAmbushFinishCamera finishCamera = root.GetComponent<CS_PlayerAmbushFinishCamera>();
        if (finishCamera == null) finishCamera = root.AddComponent<CS_PlayerAmbushFinishCamera>();

        // 遠くから映す(到着時に全身が収まる距離)・建物は透過して、壁では寄せない
        SerializedObject so = new SerializedObject(finishCamera);
        so.FindProperty("_minBehindDistance").floatValue = 4f;
        so.FindProperty("_maxBehindDistance").floatValue = 12f;
        so.FindProperty("_arrivalDistance").floatValue = 1.2f;
        so.FindProperty("_bodyMargin").floatValue = 2.2f;
        so.FindProperty("_occlusionRadius").floatValue = 1f;
        so.FindProperty("_groundClearance").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, _playerPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[AmbushSetup] 8 完了: Player.prefabにCS_PlayerAmbushFinishCameraを追加");
    }

    // ========================================
    // 9. 動作確認用(Play中に、自分のプレイヤーを悪人の背後へ移して奇襲させ、フィニッシュカメラの様子をログに出す)
    // ========================================

    private static double _debugNextLog;
    private static double _debugEnd;
    private static CS_Player _debugPlayer;
    private static CS_PlayerAmbushFinishCamera _debugFinish;
    private static CS_VillainHealth _debugVillain;
    private static bool _debugHit;
    private static readonly System.Text.StringBuilder _debugLog = new System.Text.StringBuilder();

    [MenuItem("Tools/AmbushSetup/9 Debug Ambush (Play mode)")]
    private static void DebugAmbush()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[AmbushDebug] Play中に実行してください"); return; }

        _debugPlayer = null;
        foreach (CS_Player player in Object.FindObjectsByType<CS_Player>(FindObjectsSortMode.None))
        {
            if (player.isLocalPlayer) _debugPlayer = player;
        }
        if (_debugPlayer == null) { Debug.LogWarning("[AmbushDebug] 自分のプレイヤーがいません"); return; }

        _debugVillain = null;
        float best = float.MaxValue;
        foreach (CS_VillainHealth villain in Object.FindObjectsByType<CS_VillainHealth>(FindObjectsSortMode.None))
        {
            if (villain.isDefeated || villain.currentHp <= 0f) continue;
            float distance = (villain.transform.position - _debugPlayer.transform.position).sqrMagnitude;
            if (distance < best) { best = distance; _debugVillain = villain; }
        }
        if (_debugVillain == null) { Debug.LogWarning("[AmbushDebug] 悪人がいません"); return; }

        // 狙う悪人をグループから外して(groupId -1)、「最後の1人」として扱わせる
        CS_VillainCrime crime = _debugVillain.GetComponent<CS_VillainCrime>();
        if (crime != null) crime.SetGroup(-1, 0f);

        // 悪人の背後6mへ移す
        Vector3 forward = _debugVillain.transform.forward;
        forward.y = 0f;
        Vector3 position = _debugVillain.transform.position - forward.normalized * 6f;
        _debugPlayer.transform.position = position;
        Rigidbody body = _debugPlayer.GetComponent<Rigidbody>();
        if (body != null) { body.position = position; body.linearVelocity = Vector3.zero; }

        _debugFinish = _debugPlayer.GetComponent<CS_PlayerAmbushFinishCamera>();
        CS_PlayerAmbush ambush = _debugPlayer.GetComponent<CS_PlayerAmbush>();
        _debugHit = false;
        _debugLog.Clear();
        ambush.onAmbushHitTiming += _ => { _debugHit = true; LogDebug("HIT"); };

        EditorApplication.delayCall += () =>
        {
            bool started = ambush.TryStartAmbush();
            LogDebug($"TryStartAmbush={started}");
            _debugNextLog = EditorApplication.timeSinceStartup;
            _debugEnd = EditorApplication.timeSinceStartup + 3.0;
            EditorApplication.update -= DebugUpdate;
            EditorApplication.update += DebugUpdate;
        };
    }

    private static void DebugUpdate()
    {
        if (!Application.isPlaying || EditorApplication.timeSinceStartup > _debugEnd)
        {
            EditorApplication.update -= DebugUpdate;
            Debug.Log("[AmbushDebug] まとめ\n" + _debugLog);
            return;
        }
        if (EditorApplication.timeSinceStartup < _debugNextLog) return;
        _debugNextLog = EditorApplication.timeSinceStartup + 0.1;
        LogDebug("tick");
    }

    private static void LogDebug(string label)
    {
        if (_debugPlayer == null) return;
        string state = "?";
        System.Reflection.FieldInfo field = typeof(CS_PlayerAmbushFinishCamera).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (_debugFinish != null && field != null) state = field.GetValue(_debugFinish).ToString();

        Camera camera = _debugPlayer.playerCamera;
        string active = "-";
        string blend = "-";
        if (camera != null && camera.TryGetComponent(out Unity.Cinemachine.CinemachineBrain brain))
        {
            active = brain.ActiveVirtualCamera != null ? brain.ActiveVirtualCamera.Name : "null";
            blend = brain.DefaultBlend.Style + (brain.IsBlending ? "(blending)" : "");
        }

        string fit = "";
        if (camera != null)
        {
            Vector3 feet = camera.WorldToViewportPoint(_debugPlayer.transform.position);
            Vector3 head = camera.WorldToViewportPoint(_debugPlayer.transform.position + Vector3.up * 1.8f);
            fit = $" feet.y={feet.y:F2} head.y={head.y:F2} x={feet.x:F2}";
        }

        float distance = _debugVillain != null ? Vector3.Distance(_debugPlayer.transform.position, _debugVillain.transform.position) : -1f;
        _debugLog.AppendLine($"[AmbushDebug] t={Time.time:F2} {label} state={state} activeCam={active} blend={blend} playerToVillain={distance:F1}{fit} camPos={(camera != null ? camera.transform.position.ToString("F1") : "-")}");
    }

    // ========================================
    // 6. UICanvasに星マークを足す
    // ========================================

    [MenuItem("Tools/AmbushSetup/6 UICanvas Mark")]
    private static void SetupUiCanvas()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_uiCanvasPath);

        if (root.GetComponentInChildren<CS_UIAmbushMarkPresenter>(true) != null)
        {
            Debug.Log("[AmbushSetup] 6: すでに星マークがあります");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        // ロックオンのグループ(LockOn)と同じ親・同じ位置の取り方で、すぐ隣に置く
        Transform lockOnGroup = root.GetComponentInChildren<CS_UILockOnPresenter>(true).transform.parent;
        RectTransform groupRect = (RectTransform)lockOnGroup;

        GameObject mark = new GameObject("AmbushMark", typeof(RectTransform));
        mark.layer = lockOnGroup.gameObject.layer;
        RectTransform markRect = (RectTransform)mark.transform;
        markRect.SetParent(lockOnGroup.parent, false);
        markRect.SetSiblingIndex(lockOnGroup.GetSiblingIndex() + 1);
        markRect.anchorMin = groupRect.anchorMin;
        markRect.anchorMax = groupRect.anchorMax;
        markRect.pivot = groupRect.pivot;
        markRect.anchoredPosition = groupRect.anchoredPosition;
        markRect.sizeDelta = groupRect.sizeDelta;

        GameObject marker = new GameObject("AmbushMarker", typeof(RectTransform));
        marker.layer = mark.layer;
        RectTransform markerRect = (RectTransform)marker.transform;
        markerRect.SetParent(markRect, false);
        markerRect.anchorMin = markerRect.anchorMax = markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = Vector2.zero;
        markerRect.sizeDelta = new Vector2(120f, 120f);

        GameObject star = new GameObject("Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        star.layer = mark.layer;
        RectTransform starRect = (RectTransform)star.transform;
        starRect.SetParent(markerRect, false);
        starRect.anchorMin = starRect.anchorMax = starRect.pivot = new Vector2(0.5f, 0.5f);
        starRect.anchoredPosition = Vector2.zero;
        starRect.sizeDelta = new Vector2(120f, 120f);
        Image image = star.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(_starPath);
        image.color = new Color(1f, 0.88f, 0.3f, 1f);
        image.raycastTarget = false;

        CS_UIAmbushMarkPresenter presenter = mark.AddComponent<CS_UIAmbushMarkPresenter>();
        CS_UIAmbushMarkView view = mark.GetComponent<CS_UIAmbushMarkView>();

        SerializedObject viewObject = new SerializedObject(view);
        viewObject.FindProperty("_markerRect").objectReferenceValue = markerRect;
        viewObject.FindProperty("_animationRoot").objectReferenceValue = starRect;
        viewObject.FindProperty("_starImage").objectReferenceValue = image;
        viewObject.ApplyModifiedPropertiesWithoutUndo();

        Canvas canvas = mark.GetComponentInParent<Canvas>();
        SerializedObject presenterObject = new SerializedObject(presenter);
        presenterObject.FindProperty("_canvas").objectReferenceValue = canvas;
        presenterObject.FindProperty("_canvasRect").objectReferenceValue = canvas.transform as RectTransform;
        presenterObject.ApplyModifiedPropertiesWithoutUndo();

        marker.SetActive(false);   // 最初は出さない(Viewが出し入れする)

        PrefabUtility.SaveAsPrefabAsset(root, _uiCanvasPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[AmbushSetup] 6 完了: UICanvasにAmbushMarkを追加");
    }
}
