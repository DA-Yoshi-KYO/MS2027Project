using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/*
+=====================================
 File   : CS_MayaPreviewIcons
 Summary: Type icons for the preview GUI (Hierarchy rows and the markers drawn in the view).
          Glyphs are rasterised once from signed distance shapes, so they stay sharp and
          never depend on a font. White glyph + tint colour per type.
 History: 2026/10/11 New (replaces the line-drawn icons that scattered yellow dots)
=====================================+
*/
public enum CS_MayaPreviewKind
{
    Empty, Group, Mesh, Skinned, Character, MayaMesh, MayaGroup,
    DirectionalLight, PointLight, SpotLight, AreaLight, Camera, Volume, Audio, Probe
}

public static class CS_MayaPreviewIcons
{
    private const int Size = 32;
    private static readonly Vector2 C = new Vector2(16f, 16f);
    private static readonly Dictionary<CS_MayaPreviewKind, Texture2D> s_icons = new Dictionary<CS_MayaPreviewKind, Texture2D>();
    private static Texture2D s_disc, s_ring, s_right, s_down, s_close;


    // =========================================================
    // Public
    // =========================================================

    public static Texture2D Get(CS_MayaPreviewKind kind)
    {
        if (!s_icons.TryGetValue(kind, out Texture2D texture) || texture == null)
        {
            texture = Build(Glyph(kind));
            s_icons[kind] = texture;
        }
        return texture;
    }

    public static Texture2D Disc => s_disc != null ? s_disc : s_disc = Build(p => Cov(Circle(p, C, 15.2f)));
    public static Texture2D Ring => s_ring != null ? s_ring : s_ring = Build(p => Cov(RingD(p, C, 14.2f, 2.2f)));
    public static Texture2D ArrowRight => s_right != null ? s_right : s_right = Build(p => Cov(Tri(p, new Vector2(11, 8), new Vector2(22, 16), new Vector2(11, 24))));
    public static Texture2D ArrowDown => s_down != null ? s_down : s_down = Build(p => Cov(Tri(p, new Vector2(8, 11), new Vector2(24, 11), new Vector2(16, 22))));
    public static Texture2D Close => s_close != null ? s_close : s_close = Build(p => Mathf.Max(
        Cov(Seg(p, new Vector2(9, 9), new Vector2(23, 23), 3f)), Cov(Seg(p, new Vector2(23, 9), new Vector2(9, 23), 3f))));

    public static Color Tint(CS_MayaPreviewKind kind)
    {
        switch (kind)
        {
            case CS_MayaPreviewKind.DirectionalLight: return new Color(1f, 0.82f, 0.28f);
            case CS_MayaPreviewKind.PointLight:
            case CS_MayaPreviewKind.SpotLight:
            case CS_MayaPreviewKind.AreaLight: return new Color(1f, 0.92f, 0.55f);
            case CS_MayaPreviewKind.Camera: return new Color(0.55f, 0.80f, 1f);
            case CS_MayaPreviewKind.Volume: return new Color(0.55f, 0.95f, 0.62f);
            case CS_MayaPreviewKind.Audio: return new Color(1f, 0.66f, 0.36f);
            case CS_MayaPreviewKind.Probe: return new Color(0.80f, 0.70f, 1f);
            case CS_MayaPreviewKind.Character:
            case CS_MayaPreviewKind.Skinned: return new Color(0.98f, 0.70f, 0.82f);
            case CS_MayaPreviewKind.MayaMesh: return new Color(0.35f, 0.86f, 0.86f);
            case CS_MayaPreviewKind.MayaGroup: return new Color(0.40f, 0.78f, 0.90f);
            case CS_MayaPreviewKind.Mesh: return new Color(0.76f, 0.82f, 0.92f);
            case CS_MayaPreviewKind.Group: return new Color(0.88f, 0.80f, 0.55f);
            default: return new Color(0.62f, 0.62f, 0.62f);
        }
    }

    public static string Label(CS_MayaPreviewKind kind)
    {
        switch (kind)
        {
            case CS_MayaPreviewKind.DirectionalLight: return "Directional";
            case CS_MayaPreviewKind.PointLight: return "Point light";
            case CS_MayaPreviewKind.SpotLight: return "Spot light";
            case CS_MayaPreviewKind.AreaLight: return "Area light";
            case CS_MayaPreviewKind.Camera: return "Camera";
            case CS_MayaPreviewKind.Volume: return "Volume";
            case CS_MayaPreviewKind.Audio: return "Audio";
            case CS_MayaPreviewKind.Probe: return "Probe";
            case CS_MayaPreviewKind.Character: return "Character";
            case CS_MayaPreviewKind.Skinned: return "Skinned";
            case CS_MayaPreviewKind.MayaMesh: return "Maya";
            case CS_MayaPreviewKind.MayaGroup: return "Maya group";
            case CS_MayaPreviewKind.Mesh: return "Mesh";
            case CS_MayaPreviewKind.Group: return "Group";
            default: return "";
        }
    }

