using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/*
 * ネオン看板・ホログラム広告と、AEGISのタワーの見た目を作って街に置くエディタ拡張(画づくり用)
 * Tools/Claude/Build Signage から実行する。Unity MCPのexecute_menu_itemからも呼べる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・文字はNoto Sans JP(OFLライセンス。商用利用可)のTMPフォントのSDFアトラスから描く(外部の画像は使わない)
 *   SDFの値から、文字の形(0.5付近)と、にじんだ光(ぼかし)を作る
 * ・看板は2種類
 *   縦看板: ビルの角から通りへ突き出す日本語の看板(居酒屋・ホテルなど)。通りを歩きながら読める向きに置く
 *   横長の広告: ビルの上の方の壁に貼るホログラム広告(AEGIS・犯罪件数0 など。世界観を伝える)
 * ・置く場所: 外壁(OZ_Facade)のビルのうち、高さが_minBuildingHeight m以上で、壁の前が開けている面
 *   乱数は固定なので、何度実行しても同じ配置になる。置いた物はシーン直下の「OZ_Signage」にまとめる(消す時はそれを消す)
 *   看板の一部には色の付いた小さなライトを付け、濡れた地面に色が映るようにする
 * ・AEGISのタワー: 街の中央(原点付近)の一番高いビルを、縦の光のラインが走る専用の外壁(OZ_Tower)にし、上の方の4面に「AEGIS」の看板を付ける
 */
// ========================================

public static class CSED_ClaudeSignage
{
    private const string _fontPath = "Assets/Programmer/PlaceholderAsset/UI/Font/NotoSansJP-VariableFont_wght SDF.asset";
    private const string _textureFolder = "Assets/Programmer/Texture/OrderZero/Signs";
    private const string _materialFolder = "Assets/Programmer/Material/OrderZero/Signs";
    private const string _rootName = "OZ_Signage";
    private const float _minBuildingHeight = 7f;
    private const int _seed = 2027;

    private static readonly string[] _verticalTexts =
    {
        "居酒屋", "ホテル", "カラオケ", "薬局", "銀行", "電脳", "保険", "整備中", "警備中", "安全",
        "治安", "未来", "酒場", "質屋", "占い", "中華", "寿司", "珈琲", "診療所", "宿",
    };

    private static readonly string[] _horizontalTexts =
    {
        "AEGIS", "ORDER:ZERO", "SECURITY", "KEEP OUT", "犯罪件数 0", "24時間 安全", "AEGIS 治安維持", "定期メンテナンス中",
    };

    private static readonly Color[] _neonColors =
    {
        new Color(0.2f, 0.95f, 1f),    // 水色
        new Color(1f, 0.2f, 0.75f),    // 紫
        new Color(1f, 0.6f, 0.15f),    // 橙
        new Color(1f, 0.18f, 0.22f),   // 赤
        new Color(0.35f, 1f, 0.55f),   // 緑
        new Color(0.9f, 0.95f, 1f),    // 白
    };

    private static TMP_FontAsset _font;
    private static Color[] _atlas;
    private static int _atlasWidth;

