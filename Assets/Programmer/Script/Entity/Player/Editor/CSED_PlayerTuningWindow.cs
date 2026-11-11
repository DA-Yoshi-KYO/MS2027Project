using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/*
 * プレイヤーのレベルデザイン用ウィンドウ(プランナー向け)
 * メニューの「Tools → プレイヤー調整」で開く
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・タブは3つ
 *   キャラクター: プレイアブルキャラクター(CSO_PlayableCharacter)ごとの基礎数値・必殺技・必殺技の効果。テストで使うキャラクターもここで選ぶ
 *   共通の数値  : Player.prefabに付いているコンポーネントの数値(必殺ゲージ・スキャン・ブリンク・奇襲・SAなど)。全キャラクター共通
 *   通常攻撃    : 5段コンボの各段の攻撃(CSO_AttackData)
 * ・値の変更はUndo(Ctrl+Z)で戻せる。保存はCtrl+S、またはウィンドウ上部の「保存」ボタン
 * ・表示する項目は、下のFieldDefの一覧(項目名・表示名・説明)で決める。スクリプトの項目名が変わって見つからない時は、警告を出す
 *   項目を増やす時は、一覧に1行足すだけでよい
 * ・キャラクターのデータが無い時は「4キャラの初期データを作る」ボタンで、仕様書の4キャラ(ギロ・アブ・キース・イシュ)を作り、Player.prefabに付ける
 * ・キャラクターの基礎数値・必殺技の攻撃は、未設定なら共通のデータ(Player.prefabのDB_PlayerStats・DB_PlayerSpecial)を表示・編集する
 *   (別のレベルデザインツール「Tools → レベルデザイン」も共通のデータを編集するので、両方の結果がそろう)
 *   キャラクターごとに変える時は「このキャラ専用にする」で共通のデータをコピーする
 */
// ========================================

public class CSED_PlayerTuningWindow : EditorWindow
{
    private const string _playerPrefabPath = "Assets/Programmer/Prefab/Entity/Player/Player.prefab";
    private const string _characterFolder = "Assets/Programmer/Database/Player/Character";

    // 表示する項目(スクリプトの項目名・表示名・説明)
    private readonly struct FieldDef
    {
        public readonly string path;
        public readonly GUIContent label;

        public FieldDef(string path, string label, string tooltip)
        {
            this.path = path;
            this.label = new GUIContent(label, tooltip);
        }
    }

    // 共通の数値の1区切り(見出し・コンポーネント・項目)
    private sealed class Section
    {
        public readonly string title;
        public readonly Type componentType;
        public readonly FieldDef[] fields;
        public readonly Func<GameObject, string> info;   // 項目の下に出す計算結果(無ければnull)

        public Section(string title, Type componentType, FieldDef[] fields, Func<GameObject, string> info = null)
        {
            this.title = title;
            this.componentType = componentType;
            this.fields = fields;
            this.info = info;
        }
    }

    private static readonly FieldDef[] _statsFields =
    {
        new FieldDef("_maxHp", "最大HP", "HPの最大値"),
        new FieldDef("_attackPower", "攻撃力(倍率)", "通常攻撃のダメージに掛ける倍率。1で等倍"),
        new FieldDef("_moveSpeed", "移動速度", "歩く速さ(m/秒)"),
        new FieldDef("_jumpPower", "ジャンプ力", "ジャンプの強さ"),
        new FieldDef("_dashSpeed", "ブリンクの速さ", "ブリンク(ダッシュボタン)中の速さ(m/秒)。距離 = 速さ × ブリンクの時間(共通の数値)"),
        new FieldDef("_sprintSpeed", "スプリントの速さ", "ブリンクの後、ダッシュボタンを押し続けている間の速さ(m/秒)"),
        new FieldDef("_maxGauge", "必殺ゲージの最大", "必殺技を使うのに必要なゲージの量"),
        new FieldDef("_specialAttackPower", "必殺技の威力(倍率)", "必殺技のダメージに掛ける倍率。1で等倍"),
    };

    private static readonly FieldDef[] _attackFields =
    {
        new FieldDef("_damage", "ダメージ", "1回当たった時のダメージ(攻撃力の倍率が掛かる)"),
        new FieldDef("_hitDelay", "当たるまでの時間(秒)", "攻撃を始めてから、当たり判定が出るまでの時間"),
        new FieldDef("_duration", "モーションの長さ(秒)", "攻撃を始めてから、次の行動ができるまでの時間"),
        new FieldDef("_comboWindow", "次の段の受付(秒)", "この時間内に攻撃ボタンを押すと、次の段につながる(通常攻撃のみ)"),
        new FieldDef("_hitRange", "判定の前の距離(m)", "体の正面のどれだけ先に当たり判定を出すか"),
        new FieldDef("_hitRadius", "判定の半径(m)", "当たり判定(球)の大きさ"),
        new FieldDef("_gaugeGain", "当たった時に溜まるゲージ", "1回当たるごとに溜まる必殺ゲージの量(必殺技は0推奨)"),
        new FieldDef("_cameraShakeForce", "カメラの揺れ", "当たり判定が出る瞬間のカメラの揺れの強さ(0で揺れない)"),
    };

