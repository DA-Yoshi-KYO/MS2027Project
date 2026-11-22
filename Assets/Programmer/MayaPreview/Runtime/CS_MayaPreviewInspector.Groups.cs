using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Groups
 Summary: The shader properties of the Material panel, in groups that open and close.
          - Toon shaders: groups by what they do (基本, 影, 顔のSDF影, リムライト ...).
          - Other shaders (HDRP/Lit ...): groups by the words in the property names.
          - Only 基本 is open at first; what was opened / closed is kept between runs.
          - A closed group shows how many of its values were changed ("* 2").
          - Items that cannot matter for the material are left out (.Relevance).
 History: 2026/11/22 New
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private const string GroupPrefsKey = "MayaPreview.ShaderGroup.";
    private const string BasicGroup = "基本";
    private const string OtherGroup = "その他";

    private class PropertyGroup
    {
        public string title;
        public string note;
        public string prefsKey;
        public bool open;
        public readonly List<int> properties = new List<int>();
    }

    private readonly Dictionary<Shader, List<PropertyGroup>> _propertyGroups = new Dictionary<Shader, List<PropertyGroup>>();

    // 0 / 1 values that are drawn as a check box.
    private static readonly HashSet<string> CheckBoxProps = new HashSet<string>
    {
        "_FaceSDFFlip", "_HairHighlightFlow", "_ClothStitchTwin", "_ClothStitchGrid"
    };

    // Toon shaders: (group, its properties in the order they are shown).
    private static readonly (string title, string[] props)[] ToonGroups =
    {
        (BasicGroup, new[] { "_BaseMap", "_BaseColor", "_CelBrightness", "_NTESaturation" }),
        ("ハイライト", new[] { "_EyeHighlightColor", "_EyeHighlightPower", "_EyeHighlightThreshold", "_EyeHighlightMask" }),
        ("貼り付けテクスチャ", new[] { "_EyeOverlayColorMap", "_EyeOverlayColor", "_EyeOverlayStrength", "_EyeOverlayFollow" }),
        ("光の反射", new[] { "_EyeGlintColor", "_EyeGlintStrength", "_EyeGlintSize" }),
        ("顔のSDF影", new[] { "_FaceSDFMap", "_FaceSDFStrength", "_FaceSDFSoftness", "_FaceSDFFlip" }),
        ("鼻先", new[] { "_NoseHideAngle", "_NoseSinkDepth" }),
        ("顔の向き", new[] { "_FaceAngle" }),
        ("ハイライト", new[] { "_HairHighlightColor", "_HairHighlightStrength", "_HairHighlightPosition", "_HairHighlightSharpness",
                               "_HairHighlightJitter", "_HairHighlightStreaks", "_HairHighlightJitterMap", "_HairHighlightFlow" }),
        ("ハイトマップ", new[] { "_HairHeightMap", "_HairHeightStrength" }),
        ("影", new[] { "_ShadowColor", "_CelShadowStrength", "_CelShadowThreshold", "_CelShadowSoftness", "_CelShadowSaturation",
                       "_DeepShadowColor", "_DeepShadowStrength", "_CelCastShadowStrength", "_LightSharpness" }),
        ("ハイライト", new[] { "_CelSpecColor", "_CelSpecStrength", "_CelSpecSize" }),
        ("質感", new[] { "_DetailStrength", "_DetailScale", "_FabricSheen" }),
        ("織り目", new[] { "_ClothThreadSize", "_ClothThreadAspect", "_ClothThreadAngle", "_ClothThreadBump", "_ClothThreadShade",
                           "_ClothThreadMap" }),
        ("毛羽・色むら", new[] { "_ClothFuzzStrength", "_ClothFuzzColor", "_ClothFuzzSize", "_ClothMottle", "_ClothFuzzMap" }),
        ("光沢", new[] { "_ClothGlossStrength", "_ClothGlossColor", "_ClothSmoothness", "_ClothThreadSmoothness", "_ClothAnisotropy",
                         "_ClothSheenStrength", "_ClothSheenColor" }),
        ("縫い目", new[] { "_ClothStitchStrength", "_ClothStitchColor", "_ClothStitchLength", "_ClothStitchWidth", "_ClothStitchSpacing",
                           "_ClothStitchOffset", "_ClothStitchAngle", "_ClothStitchTwin", "_ClothStitchGrid", "_ClothStitchMask" }),
        ("上下の明るさ", new[] { "_SkyLight", "_BottomShade", "_BackFaceShade" }),
        ("リムライト", new[] { "_CelRimColor", "_CelRimStrength", "_CelRimWidth" }),
    };

    private static readonly (string title, string[] props)[] OutlineGroups =
    {
        (BasicGroup, new[] { "_OutlineColor", "_OutlineWidth", "_OutlineMaxPixels" }),
        ("線の色のなじませ", new[] { "_OutlineTexBlend", "_OutlineTexDarken", "_BaseMap" }),
        ("線を消す", new[] { "_OutlineDepthPush", "_OutlineWidthMask" }),
    };

    // Other shaders: the first group with a word that is in the property name.
    private static readonly (string title, string[] words)[] WordGroups =
    {
        ("発光", new[] { "emiss" }),
        ("凹凸", new[] { "normal", "bump", "height", "parallax", "displacement", "tessellation" }),
        ("ディテール", new[] { "detail" }),
        ("透明・切り抜き", new[] { "transparen", "alpha", "refraction", "surfacetype", "blendmode", "opacity" }),
        ("影", new[] { "shadow", "occlusion", "shade" }),
        ("反射・質感", new[] { "metallic", "smoothness", "specular", "coat", "anisotrop", "iridescen", "mask", "ssr" }),
        ("リムライト", new[] { "rim" }),
        (BasicGroup, new[] { "basecolor", "basemap", "maintex", "color", "bright", "saturation" }),
    };

    // Said under the group's title when it is open.
    private static string GroupNote(Shader shader, string title)
    {
        if (shader.name == CS_MayaPreviewMeshData.ClothShader && title == "縫い目")
            return "「濃さ」を上げると列で縫い目が出ます。場所を絞る時は「縫う場所」に白黒の画像を入れます。";
        if (shader.name != CS_MayaPreviewMeshData.SkinShader) return null;
        if (title == "鼻先") return "このマテリアルのメッシュ全体が、横顔で顔に沈んで消えます（線も出ません）。";
        if (title == "顔の向き") return "SDF影と鼻先に使います。Maya で +Z 向きに作ったモデルは 0 のままで合います。";
        if (title == "顔のSDF影") return "SDFマップを入れると、顔の影がこのマップの形になります。";
        return null;
    }

    private List<PropertyGroup> GroupsOf(Shader shader)
    {
        if (_propertyGroups.TryGetValue(shader, out List<PropertyGroup> groups)) return groups;
        groups = new List<PropertyGroup>();

        var remaining = new List<int>();
        int count = shader.GetPropertyCount();
        for (int p = 0; p < count; p++)
        {
            ShaderPropertyFlags flags = shader.GetPropertyFlags(p);
            if ((flags & (ShaderPropertyFlags.HideInInspector | ShaderPropertyFlags.PerRendererData)) != 0) continue;
            remaining.Add(p);
        }

        (string title, string[] props)[] table =
            shader.name == CS_MayaPreviewOutline.ShaderName ? OutlineGroups
            : shader.name.Contains("SHG_NTE_") ? ToonGroups
            : null;
        if (table != null)
        {
            foreach ((string title, string[] props) in table)
            {
                foreach (string prop in props)
                {
                    int index = shader.FindPropertyIndex(prop);
                    if (index >= 0 && remaining.Remove(index)) GroupNamed(groups, shader, title).properties.Add(index);
                }
            }
        }
        else
        {
            GroupNamed(groups, shader, BasicGroup);     // first, whatever order the shader has
            foreach (int index in remaining.ToArray())
            {
                string name = shader.GetPropertyName(index).ToLowerInvariant();
                foreach ((string title, string[] words) in WordGroups)
                {
                    if (!System.Array.Exists(words, word => name.Contains(word))) continue;
                    GroupNamed(groups, shader, title).properties.Add(index);
                    remaining.Remove(index);
                    break;
                }
            }
        }
        foreach (int index in remaining) GroupNamed(groups, shader, OtherGroup).properties.Add(index);
        groups.RemoveAll(group => group.properties.Count == 0);

        for (int i = 0; i < groups.Count; i++)
            groups[i].open = PlayerPrefs.GetInt(groups[i].prefsKey, i == 0 ? 1 : 0) != 0;
        _propertyGroups[shader] = groups;
        return groups;
    }

    private static PropertyGroup GroupNamed(List<PropertyGroup> groups, Shader shader, string title)
    {
        PropertyGroup group = groups.Find(g => g.title == title);
        if (group != null) return group;
        group = new PropertyGroup { title = title, note = GroupNote(shader, title), prefsKey = GroupPrefsKey + shader.name + "." + title };
        groups.Add(group);
        return group;
    }

    private static void SetGroupOpen(PropertyGroup group, bool open)
    {
        if (group.open == open) return;
        group.open = open;
        PlayerPrefs.SetInt(group.prefsKey, open ? 1 : 0);
    }

    private void DrawGroupButtons(List<PropertyGroup> groups)
    {
        if (groups.Count < 2) return;
        GUILayout.Space(3);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("全部開く", _button, GUILayout.Width(64), GUILayout.Height(RowHeight)))
            foreach (PropertyGroup group in groups) SetGroupOpen(group, true);
        if (GUILayout.Button("全部閉じる", _button, GUILayout.Width(72), GUILayout.Height(RowHeight)))
            foreach (PropertyGroup group in groups) SetGroupOpen(group, false);
        GUILayout.Space(6);
        GUILayout.EndHorizontal();
    }

    // The title bar of a group. Returns whether it is open.
    private bool DrawGroupHeader(PropertyGroup group, int shown, int changed)
    {
        GUILayout.Space(3);
        Rect bar = GUILayoutUtility.GetRect(10, 21, GUILayout.ExpandWidth(true));
        if (GUI.Button(bar, GUIContent.none, GUIStyle.none))
        {
            SetGroupOpen(group, !group.open);
            GUI.FocusControl(null);
        }
        Fill(bar, SectionColor);
        Fill(new Rect(bar.x, bar.y, bar.width, 1), BorderColor);
        if (bar.Contains(Event.current.mousePosition)) Fill(bar, HoverColor);
        DrawTinted(new Rect(bar.x + 6, bar.y + 6, 9, 9), group.open ? CS_MayaPreviewIcons.ArrowDown : CS_MayaPreviewIcons.ArrowRight, DimColor);
        GUI.Label(new Rect(bar.x + 20, bar.y, bar.width - 90, bar.height), group.title, _section);
        string count = (changed > 0 ? "* " + changed + "   " : "") + shown + " 項目";
        GUI.Label(new Rect(bar.x + 20, bar.y, bar.width - 28, bar.height), count, _smallRight);
        if (group.open) GUILayout.Space(3);
        return group.open;
    }
}