    // Objects drawn as a marker in the view (they have no surface to click).
    public static bool IsMarker(CS_MayaPreviewKind kind) =>
        kind == CS_MayaPreviewKind.DirectionalLight || kind == CS_MayaPreviewKind.PointLight ||
        kind == CS_MayaPreviewKind.SpotLight || kind == CS_MayaPreviewKind.AreaLight ||
        kind == CS_MayaPreviewKind.Camera || kind == CS_MayaPreviewKind.Volume ||
        kind == CS_MayaPreviewKind.Audio || kind == CS_MayaPreviewKind.Probe;

    public static CS_MayaPreviewKind KindOf(Transform t)
    {
        if (t == null) return CS_MayaPreviewKind.Empty;
        if (t.TryGetComponent(out Light light))
        {
            switch (light.type)
            {
                case LightType.Directional: return CS_MayaPreviewKind.DirectionalLight;
                case LightType.Spot: return CS_MayaPreviewKind.SpotLight;
                case LightType.Point: return CS_MayaPreviewKind.PointLight;
                default: return CS_MayaPreviewKind.AreaLight;
            }
        }
        if (t.TryGetComponent(out Camera _)) return CS_MayaPreviewKind.Camera;
        if (t.TryGetComponent(out Volume _)) return CS_MayaPreviewKind.Volume;
        if (t.TryGetComponent(out AudioSource _)) return CS_MayaPreviewKind.Audio;
        if (t.TryGetComponent(out ReflectionProbe _)) return CS_MayaPreviewKind.Probe;
        if (t.TryGetComponent(out Animator _)) return CS_MayaPreviewKind.Character;
        if (t.TryGetComponent(out SkinnedMeshRenderer _)) return CS_MayaPreviewKind.Skinned;
        if (t.TryGetComponent(out MeshRenderer _))
            return t.name.StartsWith("MayaLive_") ? CS_MayaPreviewKind.MayaMesh : CS_MayaPreviewKind.Mesh;
        return t.childCount > 0 ? CS_MayaPreviewKind.Group : CS_MayaPreviewKind.Empty;
    }


    // =========================================================
    // Glyphs (32 x 32, y down)
    // =========================================================

