using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Hierarchy
 Summary: Hierarchy panel.
          - Type icon + name per row, the type written on the right (Light, Camera, Volume...),
            indentation guides, zebra rows, search box.
          - Maya meshes are shown in their Maya hierarchy (groups from the Maya path);
            clicking a Maya group selects every mesh in it.
          - Rows can be dragged into the Export panel.
 History: 2026/10/11 New layout, Maya groups, drag to Export
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private class Node
    {
        public string key;
        public string label;
        public CS_MayaPreviewKind kind;
        public Transform t;          // null for Maya groups
        public string mayaGroup;     // Maya group path
        public bool active = true;
        public readonly List<Node> children = new List<Node>();
    }

    private const float TreeRow = 20f;
    private const float Indent = 14f;
    private readonly List<Node> _tree = new List<Node>();
    private readonly List<Transform> _markers = new List<Transform>();
    private readonly Dictionary<string, bool> _fold = new Dictionary<string, bool>();
    private readonly List<KeyValuePair<Node, int>> _rows = new List<KeyValuePair<Node, int>>();
    private float _nextScan;
    private string _search = "";
    private Vector2 _hierarchyScroll;
    private Node _pressNode;
    private Vector2 _pressPosition;
    private List<CS_MayaPreviewExport.Item> _drag;
    private int _objectCount;
    private Transform _scanModelRoot;


    // =========================================================
    // Scan the scene (once a second)
    // =========================================================

    private void Scan()
    {
        _nextScan = Time.unscaledTime + 1f;
        var visible = new HashSet<Transform>();
        _markers.Clear();

        void Add(Component component, bool marker)
        {
            if (component == null || CS_MayaPreviewInternal.Is(component)) return;
            if (marker && !_markers.Contains(component.transform)) _markers.Add(component.transform);
            for (Transform t = component.transform; t != null && visible.Add(t); t = t.parent) { }
        }

        foreach (var c in FindObjectsByType<Light>(FindObjectsSortMode.None)) Add(c, true);
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Add(c, c != _view);
        foreach (var c in FindObjectsByType<Volume>(FindObjectsSortMode.None)) Add(c, true);
        foreach (var c in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) Add(c, true);
        foreach (var c in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) Add(c, true);
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (CS_MayaPreviewSelection.Selectable(r)) Add(r, false);

        _scanModelRoot = CS_MayaPreviewExport.ModelRoot();
        var roots = new List<Transform>();
        foreach (Transform t in visible) if (t.parent == null) roots.Add(t);
        roots.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
        _tree.Clear();
        _objectCount = visible.Count;
        foreach (Transform root in roots) _tree.Add(Build(root, visible));
    }

    private Node Build(Transform t, HashSet<Transform> visible)
    {
        var node = new Node
        {
            key = "t:" + t.GetInstanceID(), label = t.name, kind = CS_MayaPreviewIcons.KindOf(t), t = t,
            active = t.gameObject.activeInHierarchy
        };
        if (t == _scanModelRoot)
        {
            node.kind = CS_MayaPreviewKind.MayaGroup;
            BuildMaya(node);
            // Objects made in the preview (Ctrl+D copies) are ordinary objects under the Maya root.
            var made = new List<Transform>();
            foreach (Transform child in t)
                if (visible.Contains(child) && !CS_MayaPreviewExport.IsLive(child.gameObject)) made.Add(child);
            made.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            foreach (Transform child in made) node.children.Add(Build(child, visible));
            return node;
        }
        var children = new List<Transform>();
        foreach (Transform child in t) if (visible.Contains(child)) children.Add(child);
        children.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
        foreach (Transform child in children) node.children.Add(Build(child, visible));
        return node;
    }

    // Maya meshes are flat under Maya_Model_Root; their names carry the Maya path.
    private void BuildMaya(Node root)
    {
        List<GameObject> live = CS_MayaPreviewExport.LiveObjects();
        live.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var groupPaths = new HashSet<string>();
        foreach (GameObject o in live)
        {
            string path = CS_MayaPreviewExport.TransformPath(CS_MayaPreviewExport.ShapePath(o));
            for (int bar = path.LastIndexOf('|'); bar > 0; bar = path.LastIndexOf('|', bar - 1))
                groupPaths.Add(path.Substring(0, bar));
        }

        var groups = new Dictionary<string, Node>();
        Node Group(string path)
        {
            if (groups.TryGetValue(path, out Node node)) return node;
            int bar = path.LastIndexOf('|');
            Node parent = bar > 0 ? Group(path.Substring(0, bar)) : root;
            node = new Node
            {
                key = "maya:" + path, label = CS_MayaPreviewExport.LastSegment(path),
                kind = CS_MayaPreviewKind.MayaGroup, mayaGroup = path
            };
            parent.children.Add(node);
            groups[path] = node;
            return node;
        }

        foreach (GameObject o in live)
        {
            string path = CS_MayaPreviewExport.TransformPath(CS_MayaPreviewExport.ShapePath(o));
            int bar = path.LastIndexOf('|');
            Node parent = groupPaths.Contains(path) ? Group(path) : bar > 0 ? Group(path.Substring(0, bar)) : root;
            parent.children.Add(new Node
            {
                key = "t:" + o.transform.GetInstanceID(), label = CS_MayaPreviewExport.LastSegment(path),
                kind = CS_MayaPreviewKind.MayaMesh, t = o.transform, active = o.activeInHierarchy
            });
        }
    }

    private bool Expanded(Node node, int depth)
    {
        if (_fold.TryGetValue(node.key, out bool open)) return open;
        return depth == 0 || node.mayaGroup != null || node.kind == CS_MayaPreviewKind.MayaGroup;
    }

    private void BuildRows()
    {
        _rows.Clear();
        if (!string.IsNullOrEmpty(_search))
        {
            foreach (Node node in _tree) Search(node);
            return;
        }
        foreach (Node node in _tree) Flatten(node, 0);
    }

    private void Flatten(Node node, int depth)
    {
        _rows.Add(new KeyValuePair<Node, int>(node, depth));
        if (node.children.Count == 0 || !Expanded(node, depth)) return;
        foreach (Node child in node.children) Flatten(child, depth + 1);
    }

    private void Search(Node node)
    {
        if (node.label.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) _rows.Add(new KeyValuePair<Node, int>(node, 0));
        foreach (Node child in node.children) Search(child);
    }


    // =========================================================
    // Drawing
    // =========================================================

    private void DrawHierarchy()
    {
        Rect content = DrawWindow(_hierarchyRect, "Hierarchy", _objectCount + " objects");
        Rect search = new Rect(content.x + 4, content.y + 3, content.width - 8, 18);
        GUI.SetNextControlName("F.hierarchy.search");
        _search = GUI.TextField(search, _search ?? "");
        if (string.IsNullOrEmpty(_search) && Event.current.type == EventType.Repaint)
            GUI.Label(new Rect(search.x + 6, search.y, search.width - 10, search.height), "Search", _placeholder);
        else if (!string.IsNullOrEmpty(_search))
        {
            Rect clear = new Rect(search.xMax - 17, search.y + 2, 14, 14);
            if (GUI.Button(clear, GUIContent.none, GUIStyle.none)) { _search = ""; GUI.FocusControl(null); }
            DrawTinted(new Rect(clear.x + 2, clear.y + 2, 10, 10), CS_MayaPreviewIcons.Close, DimColor);
        }

        Rect list = new Rect(content.x, search.yMax + 4, content.width, content.yMax - search.yMax - 4);
        BuildRows();
        float height = _rows.Count * TreeRow;
        bool scroll = height > list.height;
        Rect view = new Rect(0, 0, list.width - (scroll ? 10 : 0), Mathf.Max(height, list.height));
        _hierarchyScroll = GUI.BeginScrollView(list, _hierarchyScroll, view, false, false, GUIStyle.none, _skin.verticalScrollbar);
        int first = Mathf.Max(0, Mathf.FloorToInt(_hierarchyScroll.y / TreeRow));
        int last = Mathf.Min(_rows.Count - 1, first + Mathf.CeilToInt(list.height / TreeRow) + 1);
        for (int i = first; i <= last; i++)
            DrawTreeRow(new Rect(0, i * TreeRow, view.width, TreeRow), _rows[i].Key, _rows[i].Value, i);

        // Click on the empty part of the list clears the selection.
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && e.mousePosition.y > height && view.Contains(e.mousePosition))
        { _selection.Select(null); GUI.FocusControl(null); e.Use(); }
        GUI.EndScrollView();
    }

    private void DrawTreeRow(Rect rect, Node node, int depth, int index)
    {
        Event e = Event.current;
        GameObject go = node.t != null ? node.t.gameObject : null;
        bool selected = go != null ? _selection.IsSelected(go) : node.mayaGroup != null && _selection.GroupPath == node.mayaGroup;
        bool hover = rect.Contains(e.mousePosition);

        if (e.type == EventType.Repaint)
        {
            if (index % 2 == 1) Fill(rect, StripeColor);
            if (selected) Fill(rect, SelectColor);
            else if (hover && _drag == null) Fill(rect, HoverColor);
            for (int level = 1; level <= depth; level++)
                Fill(new Rect(rect.x + 10 + (level - 1) * Indent, rect.y, 1, rect.height), GuideColor);
        }

        float x = rect.x + 4 + depth * Indent;
        bool hasChildren = node.children.Count > 0 && string.IsNullOrEmpty(_search);
        if (hasChildren)
        {
            bool open = Expanded(node, depth);
            Rect fold = new Rect(x - 2, rect.y, 16, rect.height);
            DrawTinted(new Rect(x + 1, rect.y + 5, 10, 10), open ? CS_MayaPreviewIcons.ArrowDown : CS_MayaPreviewIcons.ArrowRight, DimColor);
            if (e.type == EventType.MouseDown && e.button == 0 && fold.Contains(e.mousePosition))
            {
                _fold[node.key] = !open;
                e.Use();
                return;
            }
        }

        Rect icon = new Rect(x + 14, rect.y + 2, 16, 16);
        Color tint = CS_MayaPreviewIcons.Tint(node.kind);
        if (!node.active) tint = Color.Lerp(tint, Color.gray, 0.6f);
        DrawTinted(icon, CS_MayaPreviewIcons.Get(node.kind), tint);

        string kind = ShowKind(node.kind) ? CS_MayaPreviewIcons.Label(node.kind) : "";
        float kindWidth = kind.Length > 0 ? _smallRight.CalcSize(new GUIContent(kind)).x + 6 : 0f;
        float labelX = icon.xMax + 5;
        float labelWidth = rect.xMax - labelX - kindWidth - 4;
        string shown = Fit(node.label, _row, labelWidth);
        GUI.Label(new Rect(labelX, rect.y, labelWidth, rect.height), shown, node.active ? _row : _rowDim);
        if (kindWidth > 0) GUI.Label(new Rect(rect.xMax - kindWidth - 4, rect.y, kindWidth, rect.height), kind, _smallRight);
        if (hover && shown != node.label) _tooltip = node.label;

        if (e.type == EventType.MouseDown && e.button == 0 && hover)
        {
            GUI.FocusControl(null);
            SelectNode(node, e.shift || e.control);
            if (e.clickCount == 2 && _controller != null) _controller.FocusSelection();
            _pressNode = node;
            _pressPosition = e.mousePosition;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && _pressNode == node && _drag == null
                 && (e.mousePosition - _pressPosition).magnitude > 6f)
        {
            _drag = ItemsFor(node);
            _exportOpen = true;   // the drop target
            e.Use();
        }
    }

    private static bool ShowKind(CS_MayaPreviewKind kind) =>
        CS_MayaPreviewIcons.IsMarker(kind) || kind == CS_MayaPreviewKind.Character || kind == CS_MayaPreviewKind.MayaGroup;

    private void SelectNode(Node node, bool additive)
    {
        if (node.mayaGroup != null)
        {
            var objects = CS_MayaPreviewExport.LiveObjectsIn(node.mayaGroup);
            if (additive) foreach (GameObject o in _selection.Selected) if (!objects.Contains(o)) objects.Add(o);
            _selection.Select(objects, additive ? null : node.label, additive ? null : node.mayaGroup);
        }
        else if (node.t != null)
        {
            if (additive) _selection.Toggle(node.t.gameObject);
            else _selection.SelectObject(node.t.gameObject);
        }
    }

    private List<CS_MayaPreviewExport.Item> ItemsFor(Node node)
    {
        var items = new List<CS_MayaPreviewExport.Item>();
        if (node.mayaGroup == null && node.kind == CS_MayaPreviewKind.MayaGroup)
        {
            // Maya_Model_Root: every top-level Maya group / mesh
            foreach (Node child in node.children) items.AddRange(ItemsFor(child));
            return items;
        }
        items.Add(new CS_MayaPreviewExport.Item
        {
            label = node.label, mayaGroup = node.mayaGroup, target = node.t != null ? node.t.gameObject : null, kind = node.kind
        });
        return items;
    }


    // =========================================================
    // Drag & drop (Hierarchy -> Export)
    // =========================================================

    private void HandleDrag()
    {
        Event e = Event.current;
        if (e.type == EventType.MouseUp) _pressNode = null;
        if (_drag == null) return;
        if (e.type == EventType.MouseUp)
        {
            if (_exportOpen && _exportRect.Contains(e.mousePosition)) AddExportItems(_drag);
            _drag = null;
            e.Use();
        }
    }

    private void DrawDragGhost()
    {
        if (_drag == null || _drag.Count == 0 || Event.current.type != EventType.Repaint) return;
        Vector2 mouse = Event.current.mousePosition;
        string text = _drag.Count == 1 ? _drag[0].label : _drag.Count + " objects";
        bool over = _exportOpen && _exportRect.Contains(mouse);
        Vector2 size = _row.CalcSize(new GUIContent(text));
        Rect rect = new Rect(mouse.x + 12, mouse.y + 6, size.x + 34, 22);
        Fill(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), over ? AccentColor : BorderColor);
        Fill(rect, new Color(0.18f, 0.18f, 0.18f, 0.95f));
        DrawTinted(new Rect(rect.x + 6, rect.y + 3, 16, 16), CS_MayaPreviewIcons.Get(_drag[0].kind), CS_MayaPreviewIcons.Tint(_drag[0].kind));
        GUI.Label(new Rect(rect.x + 26, rect.y, rect.width - 28, rect.height), text, _row);
    }
}
