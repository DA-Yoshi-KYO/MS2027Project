using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Outline
 Summary: Right click in the view (a click, not a drag: right drag still dollies the camera)
          opens a small menu at the cursor. The object under the cursor is selected first.
            アウトライン作成 : adds an outline (CS_MayaPreviewOutline, SHG_Outline) to every
                               selected object that has a mesh
            アウトライン削除 : removes them
          The Inspector shows an "アウトライン" section (width, colour, delete) for the
          selected object while it has one.
 History: 2026/11/13 New
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private const float ContextClickPixels = 5f;   // more movement = a camera drag, no menu
    private const float ContextItemHeight = 24f;
    private const float ContextWidth = 170f;

    private bool _rightDown;
    private Vector2 _rightDownPosition;            // screen pixels (Input System, y up)
    private bool _contextOpen;
    private Vector2 _contextPosition;              // GUI space (y down, divided by the GUI scale)
    private Rect _contextRect;

    private bool ContextMenuBlocks(Vector2 guiPoint) => _contextOpen && _contextRect.Contains(guiPoint);


    // =========================================================
    // Right click (Update)
    // =========================================================

    private void TickContextMenu()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || _uiHidden || ExportDialogOpen) { _rightDown = false; return; }
        Vector2 position = mouse.position.ReadValue();
        if (mouse.rightButton.wasPressedThisFrame)
        {
            // only presses on the 3D view (not on a panel)
            _rightDown = !BlocksInput(position) && _view != null && _view.pixelRect.Contains(position);
            _rightDownPosition = position;
        }
        if (_rightDown && mouse.rightButton.wasReleasedThisFrame)
        {
            _rightDown = false;
            if ((position - _rightDownPosition).magnitude > ContextClickPixels) return;
            _selection.Pick(_view, position, false);
            if (_selection.Count == 0) { _contextOpen = false; return; }
            _contextOpen = true;
            _contextPosition = new Vector2(position.x, Screen.height - position.y) / _scale;
        }
    }

    private List<GameObject> OutlineTargets()
    {
        var list = new List<GameObject>();
        foreach (GameObject o in _selection.Selected)
            if (o != null && !CS_MayaPreviewInternal.Is(o.transform)) list.Add(o);
        return list;
    }


    // =========================================================
    // Menu (OnGUI, on top of everything)
    // =========================================================

    private void DrawContextMenu()
    {
        if (!_contextOpen) return;
        List<GameObject> targets = OutlineTargets();
        bool canCreate = targets.Exists(CS_MayaPreviewOutline.CanHave);
        bool canRemove = targets.Exists(o => CS_MayaPreviewOutline.Of(o) != null);
        var items = new List<(string label, bool enabled, System.Action action)>
        {
            ("アウトライン作成", canCreate, CreateOutlines),
            ("アウトライン削除", canRemove, RemoveOutlines),
        };

        Vector2 screen = new Vector2(Screen.width, Screen.height) / _scale;
        float height = items.Count * ContextItemHeight + 4;
        float x = Mathf.Min(_contextPosition.x, screen.x - ContextWidth - 2);
        float y = Mathf.Min(_contextPosition.y, screen.y - height - 2);
        _contextRect = new Rect(x, y, ContextWidth, height);

        Event e = Event.current;
        // A click anywhere else closes the menu (and still does what it does there).
        if (e.type == EventType.MouseDown && !_contextRect.Contains(e.mousePosition)) { _contextOpen = false; return; }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { _contextOpen = false; e.Use(); return; }

        Fill(new Rect(x - 1, y - 1, ContextWidth + 2, height + 2), BorderColor);
        Fill(_contextRect, PanelColor);
        for (int i = 0; i < items.Count; i++)
        {
            Rect row = new Rect(x + 2, y + 2 + i * ContextItemHeight, ContextWidth - 4, ContextItemHeight);
            if (items[i].enabled && row.Contains(e.mousePosition)) Fill(row, SelectColor);
            GUI.enabled = items[i].enabled;
            if (GUI.Button(row, "  " + items[i].label, _row))
            {
                _contextOpen = false;
                items[i].action();
            }
            GUI.enabled = true;
        }
    }

    private void CreateOutlines()
    {
        int made = 0;
        string error = null;
        foreach (GameObject target in OutlineTargets())
        {
            if (!CS_MayaPreviewOutline.CanHave(target)) continue;
            if (CS_MayaPreviewOutline.Create(target, out string message) != null) made++;
            else error = message;
        }
        Flash(made > 0 ? "アウトラインを作成しました（" + made + "）" : error ?? "アウトラインを作れるメッシュがありません");
        _inspectorOpen = true;
        ClearFields();
    }

    private void RemoveOutlines()
    {
        int removed = 0;
        foreach (GameObject target in OutlineTargets())
        {
            CS_MayaPreviewOutline outline = CS_MayaPreviewOutline.Of(target);
            if (outline == null) continue;
            Destroy(outline.gameObject);
            removed++;
        }
        Flash("アウトラインを削除しました（" + removed + "）");
        ClearFields();
    }


    // =========================================================
    // Inspector section
    // =========================================================

    private void DrawOutlineSection(GameObject target)
    {
        CS_MayaPreviewOutline outline = CS_MayaPreviewOutline.Of(target);
        if (outline == null || !Section("アウトライン", CS_MayaPreviewIcons.Get(CS_MayaPreviewKind.Mesh), null)) return;
        Note("シェーダー: " + (outline.Material != null ? outline.Material.shader.name : "-") +
             "\n全部の設定は「設定を開く」（下の Materials の " + (outline.Material != null ? outline.Material.name : "") + "）から変えられます。");
        GUILayout.BeginHorizontal();
        GUILayout.Space(8);
        if (GUILayout.Button("設定を開く", _button, GUILayout.Height(RowHeight + 2)) && outline.Material != null)
        {
            MaterialSlot slot = CollectMaterials().Find(s => s.material == outline.Material);
            if (slot != null) EditMaterial(slot);
        }
        if (GUILayout.Button("アウトラインを削除", _button, GUILayout.Height(RowHeight + 2)))
        {
            Destroy(outline.gameObject);
            ClearFields();
        }
        GUILayout.Space(8);
        GUILayout.EndHorizontal();
    }
}