    [MenuItem("Tools/Claude/Build Signage")]
    public static void Build()
    {
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(_fontPath);
        if (_font == null)
        {
            Debug.LogError("CSED_ClaudeSignage: フォントがありません: " + _fontPath);
            return;
        }

        Directory.CreateDirectory(_textureFolder);
        Directory.CreateDirectory(_materialFolder);
        ReadAtlas();

        GameObject old = GameObject.Find(_rootName);
        if (old != null) Object.DestroyImmediate(old);
        GameObject root = new GameObject(_rootName);

        List<Material> vertical = new List<Material>();
        for (int i = 0; i < _verticalTexts.Length; i++)
        {
            vertical.Add(CreateSignMaterial($"OZ_SignV_{i:00}", RenderSign(_verticalTexts[i], true, _neonColors[i % _neonColors.Length]), 5f));
        }

        List<Material> horizontal = new List<Material>();
        for (int i = 0; i < _horizontalTexts.Length; i++)
        {
            horizontal.Add(CreateSignMaterial($"OZ_SignH_{i:00}", RenderSign(_horizontalTexts[i], false, _neonColors[(i * 2 + 1) % _neonColors.Length]), 4f));
        }

        int count = PlaceSigns(root.transform, vertical, horizontal);
        BuildTower(root.transform);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"CSED_ClaudeSignage: 看板を{count}枚置きました");
    }

    // ========================================
    // 文字の描画(SDFアトラスから)
    // ========================================

    private static void ReadAtlas()
    {
        Texture2D atlas = _font.atlasTexture;
        RenderTexture rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(atlas, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D readable = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false, true);
        readable.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        _atlas = readable.GetPixels();
        _atlasWidth = atlas.width;
        Object.DestroyImmediate(readable);
    }

    // アトラスの中の文字のSDF値(0〜1。0.5が輪郭)。範囲外は0
    private static float SampleGlyph(UnityEngine.TextCore.GlyphRect rect, float u, float v)
    {
        if (u < 0f || u > 1f || v < 0f || v > 1f) return 0f;

        float x = rect.x + u * (rect.width - 1);
        float y = rect.y + v * (rect.height - 1);
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        float a = Atlas(x0, y0), b = Atlas(x0 + 1, y0), c = Atlas(x0, y0 + 1), d = Atlas(x0 + 1, y0 + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Atlas(int x, int y)
    {
        int index = y * _atlasWidth + x;
        return index >= 0 && index < _atlas.Length ? _atlas[index].a : 0f;
    }

    // 看板の画像(発光の色)を作る。縦看板は文字を縦に積む
    private static Color[] RenderSign(string text, bool isVertical, Color color)
    {
        int cell = 128;
        int count = 0;
        foreach (char c in text) if (c != ' ') count++;
        int width = isVertical ? 192 : Mathf.Max(512, text.Length * 104 + 96);
        int height = isVertical ? count * cell + 96 : 192;
        width = Mathf.NextPowerOfTwo(width);
        height = Mathf.NextPowerOfTwo(height);

        float[] coverage = new float[width * height];
        float pen = isVertical ? height - 48 - cell : 48;
        foreach (char c in text)
        {
            if (c == ' ')
            {
                pen += isVertical ? -cell * 0.5f : cell * 0.45f;
                continue;
            }

            if (!_font.characterLookupTable.TryGetValue(c, out TMP_Character character))
            {
                Debug.LogWarning($"CSED_ClaudeSignage: フォントに無い文字: {c}");
                continue;
            }

            UnityEngine.TextCore.GlyphRect rect = character.glyph.glyphRect;
            UnityEngine.TextCore.GlyphMetrics metrics = character.glyph.metrics;
            float scale = cell / _font.faceInfo.pointSize * 0.9f;
            float glyphWidth = rect.width * scale, glyphHeight = rect.height * scale;

            float originX, originY;
            if (isVertical)
            {
                originX = (width - glyphWidth) * 0.5f;
                originY = pen + (cell - glyphHeight) * 0.5f;
                pen -= cell;
            }
            else
            {
                originX = pen + metrics.horizontalBearingX * scale;
                originY = (height - cell) * 0.5f + (metrics.horizontalBearingY - metrics.height) * scale + cell * 0.22f;
                pen += metrics.horizontalAdvance * scale;
            }

            DrawGlyph(coverage, width, height, rect, originX, originY, glyphWidth, glyphHeight);
        }

        // 光のにじみ(文字をぼかしたもの)と、枠のネオン管
        float[] glow = Blur(coverage, width, height, 10);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                bool isFrame = x < 10 || x >= width - 10 || y < 10 || y >= height - 10;
                bool isFrameTube = isFrame && (x >= 4 && x < width - 4 && y >= 4 && y < height - 4) && !(x >= 7 && x < width - 7 && y >= 7 && y < height - 7);
                float light = Mathf.Clamp01(coverage[index] + glow[index] * 0.45f + (isFrameTube ? 0.8f : 0f));
                pixels[index] = new Color(color.r * light, color.g * light, color.b * light, 1f) + new Color(0.01f, 0.012f, 0.02f, 0f);
            }
        }
        return Wrap(pixels, width, height);
    }

    private static void DrawGlyph(float[] coverage, int width, int height, UnityEngine.TextCore.GlyphRect rect, float originX, float originY, float glyphWidth, float glyphHeight)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(originX)), x1 = Mathf.Min(width - 1, Mathf.CeilToInt(originX + glyphWidth));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(originY)), y1 = Mathf.Min(height - 1, Mathf.CeilToInt(originY + glyphHeight));
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float sdf = SampleGlyph(rect, (x - originX) / glyphWidth, (y - originY) / glyphHeight);
                float value = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.44f, 0.56f, sdf));
                int index = y * width + x;
                coverage[index] = Mathf.Max(coverage[index], value);
            }
        }
    }

    private static float[] Blur(float[] source, int width, int height, int radius)
    {
        float[] temp = new float[source.Length];
        float[] result = new float[source.Length];
        for (int y = 0; y < height; y++)
        {
            float sum = 0f;
            for (int x = -radius; x <= radius; x++) sum += source[y * width + Mathf.Clamp(x, 0, width - 1)];
            for (int x = 0; x < width; x++)
            {
                temp[y * width + x] = sum / (radius * 2 + 1);
                sum += source[y * width + Mathf.Min(width - 1, x + radius + 1)] - source[y * width + Mathf.Max(0, x - radius)];
            }
        }
        for (int x = 0; x < width; x++)
        {
            float sum = 0f;
            for (int y = -radius; y <= radius; y++) sum += temp[Mathf.Clamp(y, 0, height - 1) * width + x];
            for (int y = 0; y < height; y++)
            {
                result[y * width + x] = sum / (radius * 2 + 1);
                sum += temp[Mathf.Min(height - 1, y + radius + 1) * width + x] - temp[Mathf.Max(0, y - radius) * width + x];
            }
        }
        return result;
    }

    // 幅と高さを、画像の配列と一緒に持ち回るための入れ物(配列の先頭2つに入れない)
    private static readonly Dictionary<Color[], Vector2Int> _sizes = new Dictionary<Color[], Vector2Int>();
    private static readonly Dictionary<Material, Vector2> _materialSizes = new Dictionary<Material, Vector2>();

    private static Color[] Wrap(Color[] pixels, int width, int height)
    {
        _sizes[pixels] = new Vector2Int(width, height);
        return pixels;
    }

    // ========================================
    // マテリアル
    // ========================================

    private static Material CreateSignMaterial(string name, Color[] pixels, float intensity)
    {
        Vector2Int size = _sizes[pixels];
        Texture2D texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, true, false);
        texture.SetPixels(pixels);
        texture.Apply();
        string texturePath = $"{_textureFolder}/{name}.png";
        File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.anisoLevel = 4;
        importer.SaveAndReimport();
        Texture2D asset = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        string path = $"{_materialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.025f));
        material.SetFloat("_Smoothness", 0.85f);
        material.SetTexture("_EmissiveColorMap", asset);
        material.SetColor("_EmissiveColor", Color.white * intensity);
        material.SetFloat("_DoubleSidedEnable", 1f);
        material.EnableKeyword("_EMISSIVE_COLOR_MAP");
        HDMaterial.ValidateMaterial(material);
        _materialSizes[material] = size;   // 置く時に縦横比を使う
        EditorUtility.SetDirty(material);
        return material;
    }

    // ========================================
    // 置く
    // ========================================

    private static int PlaceSigns(Transform root, List<Material> vertical, List<Material> horizontal)
    {
        System.Random random = new System.Random(_seed);
        int count = 0, lights = 0;

        foreach (MeshRenderer building in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.InstanceID))
        {
            if (building.sharedMaterial == null || building.sharedMaterial.name != "OZ_Facade") continue;

            Bounds bounds = building.bounds;
            if (bounds.size.y < _minBuildingHeight || Mathf.Abs(bounds.center.x) < 12f && Mathf.Abs(bounds.center.z) < 12f) continue;   // 中央のタワーは別に作る
            if (random.NextDouble() > 0.5) continue;

            if (!TryPickOpenFace(bounds, random, out Vector3 normal, out Vector3 tangent, out float halfWidth)) continue;

            bool isVertical = random.NextDouble() < 0.65 || halfWidth < 3.5f;
            Material material = isVertical ? vertical[random.Next(vertical.Count)] : horizontal[random.Next(horizontal.Count)];
            Vector2 textureSize = _materialSizes[material];
            Vector3 faceCenter = bounds.center + normal * (Vector3.Dot(bounds.extents, Abs(normal)));

            Vector3 position;
            Quaternion rotation;
            Vector2 size;
            if (isVertical)
            {
                // 角から通りへ突き出す縦看板(看板の面は壁と直角。通りを歩く人から読める)
                float height = Mathf.Min(5.5f, bounds.size.y - 3f);
                size = new Vector2(height * textureSize.x / textureSize.y, height);
                float side = random.NextDouble() < 0.5 ? -1f : 1f;
                float y = bounds.min.y + 3.2f + height * 0.5f + (float)random.NextDouble() * Mathf.Max(0f, bounds.size.y - height - 5f);
                position = new Vector3(faceCenter.x, y, faceCenter.z) + tangent * side * (halfWidth - 0.8f) + normal * (size.x * 0.5f + 0.15f);
                rotation = Quaternion.LookRotation(tangent, Vector3.up);
            }
            else
            {
                // 壁に貼る横長の広告(ビルの上の方)
                float width = Mathf.Min(halfWidth * 1.6f, 9f);
                size = new Vector2(width, width * textureSize.y / textureSize.x);
                position = new Vector3(faceCenter.x, bounds.max.y - size.y * 0.5f - 1.2f, faceCenter.z) + normal * 0.08f;
                rotation = Quaternion.LookRotation(-normal, Vector3.up);
            }

            GameObject sign = CreateQuad(root, $"Sign_{count:000}", position, rotation, size, material);
            count++;

            // 3枚に1枚、色の付いた光を足す(濡れた地面に色が映るように)
            if (count % 3 == 0 && lights < 60)
            {
                Color color = material.GetTexture("_EmissiveColorMap") != null ? AverageNeon(material) : Color.white;
                AddLight(sign.transform, position + normal * 1.5f, color, 500f, 9f);
                lights++;
            }
        }
        return count;
    }

    // ビルの4面のうち、前が開けている面を1つ選ぶ
    private static bool TryPickOpenFace(Bounds bounds, System.Random random, out Vector3 normal, out Vector3 tangent, out float halfWidth)
    {
        Vector3[] normals = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        int start = random.Next(4);
        for (int i = 0; i < 4; i++)
        {
            Vector3 n = normals[(start + i) % 4];
            Vector3 t = new Vector3(-n.z, 0f, n.x);
            Vector3 face = bounds.center + n * Vector3.Dot(bounds.extents, Abs(n));
            Vector3 origin = new Vector3(face.x, bounds.min.y + 4f, face.z) + n * 0.2f;
            if (Physics.Raycast(origin, n, 7f, ~0, QueryTriggerInteraction.Ignore)) continue;   // 目の前に別の建物

            normal = n;
            tangent = t;
            halfWidth = Vector3.Dot(bounds.extents, Abs(t));
            return true;
        }

        normal = tangent = Vector3.zero;
        halfWidth = 0f;
        return false;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private static GameObject CreateQuad(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        quad.transform.SetPositionAndRotation(position, rotation);
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return quad;
    }

    private static void AddLight(Transform parent, Vector3 position, Color color, float lumen, float range)
    {
        GameObject lightObject = new GameObject("Light");
        lightObject.transform.SetParent(parent, true);
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        if (!lightObject.TryGetComponent(out HDAdditionalLightData hdLight)) hdLight = lightObject.AddComponent<HDAdditionalLightData>();
        hdLight.volumetricDimmer = 0.3f;
        light.type = LightType.Point;
        light.color = color;
        light.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        light.intensity = lumen;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    // 看板の色(画像の明るい部分の平均)
    private static Color AverageNeon(Material material)
    {
        string name = material.name;
        int index = int.Parse(name.Substring(name.Length - 2));
        bool isVertical = name.StartsWith("OZ_SignV");
        return isVertical ? _neonColors[index % _neonColors.Length] : _neonColors[(index * 2 + 1) % _neonColors.Length];
    }

    // ========================================
    // AEGISのタワー
    // ========================================

    private static void BuildTower(Transform root)
    {
        MeshRenderer tower = null;
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.InstanceID))
        {
            Bounds b = renderer.bounds;
            if (Mathf.Abs(b.center.x) > 15f || Mathf.Abs(b.center.z) > 15f) continue;
            if (tower == null || b.max.y > tower.bounds.max.y) tower = renderer;
        }
        if (tower == null) return;

        Material material = CreateTowerMaterial();
        Undo.RecordObject(tower, "AEGIS Tower");
        tower.sharedMaterial = material;
        PrefabUtility.RecordPrefabInstancePropertyModifications(tower);
        EditorUtility.SetDirty(tower);

        Material aegis = CreateSignMaterial("OZ_SignTower_AEGIS", RenderSign("AEGIS", false, new Color(0.25f, 0.92f, 1f)), 9f);
        Material maintenance = CreateSignMaterial("OZ_SignTower_Maintenance", RenderSign("定期メンテナンス中", false, new Color(1f, 0.55f, 0.12f)), 6f);

        Bounds bounds = tower.bounds;
        foreach (Vector3 normal in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            Vector3 tangent = new Vector3(-normal.z, 0f, normal.x);
            float halfWidth = Vector3.Dot(bounds.extents, Abs(tangent));
            Vector3 face = bounds.center + normal * (Vector3.Dot(bounds.extents, Abs(normal)) + 0.1f);
            float width = halfWidth * 1.8f;

            Vector2 aegisSize = new Vector2(width, width * _materialSizes[aegis].y / _materialSizes[aegis].x);
            CreateQuad(root, "Tower_AEGIS", new Vector3(face.x, bounds.max.y - aegisSize.y * 0.5f - 2f, face.z), Quaternion.LookRotation(-normal), aegisSize, aegis);

            Vector2 bandSize = new Vector2(width, width * _materialSizes[maintenance].y / _materialSizes[maintenance].x);
            CreateQuad(root, "Tower_Maintenance", new Vector3(face.x, bounds.max.y - aegisSize.y - bandSize.y * 0.5f - 3f, face.z), Quaternion.LookRotation(-normal), bandSize, maintenance);
        }
    }

    // 縦の光のラインと、6mごとの横の帯が光るタワーの外壁(Triplanar。屋上は光らせない)
    private static Material CreateTowerMaterial()
    {
        const int size = 512;   // 1枚で8m
        Color[] color = new Color[size * size], mask = new Color[size * size], emission = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                bool isLine = x % 64 < 3;                 // 1mごとの縦のライン
                bool isBand = y % 384 < 6;                // 6mごとの横の帯
                color[index] = isLine ? new Color(0.1f, 0.1f, 0.12f) : new Color(0.02f, 0.03f, 0.045f);
                mask[index] = new Color(0.7f, 1f, 0f, 0.92f);
                float pulse = 0.6f + 0.4f * Mathf.Sin(y * 0.02f);
                emission[index] = isBand ? new Color(1f, 0.55f, 0.12f) : isLine ? new Color(0.25f, 0.9f, 1f) * pulse : Color.black;
            }
        }

        string folder = "Assets/Programmer/Texture/OrderZero";
        Texture2D colorTexture = SaveTexture(folder + "/OZ_Tower_Color.png", color, size, true);
        Texture2D maskTexture = SaveTexture(folder + "/OZ_Tower_Mask.png", mask, size, false);
        Texture2D emissionTexture = SaveTexture(folder + "/OZ_Tower_Emission.png", emission, size, true);

        string path = "Assets/Programmer/Material/OrderZero/OZ_Tower.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("ORDERZERO/LitDither"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseColorMap", colorTexture);
        material.SetTexture("_MaskMap", maskTexture);
        material.SetFloat("_UVBase", 5f);
        material.SetFloat("_TexWorldScale", 1f / 8f);
        material.EnableKeyword("_MAPPING_TRIPLANAR");
        material.EnableKeyword("_MASKMAP");
        material.SetTexture("_EmissiveColorMap", emissionTexture);
        material.SetColor("_EmissiveColor", Color.white * 3f);
        material.SetFloat("_UVEmissive", 6f);
        material.EnableKeyword("_EMISSIVE_COLOR_MAP");
        material.EnableKeyword("_EMISSIVE_MAPPING_BASE");
        material.SetFloat("_RoofBlend", 1f);
        material.SetColor("_RoofColor", new Color(0.03f, 0.03f, 0.04f));
        material.SetFloat("_DitherFade", 1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D SaveTexture(string path, Color[] pixels, int size, bool isSrgb)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true, !isSrgb);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = isSrgb;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
