using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/*
+=====================================
 File   : CS_MayaPreviewInspector
 Summary: Runtime GUI of the Maya preview player (Unity editor dark look).
          Toolbar  left : [Hierarchy] [View] [Export]   right : [Outline] [Material] [Inspector]
                   (Outline: yellow outline of the selected objects on / off)
          Panels   left : Hierarchy > View > Export, packed to the left in that order
                   right: Inspector at the top right, Material below it
                          (Material alone goes to the top right).
          - The Inspector lists the materials of the selection; Edit shows one of them in the
            Material panel (the Material panel is empty until a material is picked).
          - Type icons in the view for lights, cameras, volumes, audio and probes.
          - Ctrl+Z / Ctrl+Y (Ctrl+Shift+Z) undo / redo, Ctrl+D duplicate, Delete delete
            (.Edit), Tab hides every panel.
          - The whole GUI scales with the window (GUI.matrix).
          Split into partial files: .Hierarchy / .Properties / .Export / .Fields / .Edit
 History: 2026/10/11 Material panel, export, icons, undo, new hierarchy
          2026/10/11 Outline on/off button, Ctrl+D duplicate, Delete
=====================================+
*/
public partial class CS_MayaPreviewInspector : MonoBehaviour
{
    // =========================================================
    // References / state
    // =========================================================

    private CS_MayaPreviewSelection _selection;
    private CS_MayaPreviewWindow _window;
    private CS_MayaPreviewCameraController _controller;
    private CS_MayaPreviewTransformTool _tool;
    private CS_MayaPreviewGuides _guides;
    private Camera _view;

    private bool _hierarchyOpen = true;
    private bool _viewOpen;
    private bool _exportOpen;
    private bool _materialOpen = true;
    private bool _inspectorOpen = true;
    private bool _uiHidden;
    private bool _iconsVisible = true;
    private bool _typing;
    private string _tooltip;

    public bool UiHidden => _uiHidden;
    public bool Typing => _typing;


    // =========================================================
    // Layout (virtual GUI units; GUI.matrix scales them to the window)
    // =========================================================

    private const float RefWidth = 1280f;
    private const float RefHeight = 720f;
    private const float MinScale = 0.6f;
    private const float MaxScale = 1.25f;
    private const float Margin = 6f;
    private const float ButtonHeight = 22f;
    private const float ButtonGap = 2f;
    private const float PanelGap = 4f;
    private const float HeaderHeight = 22f;
    private const float RowHeight = 18f;
    private const float HierarchyWidth = 260f;
    private const float ViewWidth = 230f;
    private const float ExportWidth = 300f;
    private const float RightWidth = 420f;

    private float _scale = 1f;
    private float VW => Screen.width / _scale;
    private float VH => Screen.height / _scale;

    private Rect _hierarchyButton, _viewButton, _exportButton, _outlineButton, _materialButton, _inspectorButton;
    private Rect _hierarchyRect, _viewRect, _exportRect, _materialRect, _inspectorRect;
    private float _inspectorContentHeight = 120f;
    private readonly List<Rect> _iconRects = new List<Rect>();

