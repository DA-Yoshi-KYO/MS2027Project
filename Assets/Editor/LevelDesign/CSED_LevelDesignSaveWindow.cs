using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/*
 * 再生終了時に、調整した項目ごとに「編集前／編集後」のどちらを保存するか選ぶウィンドウ
 * すべての項目を選んで「決定」するまで閉じられず、Playもできない
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// プレイヤーの項目1つ分の選択(保存先はDB_PlayerStats)
[Serializable]
public class CSED_LevelDesignPlayerChoice
{
    [SerializeField] private string _label;
    [SerializeField] private string _property;
    [SerializeField] private CSO_PlayerStats _target;
    [SerializeField] private float _before;
    [SerializeField] private string[] _playerLabels;   // P1, P2...
    [SerializeField] private float[] _values;          // 各プレイヤーの編集後の値
    [SerializeField] private int _choice = -1;         // -1:未選択 0:編集前 1〜:そのプレイヤーの値

    public string label => _label;
    public int choice { get => _choice; set => _choice = value; }

    public CSED_LevelDesignPlayerChoice(string label, string property, CSO_PlayerStats target, float before, string[] playerLabels, float[] values)
    {
        _label = label;
        _property = property;
        _target = target;
        _before = before;
        _playerLabels = playerLabels;
        _values = values;
    }

    // 選択肢の表示名(0番目が編集前)
    public string[] GetOptions()
    {
        string[] options = new string[_values.Length + 1];
        options[0] = $"編集前 {_before:0.###}";
        for (int i = 0; i < _values.Length; i++)
        {
            options[i + 1] = _values.Length == 1 ? $"編集後 {_values[i]:0.###}" : $"{_playerLabels[i]} {_values[i]:0.###}";
        }
        return options;
    }

    // 編集後を選んだ時だけDB_PlayerStatsに書き込む(編集前はデータが変わっていないので何もしない)
    public void Apply()
    {
        if (_choice <= 0 || _target == null) return;

        SerializedObject target = new SerializedObject(_target);
        SerializedProperty property = target.FindProperty(_property);
        if (property == null) return;

        property.floatValue = _values[_choice - 1];
        target.ApplyModifiedProperties();
        EditorUtility.SetDirty(_target);
    }
}

// 悪人・警察のデータの項目1つ分の選択
[Serializable]
public class CSED_LevelDesignAssetChoice
{
    [SerializeField] private ScriptableObject _asset;
    [SerializeField] private ScriptableObject _snapshot;   // 編集前の値を持つ複製
    [SerializeField] private string _path;
    [SerializeField] private string _label;
    [SerializeField] private string _before;
    [SerializeField] private string _after;
    [SerializeField] private int _choice = -1;             // -1:未選択 0:編集前 1:編集後

    public string label => _label;
    public int choice { get => _choice; set => _choice = value; }
    public string[] GetOptions() => new[] { $"編集前 {_before}", $"編集後 {_after}" };

    public CSED_LevelDesignAssetChoice(ScriptableObject asset, ScriptableObject snapshot, string path, string label, string before, string after)
    {
        _asset = asset;
        _snapshot = snapshot;
        _path = path;
        _label = label;
        _before = before;
        _after = after;
    }

    // データは既に編集後の値になっているので、編集前を選んだ時だけ複製から書き戻す
    public void Apply()
    {
        if (_choice != 0 || _asset == null || _snapshot == null) return;

        SerializedProperty source = new SerializedObject(_snapshot).FindProperty(_path);
        if (source == null) return;

        SerializedObject target = new SerializedObject(_asset);
        target.CopyFromSerializedProperty(source);
        target.ApplyModifiedProperties();
        EditorUtility.SetDirty(_asset);
    }
}

public class CSED_LevelDesignSaveWindow : EditorWindow
{
    [SerializeField] private List<CSED_LevelDesignPlayerChoice> _playerChoices = new List<CSED_LevelDesignPlayerChoice>();
    [SerializeField] private List<CSED_LevelDesignAssetChoice> _assetChoices = new List<CSED_LevelDesignAssetChoice>();
    [SerializeField] private bool _isDone;
    private Vector2 _scroll;

    // ウィンドウを閉じられた時に開き直すための控え
    private static List<CSED_LevelDesignPlayerChoice> _keptPlayerChoices;
    private static List<CSED_LevelDesignAssetChoice> _keptAssetChoices;

    public static void Open(List<CSED_LevelDesignPlayerChoice> playerChoices, List<CSED_LevelDesignAssetChoice> assetChoices)
    {
        CSED_LevelDesignSaveWindow window = GetWindow<CSED_LevelDesignSaveWindow>(true, "調整値の保存", true);
        window._playerChoices = playerChoices;
        window._assetChoices = assetChoices;
        window._isDone = false;
        window.minSize = new Vector2(480, 320);
    }

    // 既に開いていれば前に出し、閉じられていれば控えから開き直す
    public static void Open()
    {
        if (HasOpenInstances<CSED_LevelDesignSaveWindow>())
        {
            FocusWindowIfItsOpen<CSED_LevelDesignSaveWindow>();
            return;
        }
        if (_keptPlayerChoices != null || _keptAssetChoices != null)
        {
            Open(_keptPlayerChoices ?? new List<CSED_LevelDesignPlayerChoice>(), _keptAssetChoices ?? new List<CSED_LevelDesignAssetChoice>());
            return;
        }

        // Unityの再コンパイルなどで選択内容が失われた場合は、データの値をそのまま残してロックだけ解除する
        CSED_LevelDesignSession.isChoicePending = false;
        Debug.LogWarning("ステータス調整: 保存の選択内容が見つからなかったため、データは今の値のままにしてロックを解除しました");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("調整した値を、項目ごとに「編集前」「編集後」のどちらで保存するか選んでください。\nすべて選んで「決定」するまで、次のPlayはできません。", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("すべて編集前にする")) SelectAll(false);
            if (GUILayout.Button("すべて編集後にする")) SelectAll(true);
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        if (_playerChoices.Count > 0)
        {
            EditorGUILayout.LabelField("プレイヤー(DB_PlayerStatsに保存)", EditorStyles.boldLabel);
            foreach (CSED_LevelDesignPlayerChoice choice in _playerChoices) choice.choice = DrawChoice(choice.label, choice.choice, choice.GetOptions());
        }
        if (_assetChoices.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("悪人・警察のデータ", EditorStyles.boldLabel);
            foreach (CSED_LevelDesignAssetChoice choice in _assetChoices) choice.choice = DrawChoice(choice.label, choice.choice, choice.GetOptions());
        }
        EditorGUILayout.EndScrollView();

        int remaining = _playerChoices.Count(c => c.choice < 0) + _assetChoices.Count(c => c.choice < 0);
        using (new EditorGUI.DisabledScope(remaining > 0))
        {
            if (GUILayout.Button(remaining > 0 ? $"決定(未選択があと{remaining}件)" : "決定", GUILayout.Height(28))) Decide();
        }
    }

    // 項目名と、選択肢のボタン(どれも選ばれていない状態から始まる)
    private static int DrawChoice(string label, int choice, string[] options)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(220));
            return GUILayout.Toolbar(choice, options);
        }
    }

    private void SelectAll(bool useEdited)
    {
        // プレイヤーの編集後は人数分あるので、まとめて選ぶ時はP1(ソロなら操作キャラ)の値にする
        foreach (CSED_LevelDesignPlayerChoice choice in _playerChoices) choice.choice = useEdited ? 1 : 0;
        foreach (CSED_LevelDesignAssetChoice choice in _assetChoices) choice.choice = useEdited ? 1 : 0;
    }

    private void Decide()
    {
        foreach (CSED_LevelDesignPlayerChoice choice in _playerChoices) choice.Apply();
        foreach (CSED_LevelDesignAssetChoice choice in _assetChoices) choice.Apply();
        AssetDatabase.SaveAssets();

        _isDone = true;
        _keptPlayerChoices = null;
        _keptAssetChoices = null;
        CSED_LevelDesignSession.isChoicePending = false;
        Debug.Log("ステータス調整: 選んだ値を保存しました");
        Close();
    }

    // 決定せずに閉じられたら、選択内容を控えて開き直す
    private void OnDestroy()
    {
        if (_isDone) return;

        _keptPlayerChoices = _playerChoices;
        _keptAssetChoices = _assetChoices;
        EditorApplication.delayCall += () => Open();
    }
}
