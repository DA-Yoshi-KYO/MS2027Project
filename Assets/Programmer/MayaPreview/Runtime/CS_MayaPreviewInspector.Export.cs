using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Export
 Summary: Export panel (next to View, or packed left when View is closed) and the export
          dialog in the middle of the screen. Japanese only.
          - Drag objects / Maya groups from the Hierarchy into the list (or 選択中を追加).
          - The name is the name of the first object in the list (click the name in the
            Inspector to change it); the file names follow the rules of
            MayaPreviewExportTypes.json (FBX / Prefab: UpperCamel, Material: MT_<type>_<name>).
          - 書き出す opens the dialog: choose what it is (キャラクター, 背景 ... from
            MayaPreviewExportTypes.json), then 書き出す. Everything goes into Assets/Designer,
            one folder per type (Mesh/<type>, Texture/<type>, Material/<type>, Prefab/<type>,
            Json/<type>).
          - The export remembers the Maya scene (.mb). When Maya opens that scene again its last
            export is restored (material settings, offsets, list, renames, type), and the
            dialog asks first: 上書きする (same place) or いいえ (choose another type).
 History: 2026/10/11 New
          2026/10/11 FBX into the project folder, folder remembered
          2026/11/09 Export types, no folder field, restore and overwrite per Maya scene
          2026/11/09 Dialog in the middle, Japanese only, name from the Inspector, name rules
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private const float RestoreDelay = 1.5f;      // let Maya's meshes and materials arrive first
    private const float DialogWidth = 440f;

    private enum ExportStep { Closed, AskScene, ChooseType, AskSameName }

    private readonly List<CS_MayaPreviewExport.Item> _exportItems = new List<CS_MayaPreviewExport.Item>();
    private string _exportType;
    private string _exportStatus = "";
    private List<CS_MayaPreviewExport.Saved> _savedExports = new List<CS_MayaPreviewExport.Saved>();
    private float _nextExportScan;
    private Vector2 _exportScroll;
    private ExportStep _exportStep;
    private CS_MayaPreviewExport.Saved _exportPrevious;   // the export the question is about

    private bool ExportDialogOpen => _exportStep != ExportStep.Closed;

    private void AddExportItems(List<CS_MayaPreviewExport.Item> items)
    {
        foreach (CS_MayaPreviewExport.Item item in items)
        {
            if (item == null || (item.mayaGroup == null && item.target == null)) continue;
            if (_exportItems.Exists(existing => existing.Same(item))) continue;
            _exportItems.Add(item);
        }
    }

    private void AddSelectedToExport()
    {
        var items = new List<CS_MayaPreviewExport.Item>();
        if (_selection.GroupPath != null)
            items.Add(new CS_MayaPreviewExport.Item { label = _selection.GroupLabel, mayaGroup = _selection.GroupPath, kind = CS_MayaPreviewKind.MayaGroup });
        else
            foreach (GameObject o in _selection.Selected)
                items.Add(new CS_MayaPreviewExport.Item { label = DisplayName(o), target = o, kind = CS_MayaPreviewIcons.KindOf(o.transform) });
        AddExportItems(items);
    }

    // Current name of a list row (renames in the Inspector show at once).
    private static string ItemName(CS_MayaPreviewExport.Item item)
    {
        if (item.mayaGroup != null) return CS_MayaPreviewExport.NameOf(item.mayaGroup);
        if (item.target != null) return DisplayName(item.target);
        return item.label;
    }

    private string ObjectName => _exportItems.Count > 0 ? ItemName(_exportItems[0]) : "";

    private static string TypeLabel(string id)
    {
        CS_MayaPreviewExportType type = CS_MayaPreviewExport.FindType(id);
        return type != null ? type.label : string.IsNullOrEmpty(id) ? "（種類なし）" : id;
    }

    private static string Place(CS_MayaPreviewExport.Saved saved) => TypeLabel(saved.type) + " / " + saved.name;


    // =========================================================
    // Restore the last export of the scene Maya opened (Update)
    // =========================================================

    private void TickExportRestore()
    {
        string file = CS_MayaPreviewExport.PendingRestore;
        if (file == null || Time.unscaledTime < CS_MayaPreviewExport.SceneTime + RestoreDelay) return;
        if (CS_MayaPreviewExport.LiveObjects().Count == 0) return;   // Maya has not sent the meshes yet
        CS_MayaPreviewExport.ClearPendingRestore();
        LoadExport(file, "このシーンの前回の書き出しを復元しました");
    }

    private void LoadExport(string file, string what)
    {
        CS_MayaPreviewExportManifest manifest = CS_MayaPreviewExport.ReadManifest(file);
        string result = CS_MayaPreviewExport.Load(file, _exportItems);
        if (manifest != null && CS_MayaPreviewExport.FindType(manifest.type) != null) _exportType = manifest.type;
        _exportStatus = what + "（" + (manifest != null ? TypeLabel(manifest.type) + " / " + manifest.name : file) + "）\n" + result;
        _nextExportScan = 0f;
        _nextScan = 0f;   // the Hierarchy shows restored names
        ClearFields();
    }


    // =========================================================
    // Export steps
    // =========================================================

    private void OpenExportDialog(string root)
    {
        _savedExports = CS_MayaPreviewExport.FindExports(root);
        // This .mb was exported before: overwrite it, or choose another type.
        if (CS_MayaPreviewExport.FindByScene(_savedExports, CS_MayaPreviewExport.Scene, out CS_MayaPreviewExport.Saved previous))
        {
            _exportPrevious = previous;
            _exportStep = ExportStep.AskScene;
        }
        else _exportStep = ExportStep.ChooseType;
    }

    private void ChooseExport(string root)
    {
        CS_MayaPreviewExportType type = CS_MayaPreviewExport.FindType(_exportType);
        if (type == null) return;
        string name = CS_MayaPreviewExport.ExportName(type, ObjectName);
        // Another export already uses this type / name.
        if (File.Exists(CS_MayaPreviewExport.ManifestPath(root, type.id, name)))
        {
            _exportPrevious = new CS_MayaPreviewExport.Saved { type = type.id, name = name };
            _exportStep = ExportStep.AskSameName;
            return;
        }
        RunExport(root, type.id, name);
    }

    private void RunExport(string root, string typeId, string name)
    {
        _exportStep = ExportStep.Closed;
        _exportType = typeId;
        _exportStatus = CS_MayaPreviewExport.Export(_exportItems, name, root, CS_MayaPreviewExport.FindType(typeId));
        _nextExportScan = 0f;
        ClearFields();
    }

    // Overwrite the export the question is about, at the same place.
    private void Overwrite(string root)
    {
        // An old export (no type) goes to the type chosen now.
        string type = CS_MayaPreviewExport.FindType(_exportPrevious.type) != null ? _exportPrevious.type : _exportType;
        if (CS_MayaPreviewExport.FindType(type) == null) { _exportStep = ExportStep.ChooseType; return; }
        RunExport(root, type, _exportPrevious.name);
    }


    // =========================================================
    // Dialog (middle of the screen, modal)
    // =========================================================

    private void DrawExportDialog()
    {
        string root = CS_MayaPreviewExport.ProjectFolder();
        if (root == null) { _exportStep = ExportStep.Closed; return; }
        CS_MayaPreviewExportType[] types = CS_MayaPreviewExport.Types();
        if (CS_MayaPreviewExport.FindType(_exportType) == null) _exportType = types.Length > 0 ? types[0].id : null;
        CS_MayaPreviewExportType current = CS_MayaPreviewExport.FindType(_exportType);

        int typeRows = (types.Length + 1) / 2;
        float height = _exportStep == ExportStep.ChooseType ? 132f + typeRows * 30f : 128f;
        Vector2 screen = new Vector2(Screen.width, Screen.height) / _scale;
        Rect box = new Rect(Mathf.Round((screen.x - DialogWidth) * 0.5f), Mathf.Round((screen.y - height) * 0.5f), DialogWidth, height);

        Fill(new Rect(0, 0, screen.x, screen.y), new Color(0f, 0f, 0f, 0.45f));
        Fill(new Rect(box.x - 1, box.y - 1, box.width + 2, box.height + 2), AccentColor);
        Fill(box, PanelColor);
        Fill(new Rect(box.x, box.y, box.width, 26), HeaderColor);
        GUI.Label(new Rect(box.x + 10, box.y + 2, box.width - 20, 22), "書き出し", _bold);

        float x = box.x + 12, width = box.width - 24, y = box.y + 34;
        float half = (width - 6) * 0.5f;
        switch (_exportStep)
        {
            case ExportStep.AskScene:
            case ExportStep.AskSameName:
            {
                string question = _exportStep == ExportStep.AskScene
                    ? "この mb は前回「" + Place(_exportPrevious) + "」に書き出しています。\n上書きしますか？"
                    : "「" + Place(_exportPrevious) + "」はすでにあります。\n上書きしますか？";
                GUI.Label(new Rect(x, y, width, 46), question, _wrapDim);
                y = box.yMax - 40;
                float third = (width - 12) / 3f;
                if (GUI.Button(new Rect(x, y, third, 28), "上書きする", _bigButton)) Overwrite(root);
                string no = _exportStep == ExportStep.AskScene ? "いいえ（種類を選ぶ）" : "戻る";
                if (GUI.Button(new Rect(x + third + 6, y, third, 28), no, _button)) _exportStep = ExportStep.ChooseType;
                if (GUI.Button(new Rect(x + (third + 6) * 2, y, third, 28), "キャンセル", _button)) _exportStep = ExportStep.Closed;
                break;
            }
            case ExportStep.ChooseType:
            {
                GUI.Label(new Rect(x, y, width, 20), "何を書き出しますか？", _bold);
                y += 24;
                for (int i = 0; i < types.Length; i++)
                {
                    Rect button = new Rect(x + (i % 2) * (half + 6), y + (i / 2) * 30f, half, 26);
                    bool on = types[i] == current;
                    if (GUI.Button(button, types[i].label, on ? _bigButton : _button)) _exportType = types[i].id;
                }
                y += typeRows * 30f + 4;
                if (current != null)
                {
                    string name = CS_MayaPreviewExport.ExportName(current, ObjectName);
                    string where = "名前: " + name + "\n" + CS_MayaPreviewExport.FbxFile(current, name) +
                                   (current.materials ? "、Material/" + current.id + "、Texture/" + current.id : "（マテリアルなし）") +
                                   (current.prefab ? "、Prefab/" + current.id : "（Prefab なし）");
                    GUI.Label(new Rect(x, y, width, 40), where, _wrapDim);
                }
                y = box.yMax - 40;
                GUI.enabled = current != null;
                if (GUI.Button(new Rect(x, y, half, 28), "書き出す", _bigButton)) ChooseExport(root);
                GUI.enabled = true;
                if (GUI.Button(new Rect(x + half + 6, y, half, 28), "キャンセル", _button)) _exportStep = ExportStep.Closed;
                break;
            }
        }
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { _exportStep = ExportStep.Closed; e.Use(); }
    }


    // =========================================================
    // Panel
    // =========================================================

    private void DrawExport()
    {
        Rect content = DrawWindow(_exportRect, "書き出し", _exportItems.Count > 0 ? _exportItems.Count + " 個" : null);
        string root = CS_MayaPreviewExport.ProjectFolder();
        if (Time.unscaledTime >= _nextExportScan)
        {
            _nextExportScan = Time.unscaledTime + 2f;
            _savedExports = CS_MayaPreviewExport.FindExports(root);
        }
        _labelWidth = 54f;
        _fieldScope = "Export.";

        GUILayout.BeginArea(new Rect(content.x + 6, content.y + 4, content.width - 12, content.height - 6));
        _exportScroll = GUILayout.BeginScrollView(_exportScroll, false, false, GUIStyle.none, _skin.verticalScrollbar, GUIStyle.none);
        // Keep everything inside the panel width (long paths must not widen the layout).
        GUILayout.BeginVertical(GUILayout.Width(content.width - 24));

        GUILayout.Label("書き出すオブジェクト", _bold);
        DrawDropList();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("選択中を追加", _button, GUILayout.Height(RowHeight + 2))) AddSelectedToExport();
        if (GUILayout.Button("クリア", _button, GUILayout.Width(60), GUILayout.Height(RowHeight + 2))) _exportItems.Clear();
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label(_exportItems.Count > 0
            ? "名前: " + ObjectName + "（Inspector の名前をクリックで変更）"
            : "名前: リストの最初のオブジェクトの名前になります", _wrapDim);
        GUILayout.Label(string.IsNullOrEmpty(CS_MayaPreviewExport.Scene)
            ? "Maya シーン: 未保存（mb を保存すると書き出しと紐付きます）"
            : "Maya シーン: " + Path.GetFileName(CS_MayaPreviewExport.Scene), _wrapDim);

        GUILayout.Space(4);
        if (root == null)
            GUILayout.Label("Unity プロジェクトが見つかりません（MayaPreviewProject.txt）。プロジェクトから exe をビルドしてください。", _wrapDim);
        else
        {
            GUI.enabled = _exportItems.Count > 0;
            if (GUILayout.Button("書き出す…", _bigButton, GUILayout.Height(26))) OpenExportDialog(root);
            GUI.enabled = true;
            if (GUILayout.Button("書き出し先（Assets/Designer）を開く", _button, GUILayout.Height(RowHeight + 2))) OpenFolder(root);
        }
        if (!string.IsNullOrEmpty(_exportStatus)) GUILayout.Label(_exportStatus, _wrapDim);

        GUILayout.Space(10);
        Fill(GUILayoutUtility.GetRect(10, 1, GUILayout.ExpandWidth(true)), BorderColor);
        GUILayout.Space(4);
        GUILayout.Label("書き出し履歴", _bold);
        GUILayout.Label("読み込み: その書き出しのマテリアル設定・位置・名前・種類を戻します。", _wrapDim);
        if (_savedExports.Count == 0) GUILayout.Label("まだありません", _dim);
        foreach (CS_MayaPreviewExport.Saved saved in _savedExports)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight + 2));
            Rect label = GUILayoutUtility.GetRect(10, RowHeight + 2, GUILayout.ExpandWidth(true));
            bool mine = CS_MayaPreviewExport.SameScene(saved.scene, CS_MayaPreviewExport.Scene);
            GUI.Label(label, Fit((mine ? "● " : "") + Place(saved), _row, label.width - 70), _row);
            GUI.Label(label, saved.time.ToString("MM/dd HH:mm"), _smallRight);
            if (GUILayout.Button("読み込み", _button, GUILayout.Width(64), GUILayout.Height(RowHeight + 2)))
                LoadExport(saved.file, "読み込みました");
            GUILayout.EndHorizontal();
        }
        if (_savedExports.Count > 0) GUILayout.Label("● = 今 Maya で開いているシーンの書き出し", _dim);

        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawDropList()
    {
        float height = Mathf.Max(56f, Mathf.Min(_exportItems.Count * 20f + 8f, 220f));
        Rect box = GUILayoutUtility.GetRect(10, height, GUILayout.ExpandWidth(true));
        bool hover = _drag != null && box.Contains(Event.current.mousePosition);
        Fill(new Rect(box.x - 1, box.y - 1, box.width + 2, box.height + 2), hover ? AccentColor : BorderColor);
        Fill(box, hover ? new Color(0.17f, 0.24f, 0.32f, 1f) : FieldColor);
        if (_exportItems.Count == 0)
        {
            GUI.Label(box, _drag != null ? "ここにドロップ" : "Hierarchy からここへドラッグ", _wrapCenter);
            return;
        }
        int remove = -1;
        for (int i = 0; i < _exportItems.Count; i++)
        {
            CS_MayaPreviewExport.Item item = _exportItems[i];
            Rect row = new Rect(box.x + 4, box.y + 4 + i * 20f, box.width - 8, 20f);
            if (row.yMax > box.yMax) break;
            if (i % 2 == 1) Fill(row, StripeColor);
            bool missing = item.mayaGroup == null && item.target == null;
            DrawTinted(new Rect(row.x + 2, row.y + 2, 16, 16), CS_MayaPreviewIcons.Get(item.kind), CS_MayaPreviewIcons.Tint(item.kind));
            string text = ItemName(item) + (missing ? "（見つかりません）" : "");
            GUI.Label(new Rect(row.x + 22, row.y, row.width - 44, row.height), Fit(text, _row, row.width - 44), missing ? _rowDim : _row);
            Rect close = new Rect(row.xMax - 18, row.y + 3, 14, 14);
            if (GUI.Button(close, GUIContent.none, GUIStyle.none)) remove = i;
            DrawTinted(new Rect(close.x + 2, close.y + 2, 10, 10), CS_MayaPreviewIcons.Close, close.Contains(Event.current.mousePosition) ? Color.white : DimColor);
        }
        if (remove >= 0) _exportItems.RemoveAt(remove);
    }

    private void OpenFolder(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) { _exportStatus = "フォルダーがまだありません: " + folder; return; }
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }
        catch (Exception exception) { _exportStatus = exception.Message; }
    }
}
