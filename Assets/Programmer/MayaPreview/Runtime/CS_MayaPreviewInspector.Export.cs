using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Export
 Summary: Export panel (next to View, or packed left when View is closed).
          - Drag objects / Maya groups from the Hierarchy into the list (or "Add selected").
          - Export writes straight into the Unity project (default Assets/Designer):
            Mesh/<name>.fbx, Texture/*, MayaPreview/Exports/<name>.mpx.json; the Unity editor
            makes Material/MT_*.mat from it by itself (no menu).
          - Saved exports can be loaded back to continue the shader work from them.
 History: 2026/10/11 New
          2026/10/11 FBX into the project folder, folder remembered
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private readonly List<CS_MayaPreviewExport.Item> _exportItems = new List<CS_MayaPreviewExport.Item>();
    private string _exportName = "";
    private string _exportRoot;
    private string _exportStatus = "";
    private List<CS_MayaPreviewExport.Saved> _savedExports = new List<CS_MayaPreviewExport.Saved>();
    private float _nextExportScan;
    private Vector2 _exportScroll;

    private void AddExportItems(List<CS_MayaPreviewExport.Item> items)
    {
        foreach (CS_MayaPreviewExport.Item item in items)
        {
            if (item == null || (item.mayaGroup == null && item.target == null)) continue;
            if (_exportItems.Exists(existing => existing.Same(item))) continue;
            _exportItems.Add(item);
        }
        if (string.IsNullOrEmpty(_exportName) && _exportItems.Count > 0)
            _exportName = CS_MayaPreviewExport.SafeName(_exportItems[0].label);
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

    private void DrawExport()
    {
        Rect content = DrawWindow(_exportRect, "Export", _exportItems.Count > 0 ? _exportItems.Count + " in list" : null);
        if (string.IsNullOrEmpty(_exportRoot)) _exportRoot = CS_MayaPreviewExport.DefaultRoot();
        if (Time.unscaledTime >= _nextExportScan)
        {
            _nextExportScan = Time.unscaledTime + 2f;
            _savedExports = CS_MayaPreviewExport.FindExports(_exportRoot);
        }
        _labelWidth = 54f;
        _fieldScope = "Export.";

        GUILayout.BeginArea(new Rect(content.x + 6, content.y + 4, content.width - 12, content.height - 6));
        _exportScroll = GUILayout.BeginScrollView(_exportScroll, false, false, GUIStyle.none, _skin.verticalScrollbar, GUIStyle.none);
        // Keep everything inside the panel width (long paths must not widen the layout).
        GUILayout.BeginVertical(GUILayout.Width(content.width - 24));

        GUILayout.Label("Objects", _bold);
        DrawDropList();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add selected", _button, GUILayout.Height(RowHeight + 2))) AddSelectedToExport();
        if (GUILayout.Button("Clear", _button, GUILayout.Width(60), GUILayout.Height(RowHeight + 2))) _exportItems.Clear();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        BeginRow("Name");
        _exportName = TextBox("name", _exportName, 0, "Export name");
        EndRow();
        BeginRow("Folder", "FBX: <folder>/Mesh, textures: <folder>/Texture, materials: <folder>/Material");
        string root = TextBox("root", _exportRoot, 0);
        if (root != _exportRoot) { _exportRoot = root; _nextExportScan = 0f; }
        EndRow();
        string project = CS_MayaPreviewExport.ProjectFolder();
        if (project != null && CS_MayaPreviewExport.Normalize(_exportRoot) != project)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_labelWidth + 4);
            if (GUILayout.Button(new GUIContent("Use Assets/Designer", project), _button, GUILayout.Height(RowHeight + 2)))
            {
                _exportRoot = project;
                _nextExportScan = 0f;
                ClearFields();
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4);
        GUI.enabled = _exportItems.Count > 0;
        if (GUILayout.Button("Export", _bigButton, GUILayout.Height(26)))
        {
            string name = string.IsNullOrWhiteSpace(_exportName) && _exportItems.Count > 0 ? _exportItems[0].label : _exportName;
            _exportName = CS_MayaPreviewExport.SafeName(name);
            _exportRoot = CS_MayaPreviewExport.Normalize(_exportRoot);
            CS_MayaPreviewExport.RememberRoot(_exportRoot);
            _exportStatus = CS_MayaPreviewExport.Export(_exportItems, _exportName, _exportRoot);
            _nextExportScan = 0f;
        }
        GUI.enabled = true;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Open folder", _button, GUILayout.Height(RowHeight + 2))) OpenFolder(_exportRoot);
        if (GUILayout.Button("Open Mesh", _button, GUILayout.Height(RowHeight + 2))) OpenFolder(_exportRoot + "/" + CS_MayaPreviewExport.MeshFolder);
        GUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(_exportStatus)) GUILayout.Label(_exportStatus, _wrapDim);
        GUILayout.Label("FBX goes to Mesh/, textures to Texture/. Material names get \"MT_\". " +
                        "Unity makes Material/MT_*.mat and sets them on the FBX by itself when the editor gets focus.", _wrapDim);

        GUILayout.Space(10);
        Fill(GUILayoutUtility.GetRect(10, 1, GUILayout.ExpandWidth(true)), BorderColor);
        GUILayout.Space(4);
        GUILayout.Label("Saved exports", _bold);
        GUILayout.Label("Load: restores the Maya material settings and offsets of that export.", _wrapDim);
        if (_savedExports.Count == 0) GUILayout.Label("None yet", _dim);
        foreach (CS_MayaPreviewExport.Saved saved in _savedExports)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight + 2));
            Rect label = GUILayoutUtility.GetRect(10, RowHeight + 2, GUILayout.ExpandWidth(true));
            GUI.Label(label, Fit(saved.name, _row, label.width - 96), _row);
            GUI.Label(label, saved.time.ToString("MM/dd HH:mm"), _smallRight);
            if (GUILayout.Button("Load", _button, GUILayout.Width(46), GUILayout.Height(RowHeight + 2)))
            {
                _exportStatus = CS_MayaPreviewExport.Load(saved.file, _exportItems);
                _exportName = saved.name;
                ClearFields();
            }
            if (GUILayout.Button("Open", _button, GUILayout.Width(46), GUILayout.Height(RowHeight + 2)))
                OpenFolder(_exportRoot + "/" + CS_MayaPreviewExport.MeshFolder);
            GUILayout.EndHorizontal();
        }

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
            GUI.Label(box, _drag != null ? "Drop here" : "Drag objects from the Hierarchy here\n(Hierarchy からここへドラッグ)", _wrapCenter);
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
            string text = item.label + (missing ? "  (missing)" : "");
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
            if (!Directory.Exists(folder)) { _exportStatus = "Folder does not exist yet: " + folder; return; }
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }
        catch (Exception exception) { _exportStatus = exception.Message; }
    }
}
