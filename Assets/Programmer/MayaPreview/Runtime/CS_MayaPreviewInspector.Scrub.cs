using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Scrub
 Summary: Drag numbers like Houdini.
          - X / Y / Z label (between the axis letter and the box): the cursor becomes <->.
            Left drag: +-1 for every few pixels to the right / left.
          - Middle button on the label or on the number box: a ladder (100 / 10 / 1 / 0.1 / 0.01)
            appears around the cursor. Keep the middle button held, move up / down onto a step,
            then slide sideways: the value changes by that step. Release to finish.
          The middle drag is read from the Input System every frame (IMGUI does not deliver
          middle-button drags reliably in the player).
          The whole drag is one undo step (CS_MayaPreviewUndo merges while the mouse is held).
 History: 2026/11/06 New
          2026/11/06 Middle-button ladder driven by the Input System; also on the number box
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private static readonly float[] LadderSteps = { 100f, 10f, 1f, 0.1f, 0.01f };
    private const int LadderDefault = 2;            // "1"
    private const float LadderRowHeight = 24f;
    private const float LadderWidth = 70f;
    private const float ScrubPixelsPerStep = 4f;     // left drag: GUI pixels per +-1
    private const float LadderPixelsPerStep = 8f;    // ladder: GUI pixels per +-step
    private const float ScrubHandleWidth = 16f;

    private string _scrubKey;
    private int _scrubControl;
    private int _scrubButton;                        // 0 = left (IMGUI), 2 = middle (Input System)
    private float _scrubValue;
    private float _scrubCarry;
    private Vector2 _ladderCentre;                   // GUI space of the top level (the field is in a scroll view)
    private float _ladderPressY;                     // screen y (Input System, y up) at the press
    private int _ladderStep = LadderDefault;
    private bool _scrubHover, _splitHover;
    private int _cursorShown;                        // 0 = normal, 1 = <-> (scrub), 2 = up / down (panel splitter)
    private Texture2D _scrubCursor, _splitCursor;

    private static bool MiddlePressedNow()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.middleButton.wasPressedThisFrame;
    }

    // Draws the axis letter as a drag handle. Returns the value being dragged (or value).
    private float Scrub(string key, float value, string label, GUIStyle style)
    {
        Rect rect = GUILayoutUtility.GetRect(ScrubHandleWidth, RowHeight, GUILayout.Width(ScrubHandleWidth));
        string id = FieldId(key);
        int control = GUIUtility.GetControlID(FocusType.Passive, rect);
        GUI.Label(rect, label, style);
        Event e = Event.current;
        bool leftDragging = _scrubKey == id && _scrubButton == 0 && GUIUtility.hotControl == control;
        bool over = rect.Contains(e.mousePosition);
        if (over || (_scrubKey == id)) _scrubHover = true;

        if (over) TryStartLadder(id, value);

        switch (e.GetTypeForControl(control))
        {
            case EventType.MouseDown:
                if (over && e.button == 0 && _scrubKey == null)
                {
                    GUIUtility.hotControl = control;
                    _scrubKey = id;
                    _scrubControl = control;
                    _scrubButton = 0;
                    _scrubValue = value;
                    _scrubCarry = 0f;
                    GUI.FocusControl(null);
                    e.Use();
                }
                else if (over && e.button == 2) e.Use();
                break;
            case EventType.MouseDrag:
                if (leftDragging)
                {
                    _scrubCarry += e.delta.x;
                    int steps = (int)(_scrubCarry / ScrubPixelsPerStep);
                    if (steps != 0)
                    {
                        _scrubCarry -= steps * ScrubPixelsPerStep;
                        _scrubValue = (float)System.Math.Round(_scrubValue + steps, 0);
                        _fields.Remove(id);
                    }
                    e.Use();
                }
                break;
            case EventType.MouseUp:
                if (leftDragging) { EndScrub(); e.Use(); }
                break;
        }
        return _scrubKey == id ? _scrubValue : value;
    }

    // Middle button pressed over a label or a number box: open the ladder there.
    private void TryStartLadder(string id, float value)
    {
        // Layout events only have placeholder rects
        if (_scrubKey != null || Event.current.type == EventType.Layout || !MiddlePressedNow()) return;
        _scrubKey = id;
        _scrubControl = 0;
        _scrubButton = 2;
        _scrubValue = value;
        _scrubCarry = 0f;
        _ladderStep = LadderDefault;
        Vector2 screen = Mouse.current.position.ReadValue();     // pixels, y up
        _ladderPressY = screen.y;
        // top-level GUI space (y down, divided by the GUI scale), independent of scroll views
        _ladderCentre = new Vector2(screen.x, Screen.height - screen.y) / _scale;
        GUI.FocusControl(null);
        _fields.Remove(id);
    }

    // Every frame (Update): follow the middle-button ladder with the Input System.
    private void TickLadder()
    {
        if (_scrubKey == null || _scrubButton != 2) return;
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.middleButton.isPressed || !Application.isFocused) { EndScrub(); return; }
        Vector2 position = mouse.position.ReadValue();
        // GUI y grows downwards; rows are LadderRowHeight GUI pixels tall
        float down = (_ladderPressY - position.y) / _scale;
        _ladderStep = Mathf.Clamp(LadderDefault + Mathf.FloorToInt(down / LadderRowHeight + 0.5f), 0, LadderSteps.Length - 1);
        _scrubCarry += mouse.delta.ReadValue().x / _scale;
        int steps = (int)(_scrubCarry / LadderPixelsPerStep);
        if (steps == 0) return;
        _scrubCarry -= steps * LadderPixelsPerStep;
        float step = LadderSteps[_ladderStep];
        // round to the step so 0.1 + 0.1 + 0.1 stays 0.3 on screen
        _scrubValue = (float)System.Math.Round(_scrubValue + steps * step, Decimals(step));
        _fields.Remove(_scrubKey);
    }

    private void EndScrub()
    {
        if (_scrubControl != 0 && GUIUtility.hotControl == _scrubControl) GUIUtility.hotControl = 0;
        _scrubKey = null;
        _scrubControl = 0;
    }

    private static int Decimals(float step) => step >= 1f ? 0 : step >= 0.1f ? 1 : 2;

    // Called at the end of OnGUI: the ladder on top of everything, and the <-> cursor.
    private void DrawScrubOverlay()
    {
        // a left drag ended outside this window (no MouseUp arrived)
        if (_scrubKey != null && _scrubButton == 0 && GUIUtility.hotControl != _scrubControl) EndScrub();

        if (_scrubKey != null && _scrubButton == 2 && Event.current.type == EventType.Repaint)
        {
            Vector2 centre = _ladderCentre;
            float top = centre.y - LadderDefault * LadderRowHeight - LadderRowHeight * 0.5f;
            float left = centre.x - LadderWidth * 0.5f;
            Rect frame = new Rect(left - 1, top - 1, LadderWidth + 2, LadderSteps.Length * LadderRowHeight + 2);
            Fill(frame, BorderColor);
            for (int i = 0; i < LadderSteps.Length; i++)
            {
                Rect row = new Rect(left, top + i * LadderRowHeight, LadderWidth, LadderRowHeight - 1);
                Fill(row, i == _ladderStep ? SelectColor : SectionColor);
                GUI.Label(row, LadderSteps[i].ToString("0.##", CultureInfo.InvariantCulture), _center);
            }
            Rect valueRect = new Rect(frame.xMax + 6, top + _ladderStep * LadderRowHeight, 96, LadderRowHeight - 1);
            Fill(new Rect(valueRect.x - 1, valueRect.y - 1, valueRect.width + 2, valueRect.height + 2), BorderColor);
            Fill(valueRect, FieldColor);
            GUI.Label(valueRect, _scrubValue.ToString("G6", CultureInfo.InvariantCulture), _center);
        }

        if (Event.current.type != EventType.Repaint) return;
        int want = _scrubHover || _scrubKey != null ? 1 : _splitHover || _splitUpper >= 0 ? 2 : 0;
        _scrubHover = _splitHover = false;
        SetCursorShape(want);
    }

    private void SetCursorShape(int shape)
    {
        if (shape == _cursorShown) return;
        _cursorShown = shape;
        if (shape == 1) Cursor.SetCursor(ScrubCursor(), new Vector2(16, 16), CursorMode.Auto);
        else if (shape == 2) Cursor.SetCursor(SplitCursor(), new Vector2(16, 16), CursorMode.Auto);
        else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    // The <-> cursor turned upright, for the splitters between panels.
    private Texture2D SplitCursor()
    {
        if (_splitCursor != null) return _splitCursor;
        Texture2D across = ScrubCursor();
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, across.GetPixel(y, x));
        texture.Apply();
        _owned.Add(texture);
        _splitCursor = texture;
        return texture;
    }

    // A white <-> with a black outline, made once.
    private Texture2D ScrubCursor()
    {
        if (_scrubCursor != null) return _scrubCursor;
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
        var shape = new bool[size, size];
        for (int x = 6; x <= 25; x++) for (int y = 15; y <= 16; y++) shape[x, y] = true;     // shaft
        for (int i = 0; i <= 5; i++)
            for (int y = 16 - i; y <= 15 + i; y++)
            {
                shape[1 + i, y] = true;                                                      // left head
                shape[30 - i, y] = true;                                                     // right head
            }
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = Color.clear;
                if (shape[x, y]) c = Color.white;
                else
                {
                    for (int dy = -1; dy <= 1 && c.a == 0; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < size && ny < size && shape[nx, ny]) { c = Color.black; break; }
                        }
                }
                texture.SetPixel(x, size - 1 - y, c);
            }
        texture.Apply();
        _owned.Add(texture);
        _scrubCursor = texture;
        return texture;
    }
}
