using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/*
 * Confluenceのデータ表とアセットの値を比べて、違う所だけ反映するウィンドウ
 * Tools/仕様データ同期 から開く
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// ========================================
/*
 * メモ
 * ・Confluenceから取る場合は、各自のAtlassianメールアドレスとAPIトークンを入れる
 *   (EditorPrefsに保存するのでリポジトリには入らない。トークンは https://id.atlassian.com/manage-profile/security/api-tokens で作る)
 * ・トークンを使わない場合は、ブラウザでデータ表をコピーして「貼り付けて比較」でも読める
 * ・どの表をどのアセットに入れるかは SpecSyncMapping.json に書く
 */
// ========================================

public class CSED_SpecSyncWindow : EditorWindow
{
    private const string _emailPrefKey = "SpecSync.Email";
    private const string _tokenPrefKey = "SpecSync.Token";

    private CSED_SpecSyncMapping _mapping;
    private string _mappingError;
    private string _email = "";
    private string _token = "";
    private bool _isFetching;
    private bool _showPaste;
    private string _pasteText = "";
    private bool _showSame;
    private string _resultMessage = "";
    private MessageType _resultMessageType = MessageType.None;
    private List<CSED_SpecSyncTable> _lastTables;
    private List<CSED_SpecSyncEntry> _entries = new List<CSED_SpecSyncEntry>();
    private Vector2 _scroll;

    [MenuItem("Tools/仕様データ同期")]
    public static void Open()
    {
        GetWindow<CSED_SpecSyncWindow>("仕様データ同期");
    }

    private void OnEnable()
    {
        _email = EditorPrefs.GetString(_emailPrefKey, "");
        _token = EditorPrefs.GetString(_tokenPrefKey, "");
        LoadMapping();
    }

    private void OnGUI()
    {
        if (_mapping == null)
        {
            EditorGUILayout.HelpBox(_mappingError, MessageType.Error);
            if (GUILayout.Button("対応表を読み直す")) LoadMapping();
            return;
        }

        DrawFetchSection();
        DrawPasteSection();
        EditorGUILayout.Space();
        DrawResultSection();
    }

    private void DrawFetchSection()
    {
        EditorGUILayout.LabelField("Confluenceから取得", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("ページ", $"{_mapping.site} / {_mapping.pageId}");

        EditorGUI.BeginChangeCheck();
        _email = EditorGUILayout.TextField("メールアドレス", _email);
        _token = EditorGUILayout.PasswordField("APIトークン", _token);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString(_emailPrefKey, _email);
            EditorPrefs.SetString(_tokenPrefKey, _token);
        }

        bool canFetch = !_isFetching && !string.IsNullOrEmpty(_email) && !string.IsNullOrEmpty(_token);
        using (new EditorGUI.DisabledScope(!canFetch))
        {
            if (GUILayout.Button(_isFetching ? "取得中..." : "取得して比較")) Fetch();
        }
    }