    private static readonly Section[] _commonSections =
    {
        new Section("必殺ゲージ", typeof(CS_PlayerSpecialGauge), new[]
        {
            new FieldDef("_gaugePerSecond", "毎秒溜まる量", "時間経過で1秒ごとに溜まる量"),
            new FieldDef("_villainDefeatGaugeGain", "悪人を倒した時に溜まる量", "自分が悪人を1体倒すたびに溜まる量"),
        }, GaugeInfo),
        new Section("スキャン", typeof(CS_PlayerScan), new[]
        {
            new FieldDef("_baseRadius", "範囲(m)", "スキャンが届く横方向の距離"),
            new FieldDef("_height", "範囲の高さ(m)", "使った位置から上下それぞれ、この高さまで届く"),
            new FieldDef("_expandTime", "波が広がる時間(秒)", "波が範囲いっぱいまで広がる時間"),
            new FieldDef("_fadeTime", "波が消える時間(秒)", "広がりきった後、波が消えるまでの時間。この間は使えない"),
            new FieldDef("_revealDuration", "見え続ける時間(秒)", "波が届いた相手が見え続ける時間"),
            new FieldDef("_autoScanEnabled", "自動スキャン", "オンにすると、決まった間隔で自動でスキャンする(手動でも使える)"),
            new FieldDef("_autoScanInterval", "自動スキャンの間隔(秒)", "自動でスキャンする間隔"),
            new FieldDef("_autoScanOnStart", "開始時にスキャン", "操作できるようになった最初に1回スキャンする"),
            new FieldDef("_resetAutoScanOnManual", "手動で使ったら数え直す", "手動で使った時も、次の自動スキャンまでの時間を数え直す"),
        }),
        new Section("ブリンク(ダッシュボタン)", typeof(CS_Player), new[]
        {
            new FieldDef("_dashDuration", "ブリンクの時間(秒)", "ブリンクが続く時間。速さはキャラクターの基礎数値"),
            new FieldDef("_dashCooldown", "クールタイム(秒)", "次のブリンクができるまでの時間"),
        }, BlinkInfo),
        new Section("ブリンクの反撃(パリー)", typeof(CS_PlayerParry), new[]
        {
            new FieldDef("_parryWindow", "受付時間(秒)", "攻撃が当たる何秒前までにブリンクを押していれば成功か"),
            new FieldDef("_searchRadius", "反撃する相手を探す範囲(m)", ""),
            new FieldDef("_invincibleTime", "成功時の無敵(秒)", ""),
            new FieldDef("_counterDamage", "反撃のダメージ", "攻撃力の倍率が掛かる"),
            new FieldDef("_policeStunTime", "警察のスタン(秒)", ""),
            new FieldDef("_playerStunTime", "他プレイヤーのスタン(秒)", ""),
        }),
        new Section("奇襲", typeof(CS_PlayerAmbush), new[]
        {
            new FieldDef("_range", "奇襲できる距離(m)", "悪人との横方向の距離がこれ以内なら奇襲できる(飛び込む距離)"),
            new FieldDef("_backAngle", "背後とみなす角度(度)", "悪人の正面から見て、この角度以上うしろなら背後"),
            new FieldDef("_aboveHeight", "上とみなす高さ(m)", "悪人よりこれ以上高ければ上を取っている"),
            new FieldDef("_dashSpeed", "飛び込む速さ(m/秒)", ""),
            new FieldDef("_strikeDistance", "着いたとみなす距離(m)", ""),
            new FieldDef("_verticalTolerance", "高さを合わせる誤差(m)", "悪人との足元の高さの差がこれ以内なら、縦に飛び込まない"),
            new FieldDef("_splashRadius", "巻き込む範囲(m)", "奇襲で倒した悪人から、この範囲の悪人も一撃で倒す(0で巻き込まない)"),
            new FieldDef("_splashIncludesRaidBoss", "レイドボスも巻き込む", ""),
        }),
        new Section("SA(ハイドポイントへ飛ぶ)", typeof(CS_PlayerHide), new[]
        {
            new FieldDef("_flySpeed", "飛ぶ速さ(m/秒)", ""),
            new FieldDef("_maxFlyTime", "飛ぶ最大時間(秒)", "これ以上かかっても着かなければやめる"),
            new FieldDef("_requiredVisibleRatio", "見えている割合", "隠れられる箱がこの割合以上カメラから見えていれば飛べる"),
            new FieldDef("_revealDistance", "隠れていても見つかる距離(m)", "警察・悪人とこの距離以内なら、隠れていても見つかる"),
        }),
        new Section("被弾・復活", typeof(CS_PlayerHealth), new[]
        {
            new FieldDef("_postDamageInvincibleDuration", "被弾後の無敵(秒)", "0以下で無敵にならない"),
        }),
        new Section("復活", typeof(CS_PlayerRespawn), new[]
        {
            new FieldDef("_respawnDelay", "復活までの時間(秒)", "HPが0になってから復活するまでの時間"),
            new FieldDef("_invincibleDuration", "復活後の無敵(秒)", "この間は警察にも見つからない"),
        }),
        new Section("スタンのコマンド入力", typeof(CS_PlayerStunEscape), new[]
        {
            new FieldDef("_shortenPerCycle", "1周で短くなる秒数", "左右(スティック1周)を1周入力するごとに、スタンが短くなる秒数"),
        }),
        new Section("暗躍ボーナス", typeof(CS_PlayerStealthBonus), new[]
        {
            new FieldDef("_bonusStartCount", "ボーナスが付く人数", "続けてこの人数目を倒した時からボーナスが付く"),
            new FieldDef("_bonusMaxCount", "ボーナスが最大になる人数", ""),
            new FieldDef("_scoreStep", "1段階ごとのスコア倍率", ""),
            new FieldDef("_statusStep", "1段階ごとのステータス倍率", "移動速度・ジャンプ力・攻撃力・コンボの速さ"),
            new FieldDef("_scanRadiusStep", "1段階ごとのスキャン範囲(m)", ""),
        }),
        new Section("攻撃の補正(エイムアシスト)", typeof(CS_PlayerAimAssist), new[]
        {
            new FieldDef("_isAssistEnabled", "補正を使う", ""),
            new FieldDef("_range", "補正する距離(m)", ""),
            new FieldDef("_angle", "補正する角度(度)", "狙っている方向から左右何度以内の相手を補正するか"),
            new FieldDef("_maxLungeDistance", "踏み込む最大距離(m)", "0で踏み込まない"),
        }),
    };

