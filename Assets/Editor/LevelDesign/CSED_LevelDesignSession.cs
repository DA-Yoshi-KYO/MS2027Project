using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/*
 * ステータス調整の「編集前の値」を覚えておき、再生終了時に保存する値を選ばせるクラス
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// ========================================
/*
 * メモ
 * ・流れ
 *   1. 再生開始時(ステータス調整ウィンドウが開いている時だけ)、悪人・警察のデータの複製を取っておく(=編集前)
 *   2. プレイヤーは、ウィンドウに初めて表示した時の値を編集前として覚える
 *   3. 再生終了直前に、各プレイヤーの編集後の値を記録する
 *   4. 編集モードに戻ったら、変わった項目があれば保存選択ウィンドウを出す
 * ・保存選択が終わっていない間は、Playを押しても再生を止めて保存選択ウィンドウを出す
 * ・再生中にScriptableObjectを変えると再生を止めても戻らないため、「編集前」を選んだ項目は複製から書き戻す
 */
// ========================================

[InitializeOnLoad]
public static class CSED_LevelDesignSession
{
    private const string _pendingKey = "MS2027.LevelDesign.PendingChoice";

    private static readonly Dictionary<ScriptableObject, ScriptableObject> _snapshots = new Dictionary<ScriptableObject, ScriptableObject>();
    private static readonly Dictionary<CS_PlayerStats, float[]> _playerOriginals = new Dictionary<CS_PlayerStats, float[]>();
    private static readonly List<CSED_LevelDesignPlayerChoice> _playerChoices = new List<CSED_LevelDesignPlayerChoice>();
    private static bool _isTracking;

    // 保存する値をまだ選んでいない(この間はPlayできない)
    public static bool isChoicePending
    {
        get => SessionState.GetBool(_pendingKey, false);
        set => SessionState.SetBool(_pendingKey, value);
    }

    static CSED_LevelDesignSession()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    // ---------- 悪人・警察のデータ ----------

    // 編集前の値として、まだ複製が無ければ今の値を複製しておく
    public static void EnsureSnapshot(ScriptableObject asset)
    {
        if (asset == null) return;
        if (_snapshots.TryGetValue(asset, out ScriptableObject snapshot) && snapshot != null) return;

        snapshot = Object.Instantiate(asset);
        snapshot.hideFlags = HideFlags.HideAndDontSave;
        _snapshots[asset] = snapshot;
    }

    public static ScriptableObject GetSnapshot(ScriptableObject asset)
    {
        return asset != null && _snapshots.TryGetValue(asset, out ScriptableObject snapshot) ? snapshot : null;
    }