    private static Func<Vector2, float> Glyph(CS_MayaPreviewKind kind)
    {
        switch (kind)
        {
            case CS_MayaPreviewKind.DirectionalLight:
                return p =>
                {
                    float c = Cov(Circle(p, C, 5.5f));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        c = Mathf.Max(c, Cov(Seg(p, C + d * 9f, C + d * 13.5f, 2.4f)));
                    }
                    return c;
                };
            case CS_MayaPreviewKind.PointLight:
                return p => Max(Cov(Circle(p, new Vector2(16, 12.5f), 7.5f)),
                    Cov(Box(p, new Vector2(16, 22.5f), new Vector2(4f, 2.6f), 1f)),
                    Cov(Box(p, new Vector2(16, 27f), new Vector2(2.6f, 1.3f), 0.8f)));
            case CS_MayaPreviewKind.SpotLight:
                return p => Max(Cov(Circle(p, new Vector2(16, 7f), 3.6f)),
                    Cov(Seg(p, new Vector2(16, 8), new Vector2(7.5f, 26), 2.3f)),
                    Cov(Seg(p, new Vector2(16, 8), new Vector2(24.5f, 26), 2.3f)),
                    Cov(Seg(p, new Vector2(7.5f, 26), new Vector2(24.5f, 26), 2.3f)));
            case CS_MayaPreviewKind.AreaLight:
                return p => Max(Cov(Mathf.Abs(Box(p, C, new Vector2(10, 10), 2f)) - 1.1f),
                    Cov(Seg(p, new Vector2(10, 22), new Vector2(22, 10), 2f)),
                    Cov(Seg(p, new Vector2(10, 15.5f), new Vector2(15.5f, 10), 2f)),
                    Cov(Seg(p, new Vector2(16.5f, 22), new Vector2(22, 16.5f), 2f)));
            case CS_MayaPreviewKind.Camera:
                return p => Max(Cov(Box(p, new Vector2(12.5f, 16), new Vector2(8.5f, 6.5f), 2f)),
                                Cov(Tri(p, new Vector2(20, 16), new Vector2(29, 10), new Vector2(29, 22))))
                            * (1f - Cov(Circle(p, new Vector2(12.5f, 16), 3f)));
            case CS_MayaPreviewKind.Volume:
                return p =>
                {
                    Vector2 a = new Vector2(6, 12), b = new Vector2(20, 12), c = new Vector2(20, 26), d = new Vector2(6, 26);
                    Vector2 o = new Vector2(6, -6);
                    float w = 1.9f;
                    return Max(Cov(Seg(p, a, b, w)), Cov(Seg(p, b, c, w)), Cov(Seg(p, c, d, w)), Cov(Seg(p, d, a, w)),
                        Cov(Seg(p, a + o, b + o, w)), Cov(Seg(p, b + o, c + o, w)),
                        Cov(Seg(p, a, a + o, w)), Cov(Seg(p, b, b + o, w)), Cov(Seg(p, c, c + o, w)),
                        Cov(Box(p, (a + c) * 0.5f, new Vector2(5, 5), 1f)) * 0.55f);
                };
            case CS_MayaPreviewKind.Audio:
                return p =>
                {
                    float arcs = p.x > 20.5f && Mathf.Abs(p.y - 16f) < 8.5f
                        ? Mathf.Max(Cov(RingD(p, new Vector2(16, 16), 7f, 2f)), Cov(RingD(p, new Vector2(16, 16), 11.5f, 2f)))
                        : 0f;
                    return Max(Cov(Box(p, new Vector2(8, 16), new Vector2(3f, 4.5f), 0.8f)),
                        Cov(Tri(p, new Vector2(9.5f, 16), new Vector2(18, 7), new Vector2(18, 25))), arcs);
                };
            case CS_MayaPreviewKind.Probe:
                return p => Max(Cov(RingD(p, C, 10f, 2.2f)), Cov(Circle(p, new Vector2(12.5f, 12.5f), 2.8f)));
            case CS_MayaPreviewKind.Character:
            case CS_MayaPreviewKind.Skinned:
                return p => Max(Cov(Circle(p, new Vector2(16, 7.5f), 4.2f)),
                    Cov(Seg(p, new Vector2(16, 13), new Vector2(16, 20.5f), 5.6f)),
                    Cov(Seg(p, new Vector2(8, 14.5f), new Vector2(24, 14.5f), 2.6f)),
                    Cov(Seg(p, new Vector2(14.2f, 21), new Vector2(12.5f, 28), 2.8f)),
                    Cov(Seg(p, new Vector2(17.8f, 21), new Vector2(19.5f, 28), 2.8f)));
            case CS_MayaPreviewKind.Mesh:
            case CS_MayaPreviewKind.MayaMesh:
                return p =>
                {
                    Vector2 top = new Vector2(16, 4.5f), ur = new Vector2(26.5f, 10.5f), lr = new Vector2(26.5f, 21.5f);
                    Vector2 bottom = new Vector2(16, 27.5f), ll = new Vector2(5.5f, 21.5f), ul = new Vector2(5.5f, 10.5f);
                    Vector2 mid = new Vector2(16, 16.5f);
                    float w = 2.1f;
                    return Max(Cov(Seg(p, top, ur, w)), Cov(Seg(p, ur, lr, w)), Cov(Seg(p, lr, bottom, w)),
                        Cov(Seg(p, bottom, ll, w)), Cov(Seg(p, ll, ul, w)), Cov(Seg(p, ul, top, w)),
                        Cov(Seg(p, mid, bottom, w)), Cov(Seg(p, mid, ul, w)), Cov(Seg(p, mid, ur, w)));
                };
            case CS_MayaPreviewKind.Group:
            case CS_MayaPreviewKind.MayaGroup:
                return p => Max(Cov(Box(p, new Vector2(10.5f, 9.5f), new Vector2(5.5f, 2.6f), 1.4f)),
                    Cov(Mathf.Abs(Box(p, new Vector2(16, 18.5f), new Vector2(11.5f, 8f), 2f)) - 1.15f),
                    Cov(Box(p, new Vector2(16, 18.5f), new Vector2(11.5f, 8f), 2f)) * 0.35f);
            default:
                return p => Max(Cov((Mathf.Abs(Mathf.Abs(p.x - 16f) + Mathf.Abs(p.y - 16f) - 9f)) * 0.7071f - 1.1f),
                    Cov(Circle(p, C, 2.2f)));
        }
    }


    // =========================================================
    // Signed distances (pixels)
    // =========================================================

    private static float Cov(float distance) => Mathf.Clamp01(0.5f - distance);

    private static float Max(params float[] values)
    {
        float result = 0f;
        foreach (float value in values) if (value > result) result = value;
        return result;
    }

    private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

    private static float RingD(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs((p - c).magnitude - r) - w * 0.5f;

    private static float SegDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-6f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude;
    }

    private static float Seg(Vector2 p, Vector2 a, Vector2 b, float w) => SegDistance(p, a, b) - w * 0.5f;

    private static float Box(Vector2 p, Vector2 c, Vector2 half, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    private static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d = Mathf.Min(SegDistance(p, a, b), Mathf.Min(SegDistance(p, b, c), SegDistance(p, c, a)));
        float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
        bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
        return inside ? -d : d;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static Texture2D Build(Func<Vector2, float> coverage)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // Texture rows start at the bottom; glyphs are written with y down.
                float alpha = coverage(new Vector2(x + 0.5f, Size - 1 - y + 0.5f));
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