    private void Layout()
    {
        float top = Margin;
        _hierarchyButton = new Rect(Margin, top, 104f, ButtonHeight);
        _viewButton = new Rect(_hierarchyButton.xMax + ButtonGap, top, 74f, ButtonHeight);
        _exportButton = new Rect(_viewButton.xMax + ButtonGap, top, 84f, ButtonHeight);
        _inspectorButton = new Rect(VW - Margin - 104f, top, 104f, ButtonHeight);
        _materialButton = new Rect(_inspectorButton.x - ButtonGap - 98f, top, 98f, ButtonHeight);
        _outlineButton = new Rect(_materialButton.x - ButtonGap - 92f, top, 92f, ButtonHeight);

        float panelTop = top + ButtonHeight + PanelGap;
        float full = Mathf.Max(60f, VH - Margin - panelTop);
        float rightWidth = Mathf.Min(RightWidth, VW * 0.42f);
        float rightX = VW - Margin - rightWidth;

        // Left: Hierarchy > View > Export, each packed against the previous open one.
        float x = Margin;
        _hierarchyRect = _viewRect = _exportRect = Rect.zero;
        if (_hierarchyOpen) { _hierarchyRect = new Rect(x, panelTop, Mathf.Min(HierarchyWidth, VW * 0.3f), full); x = _hierarchyRect.xMax + PanelGap; }
        if (_viewOpen) { _viewRect = new Rect(x, panelTop, ViewWidth, Mathf.Min(full, HeaderHeight + 214f)); x = _viewRect.xMax + PanelGap; }
        if (_exportOpen) { _exportRect = new Rect(x, panelTop, ExportWidth, full); }

        // Right: Inspector on top, Material below.
        _inspectorRect = _materialRect = Rect.zero;
        bool selected = _selection != null && _selection.Count > 0;
        float inspectorHeight = selected ? _inspectorContentHeight + HeaderHeight + 30f : HeaderHeight + 34f;
        if (_inspectorOpen && _materialOpen)
        {
            // Nothing picked for the Material panel: the Inspector may use almost all the height.
            float inspectorMax = _activeMaterialKey != null ? full * 0.5f : full - 60f - PanelGap;
            inspectorHeight = Mathf.Clamp(inspectorHeight, HeaderHeight + 34f, Mathf.Max(HeaderHeight + 34f, inspectorMax));
            _inspectorRect = new Rect(rightX, panelTop, rightWidth, inspectorHeight);
            float materialTop = _inspectorRect.yMax + PanelGap;
            _materialRect = new Rect(rightX, materialTop, rightWidth, Mathf.Max(60f, VH - Margin - materialTop));
        }
        else if (_inspectorOpen) _inspectorRect = new Rect(rightX, panelTop, rightWidth, Mathf.Min(full, Mathf.Max(inspectorHeight, selected ? full : 0f)));
        else if (_materialOpen) _materialRect = new Rect(rightX, panelTop, rightWidth, full);
    }

    private bool OverPanel(Vector2 point)
    {
        if (_hierarchyButton.Contains(point) || _viewButton.Contains(point) || _exportButton.Contains(point)
            || _outlineButton.Contains(point) || _materialButton.Contains(point) || _inspectorButton.Contains(point)) return true;
        if (_hierarchyOpen && _hierarchyRect.Contains(point)) return true;
        if (_viewOpen && _viewRect.Contains(point)) return true;
        if (_exportOpen && _exportRect.Contains(point)) return true;
        if (_inspectorOpen && _inspectorRect.Contains(point)) return true;
        if (_materialOpen && _materialRect.Contains(point)) return true;
        return false;
    }

    private bool OverlapsPanel(Rect rect)
    {
        if (_hierarchyButton.Overlaps(rect) || _viewButton.Overlaps(rect) || _exportButton.Overlaps(rect)
            || _outlineButton.Overlaps(rect) || _materialButton.Overlaps(rect) || _inspectorButton.Overlaps(rect)) return true;
        if (_hierarchyOpen && _hierarchyRect.Overlaps(rect)) return true;
        if (_viewOpen && _viewRect.Overlaps(rect)) return true;
        if (_exportOpen && _exportRect.Overlaps(rect)) return true;
        if (_inspectorOpen && _inspectorRect.Overlaps(rect)) return true;
        if (_materialOpen && _materialRect.Overlaps(rect)) return true;
        return false;
    }

    // Camera / transform tools ask this before using the mouse (screen position, y up).
    public bool BlocksInput(Vector2 screenPosition)
    {
        if (_uiHidden) return false;
        if (_typing || _drag != null || _previewDragging) return true;
        Vector2 point = new Vector2(screenPosition.x, Screen.height - screenPosition.y) / _scale;
        if (OverPanel(point)) return true;
        foreach (Rect rect in _iconRects) if (rect.Contains(point)) return true;
        return false;
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        _selection = GetComponent<CS_MayaPreviewSelection>();
        _view = GetComponent<Camera>();
        _controller = GetComponent<CS_MayaPreviewCameraController>();
        _tool = GetComponent<CS_MayaPreviewTransformTool>();
        _guides = GetComponent<CS_MayaPreviewGuides>();
        _window = GetComponent<CS_MayaPreviewWindow>();
        if (_window == null) _window = gameObject.AddComponent<CS_MayaPreviewWindow>();
        _selection.Changed += OnSelectionChanged;
        CS_MayaPreviewUndo.Applied += ClearFields;
        Scan();
    }