    private static readonly string[] _tabNames = { "キャラクター", "共通の数値", "通常攻撃" };

    private int _tab;
    private Vector2 _scroll;
    private Vector2 _listScroll;
    private int _selectedCharacter;
    private CSO_PlayableCharacterRoster _roster;
    private GameObject _playerPrefab;
    private readonly Dictionary<UnityEngine.Object, SerializedObject> _serializedObjects = new Dictionary<UnityEngine.Object, SerializedObject>();

    [MenuItem("Tools/プレイヤー調整")]
    private static void Open()
    {
        CSED_PlayerTuningWindow window = GetWindow<CSED_PlayerTuningWindow>("プレイヤー調整");
        window.minSize = new Vector2(560f, 400f);
    }

    private void OnEnable()
    {
        FindAssets();
    }

    private void OnFocus()
    {
        FindAssets();
    }

    private void FindAssets()
    {
        _playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(_playerPrefabPath);

        CS_PlayerCharacter character = _playerPrefab != null ? _playerPrefab.GetComponent<CS_PlayerCharacter>() : null;
        _roster = character != null && character.roster != null ? character.roster : FindFirstAsset<CSO_PlayableCharacterRoster>();
        _serializedObjects.Clear();
    }

    private void OnGUI()
    {
        DrawHeader();

        _tab = GUILayout.Toolbar(_tab, _tabNames, GUILayout.Height(26f));
        EditorGUILayout.Space(4f);

        if (_playerPrefab == null)
        {
            EditorGUILayout.HelpBox($"Player.prefabが見つかりません({_playerPrefabPath})。プログラマーに連絡してください", MessageType.Error);
            return;
        }

        switch (_tab)
        {
            case 0: DrawCharacterTab(); break;
            case 1: DrawCommonTab(); break;
            default: DrawComboTab(); break;
        }
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label(EditorApplication.isPlaying ? "プレイ中: 変更は保存され、次に再生した時(スポーンした時)から反映されます" : "変更はCtrl+Zで戻せます",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Player.prefabを選ぶ", EditorStyles.toolbarButton)) Selection.activeObject = _playerPrefab;
            if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(50f))) AssetDatabase.SaveAssets();
        }
    }

    // ========================================
    // キャラクター
    // ========================================

    private void DrawCharacterTab()
    {
        if (_roster == null)
        {
            EditorGUILayout.HelpBox("キャラクターのデータがまだありません。下のボタンで、仕様書の4キャラ(ギロ・アブ・キース・イシュ)の初期データを作ります。\n" +
                "基礎数値・必殺技は最初は全員共通のデータ(DB_PlayerStats・DB_PlayerSpecial)を使います。キャラクターごとに変える時は「このキャラ専用にする」を押してください", MessageType.Info);
            if (GUILayout.Button("4キャラの初期データを作る", GUILayout.Height(32f))) CreateDefaultCharacters();
            return;
        }

        SerializedObject rosterObject = GetSerialized(_roster);
        rosterObject.Update();
        SerializedProperty characters = rosterObject.FindProperty("_characters");
        SerializedProperty testIndex = rosterObject.FindProperty("_testCharacterIndex");
        _selectedCharacter = Mathf.Clamp(_selectedCharacter, 0, Mathf.Max(0, characters.arraySize - 1));

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawCharacterList(rosterObject, characters, testIndex);

            using (new EditorGUILayout.VerticalScope())
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                CSO_PlayableCharacter character = characters.arraySize > 0
                    ? characters.GetArrayElementAtIndex(_selectedCharacter).objectReferenceValue as CSO_PlayableCharacter
                    : null;
                if (character != null) DrawCharacter(character);
                else EditorGUILayout.HelpBox("キャラクターを選んでください(空の枠は、左の一覧で削除するか、キャラクターを入れてください)", MessageType.Info);

                EditorGUILayout.Space(12f);
                DrawComparison(characters);
                EditorGUILayout.EndScrollView();
            }
        }
        rosterObject.ApplyModifiedProperties();
    }

    private void DrawCharacterList(SerializedObject rosterObject, SerializedProperty characters, SerializedProperty testIndex)
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(190f)))
        {
            EditorGUILayout.LabelField("キャラクター一覧", EditorStyles.boldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < characters.arraySize; i++)
            {
                CSO_PlayableCharacter character = characters.GetArrayElementAtIndex(i).objectReferenceValue as CSO_PlayableCharacter;
                string name = character != null ? character.displayName : "(空)";
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isTest = testIndex.intValue == i;
                    if (GUILayout.Toggle(isTest, new GUIContent("", "テストで使うキャラクター(再生した時、全員がこのキャラクターになる)"), EditorStyles.radioButton, GUILayout.Width(16f)) && !isTest)
                    {
                        testIndex.intValue = i;
                    }
                    if (GUILayout.Toggle(_selectedCharacter == i, $"{i + 1}. {name}", "Button")) _selectedCharacter = i;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField("○ = テストで使うキャラクター", EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("複製", "選んでいるキャラクターを、基礎数値・必殺技ごとコピーして追加する")))
                {
                    AddCopiedCharacter(characters, characters.arraySize > 0 ? characters.GetArrayElementAtIndex(_selectedCharacter).objectReferenceValue as CSO_PlayableCharacter : null);
                }
                using (new EditorGUI.DisabledScope(characters.arraySize == 0))
                {
                    if (GUILayout.Button(new GUIContent("一覧から外す", "一覧から外すだけで、データ(ファイル)は消さない")) &&
                        EditorUtility.DisplayDialog("一覧から外す", "選んでいるキャラクターを一覧から外しますか？(データは消えません)", "外す", "やめる"))
                    {
                        characters.DeleteArrayElementAtIndex(_selectedCharacter);
                        _selectedCharacter = Mathf.Max(0, _selectedCharacter - 1);
                        testIndex.intValue = Mathf.Clamp(testIndex.intValue, 0, Mathf.Max(0, characters.arraySize - 1));
                    }
                }
            }
            if (GUILayout.Button("一覧のデータを選ぶ")) Selection.activeObject = _roster;
        }
    }

    private void DrawCharacter(CSO_PlayableCharacter character)
    {
        SerializedObject characterObject = GetSerialized(character);
        characterObject.Update();

        EditorGUILayout.LabelField(character.displayName, EditorStyles.largeLabel);
        DrawProperty(characterObject, new FieldDef("_displayName", "表示名", ""));
        DrawProperty(characterObject, new FieldDef("_icon", "アイコン", "UIで使う画像(未設定でもよい)"));
        DrawProperty(characterObject, new FieldDef("_themeColor", "キャラクターの色", "UIで使う色"));
        DrawProperty(characterObject, new FieldDef("_memo", "内容", "プランナー用のメモ(ゲームには使わない)"));

        // 基礎数値
        EditorGUILayout.Space(8f);
        SerializedProperty statsProperty = characterObject.FindProperty("_stats");
        EditorGUILayout.PropertyField(statsProperty, new GUIContent("基礎数値のデータ", "未設定なら、Player.prefabの共通の基礎数値を使う"));
        CSO_PlayerStats stats = statsProperty.objectReferenceValue as CSO_PlayerStats;
        if (stats != null)
        {
            DrawSharedWarning(stats, c => c.stats, () => statsProperty.objectReferenceValue = CopyAsset(stats, character.name + "_Stats"));
            DrawFields(GetSerialized(stats), _statsFields);
        }
        else
        {
            CSO_PlayerStats commonStats = GetCommonStats();
            DrawCommonNotice("基礎数値", commonStats, () => statsProperty.objectReferenceValue = CopyAsset(commonStats, character.name + "_Stats"));
            if (commonStats != null) DrawFields(GetSerialized(commonStats), _statsFields);
        }

        // 必殺技
        EditorGUILayout.Space(8f);
        DrawProperty(characterObject, new FieldDef("_specialName", "必殺技の名前", ""));
        SerializedProperty attackProperty = characterObject.FindProperty("_specialAttack");
        EditorGUILayout.PropertyField(attackProperty, new GUIContent("必殺技の攻撃のデータ", "未設定なら、Player.prefabの共通の必殺技の攻撃を使う"));
        CSO_AttackData attack = attackProperty.objectReferenceValue as CSO_AttackData;
        if (attack != null)
        {
            DrawSharedWarning(attack, c => c.specialAttack, () => attackProperty.objectReferenceValue = CopyAsset(attack, character.name + "_Special"));
            DrawFields(GetSerialized(attack), _attackFields, "_comboWindow");
        }
        else
        {
            CSO_AttackData commonAttack = GetCommonSpecialAttack();
            DrawCommonNotice("必殺技の攻撃", commonAttack, () => attackProperty.objectReferenceValue = CopyAsset(commonAttack, character.name + "_Special"));
            if (commonAttack != null) DrawFields(GetSerialized(commonAttack), _attackFields, "_comboWindow");
        }

        // 必殺技の効果
        EditorGUILayout.Space(4f);
        SerializedProperty effect = characterObject.FindProperty("_specialEffect");
        EditorGUILayout.PropertyField(effect, new GUIContent("使った後の効果", "必殺技が当たった瞬間から、効果時間のあいだ続く"));
        CSE_PlayerSpecialEffect effectType = (CSE_PlayerSpecialEffect)effect.enumValueIndex;
        EditorGUILayout.HelpBox(GetEffectDescription(effectType), MessageType.None);
        if (effectType != CSE_PlayerSpecialEffect.None)
        {
            DrawProperty(characterObject, new FieldDef("_effectDuration", "効果時間(秒)", ""));
        }
        if (effectType == CSE_PlayerSpecialEffect.StatusBoost)
        {
            DrawProperty(characterObject, new FieldDef("_boostRate", "強化の倍率", "移動速度・スプリント速度・ジャンプ力・攻撃力・コンボの速さに掛ける(1で変化なし。暗躍ボーナスとも掛け合わせる)"));
        }

        characterObject.ApplyModifiedProperties();
    }

    // 他のキャラクターと同じデータを使っている時に警告し、専用にコピーするボタンを出す
    private void DrawSharedWarning<T>(T asset, Func<CSO_PlayableCharacter, T> getter, Action makeUnique) where T : UnityEngine.Object
    {
        int users = 0;
        foreach (CSO_PlayableCharacter character in _roster.characters)
        {
            if (character != null && getter(character) == asset) users++;
        }
        if (users <= 1) return;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.HelpBox($"このデータは{users}人のキャラクターで使っています。変えると全員に反映されます", MessageType.Warning);
            if (GUILayout.Button("このキャラ専用にする", GUILayout.Width(130f), GUILayout.Height(38f))) makeUnique();
        }
    }

    // 共通のデータを使っている時に知らせ、このキャラクター専用にコピーするボタンを出す
    private static void DrawCommonNotice(string title, UnityEngine.Object common, Action makeUnique)
    {
        if (common == null)
        {
            EditorGUILayout.HelpBox($"共通の{title}がPlayer.prefabに設定されていません。プログラマーに連絡してください", MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.HelpBox($"共通の{title}({common.name})を使っています。ここで変えると、共通を使っている全キャラクターに反映されます", MessageType.Info);
            if (GUILayout.Button("このキャラ専用にする", GUILayout.Width(130f), GUILayout.Height(38f))) makeUnique();
        }
    }

    // Player.prefabに設定されている共通の基礎数値・必殺技の攻撃
    private CSO_PlayerStats GetCommonStats()
    {
        CS_PlayerStats stats = _playerPrefab.GetComponent<CS_PlayerStats>();
        return stats != null ? GetSerialized(stats).FindProperty("_baseStats").objectReferenceValue as CSO_PlayerStats : null;
    }

    private CSO_AttackData GetCommonSpecialAttack()
    {
        CS_PlayerSpecialAttack special = _playerPrefab.GetComponent<CS_PlayerSpecialAttack>();
        return special != null ? GetSerialized(special).FindProperty("_specialAttackData").objectReferenceValue as CSO_AttackData : null;
    }

    // 全キャラクターの主な数値を並べて比べる
    private void DrawComparison(SerializedProperty characters)
    {
        EditorGUILayout.LabelField("キャラクターの比較", EditorStyles.boldLabel);
        CSO_PlayerStats commonStats = _playerPrefab.GetComponent<CS_PlayerStats>() != null
            ? new SerializedObject(_playerPrefab.GetComponent<CS_PlayerStats>()).FindProperty("_baseStats").objectReferenceValue as CSO_PlayerStats
            : null;

        string[] headers = { "キャラ", "HP", "攻撃", "移動", "ジャンプ", "ブリンク", "ゲージ", "必殺技の威力", "効果", "効果時間" };
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (string header in headers) GUILayout.Label(header, EditorStyles.miniBoldLabel, GUILayout.Width(header == "効果" ? 110f : 60f));
        }
        for (int i = 0; i < characters.arraySize; i++)
        {
            CSO_PlayableCharacter character = characters.GetArrayElementAtIndex(i).objectReferenceValue as CSO_PlayableCharacter;
            if (character == null) continue;

            CSO_PlayerStats stats = character.stats != null ? character.stats : commonStats;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(character.displayName, GUILayout.Width(60f));
                GUILayout.Label(stats != null ? stats.maxHp.ToString("0.##") : "-", GUILayout.Width(60f));
                GUILayout.Label(stats != null ? stats.attackPower.ToString("0.##") : "-", GUILayout.Width(60f));
                GUILayout.Label(stats != null ? stats.moveSpeed.ToString("0.##") : "-", GUILayout.Width(60f));
                GUILayout.Label(stats != null ? stats.jumpPower.ToString("0.##") : "-", GUILayout.Width(60f));
                GUILayout.Label(stats != null ? stats.dashSpeed.ToString("0.##") : "-", GUILayout.Width(60f));
                GUILayout.Label(stats != null ? stats.maxGauge.ToString("0.##") : "-", GUILayout.Width(60f));
                GUILayout.Label(stats != null ? stats.specialAttackPower.ToString("0.##") : "-", GUILayout.Width(60f));
                GUILayout.Label(GetEffectName(character.specialEffect), GUILayout.Width(110f));
                GUILayout.Label(character.specialEffect != CSE_PlayerSpecialEffect.None ? $"{character.effectDuration:0.#}秒" : "-", GUILayout.Width(60f));
            }
        }
    }

    // ========================================
    // 共通の数値(Player.prefab)
    // ========================================

    private void DrawCommonTab()
    {
        EditorGUILayout.HelpBox("全キャラクター共通の数値です(Player.prefabに保存されます)", MessageType.None);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (Section section in _commonSections)
        {
            Component component = _playerPrefab.GetComponent(section.componentType);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(section.title, EditorStyles.boldLabel);
            if (component == null)
            {
                EditorGUILayout.HelpBox($"Player.prefabに{section.componentType.Name}が付いていません", MessageType.Warning);
                continue;
            }

            DrawFields(GetSerialized(component), section.fields);
            string info = section.info?.Invoke(_playerPrefab);
            if (!string.IsNullOrEmpty(info)) EditorGUILayout.HelpBox(info, MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    // 必殺ゲージが満タンになるまでの時間
    private static string GaugeInfo(GameObject prefab)
    {
        CS_PlayerSpecialGauge gauge = prefab.GetComponent<CS_PlayerSpecialGauge>();
        CS_PlayerStats stats = prefab.GetComponent<CS_PlayerStats>();
        if (gauge == null || stats == null) return null;

        float perSecond = new SerializedObject(gauge).FindProperty("_gaugePerSecond").floatValue;
        CSO_PlayerStats baseStats = new SerializedObject(stats).FindProperty("_baseStats").objectReferenceValue as CSO_PlayerStats;
        if (baseStats == null || perSecond <= 0f) return null;

        float seconds = baseStats.maxGauge / perSecond;
        return $"共通の基礎数値(ゲージの最大 {baseStats.maxGauge:0.#})だと、時間経過だけで満タンになるまで {seconds:0.#}秒({seconds / 60f:0.#}分)。キャラクターごとの最大はキャラクタータブで決める";
    }

    // ブリンクの距離
    private static string BlinkInfo(GameObject prefab)
    {
        CS_Player player = prefab.GetComponent<CS_Player>();
        CS_PlayerStats stats = prefab.GetComponent<CS_PlayerStats>();
        if (player == null || stats == null) return null;

        float duration = new SerializedObject(player).FindProperty("_dashDuration").floatValue;
        CSO_PlayerStats baseStats = new SerializedObject(stats).FindProperty("_baseStats").objectReferenceValue as CSO_PlayerStats;
        if (baseStats == null) return null;

        return $"共通の基礎数値だと、ブリンクの距離は {baseStats.dashSpeed:0.#} × {duration:0.##} = {baseStats.dashSpeed * duration:0.##}m(ブリンクの速さはキャラクターごと)";
    }

    // ========================================
    // 通常攻撃
    // ========================================

    private void DrawComboTab()
    {
        CS_PlayerAttack attack = _playerPrefab.GetComponent<CS_PlayerAttack>();
        if (attack == null)
        {
            EditorGUILayout.HelpBox("Player.prefabにCS_PlayerAttackが付いていません", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox("通常攻撃(5段コンボ)は全キャラクター共通です。ダメージには攻撃力(倍率)が掛かります", MessageType.None);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        SerializedProperty steps = GetSerialized(attack).FindProperty("_attackSteps");
        for (int i = 0; i < steps.arraySize; i++)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"{i + 1}段目", EditorStyles.boldLabel);
            CSO_AttackData data = steps.GetArrayElementAtIndex(i).objectReferenceValue as CSO_AttackData;
            if (data == null)
            {
                EditorGUILayout.HelpBox("攻撃のデータが未設定です", MessageType.Warning);
                continue;
            }
            DrawFields(GetSerialized(data), _attackFields);
        }

        EditorGUILayout.EndScrollView();
    }

    // ========================================
    // 共通の部品
    // ========================================

    private SerializedObject GetSerialized(UnityEngine.Object target)
    {
        if (!_serializedObjects.TryGetValue(target, out SerializedObject serialized) || serialized.targetObject == null)
        {
            serialized = new SerializedObject(target);
            _serializedObjects[target] = serialized;
        }
        return serialized;
    }

    // 項目をまとめて表示する(skipPathの項目は出さない)
    private void DrawFields(SerializedObject serialized, FieldDef[] fields, string skipPath = null)
    {
        serialized.Update();
        using (new EditorGUI.IndentLevelScope())
        {
            foreach (FieldDef field in fields)
            {
                if (field.path == skipPath) continue;
                DrawProperty(serialized, field);
            }
        }
        serialized.ApplyModifiedProperties();
    }

    private static void DrawProperty(SerializedObject serialized, FieldDef field)
    {
        SerializedProperty property = serialized.FindProperty(field.path);
        if (property == null)
        {
            EditorGUILayout.HelpBox($"「{field.label.text}」({field.path})が見つかりません。プログラマーに連絡してください", MessageType.Warning);
            return;
        }
        EditorGUILayout.PropertyField(property, field.label, true);
    }

    private static string GetEffectName(CSE_PlayerSpecialEffect effect)
    {
        switch (effect)
        {
            case CSE_PlayerSpecialEffect.FrontalAmbush: return "正面から奇襲";
            case CSE_PlayerSpecialEffect.StatusBoost: return "身体能力の強化";
            case CSE_PlayerSpecialEffect.RevealAll: return "全員が見える";
            case CSE_PlayerSpecialEffect.HiddenFromPolice: return "警察に見つからない";
            default: return "なし";
        }
    }

    private static string GetEffectDescription(CSE_PlayerSpecialEffect effect)
    {
        switch (effect)
        {
            case CSE_PlayerSpecialEffect.FrontalAmbush: return "正面から奇襲: 奇襲の「背後か上」の条件を見ない(気づかれていない・距離の条件は残る)";
            case CSE_PlayerSpecialEffect.StatusBoost: return "身体能力の強化: 移動速度・スプリント速度・ジャンプ力・攻撃力・コンボの速さに倍率を掛ける";
            case CSE_PlayerSpecialEffect.RevealAll: return "全員が見える: 全ての悪人グループ・警察・巡回ドローンが、スキャンと同じ表示で見え続ける(使った本人の画面のみ)";
            case CSE_PlayerSpecialEffect.HiddenFromPolice: return "警察に見つからない: 警察の視界に入っても見つからない";
            default: return "効果なし: 必殺技の攻撃だけ";
        }
    }

    // ========================================
    // データを作る
    // ========================================

    private static T FindFirstAsset<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
    }

    // assetをキャラクターのフォルダへコピーする(同じ名前があれば番号を付ける)
    private static T CopyAsset<T>(T asset, string fileName) where T : UnityEngine.Object
    {
        EnsureFolder(_characterFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{_characterFolder}/DB_{fileName}.asset");
        AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(asset), path);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // 選んでいるキャラクターを、基礎数値・必殺技ごとコピーして一覧へ足す(無ければ空のキャラクターを足す)
    private void AddCopiedCharacter(SerializedProperty characters, CSO_PlayableCharacter source)
    {
        CSO_PlayableCharacter copy;
        if (source != null)
        {
            string baseName = source.name.Replace("DB_", "") + "_Copy";
            copy = CopyAsset(source, baseName);
            SerializedObject copyObject = new SerializedObject(copy);
            if (source.stats != null) copyObject.FindProperty("_stats").objectReferenceValue = CopyAsset(source.stats, baseName + "_Stats");
            if (source.specialAttack != null) copyObject.FindProperty("_specialAttack").objectReferenceValue = CopyAsset(source.specialAttack, baseName + "_Special");
            copyObject.FindProperty("_displayName").stringValue = source.displayName + "(コピー)";
            copyObject.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            EnsureFolder(_characterFolder);
            copy = CreateInstance<CSO_PlayableCharacter>();
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath($"{_characterFolder}/DB_Character.asset"));
        }

        characters.arraySize++;
        characters.GetArrayElementAtIndex(characters.arraySize - 1).objectReferenceValue = copy;
        _selectedCharacter = characters.arraySize - 1;
        AssetDatabase.SaveAssets();
    }

    // 仕様書の4キャラの初期データを作り、Player.prefabに付ける
    private void CreateDefaultCharacters()
    {
        CreateDefaultCharacterData();
        FindAssets();
    }

    // 仕様書「プレイヤー」の必殺技の効果一覧(予定)から作る
    // 基礎数値・必殺技の攻撃は未設定(共通のDB_PlayerStats・DB_PlayerSpecialを使う)にしておき、別のレベルデザインツール(Tools → レベルデザイン)で
    // 共通のデータを調整した結果がそのまま効くようにする。キャラクターごとに変える時は、ウィンドウの「このキャラ専用にする」でコピーする
    [MenuItem("Tools/プレイヤー調整(4キャラの初期データを作る)", priority = 1000)]
    private static void CreateDefaultCharacterData()
    {
        if (FindFirstAsset<CSO_PlayableCharacterRoster>() != null)
        {
            Debug.LogWarning("キャラクターの一覧はもうあります。作り直す時は、今の一覧を消してから実行してください");
            return;
        }

        EnsureFolder(_characterFolder);

        (string file, string name, string special, CSE_PlayerSpecialEffect effect, Color color)[] definitions =
        {
            ("Giro", "ギロ", "ギロチン", CSE_PlayerSpecialEffect.FrontalAmbush, new Color(0.85f, 0.25f, 0.3f)),
            ("Abu", "アブ", "火炙り", CSE_PlayerSpecialEffect.StatusBoost, new Color(1f, 0.55f, 0.15f)),
            ("Keith", "キース", "電気椅子", CSE_PlayerSpecialEffect.RevealAll, new Color(0.3f, 0.75f, 1f)),
            ("Ishu", "イシュ", "首吊り", CSE_PlayerSpecialEffect.HiddenFromPolice, new Color(0.6f, 0.4f, 0.9f)),
        };

        CSO_PlayableCharacterRoster roster = CreateInstance<CSO_PlayableCharacterRoster>();
        AssetDatabase.CreateAsset(roster, AssetDatabase.GenerateUniqueAssetPath($"{_characterFolder}/DB_PlayableCharacterRoster.asset"));
        SerializedObject rosterObject = new SerializedObject(roster);
        SerializedProperty characters = rosterObject.FindProperty("_characters");

        foreach ((string file, string name, string special, CSE_PlayerSpecialEffect effect, Color color) in definitions)
        {
            CSO_PlayableCharacter character = CreateInstance<CSO_PlayableCharacter>();
            AssetDatabase.CreateAsset(character, AssetDatabase.GenerateUniqueAssetPath($"{_characterFolder}/DB_Character_{file}.asset"));

            SerializedObject characterObject = new SerializedObject(character);
            characterObject.FindProperty("_displayName").stringValue = name;
            characterObject.FindProperty("_themeColor").colorValue = color;
            characterObject.FindProperty("_specialName").stringValue = special;
            characterObject.FindProperty("_specialEffect").enumValueIndex = (int)effect;
            characterObject.FindProperty("_memo").stringValue = "仕様書「プレイヤー」の必殺技の効果一覧(予定)から作成。数値は仮";
            characterObject.ApplyModifiedPropertiesWithoutUndo();

            characters.arraySize++;
            characters.GetArrayElementAtIndex(characters.arraySize - 1).objectReferenceValue = character;
        }
        rosterObject.ApplyModifiedPropertiesWithoutUndo();

        AttachRosterToPlayer(roster);
        AssetDatabase.SaveAssets();
        Debug.Log($"4キャラの初期データを作りました: {_characterFolder}");
    }

    // Player.prefabにCS_PlayerCharacterを付け(付いていなければ)、一覧を設定する
    private static void AttachRosterToPlayer(CSO_PlayableCharacterRoster roster)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_playerPrefabPath);
        try
        {
            CS_PlayerCharacter character = root.GetComponent<CS_PlayerCharacter>();
            if (character == null) character = root.AddComponent<CS_PlayerCharacter>();

            SerializedObject characterObject = new SerializedObject(character);
            characterObject.FindProperty("_roster").objectReferenceValue = roster;
            characterObject.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, _playerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
