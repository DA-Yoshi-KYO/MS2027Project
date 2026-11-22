using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Fields
 Summary: Field widgets (label column + value column, one line each, no wrapping) and the
          dark skin of the preview GUI.
 History: 2026/10/11 Split from CS_MayaPreviewInspector, ellipsis + tooltips, compact colours
          2026/11/22 Swatch opens the palette, texture slots have a button that picks the file
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    // =========================================================
    // Field state
    // =========================================================

    private readonly Dictionary<string, string> _fields = new Dictionary<string, string>();
    private string _pickedId, _pickedPath;      // answer of the file dialog, taken by its PathField
    private readonly HashSet<string> _foldedSections = new HashSet<string>();
    private string _fieldScope = "";
    private float _labelWidth = 150f;

    // Safe to call from anywhere (GUI functions only run inside OnGUI).
    private bool _dropFocus;
    private void ClearFields()
    {
        _fields.Clear();
        _dropFocus = true;
        _typing = false;
    }

    private string FieldId(string key) => "F." + _fieldScope + key;


    // =========================================================
    // Rows
    // =========================================================

    // foldable = false: a plain header that is always open (no fold arrow).
    private bool Section(string title, Texture2D icon = null, Color? tint = null, bool foldable = true)
    {
        GUILayout.Space(3);
        Rect bar = GUILayoutUtility.GetRect(10, 21, GUILayout.ExpandWidth(true));
        Fill(bar, SectionColor);
        Fill(new Rect(bar.x, bar.y, bar.width, 1), BorderColor);
        bool open = !foldable || !_foldedSections.Contains(title);
        if (foldable && GUI.Button(bar, GUIContent.none, GUIStyle.none))
        {
            if (open) _foldedSections.Add(title); else _foldedSections.Remove(title);
            open = !open;
            GUI.FocusControl(null);
        }
        if (foldable)
            DrawTinted(new Rect(bar.x + 6, bar.y + 6, 9, 9), open ? CS_MayaPreviewIcons.ArrowDown : CS_MayaPreviewIcons.ArrowRight, DimColor);
        float x = bar.x + 20;
        if (icon != null) { DrawTinted(new Rect(x, bar.y + 3, 15, 15), icon, tint ?? Color.white); x += 19; }
        GUI.Label(new Rect(x, bar.y, bar.xMax - x - 6, bar.height), Fit(title, _section, bar.xMax - x - 6), _section);
        if (open) GUILayout.Space(3);
        return open;
    }

    private void SubHeader(string title)
    {
        GUILayout.Space(4);
        GUILayout.BeginHorizontal(); GUILayout.Space(8); GUILayout.Label(title, _bold); GUILayout.EndHorizontal();
    }

    private void Note(string text)
    {
        GUILayout.BeginHorizontal(); GUILayout.Space(8); GUILayout.Label(text, _wrapDim); GUILayout.Space(6); GUILayout.EndHorizontal();
    }

    private void BeginRow(string label, string tooltip = null)
    {
        GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
        GUILayout.Space(10);
        Rect rect = GUILayoutUtility.GetRect(_labelWidth, RowHeight, GUILayout.Width(_labelWidth));
        string shown = Fit(label, _skin.label, rect.width - 4);
        GUI.Label(rect, shown, _skin.label);
        if (rect.Contains(Event.current.mousePosition) && (shown != label || !string.IsNullOrEmpty(tooltip)))
            _tooltip = string.IsNullOrEmpty(tooltip) ? label : label + "   " + tooltip;
    }

    private void EndRow()
    {
        GUILayout.Space(6);
        GUILayout.EndHorizontal();
        GUILayout.Space(2);
    }

    // Buttons of one width in columns, whatever is written on them. Returns the one clicked, or -1.
    private int ButtonGrid(string label, string tooltip, string[] names, int on, int columns)
    {
        const float gap = 3f;
        int clicked = -1;
        for (int start = 0; start < names.Length; start += columns)
        {
            BeginRow(start == 0 ? label : "", start == 0 ? tooltip : null);
            Rect row = GUILayoutUtility.GetRect(0f, 10000f, RowHeight, RowHeight, GUILayout.ExpandWidth(true));
            float width = Mathf.Max(1f, (row.width - gap * (columns - 1)) / columns);
            for (int i = start; i < Mathf.Min(start + columns, names.Length); i++)
            {
                var cell = new Rect(Mathf.Round(row.x + (i - start) * (width + gap)), row.y, Mathf.Floor(width), row.height);
                if (!GUI.Button(cell, names[i], i == on ? _buttonOn : _button)) continue;
                clicked = i;
                GUI.FocusControl(null);
            }
            EndRow();
        }
        return clicked;
    }

    // Columns in which every name fits (both blocks of a panel get the same, so all their buttons are one width).
    // The width is the one of the last repaint: Layout and Repaint of one frame must count the same rows.
    private float _buttonBlockWidth;

    private int FittingColumns(string[] first, string[] second, int most)
    {
        if (_buttonBlockWidth <= 1f) return Mathf.Min(2, most);
        float widest = 0f;
        foreach (string[] names in new[] { first, second })
            foreach (string name in names)
                widest = Mathf.Max(widest, _buttonOn.CalcSize(new GUIContent(name)).x);
        return Mathf.Clamp(Mathf.FloorToInt((_buttonBlockWidth + GridGap) / (Mathf.Ceil(widest) + 1f + GridGap)), 1, most);
    }

    private const float GridGap = 3f;

    // A title with its buttons under it, over the whole width of the panel. cannot = why a button cannot be
    // picked (it is grey then), or null. Returns the one clicked, or -1.
    private int ButtonBlock(string title, string tooltip, string[] names, int on, int columns, System.Func<int, string> cannot = null)
    {
        GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
        GUILayout.Space(10);
        Rect head = GUILayoutUtility.GetRect(0f, 10000f, RowHeight, RowHeight, GUILayout.ExpandWidth(true));
        GUI.Label(head, title, _skin.label);
        if (head.Contains(Event.current.mousePosition)) _tooltip = title + "   " + tooltip;
        GUILayout.Space(6);
        GUILayout.EndHorizontal();

        int clicked = -1;
        for (int start = 0; start < names.Length; start += columns)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            Rect row = GUILayoutUtility.GetRect(0f, 10000f, RowHeight, RowHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) _buttonBlockWidth = row.width;
            float width = Mathf.Max(1f, Mathf.Floor((row.width - GridGap * (columns - 1)) / columns));
            for (int i = start; i < Mathf.Min(start + columns, names.Length); i++)
            {
                var cell = new Rect(Mathf.Round(row.x + (i - start) * (width + GridGap)), row.y, width, row.height);
                string reason = cannot?.Invoke(i);
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && reason == null;
                if (GUI.Button(cell, names[i], i == on ? _buttonOn : _button))
                {
                    clicked = i;
                    GUI.FocusControl(null);
                }
                GUI.enabled = enabled;
                if (reason != null && cell.Contains(Event.current.mousePosition)) _tooltip = reason;
            }
            GUILayout.Space(6);
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
        return clicked;
    }

    // Text that fits the width, ending in "…" when it is too long.
    private readonly Dictionary<string, string> _fitCache = new Dictionary<string, string>();
    private string Fit(string text, GUIStyle style, float width)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string key = text + "|" + Mathf.RoundToInt(width) + "|" + style.fontSize + "|" + (int)style.fontStyle;
        if (_fitCache.TryGetValue(key, out string fitted)) return fitted;
        if (style.CalcSize(new GUIContent(text)).x <= width) fitted = text;
        else
        {
            int low = 0, high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (style.CalcSize(new GUIContent(text.Substring(0, mid) + "…")).x <= width) low = mid;
                else high = mid - 1;
            }
            fitted = text.Substring(0, low) + "…";
        }
        if (_fitCache.Count > 4000) _fitCache.Clear();
        _fitCache[key] = fitted;
        return fitted;
    }


    // =========================================================
    // Widgets (return the edited value; callers record undo before applying)
    // =========================================================

    // X / Y / Z labels are drag handles (see Scrub): left drag = 1 per step, middle drag = ladder.
    private Vector3 VectorField(string title, Vector3 value)
    {
        BeginRow(title);
        value.x = NumberBox(title + ".x", Scrub(title + ".x", value.x, "X", _axisX));
        value.y = NumberBox(title + ".y", Scrub(title + ".y", value.y, "Y", _axisY));
        value.z = NumberBox(title + ".z", Scrub(title + ".z", value.z, "Z", _axisZ));
        EndRow();
        return value;
    }

    private float Number(string label, float value, string key = null, string tooltip = null)
    {
        BeginRow(label, tooltip);
        value = NumberBox(key ?? label, value);
        EndRow();
        return value;
    }

    private float Slider(string label, float value, float min, float max, string key = null, string tooltip = null)
    {
        key = key ?? label;
        BeginRow(label, tooltip);
        float slid = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
        if (slid != value) { value = slid; _fields.Remove(FieldId(key)); }
        value = Mathf.Clamp(NumberBox(key, value, 54), min, max);
        EndRow();
        return value;
    }

    private Vector4 Vector4Field(string label, Vector4 value, string key, string tooltip = null)
    {
        BeginRow(label, tooltip);
        value.x = NumberBox(key + ".x", value.x);
        value.y = NumberBox(key + ".y", value.y);
        value.z = NumberBox(key + ".z", value.z);
        value.w = NumberBox(key + ".w", value.w);
        EndRow();
        return value;
    }

    private bool Toggle(string label, bool value, string tooltip = null)
    {
        BeginRow(label, tooltip);
        Rect box = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
        box.y += 2;
        if (GUI.Button(box, GUIContent.none, GUIStyle.none)) { value = !value; GUI.FocusControl(null); }
        if (Event.current.type == EventType.Repaint)
        {
            Fill(box, BorderColor);
            Fill(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), FieldColor);
            if (value) Fill(new Rect(box.x + 3, box.y + 3, box.width - 6, box.height - 6), AccentColor);
        }
        GUILayout.FlexibleSpace();
        EndRow();
        return value;
    }

    // Colour: swatch + hex on one line; click the swatch for the palette (.ColorPicker).
    // alpha = false: the A is not read, so it is neither shown nor changed.
    private Color ColorField(string label, Color value, bool hdr, string key, string tooltip = null, bool alpha = true)
    {
        string id = FieldId(key);
        Color before = value;
        value = SyncColorPicker(id, value);
        if (value != before) _fields.Remove(FieldId(key + ".hex"));
        BeginRow(label, tooltip);
        Rect swatch = GUILayoutUtility.GetRect(40, 14, GUILayout.Width(46));
        swatch.y += 2;
        if (GUI.enabled && swatch.Contains(Event.current.mousePosition)) _tooltip = "クリックでカラーパレット";
        if (GUI.Button(swatch, GUIContent.none, GUIStyle.none))
        {
            GUI.FocusControl(null);
            OpenColorPicker(id, label, value, hdr, alpha);
        }
        if (Event.current.type == EventType.Repaint)
        {
            Fill(swatch, _pickerId == id ? AccentColor : BorderColor);
            Color shown = hdr ? new Color(Mathf.Clamp01(value.r), Mathf.Clamp01(value.g), Mathf.Clamp01(value.b), 1) : new Color(value.r, value.g, value.b, 1);
            if (!GUI.enabled) shown = Color.Lerp(shown, PanelColor, 0.65f);
            shown.a = 1f;
            Fill(new Rect(swatch.x + 1, swatch.y + 1, swatch.width - 2, swatch.height - (alpha ? 4 : 2)), shown);
            if (alpha) Fill(new Rect(swatch.x + 1, swatch.yMax - 3, (swatch.width - 2) * Mathf.Clamp01(value.a), 2), Color.white);
        }
        if (hdr)
        {
            float max = Mathf.Max(value.r, Mathf.Max(value.g, value.b));
            GUILayout.Label(max > 1f ? "HDR x" + max.ToString("0.##", CultureInfo.InvariantCulture) : "HDR", _dim, GUILayout.Width(70));
            GUILayout.FlexibleSpace();
        }
        else
        {
            string hex = "#" + (alpha ? ColorUtility.ToHtmlStringRGBA(value) : ColorUtility.ToHtmlStringRGB(value));
            string text = TextBox(key + ".hex", hex, 0);
            if (text != hex && ColorUtility.TryParseHtmlString(text.StartsWith("#") ? text : "#" + text, out Color parsed))
            {
                if (!alpha) parsed.a = value.a;
                value = parsed;
            }
        }
        EndRow();
        return value;
    }

    // Texture slot: thumbnail + file path + folder button (thumbnail and button open the file dialog).
    // Returns the new path when a valid file (or empty) is entered or picked, else null.
    private string PathField(string label, string path, Texture texture, string key, string tooltip = null)
    {
        string id = FieldId(key);
        string picked = null;
        if (_pickedId == id)
        {
            picked = _pickedPath;
            _pickedId = _pickedPath = null;
            _fields.Remove(FieldId(key + ".path"));
        }
        BeginRow(label, tooltip);
        Rect thumb = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
        thumb.y += 1;
        bool browse = GUI.Button(thumb, GUIContent.none, GUIStyle.none);
        if (Event.current.type == EventType.Repaint)
        {
            Fill(thumb, BorderColor);
            if (texture != null) GUI.DrawTexture(new Rect(thumb.x + 1, thumb.y + 1, 14, 14), texture, ScaleMode.ScaleAndCrop, false);
            else Fill(new Rect(thumb.x + 1, thumb.y + 1, 14, 14), FieldColor);
        }
        if (thumb.Contains(Event.current.mousePosition))
            _tooltip = (texture != null ? texture.name : "None") + "  （クリックで画像を選ぶ）";
        GUILayout.Space(3);
        string shown = path;
        if (string.IsNullOrEmpty(shown) && texture != null) shown = "";
        string text = TextBox(key + ".path", shown ?? "", 0, string.IsNullOrEmpty(path) ? (texture != null ? texture.name : "None（右のボタンで選ぶ）") : null);
        bool changed = text != (shown ?? "");

        Rect folder = GUILayoutUtility.GetRect(22, RowHeight - 2, GUILayout.Width(22));
        if (GUI.Button(folder, GUIContent.none, _button)) browse = true;
        DrawTinted(new Rect(folder.x + 5, folder.y + 2, 12, 12), CS_MayaPreviewIcons.Get(CS_MayaPreviewKind.MayaGroup), TextColor);
        if (folder.Contains(Event.current.mousePosition)) _tooltip = "画像ファイルを選ぶ";
        EndRow();

        if (browse)
        {
            GUI.FocusControl(null);
            CS_MayaPreviewFileDialog.OpenImage(label.TrimStart('*', ' '), path, file => { _pickedId = id; _pickedPath = file; });
        }
        if (picked != null) return picked != path ? picked : null;
        if (!changed) return null;
        string trimmed = text.Trim().Trim('"');
        if (trimmed.Length == 0) return string.IsNullOrEmpty(path) ? null : "";
        string extension = Path.GetExtension(trimmed).ToLowerInvariant();
        bool image = extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        return image && trimmed != path && File.Exists(trimmed) ? trimmed : null;
    }

    // A text box keeps what is typed while it has focus; otherwise it shows the live value.
    private string TextBox(string key, string value, float width, string placeholder = null)
    {
        string id = FieldId(key);
        if (!_fields.ContainsKey(id) || GUI.GetNameOfFocusedControl() != id) _fields[id] = value;
        GUI.SetNextControlName(id);
        string text = width > 0
            ? GUILayout.TextField(_fields[id], GUILayout.Width(width))
            : GUILayout.TextField(_fields[id], GUILayout.MinWidth(30), GUILayout.MaxWidth(2000), GUILayout.ExpandWidth(true));
        if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(placeholder) && Event.current.type == EventType.Repaint)
        {
            Rect last = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(last.x + 5, last.y, last.width - 8, last.height), Fit(placeholder, _small, last.width - 8), _placeholder);
        }
        _fields[id] = text;
        return text;
    }

    private float NumberBox(string key, float value, float width = 0)
    {
        string id = FieldId(key);
        if (!_fields.ContainsKey(id) || GUI.GetNameOfFocusedControl() != id)
            _fields[id] = value.ToString("G6", CultureInfo.InvariantCulture);
        // The box size never depends on its text, so long values do not push the row apart
        // (the text is clipped instead). Boxes in one row share the width equally.
        GUIStyle style = GUI.skin.textField;
        Rect rect = width > 0
            ? GUILayoutUtility.GetRect(width, RowHeight - 2, style, GUILayout.Width(width))
            : GUILayoutUtility.GetRect(30, RowHeight - 2, style, GUILayout.MinWidth(30), GUILayout.ExpandWidth(true));
        // Middle button on the box: Houdini-style ladder (see Scrub).
        if (GUI.enabled && rect.Contains(Event.current.mousePosition)) TryStartLadder(id, value);
        if (_scrubKey == id) { value = _scrubValue; _fields[id] = value.ToString("G6", CultureInfo.InvariantCulture); }
        GUI.SetNextControlName(id);
        string text = GUI.TextField(rect, _fields[id], style);
        bool changed = text != _fields[id];
        _fields[id] = text;
        if (changed && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            && !float.IsNaN(parsed) && !float.IsInfinity(parsed)) return parsed;
        return value;
    }


    // =========================================================
    // Look (Unity editor dark theme)
    // =========================================================

    private static readonly Color PanelColor = new Color(0.21f, 0.21f, 0.21f, 0.97f);
    private static readonly Color HeaderColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    private static readonly Color BorderColor = new Color(0.09f, 0.09f, 0.09f, 1f);
    private static readonly Color SectionColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    private static readonly Color StripeColor = new Color(1f, 1f, 1f, 0.025f);
    private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.07f);
    private static readonly Color SelectColor = new Color(0.17f, 0.36f, 0.53f, 1f);
    private static readonly Color TextColor = new Color(0.90f, 0.90f, 0.90f, 1f);
    private static readonly Color DimColor = new Color(0.70f, 0.70f, 0.70f, 1f);
    private static readonly Color FieldColor = new Color(0.165f, 0.165f, 0.165f, 1f);
    private static readonly Color AccentColor = new Color(0.23f, 0.47f, 0.73f, 1f);
    private static readonly Color GuideColor = new Color(1f, 1f, 1f, 0.06f);

    private GUISkin _skin;
    private GUIStyle _title, _dim, _dimRight, _bold, _row, _rowDim, _small, _smallRight, _section, _center,
        _wrapDim, _wrapCenter, _placeholder, _axisX, _axisY, _axisZ,
        _toolbar, _toolbarOn, _toolbarLabel, _toolbarLabelOn, _button, _buttonOn, _chip, _chipOn, _bigButton;
    private readonly List<Object> _owned = new List<Object>();

    private static void Fill(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void DrawTinted(Rect rect, Texture texture, Color color)
    {
        if (Event.current.type != EventType.Repaint || texture == null) return;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        GUI.color = previous;
    }

    private Texture2D Tex(Color fill, Color? border = null, int size = 4)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                texture.SetPixel(x, y, border.HasValue && edge ? border.Value : fill);
            }
        texture.Apply();
        _owned.Add(texture);
        return texture;
    }

    private static void SetBackground(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active)
    {
        style.normal.background = normal; style.hover.background = hover; style.active.background = active;
        style.focused.background = normal;
        style.onNormal.background = active; style.onHover.background = active; style.onActive.background = active;
    }

    private GUIStyle ButtonStyle(Color fill, Color hover, Color border, Color text)
    {
        var one = new RectOffset(1, 1, 1, 1);
        var style = new GUIStyle(_skin.label)
        {
            alignment = TextAnchor.MiddleCenter, border = one, padding = new RectOffset(6, 6, 0, 0),
            margin = new RectOffset(1, 1, 1, 1), wordWrap = false, clipping = TextClipping.Clip
        };
        SetBackground(style, Tex(fill, border), Tex(hover, border), Tex(fill * 0.85f, border));
        style.normal.textColor = text;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        return style;
    }

    private void EnsureSkin()
    {
        if (_skin != null) return;
        _skin = Instantiate(GUI.skin);
        _owned.Add(_skin);
        var one = new RectOffset(1, 1, 1, 1);

        _skin.label = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12, padding = new RectOffset(2, 2, 1, 1), margin = new RectOffset(0, 0, 0, 0),
            alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false
        };
        _skin.label.normal.textColor = TextColor;
        _skin.label.hover.textColor = TextColor;

        _skin.textField = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 12, border = one, padding = new RectOffset(4, 4, 1, 1), margin = new RectOffset(2, 2, 1, 1),
            fixedHeight = RowHeight - 2, wordWrap = false, clipping = TextClipping.Clip
        };
        Texture2D field = Tex(FieldColor, BorderColor);
        Texture2D fieldHover = Tex(FieldColor, new Color(0.3f, 0.3f, 0.3f, 1f));
        Texture2D fieldFocus = Tex(FieldColor, AccentColor);
        _skin.textField.normal.background = field; _skin.textField.hover.background = fieldHover;
        _skin.textField.focused.background = fieldFocus; _skin.textField.active.background = fieldFocus;
        _skin.textField.onNormal.background = field; _skin.textField.onFocused.background = fieldFocus;
        _skin.textField.normal.textColor = _skin.textField.hover.textColor = _skin.textField.focused.textColor = TextColor;

        Texture2D track = new Texture2D(1, 12, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
        for (int y = 0; y < 12; y++) track.SetPixel(0, y, y == 5 || y == 6 ? new Color(0.37f, 0.37f, 0.37f, 1f) : Color.clear);
        track.Apply(); _owned.Add(track);
        _skin.horizontalSlider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 12, border = new RectOffset(0, 0, 0, 0), margin = new RectOffset(2, 4, 3, 0), padding = new RectOffset(0, 0, 0, 0), overflow = new RectOffset(0, 0, 0, 0) };
        _skin.horizontalSlider.normal.background = track;
        _skin.horizontalSliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 8, fixedHeight = 12, border = new RectOffset(0, 0, 0, 0), overflow = new RectOffset(0, 0, 0, 0), padding = new RectOffset(0, 0, 0, 0) };
        SetBackground(_skin.horizontalSliderThumb, Tex(new Color(0.6f, 0.6f, 0.6f, 1f)), Tex(new Color(0.75f, 0.75f, 0.75f, 1f)), Tex(new Color(0.85f, 0.85f, 0.85f, 1f)));

        _skin.verticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar) { fixedWidth = 8, border = new RectOffset(0, 0, 0, 0), margin = new RectOffset(1, 1, 1, 1), padding = new RectOffset(0, 0, 0, 0) };
        _skin.verticalScrollbar.normal.background = Tex(new Color(0.19f, 0.19f, 0.19f, 1f));
        _skin.verticalScrollbarThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb) { fixedWidth = 8, border = new RectOffset(0, 0, 0, 0), padding = new RectOffset(0, 0, 0, 0) };
        SetBackground(_skin.verticalScrollbarThumb, Tex(new Color(0.37f, 0.37f, 0.37f, 1f)), Tex(new Color(0.45f, 0.45f, 0.45f, 1f)), Tex(new Color(0.5f, 0.5f, 0.5f, 1f)));
        _skin.verticalScrollbarUpButton = new GUIStyle { name = "verticalscrollbarupbutton", fixedHeight = 0, fixedWidth = 0 };
        _skin.verticalScrollbarDownButton = new GUIStyle { name = "verticalscrollbardownbutton", fixedHeight = 0, fixedWidth = 0 };
        _skin.scrollView = new GUIStyle { name = "scrollview" };

        _title = new GUIStyle(_skin.label) { fontStyle = FontStyle.Bold };
        _bold = new GUIStyle(_skin.label) { fontStyle = FontStyle.Bold, fixedHeight = RowHeight + 2 };
        _dim = new GUIStyle(_skin.label);
        _dim.normal.textColor = _dim.hover.textColor = DimColor;
        _dimRight = new GUIStyle(_dim) { alignment = TextAnchor.MiddleRight, fontSize = 11 };
        _row = new GUIStyle(_skin.label);
        _rowDim = new GUIStyle(_skin.label);
        _rowDim.normal.textColor = _rowDim.hover.textColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        _small = new GUIStyle(_skin.label) { fontSize = 11 };
        _smallRight = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight };
        _smallRight.normal.textColor = _smallRight.hover.textColor = DimColor;
        _placeholder = new GUIStyle(_small);
        _placeholder.normal.textColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        _center = new GUIStyle(_skin.label) { alignment = TextAnchor.MiddleCenter };
        _wrapDim = new GUIStyle(_dim) { wordWrap = true, fontSize = 11 };
        _wrapCenter = new GUIStyle(_wrapDim) { alignment = TextAnchor.MiddleCenter };
        _axisX = new GUIStyle(_skin.label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0), fontStyle = FontStyle.Bold };
        _axisX.normal.textColor = new Color(0.86f, 0.38f, 0.38f);
        _axisY = new GUIStyle(_axisX); _axisY.normal.textColor = new Color(0.45f, 0.80f, 0.40f);
        _axisZ = new GUIStyle(_axisX); _axisZ.normal.textColor = new Color(0.40f, 0.60f, 0.95f);

        _section = new GUIStyle(_skin.label) { fontStyle = FontStyle.Bold };
        _section.hover.textColor = Color.white;

        _toolbar = ButtonStyle(new Color(0.24f, 0.24f, 0.24f, 0.95f), new Color(0.30f, 0.30f, 0.30f, 0.95f), BorderColor, TextColor);
        _toolbarOn = ButtonStyle(new Color(0.16f, 0.16f, 0.16f, 0.97f), new Color(0.20f, 0.20f, 0.20f, 0.97f), AccentColor, Color.white);
        _toolbarLabel = new GUIStyle(_skin.label);
        _toolbarLabel.normal.textColor = TextColor;
        _toolbarLabelOn = new GUIStyle(_skin.label) { fontStyle = FontStyle.Bold };
        _toolbarLabelOn.normal.textColor = Color.white;
        _button = ButtonStyle(new Color(0.28f, 0.28f, 0.28f, 1f), new Color(0.34f, 0.34f, 0.34f, 1f), BorderColor, TextColor);
        _buttonOn = ButtonStyle(new Color(0.20f, 0.33f, 0.47f, 1f), new Color(0.24f, 0.38f, 0.53f, 1f), AccentColor, Color.white);
        _chip = ButtonStyle(new Color(0.26f, 0.26f, 0.26f, 1f), new Color(0.32f, 0.32f, 0.32f, 1f), BorderColor, TextColor);
        _chip.alignment = TextAnchor.MiddleLeft;
        _chip.padding = new RectOffset(22, 6, 0, 0);
        _chipOn = ButtonStyle(new Color(0.17f, 0.36f, 0.53f, 1f), new Color(0.20f, 0.40f, 0.58f, 1f), AccentColor, Color.white);
        _chipOn.alignment = TextAnchor.MiddleLeft;
        _chipOn.padding = new RectOffset(22, 6, 0, 0);
        _chipOn.fontStyle = FontStyle.Bold;
        _bigButton = ButtonStyle(new Color(0.20f, 0.42f, 0.66f, 1f), new Color(0.25f, 0.48f, 0.74f, 1f), AccentColor, Color.white);
        _bigButton.fontStyle = FontStyle.Bold;
    }
}