    private void DrawPasteSection()
    {
        _showPaste = EditorGUILayout.Foldout(_showPaste, "貼り付けで読み込む(トークンを使わない場合)", true);
        if (!_showPaste) return;

        EditorGUILayout.HelpBox("ブラウザでデータ表のページを見出しごと選んでコピーし、下に貼り付けてください", MessageType.None);
        _pasteText = EditorGUILayout.TextArea(_pasteText, GUILayout.Height(100));
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_pasteText)))
        {
            if (GUILayout.Button("貼り付けて比較"))
            {
                LoadMapping();
                SetTables(CSED_SpecSyncTableParser.ParseText(_pasteText), "貼り付けた内容");
            }
        }
    }

    private void DrawResultSection()
    {
        if (!string.IsNullOrEmpty(_resultMessage)) EditorGUILayout.HelpBox(_resultMessage, _resultMessageType);
        if (_entries.Count == 0) return;

        _showSame = EditorGUILayout.ToggleLeft("一致している項目も表示する", _showSame);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        string currentHeading = null;
        foreach (CSED_SpecSyncEntry entry in _entries)
        {
            if (entry.status == CSE_SpecSyncStatus.Same && !_showSame) continue;

            // 表が変わったら見出しを出す
            if (entry.tableHeading != currentHeading)
            {
                currentHeading = entry.tableHeading;
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(currentHeading, EditorStyles.boldLabel);
            }
            DrawEntry(entry);
        }
        EditorGUILayout.EndScrollView();

        DrawApplyButtons();
    }

    private void DrawEntry(CSED_SpecSyncEntry entry)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (entry.status == CSE_SpecSyncStatus.Changed) entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(18));
            else GUILayout.Space(22);

            string label = string.IsNullOrEmpty(entry.rowKey) ? entry.column : $"{entry.rowKey} / {entry.column}";
            EditorGUILayout.LabelField(label, GUILayout.Width(220));
            EditorGUILayout.LabelField(GetValueText(entry), GUILayout.Width(140));
            EditorGUILayout.LabelField(GetDetailText(entry), EditorStyles.miniLabel);
        }
    }

    private void DrawApplyButtons()
    {
        List<CSED_SpecSyncEntry> changed = _entries.Where(e => e.status == CSE_SpecSyncStatus.Changed).ToList();
        int selectedCount = changed.Count(e => e.selected);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("すべて選択")) changed.ForEach(e => e.selected = true);
            if (GUILayout.Button("すべて解除")) changed.ForEach(e => e.selected = false);

            using (new EditorGUI.DisabledScope(selectedCount == 0))
            {
                if (GUILayout.Button($"選んだ{selectedCount}件を適用")) ApplySelected(selectedCount);
            }
        }
    }

    private void ApplySelected(int selectedCount)
    {
        if (!EditorUtility.DisplayDialog("仕様データ同期", $"{selectedCount}件の値をアセットに書き込みます。よろしいですか？", "適用", "やめる")) return;

        int count = CSED_SpecSyncComparer.Apply(_entries);
        SetTables(_lastTables, "前回読み込んだ内容");
        _resultMessage = $"{count}件を書き込みました(Ctrl+Zで戻せます)。" + _resultMessage;
    }

    private void Fetch()
    {
        LoadMapping();
        if (_mapping == null) return;

        _isFetching = true;
        _resultMessage = "";
        CSED_SpecSyncConfluenceClient.FetchPageHtml(_mapping.site, _mapping.pageId, _email, _token,
            html =>
            {
                _isFetching = false;
                SetTables(CSED_SpecSyncTableParser.ParseHtml(html), "Confluence");
                Repaint();
            },
            error =>
            {
                _isFetching = false;
                _resultMessage = error;
                _resultMessageType = MessageType.Error;
                Repaint();
            });
    }

    // 読み込んだ表をアセットと比べ、結果を表示用に持つ
    private void SetTables(List<CSED_SpecSyncTable> tables, string sourceName)
    {
        _lastTables = tables;
        if (tables == null || tables.Count == 0)
        {
            _entries = new List<CSED_SpecSyncEntry>();
            _resultMessage = $"{sourceName}から表が見つかりませんでした";
            _resultMessageType = MessageType.Warning;
            return;
        }

        _entries = CSED_SpecSyncComparer.Compare(_mapping, tables);
        int changed = _entries.Count(e => e.status == CSE_SpecSyncStatus.Changed);
        int errors = _entries.Count(e => e.status == CSE_SpecSyncStatus.Error);
        _resultMessage = $"{sourceName}から{tables.Count}個の表を読み込みました。差分 {changed}件";
        if (errors > 0) _resultMessage += $"、エラー {errors}件";
        _resultMessageType = errors > 0 ? MessageType.Warning : MessageType.Info;
    }

    private void LoadMapping()
    {
        _mapping = CSED_SpecSyncMapping.Load(out _mappingError);
    }

    private static string GetValueText(CSED_SpecSyncEntry entry)
    {
        switch (entry.status)
        {
            case CSE_SpecSyncStatus.Changed: return $"{entry.currentValue} → {entry.newValue}";
            case CSE_SpecSyncStatus.Same: return entry.currentValue.ToString();
            default: return entry.specText;
        }
    }

    private static string GetDetailText(CSED_SpecSyncEntry entry)
    {
        switch (entry.status)
        {
            case CSE_SpecSyncStatus.Changed: return $"{entry.targetName}(仕様: {entry.specText})";
            case CSE_SpecSyncStatus.Same: return entry.targetName;
            case CSE_SpecSyncStatus.Unmapped: return "未対応: " + entry.message;
            case CSE_SpecSyncStatus.Skipped: return "同期しない: " + entry.message;
            default: return entry.message;
        }
    }
}