    // データを編集前の値に戻す
    public static void ResetAsset(ScriptableObject asset)
    {
        ScriptableObject snapshot = GetSnapshot(asset);
        if (snapshot == null) return;

        SerializedObject source = new SerializedObject(snapshot);
        SerializedObject target = new SerializedObject(asset);
        SerializedProperty property = source.GetIterator();
        // 一番上の階層の項目だけを順にコピーする(配列や中のクラスは丸ごとコピーされる)
        for (bool enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
        {
            if (property.propertyPath == "m_Script") continue;
            target.CopyFromSerializedProperty(property);
        }
        target.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
    }

    // 編集前と値が違う項目(数値などの末端の項目)を返す
    public static List<CSED_LevelDesignAssetChoice> GetAssetChanges(ScriptableObject asset)
    {
        List<CSED_LevelDesignAssetChoice> changes = new List<CSED_LevelDesignAssetChoice>();
        ScriptableObject snapshot = GetSnapshot(asset);
        if (snapshot == null) return changes;

        SerializedObject current = new SerializedObject(asset);
        SerializedProperty before = new SerializedObject(snapshot).GetIterator();
        while (before.NextVisible(true))
        {
            if (before.propertyPath == "m_Script" || before.hasVisibleChildren) continue;

            SerializedProperty after = current.FindProperty(before.propertyPath);
            if (after == null || SerializedProperty.DataEquals(before, after)) continue;

            changes.Add(new CSED_LevelDesignAssetChoice(asset, snapshot, before.propertyPath,
                $"{asset.name} / {GetDisplayPath(before)}", ToText(before), ToText(after)));
        }
        return changes;
    }

    // ---------- プレイヤー(再生中のキャラ) ----------

    // 編集前の値を返す。初めて見るプレイヤーなら今の値を編集前として覚える
    public static float[] GetOrCaptureOriginal(CS_PlayerStats stats)
    {
        if (_playerOriginals.TryGetValue(stats, out float[] originals)) return originals;

        CSED_LevelDesignPlayerField[] fields = CSED_LevelDesignTargets.playerFields;
        originals = new float[fields.Length];
        for (int i = 0; i < fields.Length; i++) originals[i] = fields[i].Get(stats);
        _playerOriginals[stats] = originals;
        return originals;
    }

    public static void ResetPlayer(CS_PlayerStats stats)
    {
        if (stats == null || !_playerOriginals.TryGetValue(stats, out float[] originals)) return;

        CSED_LevelDesignPlayerField[] fields = CSED_LevelDesignTargets.playerFields;
        for (int i = 0; i < fields.Length; i++) fields[i].Set(stats, originals[i]);
    }

    // ---------- 再生の開始・終了 ----------

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode: BlockPlayIfPending(); break;
            case PlayModeStateChange.EnteredPlayMode: StartTracking(); break;
            case PlayModeStateChange.ExitingPlayMode: CapturePlayerChoices(); break;
            case PlayModeStateChange.EnteredEditMode: ShowChoicesIfChanged(); break;
        }
    }

    // 保存の選択が残っていたら再生を取り消す
    private static void BlockPlayIfPending()
    {
        if (!isChoicePending) return;

        EditorApplication.isPlaying = false;
        CSED_LevelDesignSaveWindow.Open();
        Debug.LogWarning("ステータス調整: 前回の調整値を「編集前／編集後」のどちらで保存するか選ぶまで再生できません");
    }

    private static void StartTracking()
    {
        _playerOriginals.Clear();
        _playerChoices.Clear();
        _isTracking = EditorWindow.HasOpenInstances<CSED_LevelDesignWindow>();
        if (!_isTracking) return;

        // 再生開始時点の値を編集前にする
        foreach (ScriptableObject snapshot in _snapshots.Values)
        {
            if (snapshot != null) Object.DestroyImmediate(snapshot);
        }
        _snapshots.Clear();
        foreach (ScriptableObject asset in CSED_LevelDesignTargets.FindAllAssets()) EnsureSnapshot(asset);
    }

    // 再生終了直前(キャラが消える前)に、編集した項目ごとの各プレイヤーの値を記録する
    private static void CapturePlayerChoices()
    {
        _playerChoices.Clear();
        if (!_isTracking) return;

        List<CS_PlayerStats> players = CSED_LevelDesignTargets.FindPlayers();
        players.RemoveAll(p => !_playerOriginals.ContainsKey(p));
        if (players.Count == 0) return;

        CSO_PlayerStats baseStats = CSED_LevelDesignTargets.GetBaseStats(players[0]);
        if (baseStats == null) return;

        CSED_LevelDesignPlayerField[] fields = CSED_LevelDesignTargets.playerFields;
        for (int i = 0; i < fields.Length; i++)
        {
            List<string> labels = new List<string>();
            List<float> values = new List<float>();
            bool edited = false;
            foreach (CS_PlayerStats stats in players)
            {
                float value = fields[i].Get(stats);
                if (!Mathf.Approximately(value, _playerOriginals[stats][i])) edited = true;
                labels.Add($"P{CSED_LevelDesignTargets.GetPlayerNumber(stats) + 1}");
                values.Add(value);
            }
            if (!edited) continue;

            float before = new SerializedObject(baseStats).FindProperty(fields[i].property)?.floatValue ?? 0f;
            _playerChoices.Add(new CSED_LevelDesignPlayerChoice(fields[i].label, fields[i].property, baseStats, before, labels.ToArray(), values.ToArray()));
        }
    }

    private static void ShowChoicesIfChanged()
    {
        if (!_isTracking) return;
        _isTracking = false;

        List<CSED_LevelDesignAssetChoice> assetChoices = new List<CSED_LevelDesignAssetChoice>();
        foreach (ScriptableObject asset in _snapshots.Keys)
        {
            if (asset != null) assetChoices.AddRange(GetAssetChanges(asset));
        }
        if (_playerChoices.Count == 0 && assetChoices.Count == 0) return;

        isChoicePending = true;
        CSED_LevelDesignSaveWindow.Open(new List<CSED_LevelDesignPlayerChoice>(_playerChoices), assetChoices);
        _playerChoices.Clear();
    }

    // ---------- 表示用 ----------

    // 「_levels.Array.data[0]._policeCount」→「Levels[0] / Police Count」
    private static string GetDisplayPath(SerializedProperty property)
    {
        string path = property.propertyPath.Replace(".Array.data[", "[");
        string[] parts = path.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            int bracket = parts[i].IndexOf('[');
            string name = bracket >= 0 ? parts[i].Substring(0, bracket) : parts[i];
            string index = bracket >= 0 ? parts[i].Substring(bracket) : "";
            parts[i] = ObjectNames.NicifyVariableName(name) + index;
        }
        return string.Join(" / ", parts);
    }

    public static string ToText(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Float: return property.floatValue.ToString("0.###");
            case SerializedPropertyType.Integer: return property.intValue.ToString();
            case SerializedPropertyType.Boolean: return property.boolValue ? "ON" : "OFF";
            case SerializedPropertyType.String: return property.stringValue;
            case SerializedPropertyType.Enum:
                int index = property.enumValueIndex;
                return index >= 0 && index < property.enumDisplayNames.Length ? property.enumDisplayNames[index] : index.ToString();
            case SerializedPropertyType.ObjectReference: return property.objectReferenceValue != null ? property.objectReferenceValue.name : "なし";
            default: return "(変更あり)";
        }
    }
}
