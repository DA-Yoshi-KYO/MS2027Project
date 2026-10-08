using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/*
 * レベルデザイン用のステータス調整ウィンドウ
 * 上部のタブで プレイヤー / 悪人 / 警察 を切り替えて値を調整する
 * Tools > レベルデザイン > 開く で、レイアウトと一緒に開く
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// ========================================
/*
 * メモ
 * ・プレイヤー: 再生中の操作キャラの値(CS_PlayerStats)を直接変える。値の変更はCS_PlayerStatsのSetメソッドを通す
 * ・悪人/警察: データ(DB_VillainStats, DB_PoliceStatusなど)を直接変える
 * ・「初期状態に戻す」は、編集前の値(プレイヤーは最初に表示した時、データは再生開始時かウィンドウを開いた時)に戻す
 * ・再生を止めると、変えた項目の保存先を選ぶウィンドウが出る(CSED_LevelDesignSession)
 * ・「調整を終了」で、レイアウトを開く前の配置に戻す(CSED_LevelDesignLayout)
 */
// ========================================

public class CSED_LevelDesignWindow : EditorWindow
{
    private static readonly string[] _tabLabels = { "プレイヤー", "悪人", "警察" };

    [SerializeField] private CSE_LevelDesignTab _tab = CSE_LevelDesignTab.Player;
    [SerializeField] private string _layoutHint;   // レイアウト未登録の時の案内(閉じるまで表示)
    private readonly Dictionary<ScriptableObject, bool> _foldouts = new Dictionary<ScriptableObject, bool>();
    private Vector2 _scroll;

    public string layoutHint
    {
        get => _layoutHint;
        set { _layoutHint = value; Repaint(); }
    }

    private const string _title = "ステータス調整";

    // ウィンドウを開く(レイアウトに無ければ指定したウィンドウの隣にタブで開く)
    public static CSED_LevelDesignWindow Open(params System.Type[] dockNextTo)
    {
        return GetWindow<CSED_LevelDesignWindow>(_title, true, dockNextTo);
    }

    private void OnEnable()
    {
        titleContent = new GUIContent(_title);
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

        CS_PlayerStats player = CSED_LevelDesignTargets.FindControlledPlayer(CSED_LevelDesignTargets.FindPlayers());
        DrawPlayerBox(player, "操作キャラ", position.width - 24);
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