    private void OnDestroy()
    {
        if (_selection != null) _selection.Changed -= OnSelectionChanged;
        CS_MayaPreviewUndo.Applied -= ClearFields;
        foreach (var asset in _owned) if (asset != null) Destroy(asset);
        foreach (var copy in _sceneOriginals.Values) if (copy != null) Destroy(copy);
        if (_preview != null) Destroy(_preview.gameObject);
    }

    private void Update()
    {
        if (_tool == null) _tool = GetComponent<CS_MayaPreviewTransformTool>();
        if (_controller == null) _controller = GetComponent<CS_MayaPreviewCameraController>();
        if (Time.unscaledTime >= _nextScan) Scan();
        _selection.Prune();

        var mouse = Mouse.current;
        bool held = mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed);
        CS_MayaPreviewUndo.Tick(held);
        if (_drag != null && mouse != null && !mouse.leftButton.isPressed) _drag = null;
        if (_previewDragging && mouse != null && !mouse.leftButton.isPressed) _previewDragging = false;

        var keyboard = Keyboard.current;
        if (keyboard == null || _typing) return;
        if (keyboard.tabKey.wasPressedThisFrame) _uiHidden = !_uiHidden;
    }

    // Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z / Ctrl+D / Delete from GUI key events: they carry the
    // modifier state of the key press itself (a fast Ctrl+Y can be released before the next
    // frame polls it). Not while a text field has the focus.
    private void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown || _typing) return;
        bool control = e.control || e.command;
        if (control && e.keyCode == KeyCode.Z) { if (e.shift) Redo(); else Undo(); e.Use(); }
        else if (control && e.keyCode == KeyCode.Y) { Redo(); e.Use(); }
        else if (control && e.keyCode == KeyCode.D) { DuplicateSelection(); e.Use(); }
        else if (!control && !e.alt && e.keyCode == KeyCode.Delete) { DeleteSelection(); e.Use(); }
    }

    private void Undo()
    {
        string label = CS_MayaPreviewUndo.NextUndoLabel;
        if (CS_MayaPreviewUndo.Undo()) Flash("Undo: " + label);
    }

    private void Redo()
    {
        string label = CS_MayaPreviewUndo.NextRedoLabel;
        if (CS_MayaPreviewUndo.Redo()) Flash("Redo: " + label);
    }

    private string _flash;
    private float _flashUntil;
    private void Flash(string text) { _flash = text; _flashUntil = Time.unscaledTime + 1.6f; }

    private void OnSelectionChanged()
    {
        ClearFields();
        _inspectorScroll = Vector2.zero;
        ValidateActiveMaterial();
    }


    // =========================================================
    // OnGUI
    // =========================================================

    private void OnGUI()
    {
        if (Event.current.type == EventType.Layout) _iconRects.Clear();
        HandleShortcuts();
        if (_uiHidden) return;

        EnsureSkin();
        GUISkin previousSkin = GUI.skin;
        Matrix4x4 previousMatrix = GUI.matrix;
        _scale = Mathf.Clamp(Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight), MinScale, MaxScale);
        GUI.skin = _skin;
        GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
        _tooltip = null;
        if (_dropFocus) { GUI.FocusControl(null); _dropFocus = false; }

        Layout();
        HandleDrag();
        if (_iconsVisible) DrawWorldIcons();
        DrawToolbar();
        if (_hierarchyOpen) DrawHierarchy();
        if (_viewOpen) DrawView();
        if (_exportOpen) DrawExport();
        if (_inspectorOpen) DrawInspector();
        if (_materialOpen) DrawMaterialPanel();
        DrawDragGhost();
        DrawFlash();
        DrawTooltip();

        _typing = GUI.GetNameOfFocusedControl().StartsWith("F.");
        if (Event.current.type == EventType.MouseDown && !OverPanel(Event.current.mousePosition))
        { GUI.FocusControl(null); _typing = false; }

        GUI.matrix = previousMatrix;
        GUI.skin = previousSkin;
    }

    private void DrawToolbar()
    {
        if (ToolbarButton(_hierarchyButton, "Hierarchy", _hierarchyOpen)) _hierarchyOpen = !_hierarchyOpen;
        if (ToolbarButton(_viewButton, "View", _viewOpen)) _viewOpen = !_viewOpen;
        if (ToolbarButton(_exportButton, "Export", _exportOpen)) _exportOpen = !_exportOpen;
        if (ToolbarToggle(_outlineButton, "Outline", _selection.ShowOutlines, "Yellow outline of the selected objects: on / off"))
            _selection.ShowOutlines = !_selection.ShowOutlines;
        if (ToolbarButton(_materialButton, "Material", _materialOpen)) _materialOpen = !_materialOpen;
        if (ToolbarButton(_inspectorButton, "Inspector", _inspectorOpen)) _inspectorOpen = !_inspectorOpen;
    }

    private bool ToolbarButton(Rect rect, string text, bool open)
    {
        bool clicked = GUI.Button(rect, GUIContent.none, open ? _toolbarOn : _toolbar);
        Texture2D arrow = open ? CS_MayaPreviewIcons.ArrowDown : CS_MayaPreviewIcons.ArrowRight;
        DrawTinted(new Rect(rect.x + 8, rect.y + 6, 10, 10), arrow, open ? Color.white : DimColor);
        GUI.Label(new Rect(rect.x + 22, rect.y, rect.width - 26, rect.height), text, open ? _toolbarLabelOn : _toolbarLabel);
        if (clicked) { GUI.FocusControl(null); _typing = false; }
        return clicked;
    }

    // On / off button for a display setting (check box instead of the panel arrow).
    private static readonly Color OutlineYellow = new Color(1f, 0.85f, 0f, 1f);
    private bool ToolbarToggle(Rect rect, string text, bool on, string tooltip)
    {
        bool clicked = GUI.Button(rect, GUIContent.none, on ? _toolbarOn : _toolbar);
        Rect box = new Rect(rect.x + 7, rect.y + 5, 12, 12);
        Fill(box, on ? OutlineYellow : DimColor);
        Fill(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), FieldColor);
        if (on) Fill(new Rect(box.x + 3, box.y + 3, box.width - 6, box.height - 6), OutlineYellow);
        GUI.Label(new Rect(rect.x + 24, rect.y, rect.width - 28, rect.height), text, on ? _toolbarLabelOn : _toolbarLabel);
        if (rect.Contains(Event.current.mousePosition)) _tooltip = tooltip;
        if (clicked) { GUI.FocusControl(null); _typing = false; }
        return clicked;
    }

    // Panel background + title bar. Returns the content rect.
    private Rect DrawWindow(Rect rect, string title, string note = null)
    {
        Fill(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), BorderColor);
        Fill(rect, PanelColor);
        Rect header = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
        Fill(header, HeaderColor);
        Fill(new Rect(rect.x, header.yMax - 1, rect.width, 1), BorderColor);
        GUI.Label(new Rect(header.x + 8, header.y, header.width - 16, header.height), title, _title);
        if (!string.IsNullOrEmpty(note))
            GUI.Label(new Rect(header.x + 8, header.y, header.width - 16, header.height), note, _dimRight);
        // Clicks on an empty part of a panel must not reach the view.
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        { GUI.FocusControl(null); }
        return new Rect(rect.x + 2, header.yMax + 2, rect.width - 4, rect.height - HeaderHeight - 4);
    }

    private void DrawFlash()
    {
        if (string.IsNullOrEmpty(_flash) || Time.unscaledTime > _flashUntil) return;
        Vector2 size = _bold.CalcSize(new GUIContent(_flash));
        Rect rect = new Rect((VW - size.x) * 0.5f - 10, VH - 40, size.x + 20, 24);
        Fill(rect, new Color(0.1f, 0.1f, 0.1f, 0.85f));
        GUI.Label(rect, _flash, _center);
    }

    private void DrawTooltip()
    {
        if (string.IsNullOrEmpty(_tooltip) || Event.current.type != EventType.Repaint) return;
        Vector2 mouse = Event.current.mousePosition;
        Vector2 size = _small.CalcSize(new GUIContent(_tooltip));
        size.x = Mathf.Min(size.x + 12, 420);
        Rect rect = new Rect(mouse.x + 14, mouse.y + 16, size.x, 20);
        if (rect.xMax > VW - 4) rect.x = VW - 4 - rect.width;
        if (rect.yMax > VH - 4) rect.y = mouse.y - 24;
        Fill(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), BorderColor);
        Fill(rect, new Color(0.13f, 0.13f, 0.13f, 0.98f));
        GUI.Label(new Rect(rect.x + 6, rect.y, rect.width - 8, rect.height), _tooltip, _small);
    }


    // =========================================================
    // View panel (aspect ratio / window size / display)
    // =========================================================

    private void DrawView()
    {
        Rect content = DrawWindow(_viewRect, "View");
        GUILayout.BeginArea(new Rect(content.x + 6, content.y + 4, content.width - 12, content.height - 6));
        GUILayout.Label("Aspect ratio", _dim);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < CS_MayaPreviewWindow.AspectNames.Length; i++)
            if (GUILayout.Button(CS_MayaPreviewWindow.AspectNames[i], _window.AspectIndex == i ? _buttonOn : _button, GUILayout.Height(RowHeight + 2)))
                _window.SetAspect(i);
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        if (GUILayout.Button("Reset ratio", _button, GUILayout.Height(RowHeight + 2))) _window.ResetRatio();
        GUILayout.Space(6);
        GUILayout.Label("Window width", _dim);
        GUILayout.BeginHorizontal();
        foreach (int width in CS_MayaPreviewWindow.WidthPresets)
            if (GUILayout.Button(width.ToString(), Screen.width == width ? _buttonOn : _button, GUILayout.Height(RowHeight + 2)))
                _window.SetClientWidth(width);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        GUILayout.Label("Display", _dim);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Icons", _iconsVisible ? _buttonOn : _button, GUILayout.Height(RowHeight + 2))) _iconsVisible = !_iconsVisible;
        bool grid = _guides != null && _guides.enabled;
        if (_guides != null && GUILayout.Button("Grid", grid ? _buttonOn : _button, GUILayout.Height(RowHeight + 2))) _guides.enabled = !grid;
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        GUILayout.Label("Tab: hide UI   Ctrl+Z / Ctrl+Y: undo / redo   Ctrl+D: duplicate   Delete: delete   F: frame   A: frame all", _wrapDim);
        GUILayout.EndArea();
    }


    // =========================================================
    // Type icons in the view (lights, cameras, volumes, audio, probes)
    // =========================================================

    private void DrawWorldIcons()
    {
        foreach (Transform target in _markers)
        {
            if (target == null || target == _view.transform || !target.gameObject.activeInHierarchy) continue;
            Vector3 point = _view.WorldToScreenPoint(target.position);
            if (point.z <= _view.nearClipPlane || !_view.pixelRect.Contains(point)) continue;
            Vector2 gui = new Vector2(point.x, Screen.height - point.y) / _scale;
            Rect rect = new Rect(gui.x - 13, gui.y - 13, 26, 26);
            if (OverlapsPanel(rect)) continue;
            _iconRects.Add(rect);
            CS_MayaPreviewKind kind = CS_MayaPreviewIcons.KindOf(target);
            bool selected = _selection.IsSelected(target.gameObject);
            bool enabled = IsEnabled(target);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                GUI.FocusControl(null);
                var keyboard = Keyboard.current;
                if (keyboard != null && (keyboard.shiftKey.isPressed || keyboard.ctrlKey.isPressed)) _selection.Toggle(target.gameObject);
                else _selection.SelectObject(target.gameObject);
            }
            if (rect.Contains(Event.current.mousePosition)) _tooltip = target.name + "  (" + CS_MayaPreviewIcons.Label(kind) + ")";
            if (Event.current.type != EventType.Repaint) continue;
            DrawTinted(rect, CS_MayaPreviewIcons.Disc, selected ? new Color(0.12f, 0.26f, 0.42f, 0.92f) : new Color(0.08f, 0.08f, 0.08f, 0.72f));
            if (selected) DrawTinted(rect, CS_MayaPreviewIcons.Ring, new Color(1f, 0.85f, 0f, 1f));
            Color tint = CS_MayaPreviewIcons.Tint(kind);
            if (!enabled) tint = Color.Lerp(tint, Color.gray, 0.6f);
            DrawTinted(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8), CS_MayaPreviewIcons.Get(kind), tint);
        }
    }

    private static bool IsEnabled(Transform t)
    {
        if (t.TryGetComponent(out Light light)) return light.enabled;
        if (t.TryGetComponent(out Volume volume)) return volume.enabled;
        if (t.TryGetComponent(out AudioSource audio)) return audio.enabled && !audio.mute;
        if (t.TryGetComponent(out Camera camera)) return camera.enabled;
        return true;
    }
}
