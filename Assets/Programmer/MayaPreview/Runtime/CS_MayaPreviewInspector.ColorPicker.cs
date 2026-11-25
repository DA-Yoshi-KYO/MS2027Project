using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

/*
+=====================================
 File   : CS_MayaPreviewInspector.ColorPicker
 Summary: Colour palette, opened by clicking a colour swatch (ColorField).
          SV square + hue bar, RGB (0-255) and HSV (360 / 100 / 100) together, hex, alpha (where it is read),
          "before" colour (click = back to it), strength for HDR colours.
          Drag a bar, or click its number and type.
          - The palette lies over the panels, so it takes the mouse / keys first and is painted last.
            It uses no IMGUI controls: those would get other IDs in the two passes and lose the focus.
          - The edited colour is handed to ColorField, which is where undo records it.
 History: 2026/11/22 New
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private enum PickerPart { None, Square, Hue, R, G, B, H, S, V, A, Strength, Hex, Move }

    private const float PickerWidth = 280f;
    private const float PickerPadding = 8f;
    private const float PickerTitle = 24f;
    private const float PickerSquare = 140f;
    private const float PickerHueWidth = 16f;
    private const float PickerRow = 20f;
    private const float PickerLabel = 34f;
    private const float PickerValue = 52f;
    private const float PickerMaxStrength = 10f;
    private const int PickerTextureSize = 64;
    private const int PickerTextLimit = 16;

    private string _pickerId;               // ColorField that opened it
    private string _pickerTitle;
    private Color _pickerColor, _pickerBefore;
    private float _pickerH, _pickerS, _pickerV, _pickerStrength = 1f;
    private bool _pickerHdr, _pickerAlpha, _pickerChanged, _pickerSeen;
    private PickerPart _pickerDrag, _pickerEdit;
    private string _pickerText;
    private bool _pickerReplace;            // the next key replaces the number
    private Vector2 _pickerPosition, _pickerGrab;
    private Texture2D _pickerSquareTexture, _pickerHueTexture;
    private float _pickerSquareHue = -1f;

    private bool ColorPickerOpen => _pickerId != null;
    private bool ColorPickerTyping => _pickerId != null && _pickerEdit != PickerPart.None;

    private bool ColorPickerBlocks(Vector2 point) =>
        ColorPickerOpen && (_pickerDrag != PickerPart.None || PickerRect().Contains(point));


    // =========================================================
    // Link with ColorField
    // =========================================================

    private void OpenColorPicker(string id, string title, Color value, bool hdr, bool alpha)
    {
        _pickerId = id;
        _pickerTitle = title.TrimStart('*', ' ');
        _pickerBefore = value;
        _pickerHdr = hdr;
        _pickerAlpha = alpha;
        _pickerH = _pickerS = 0f;
        _pickerDrag = _pickerEdit = PickerPart.None;
        _pickerChanged = false;
        _pickerSeen = true;
        SetPickerColor(value);

        Vector2 mouse = new Vector2(VW * 0.5f, VH * 0.3f);
        if (Mouse.current != null)
        {
            Vector2 screen = Mouse.current.position.ReadValue();
            mouse = new Vector2(screen.x, Screen.height - screen.y) / _scale;
        }
        // Left of the swatch, so the colour column stays visible.
        float x = mouse.x - PickerWidth - 60f;
        _pickerPosition = new Vector2(x >= 4f ? x : mouse.x + 20f, mouse.y - 40f);
    }

    private void CloseColorPicker()
    {
        _pickerId = null;
        _pickerDrag = _pickerEdit = PickerPart.None;
        _pickerChanged = false;
    }

    // Called by every ColorField: the value after the palette's edit.
    private Color SyncColorPicker(string id, Color value)
    {
        if (_pickerId != id) return value;
        _pickerSeen = true;
        if (_pickerChanged)
        {
            _pickerChanged = false;
            return _pickerColor;
        }
        if (value != _pickerColor) SetPickerColor(value);   // undo, or the hex box in the panel
        return value;
    }

    private void SetPickerColor(Color value)
    {
        _pickerColor = value;
        float max = Mathf.Max(value.r, Mathf.Max(value.g, value.b));
        _pickerStrength = _pickerHdr ? Mathf.Max(1f, max) : 1f;
        Color shown = PickerBase();
        Color.RGBToHSV(shown, out float h, out float s, out float v);
        // Grey has no hue and black no saturation: keep the ones the markers are on.
        if (s > 0f && v > 0f) _pickerH = h;
        if (v > 0f) _pickerS = s;
        _pickerV = v;
    }

    private Color PickerBase()
    {
        Color c = _pickerColor / Mathf.Max(_pickerStrength, 1e-4f);
        return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
    }


    // =========================================================
    // Layout
    // =========================================================

    private float PickerHeight =>
        PickerTitle + PickerPadding + PickerSquare + 6f + PickerRow * (7 + (_pickerHdr ? 1 : 0) + (_pickerAlpha ? 1 : 0)) + 12f + PickerPadding;

    private Rect PickerRect() => new Rect(
        Mathf.Clamp(_pickerPosition.x, 2f, Mathf.Max(2f, VW - PickerWidth - 2f)),
        Mathf.Clamp(_pickerPosition.y, 2f, Mathf.Max(2f, VH - PickerHeight - 2f)),
        PickerWidth, PickerHeight);

    private static Rect PickerClose(Rect box) => new Rect(box.xMax - PickerTitle, box.y, PickerTitle, PickerTitle);

    private static Rect PickerSquareRect(Rect box) =>
        new Rect(box.x + PickerPadding, box.y + PickerTitle + PickerPadding, PickerSquare, PickerSquare);

    private static Rect PickerHueRect(Rect box)
    {
        Rect square = PickerSquareRect(box);
        return new Rect(square.xMax + 8f, square.y, PickerHueWidth, square.height);
    }

    private static Rect PickerNow(Rect box)
    {
        Rect hue = PickerHueRect(box);
        return new Rect(hue.xMax + 10f, hue.y + 18f, box.xMax - PickerPadding - hue.xMax - 10f, 36f);
    }

    private static Rect PickerOld(Rect box)
    {
        Rect now = PickerNow(box);
        return new Rect(now.x, now.yMax + 20f, now.width, now.height);
    }

    private Rect PickerRowRect(Rect box, PickerPart part)
    {
        int index;
        float gap;
        switch (part)
        {
            case PickerPart.R: index = 0; gap = 0f; break;
            case PickerPart.G: index = 1; gap = 0f; break;
            case PickerPart.B: index = 2; gap = 0f; break;
            case PickerPart.H: index = 3; gap = 6f; break;
            case PickerPart.S: index = 4; gap = 6f; break;
            case PickerPart.V: index = 5; gap = 6f; break;
            case PickerPart.A: index = 6; gap = 12f; break;
            case PickerPart.Hex: index = 7; gap = 12f; break;
            default: index = 8; gap = 12f; break;
        }
        if (!_pickerAlpha && index > 6) index--;
        float top = PickerSquareRect(box).yMax + 6f;
        return new Rect(box.x + PickerPadding, top + index * PickerRow + gap, box.width - PickerPadding * 2f, PickerRow - 2f);
    }

    private static Rect PickerBar(Rect row) =>
        new Rect(row.x + PickerLabel, row.y + 3f, row.width - PickerLabel - PickerValue - 6f, row.height - 6f);

    private static Rect PickerBox(Rect row, PickerPart part) => part == PickerPart.Hex
        ? new Rect(row.x + PickerLabel, row.y, row.width - PickerLabel, row.height)
        : new Rect(row.xMax - PickerValue, row.y, PickerValue, row.height);

    private bool PickerHasRow(PickerPart part) =>
        part == PickerPart.A ? _pickerAlpha : part != PickerPart.Strength || _pickerHdr;

    private static readonly PickerPart[] PickerRows =
    {
        PickerPart.R, PickerPart.G, PickerPart.B, PickerPart.H, PickerPart.S, PickerPart.V,
        PickerPart.A, PickerPart.Hex, PickerPart.Strength
    };


    // =========================================================
    // Values
    // =========================================================

    private static float PickerMax(PickerPart part)
    {
        switch (part)
        {
            case PickerPart.R: case PickerPart.G: case PickerPart.B: return 255f;
            case PickerPart.H: return 360f;
            case PickerPart.Strength: return PickerMaxStrength;
            default: return 100f;
        }
    }

    private float PickerGet(PickerPart part)
    {
        Color c = PickerBase();
        switch (part)
        {
            case PickerPart.R: return c.r * 255f;
            case PickerPart.G: return c.g * 255f;
            case PickerPart.B: return c.b * 255f;
            case PickerPart.H: return _pickerH * 360f;
            case PickerPart.S: return _pickerS * 100f;
            case PickerPart.V: return _pickerV * 100f;
            case PickerPart.A: return _pickerColor.a * 100f;
            default: return _pickerStrength;
        }
    }

    private void PickerSet(PickerPart part, float value)
    {
        float t = Mathf.Clamp01(value / PickerMax(part));
        Color c = PickerBase();
        switch (part)
        {
            case PickerPart.R: c.r = t; PickerApplyRgb(c); break;
            case PickerPart.G: c.g = t; PickerApplyRgb(c); break;
            case PickerPart.B: c.b = t; PickerApplyRgb(c); break;
            case PickerPart.H: _pickerH = t; PickerApplyHsv(); break;
            case PickerPart.S: _pickerS = t; PickerApplyHsv(); break;
            case PickerPart.V: _pickerV = t; PickerApplyHsv(); break;
            case PickerPart.A: _pickerColor.a = t; _pickerChanged = true; break;
            case PickerPart.Strength:
                _pickerStrength = Mathf.Clamp(value, 1f, PickerMaxStrength);
                PickerWrite(c);
                break;
        }
    }

    private void PickerApplyRgb(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        if (s > 0f && v > 0f) _pickerH = h;
        if (v > 0f) _pickerS = s;
        _pickerV = v;
        PickerWrite(c);
    }

    private void PickerApplyHsv() => PickerWrite(Color.HSVToRGB(_pickerH, _pickerS, _pickerV));

    private void PickerWrite(Color c)
    {
        _pickerColor = new Color(c.r * _pickerStrength, c.g * _pickerStrength, c.b * _pickerStrength, _pickerColor.a);
        _pickerChanged = true;
    }

    private string PickerString(PickerPart part)
    {
        if (part == PickerPart.Hex) return "#" + ColorUtility.ToHtmlStringRGB(PickerBase());
        return PickerGet(part).ToString(part == PickerPart.Strength ? "0.##" : "0", CultureInfo.InvariantCulture);
    }


    // =========================================================
    // Input (before the panels)
    // =========================================================

    private void ColorPickerInput()
    {
        if (!ColorPickerOpen) return;
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (_pickerEdit != PickerPart.None) { PickerKey(e); e.Use(); }
            else if (e.keyCode == KeyCode.Escape) { CloseColorPicker(); e.Use(); }
            return;
        }
        if (!e.isMouse && e.type != EventType.ScrollWheel) return;

        Rect box = PickerRect();
        Vector2 mouse = e.mousePosition;
        if (_pickerDrag != PickerPart.None)
        {
            if (e.type == EventType.MouseDrag) PickerDragTo(box, mouse);
            else if (e.type == EventType.MouseUp) _pickerDrag = PickerPart.None;
            e.Use();
            return;
        }
        if (!box.Contains(mouse))
        {
            if (e.type != EventType.MouseDown) return;
            CommitPickerText();
            if (_pickerChanged) return;     // ColorField still has to take the typed value
            CloseColorPicker();
            e.Use();
            return;
        }
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            GUI.FocusControl(null);         // keys now belong to the palette, not to a box in a panel
            CommitPickerText();
            PickerPress(box, mouse);
        }
        e.Use();
    }

    private void PickerPress(Rect box, Vector2 mouse)
    {
        if (PickerClose(box).Contains(mouse)) { CloseColorPicker(); return; }
        if (PickerOld(box).Contains(mouse))
        {
            SetPickerColor(_pickerBefore);
            _pickerChanged = true;
            return;
        }
        if (PickerSquareRect(box).Contains(mouse)) _pickerDrag = PickerPart.Square;
        else if (PickerHueRect(box).Contains(mouse)) _pickerDrag = PickerPart.Hue;
        else if (mouse.y < box.y + PickerTitle) { _pickerDrag = PickerPart.Move; _pickerGrab = mouse - box.position; }
        else
        {
            foreach (PickerPart part in PickerRows)
            {
                if (!PickerHasRow(part)) continue;
                Rect row = PickerRowRect(box, part);
                if (PickerBox(row, part).Contains(mouse))
                {
                    _pickerEdit = part;
                    _pickerText = PickerString(part);
                    _pickerReplace = true;
                    return;
                }
                if (part == PickerPart.Hex) continue;
                Rect bar = PickerBar(row);
                if (mouse.y >= row.y && mouse.y <= row.yMax && mouse.x >= bar.x - 3f && mouse.x <= bar.xMax + 3f)
                {
                    _pickerDrag = part;
                    break;
                }
            }
        }
        if (_pickerDrag != PickerPart.None) PickerDragTo(box, mouse);
    }

    private void PickerDragTo(Rect box, Vector2 mouse)
    {
        switch (_pickerDrag)
        {
            case PickerPart.Move:
                _pickerPosition = mouse - _pickerGrab;
                return;
            case PickerPart.Square:
            {
                Rect square = PickerSquareRect(box);
                _pickerS = Mathf.Clamp01((mouse.x - square.x) / square.width);
                _pickerV = 1f - Mathf.Clamp01((mouse.y - square.y) / square.height);
                PickerApplyHsv();
                return;
            }
            case PickerPart.Hue:
            {
                Rect hue = PickerHueRect(box);
                PickerSet(PickerPart.H, (1f - Mathf.Clamp01((mouse.y - hue.y) / hue.height)) * 360f);
                return;
            }
            default:
            {
                Rect bar = PickerBar(PickerRowRect(box, _pickerDrag));
                PickerSet(_pickerDrag, Mathf.Clamp01((mouse.x - bar.x) / bar.width) * PickerMax(_pickerDrag));
                return;
            }
        }
    }

    private void PickerKey(Event e)
    {
        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Tab) { CommitPickerText(); return; }
        if (e.keyCode == KeyCode.Escape) { _pickerEdit = PickerPart.None; return; }
        bool control = e.control || e.command;
        if (control && e.keyCode == KeyCode.C) { GUIUtility.systemCopyBuffer = _pickerText; return; }
        if (control && e.keyCode == KeyCode.V)
        {
            string pasted = (GUIUtility.systemCopyBuffer ?? "").Trim();
            _pickerText = pasted.Length > PickerTextLimit ? pasted.Substring(0, PickerTextLimit) : pasted;
            _pickerReplace = false;
            return;
        }
        if (control) return;
        if (e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete)
        {
            _pickerText = _pickerReplace || _pickerText.Length == 0 ? "" : _pickerText.Substring(0, _pickerText.Length - 1);
            _pickerReplace = false;
            return;
        }
        char c = e.character;
        bool allowed = _pickerEdit == PickerPart.Hex
            ? c == '#' || (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')
            : c == '.' || (c >= '0' && c <= '9');
        if (!allowed) return;
        if (_pickerReplace) _pickerText = "";
        _pickerReplace = false;
        if (_pickerText.Length < PickerTextLimit) _pickerText += c;
    }

    private void CommitPickerText()
    {
        if (_pickerEdit == PickerPart.None) return;
        PickerPart part = _pickerEdit;
        _pickerEdit = PickerPart.None;
        if (_pickerReplace || string.IsNullOrEmpty(_pickerText)) return;
        if (part == PickerPart.Hex)
        {
            string hex = _pickerText.StartsWith("#") ? _pickerText : "#" + _pickerText;
            if (!ColorUtility.TryParseHtmlString(hex, out Color parsed)) return;
            if (_pickerAlpha && hex.Length == 9) _pickerColor.a = parsed.a;
            PickerApplyRgb(parsed);
        }
        else if (float.TryParse(_pickerText, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
            PickerSet(part, number);
    }


    // =========================================================
    // Draw (after the panels)
    // =========================================================

    private void DrawColorPicker()
    {
        if (!ColorPickerOpen || Event.current.type != EventType.Repaint) return;
        // Its field was not drawn (group folded, other material, panel closed): nothing to edit.
        if (!_pickerSeen) { CloseColorPicker(); return; }
        _pickerSeen = false;
        EnsurePickerTextures();
        _tooltip = null;

        Rect box = PickerRect();
        Fill(new Rect(box.x - 1, box.y - 1, box.width + 2, box.height + 2), BorderColor);
        Fill(box, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 1f));
        Fill(new Rect(box.x, box.y, box.width, PickerTitle), HeaderColor);
        GUI.Label(new Rect(box.x + 8, box.y, box.width - 40, PickerTitle), Fit(_pickerTitle, _title, box.width - 44), _title);
        GUI.Label(PickerClose(box), "×", _center);

        Rect square = PickerSquareRect(box);
        GUI.DrawTexture(square, _pickerSquareTexture, ScaleMode.StretchToFill, false);
        Vector2 mark = new Vector2(square.x + _pickerS * square.width, square.y + (1f - _pickerV) * square.height);
        DrawTinted(new Rect(mark.x - 6, mark.y - 6, 12, 12), CS_MayaPreviewIcons.Ring, _pickerV > 0.6f ? Color.black : Color.white);

        Rect hue = PickerHueRect(box);
        GUI.DrawTexture(hue, _pickerHueTexture, ScaleMode.StretchToFill, false);
        Fill(new Rect(hue.x - 2, hue.y + (1f - _pickerH) * hue.height - 1, hue.width + 4, 2), Color.white);

        Color shown = PickerBase();
        Rect now = PickerNow(box), old = PickerOld(box);
        GUI.Label(new Rect(now.x, now.y - 18, now.width, 18), "今", _small);
        Fill(now, shown);
        GUI.Label(new Rect(old.x, old.y - 18, old.width, 18), "前（戻す）", _small);
        Fill(old, new Color(Mathf.Clamp01(_pickerBefore.r), Mathf.Clamp01(_pickerBefore.g), Mathf.Clamp01(_pickerBefore.b), 1f));

        foreach (PickerPart part in PickerRows)
        {
            if (!PickerHasRow(part)) continue;
            DrawPickerRow(box, part, shown);
        }
    }

    private void DrawPickerRow(Rect box, PickerPart part, Color shown)
    {
        Rect row = PickerRowRect(box, part);
        string label = part == PickerPart.Strength ? "強さ" : part == PickerPart.Hex ? "HEX" : part.ToString();
        GUI.Label(new Rect(row.x, row.y, PickerLabel, row.height), label, _small);

        if (part != PickerPart.Hex)
        {
            Rect bar = PickerBar(row);
            const int steps = 24;
            for (int i = 0; i < steps; i++)
            {
                float x = bar.x + bar.width * i / steps;
                Fill(new Rect(x, bar.y, bar.width / steps + 0.5f, bar.height), PickerBarColor(part, (i + 0.5f) / steps, shown));
            }
            float at = bar.x + Mathf.Clamp01(PickerGet(part) / PickerMax(part)) * bar.width;
            Fill(new Rect(at - 2, bar.y - 2, 4, bar.height + 4), BorderColor);
            Fill(new Rect(at - 1, bar.y - 2, 2, bar.height + 4), Color.white);
        }

        Rect value = PickerBox(row, part);
        bool editing = _pickerEdit == part;
        Fill(value, editing ? AccentColor : BorderColor);
        Fill(new Rect(value.x + 1, value.y + 1, value.width - 2, value.height - 2), editing && _pickerReplace ? SelectColor : FieldColor);
        string text = editing ? _pickerText + (_pickerReplace ? "" : "|") : PickerString(part);
        GUI.Label(new Rect(value.x + 4, value.y, value.width - 6, value.height), text, _small);
    }

    private Color PickerBarColor(PickerPart part, float t, Color shown)
    {
        switch (part)
        {
            case PickerPart.R: return new Color(t, shown.g, shown.b, 1f);
            case PickerPart.G: return new Color(shown.r, t, shown.b, 1f);
            case PickerPart.B: return new Color(shown.r, shown.g, t, 1f);
            case PickerPart.H: return Color.HSVToRGB(t, 1f, 1f);
            case PickerPart.S: return Color.HSVToRGB(_pickerH, t, _pickerV);
            case PickerPart.V: return Color.HSVToRGB(_pickerH, _pickerS, t);
            default: return new Color(t, t, t, 1f);
        }
    }

    private void EnsurePickerTextures()
    {
        const int size = PickerTextureSize;
        if (_pickerHueTexture == null)
        {
            _pickerHueTexture = new Texture2D(1, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++) _pickerHueTexture.SetPixel(0, y, Color.HSVToRGB(y / (size - 1f), 1f, 1f));
            _pickerHueTexture.Apply();
            _owned.Add(_pickerHueTexture);
        }
        if (_pickerSquareTexture == null)
        {
            _pickerSquareTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            _owned.Add(_pickerSquareTexture);
            _pickerSquareHue = -1f;
        }
        if (_pickerSquareHue == _pickerH) return;
        _pickerSquareHue = _pickerH;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = Color.HSVToRGB(_pickerH, x / (size - 1f), y / (size - 1f));
        _pickerSquareTexture.SetPixels(pixels);
        _pickerSquareTexture.Apply();
    }
}
