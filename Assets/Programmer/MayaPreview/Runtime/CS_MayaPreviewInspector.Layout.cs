using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Layout
 Summary: Panel columns.
            left : Hierarchy / Performance / View / Export   (top to bottom)
            right: Inspector / Material / Graphics           (top to bottom)
          - Every panel is always there; the arrow at the right end of its title bar
            (or a click on the title bar) folds it to the title bar alone.
            Open: arrow down. Folded: arrow up.
          - Drag the gap under an open panel up / down to change its height
            (the next open panel below gives / takes the space).
          - Open panels share the column height in proportion to their sizes.
          - Only the MayaScaler / SelectLine check buttons remain, at the top just left
            of the right column.
          - Open / folded state and sizes are remembered between runs.
 History: 2026/11/05 New (replaces the panel buttons and drop-downs)
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private enum PanelId { Hierarchy, Performance, View, Export, Inspector, Material, Graphics, Count }

    private static readonly PanelId[] LeftColumn = { PanelId.Hierarchy, PanelId.Performance, PanelId.View, PanelId.Export };
    private static readonly PanelId[] RightColumn = { PanelId.Inspector, PanelId.Material, PanelId.Graphics };

    // v2: new default panels (Performance / View / Graphics open); old saved layouts start over once.
    private const string PanelPrefsKey = "MayaPreview.Panels.v2";
    private const float MinBody = 40f;
    private const float SplitterHeight = 8f;

    // Preferred body heights (column height is shared in proportion to these).
    private readonly float[] _panelSize = { 420f, 360f, 230f, 420f, 220f, 420f, 380f };
    private readonly Rect[] _panelRect = new Rect[(int)PanelId.Count];
    private float _leftScale = 1f, _rightScale = 1f;

    // Splitter drag: the open panel above the gap and the next open panel below it.
    private int _splitUpper = -1, _splitLower = -1;
    private float _splitScale = 1f;
    private bool _panelsLoaded;

    private bool PanelOpen(PanelId id)
    {
        switch (id)
        {
            case PanelId.Hierarchy: return _hierarchyOpen;
            case PanelId.Performance: return _performanceOpen;
            case PanelId.View: return _viewOpen;
            case PanelId.Export: return _exportOpen;
            case PanelId.Inspector: return _inspectorOpen;
            case PanelId.Material: return _materialOpen;
            case PanelId.Graphics: return _graphicsOpen;
        }
        return false;
    }

    private void SetPanelOpen(PanelId id, bool open)
    {
        switch (id)
        {
            case PanelId.Hierarchy: _hierarchyOpen = open; break;
            case PanelId.Performance: SetPerformanceOpen(open); break;
            case PanelId.View: _viewOpen = open; break;
            case PanelId.Export: _exportOpen = open; break;
            case PanelId.Inspector: _inspectorOpen = open; break;
            case PanelId.Material: _materialOpen = open; break;
            case PanelId.Graphics: _graphicsOpen = open; break;
        }
        SavePanels();
    }

    private static string PanelTitle(PanelId id) => id.ToString();

    private void LoadPanels()
    {
        _panelsLoaded = true;
        string saved = PlayerPrefs.GetString(PanelPrefsKey, "");
        if (string.IsNullOrEmpty(saved)) { SetPerformanceOpen(true); return; }   // first run
        string[] parts = saved.Split(',');
        for (int i = 0; i < parts.Length && i < (int)PanelId.Count; i++)
        {
            string[] pair = parts[i].Split(':');
            if (pair.Length != 2) continue;
            if (float.TryParse(pair[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float size))
                _panelSize[i] = Mathf.Max(MinBody, size);
            bool open = pair[0] == "1";
            if ((PanelId)i == PanelId.Performance) SetPerformanceOpen(open);
            else SetPanelOpenSilently((PanelId)i, open);
        }
    }

    private void SetPanelOpenSilently(PanelId id, bool open)
    {
        if (id == PanelId.Performance) { SetPerformanceOpen(open); return; }
        switch (id)
        {
            case PanelId.Hierarchy: _hierarchyOpen = open; break;
            case PanelId.View: _viewOpen = open; break;
            case PanelId.Export: _exportOpen = open; break;
            case PanelId.Inspector: _inspectorOpen = open; break;
            case PanelId.Material: _materialOpen = open; break;
            case PanelId.Graphics: _graphicsOpen = open; break;
        }
    }

    private void SavePanels()
    {
        if (!_panelsLoaded) return;
        var parts = new string[(int)PanelId.Count];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = (PanelOpen((PanelId)i) ? "1" : "0") + ":" + _panelSize[i].ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        PlayerPrefs.SetString(PanelPrefsKey, string.Join(",", parts));
        PlayerPrefs.Save();
    }

    private void Layout()
    {
        float top = Margin;
        float leftWidth = Mathf.Min(HierarchyWidth, VW * 0.3f);
        float rightWidth = Mathf.Min(RightWidth, VW * 0.42f);
        float rightX = VW - Margin - rightWidth;

        // Check buttons: top, just left of the right column (not above it).
        _outlineButton = new Rect(rightX - PanelGap - 104f, top, 104f, ButtonHeight);
        _mayaScalerButton = new Rect(_outlineButton.x - ButtonGap - 112f, top, 112f, ButtonHeight);

        _leftScale = LayoutColumn(LeftColumn, Margin, top, leftWidth);
        _rightScale = LayoutColumn(RightColumn, rightX, top, rightWidth);

        _hierarchyRect = _panelRect[(int)PanelId.Hierarchy];
        _performanceRect = _panelRect[(int)PanelId.Performance];
        _viewRect = _panelRect[(int)PanelId.View];
        _exportRect = _panelRect[(int)PanelId.Export];
        _inspectorRect = _panelRect[(int)PanelId.Inspector];
        _materialRect = _panelRect[(int)PanelId.Material];
        _graphicsRect = _panelRect[(int)PanelId.Graphics];
    }

    // Returns pixels per size unit for the column (used by the splitter drag).
    private float LayoutColumn(PanelId[] column, float x, float top, float width)
    {
        float available = Mathf.Max(60f, VH - Margin - top);
        float headers = column.Length * HeaderHeight + (column.Length - 1) * PanelGap;
        float sum = 0f;
        foreach (PanelId id in column) if (PanelOpen(id)) sum += _panelSize[(int)id];
        float scale = sum > 0f ? Mathf.Max(0f, available - headers) / sum : 1f;

        float y = top;
        foreach (PanelId id in column)
        {
            float body = PanelOpen(id) ? _panelSize[(int)id] * scale : 0f;
            _panelRect[(int)id] = new Rect(x, y, width, HeaderHeight + body);
            y += HeaderHeight + body + PanelGap;
        }
        return scale;
    }

    private bool OverPanel(Vector2 point)
    {
        if (_outlineButton.Contains(point) || _mayaScalerButton.Contains(point)) return true;
        foreach (Rect rect in _panelRect) if (rect.Contains(point)) return true;
        return false;
    }

    private bool OverlapsPanel(Rect rect)
    {
        if (_outlineButton.Overlaps(rect) || _mayaScalerButton.Overlaps(rect)) return true;
        foreach (Rect panel in _panelRect) if (panel.Overlaps(rect)) return true;
        return false;
    }

    // Header arrow / click on the title bar: fold or open. Drawn after the panel.
    private void DrawPanelHeader(PanelId id)
    {
        Rect rect = _panelRect[(int)id];
        Rect header = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
        bool open = PanelOpen(id);
        if (!open)
        {
            // Folded: title bar only.
            Fill(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, HeaderHeight + 2), BorderColor);
            Fill(header, HeaderColor);
            GUI.Label(new Rect(header.x + 8, header.y, header.width - 30, header.height), PanelTitle(id), _title);
        }
        Rect arrow = new Rect(header.xMax - 20, header.y + 6, 10, 10);
        bool hover = header.Contains(Event.current.mousePosition);
        DrawTinted(arrow, open ? CS_MayaPreviewIcons.ArrowDown : CS_MayaPreviewIcons.ArrowUp, hover ? Color.white : DimColor);
        if (GUI.Button(header, GUIContent.none, GUIStyle.none))
        {
            GUI.FocusControl(null);
            _typing = false;
            SetPanelOpen(id, !open);
        }
    }

    // Gaps under open panels: drag to resize. Called before the panels so it gets the mouse first.
    private void HandleSplitters()
    {
        Event e = Event.current;
        if (_splitUpper >= 0)
        {
            if (e.type == EventType.MouseDrag)
            {
                float delta = e.delta.y / Mathf.Max(0.0001f, _splitScale);
                float minSize = MinBody / Mathf.Max(0.0001f, _splitScale);
                delta = Mathf.Clamp(delta, minSize - _panelSize[_splitUpper], _panelSize[_splitLower] - minSize);
                _panelSize[_splitUpper] += delta;
                _panelSize[_splitLower] -= delta;
                e.Use();
            }
            else if (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp)
            {
                _splitUpper = _splitLower = -1;
                SavePanels();
                e.Use();
            }
            return;
        }
        Splitters(LeftColumn, _leftScale, e);
        Splitters(RightColumn, _rightScale, e);
    }

    private void Splitters(PanelId[] column, float scale, Event e)
    {
        for (int i = 0; i < column.Length; i++)
        {
            if (!PanelOpen(column[i])) continue;
            int below = -1;
            for (int j = i + 1; j < column.Length; j++) if (PanelOpen(column[j])) { below = j; break; }
            if (below < 0) return;
            Rect upper = _panelRect[(int)column[i]];
            Rect zone = new Rect(upper.x, upper.yMax - SplitterHeight * 0.5f + PanelGap * 0.5f, upper.width, SplitterHeight);
            bool hover = zone.Contains(e.mousePosition);
            if (hover && e.type == EventType.Repaint)
                Fill(new Rect(zone.x + zone.width * 0.35f, upper.yMax + PanelGap * 0.5f - 1, zone.width * 0.3f, 2), AccentColor);
            if (hover && e.type == EventType.MouseDown && e.button == 0)
            {
                _splitUpper = (int)column[i];
                _splitLower = (int)column[below];
                _splitScale = scale;
                GUI.FocusControl(null);
                e.Use();
            }
        }
    }
}
