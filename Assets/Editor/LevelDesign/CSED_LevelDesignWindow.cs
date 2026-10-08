using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/*
 * レベルデザイン用のステータス調整ウィンドウ
 * 上部のタブで プレイヤー / 悪人 / 警察 を切り替えて値を調整する
 * Tools > レベルデザイン > ソロ用 / マルチ用 で、レイアウトと一緒に開く
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// ========================================
/*
 * メモ
 * ・プレイヤー: 再生中のキャラの値(CS_PlayerStats)を直接変える。ソロは操作キャラ1人、マルチはP1〜P4を4分割で表示
 *   値の変更はCS_PlayerStatsのSetメソッドを通すので、マルチではホスト(メインエディタ)から全員分を変えられる
 * ・悪人/警察: データ(DB_VillainStats, DB_PoliceStatusなど)を直接変える。全プレイヤー共通なので分割しない
 * ・「初期状態に戻す」は、編集前の値(プレイヤーは最初に表示した時、データは再生開始時かウィンドウを開いた時)に戻す
 * ・再生を止めると、変えた項目の保存先を選ぶウィンドウが出る(CSED_LevelDesignSession)
 * ・「調整を終了」で、レイアウトを開く前の配置に戻す(CSED_LevelDesignLayout)
 */
// ========================================

public class CSED_LevelDesignWindow : EditorWindow
{
    private static readonly string[] _tabLabels = { "プレイヤー", "悪人", "警察" };

    [SerializeField] private CSE_LevelDesignMode _mode = CSE_LevelDesignMode.Solo;
    [SerializeField] private CSE_LevelDesignTab _tab = CSE_LevelDesignTab.Player;
    [SerializeField] private string _layoutHint;   // レイアウト未登録の時の案内(閉じるまで表示)
    private readonly Dictionary<ScriptableObject, bool> _foldouts = new Dictionary<ScriptableObject, bool>();
    private Vector2 _scroll;

    public CSE_LevelDesignMode mode
    {
        get => _mode;
        set { _mode = value; titleContent = new GUIContent(GetTitle(value)); Repaint(); }
    }

    public string layoutHint
    {
        get => _layoutHint;
        set { _layoutHint = value; Repaint(); }
    }

    // ウィンドウを開いてモードを設定する(レイアウトに無ければ指定したウィンドウの隣にタブで開く)
    public static CSED_LevelDesignWindow Open(CSE_LevelDesignMode mode, params System.Type[] dockNextTo)
    {
        CSED_LevelDesignWindow window = GetWindow<CSED_LevelDesignWindow>(GetTitle(mode), true, dockNextTo);
        window.mode = mode;
        return window;
    }

    private static string GetTitle(CSE_LevelDesignMode mode) => mode == CSE_LevelDesignMode.Solo ? "ステータス調整(ソロ)" : "ステータス調整(マルチ)";

    private void OnEnable()
    {
        titleContent = new GUIContent(GetTitle(_mode));
    }

    // 再生中は値が変わるので、定期的に描き直す
    private void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            _tab = (CSE_LevelDesignTab)GUILayout.Toolbar((int)_tab, _tabLabels, EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("調整を終了", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                CSED_LevelDesignLayout.EndAdjustment();
                GUIUtility.ExitGUI();
            }
            CSE_LevelDesignMode newMode = (CSE_LevelDesignMode)EditorGUILayout.EnumPopup(_mode, EditorStyles.toolbarPopup, GUILayout.Width(70));
            if (newMode != _mode) mode = newMode;
        }

        if (!string.IsNullOrEmpty(_layoutHint))
        {
            EditorGUILayout.HelpBox(_layoutHint, MessageType.Info);
            if (GUILayout.Button("案内を閉じる")) _layoutHint = null;
        }

        if (CSED_LevelDesignSession.isChoicePending)
        {
            EditorGUILayout.HelpBox("前回の調整値の保存がまだ選ばれていません。選ぶまでPlayできません。", MessageType.Warning);
            if (GUILayout.Button("保存の選択画面を開く")) CSED_LevelDesignSaveWindow.Open();
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        if (_tab == CSE_LevelDesignTab.Player) DrawPlayerTab();
        else DrawAssetTab(_tab);
        EditorGUILayout.EndScrollView();
    }

    // ---------- プレイヤー ----------

    private void DrawPlayerTab()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("再生中に、操作キャラのステータスをここで調整できます。", MessageType.Info);
            return;
        }

        List<CS_PlayerStats> players = CSED_LevelDesignTargets.FindPlayers();
        if (_mode == CSE_LevelDesignMode.Solo)
        {
            CS_PlayerStats player = CSED_LevelDesignTargets.FindSoloPlayer(players);
            DrawPlayerBox(player, "操作キャラ", position.width - 24);
            return;
        }

        // マルチ: P1〜P4を2×2で並べる
        float width = (position.width - 30) / 2;
        for (int row = 0; row < 2; row++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int column = 0; column < 2; column++)
                {
                    int number = row * 2 + column;
                    DrawPlayerBox(players.Find(p => CSED_LevelDesignTargets.GetPlayerNumber(p) == number), $"P{number + 1}", width);
                }
            }
        }
    }

    private void DrawPlayerBox(CS_PlayerStats stats, string title, float width)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width)))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (stats == null)
            {
                EditorGUILayout.LabelField("未参加");
                return;
            }
            if (stats.IsSpawned && !stats.IsServer)
            {
                EditorGUILayout.HelpBox("ホスト(メインエディタ)でのみ変更できます", MessageType.None);
                return;
            }

            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(140f, width * 0.55f);

            float[] originals = CSED_LevelDesignSession.GetOrCaptureOriginal(stats);
            CSED_LevelDesignPlayerField[] fields = CSED_LevelDesignTargets.playerFields;
            for (int i = 0; i < fields.Length; i++)
            {
                float current = fields[i].Get(stats);
                bool isEdited = !Mathf.Approximately(current, originals[i]);
                // 編集した項目は太字にし、ツールチップで編集前の値を見せる
                GUIContent label = new GUIContent(fields[i].label, $"編集前: {originals[i]:0.###}");
                GUIStyle style = isEdited ? EditorStyles.boldLabel : EditorStyles.label;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, style, GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorGUI.BeginChangeCheck();
                    float value = EditorGUILayout.DelayedFloatField(current);
                    if (EditorGUI.EndChangeCheck()) fields[i].Set(stats, value);
                }
            }
            EditorGUIUtility.labelWidth = labelWidth;

            if (GUILayout.Button("初期状態に戻す")) CSED_LevelDesignSession.ResetPlayer(stats);
        }
    }

    // ---------- 悪人・警察 ----------

    private void DrawAssetTab(CSE_LevelDesignTab tab)
    {
        if (_mode == CSE_LevelDesignMode.Multi) EditorGUILayout.HelpBox("悪人・警察の値は全プレイヤー共通です", MessageType.None);

        List<ScriptableObject> assets = CSED_LevelDesignTargets.FindAssets(tab);
        foreach (ScriptableObject asset in assets)
        {
            CSED_LevelDesignSession.EnsureSnapshot(asset);
            DrawAsset(asset);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("このタブをすべて初期状態に戻す"))
        {
            foreach (ScriptableObject asset in assets) CSED_LevelDesignSession.ResetAsset(asset);
        }
    }

    private void DrawAsset(ScriptableObject asset)
    {
        bool isEdited = CSED_LevelDesignSession.GetAssetChanges(asset).Count > 0;
        _foldouts.TryGetValue(asset, out bool isOpen);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _foldouts[asset] = EditorGUILayout.Foldout(isOpen, asset.name + (isEdited ? "  (編集中)" : ""), true, isEdited ? EditorStyles.foldoutHeader : EditorStyles.foldout);
                using (new EditorGUI.DisabledScope(!isEdited))
                {
                    if (GUILayout.Button("初期状態に戻す", GUILayout.Width(100))) CSED_LevelDesignSession.ResetAsset(asset);
                }
            }
            if (!_foldouts[asset]) return;

            // Inspectorと同じ表示で、データの全項目を並べる
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty property = serialized.GetIterator();
            for (bool enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
            {
                if (property.propertyPath == "m_Script") continue;
                EditorGUILayout.PropertyField(property, true);
            }
            serialized.ApplyModifiedProperties();
        }
    }
}
