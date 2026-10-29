using System.IO;
using UnityEditor;
using UnityEngine;

/*
 * 夜の未来オフィス街の見た目(テクスチャ・マテリアル)を作り、フィールドの部品プレハブに当てるエディタ拡張(画づくり用)
 * Tools/Claude/Build City Look から実行する。Unity MCPのexecute_menu_itemからも呼べる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・テクスチャはすべてこのスクリプトで作る(外部の素材を使わないので、ライセンスの心配がない)
 *   出力先: Assets/Programmer/Texture/OrderZero/(PNG) / マテリアル: Assets/Programmer/Material/OrderZero/
 * ・シェーダーはORDERZERO/LitDither(HDRP/Litに、カメラの透過用の_DitherFadeを足したもの)
 *   テクスチャはワールド座標の3方向から貼る(Triplanar)。ビルは箱を引き伸ばして作っているので、UVだと模様が伸びるため
 * ・作るもの
 *   外壁(Bill)        : 暗いガラスと枠の格子。窓の一部がランダムに灯る(暖色・白・まれにネオン)。1枚で24m四方(階の高さ3m・窓の幅1.5m)
 *                       上向きの面(屋上)はシェーダーの_RoofBlendで、窓の無い屋上の色にする
 *   歩道(Floor)       : 0.5mの敷石と目地。ところどころ水たまり(つやが強い)
 *   道路(Road)        : 濡れたアスファルト
 *   路地(BackAlley)   : 暗く汚れたアスファルト(大通りと見分けやすいように)
 *   壁・階段・橋      : 2mごとのパネルの継ぎ目があるコンクリート
 * ・実行するたびに作り直す(数値を変えて試せる)
 */
// ========================================

public static class CSED_ClaudeCityLook
{
    private const string _textureFolder = "Assets/Programmer/Texture/OrderZero";
    private const string _materialFolder = "Assets/Programmer/Material/OrderZero";
    private const string _prefabFolder = "Assets/Programmer/Prefab/Field/FieldElement/Building";
    private const string _shaderName = "ORDERZERO/LitDither";

    [MenuItem("Tools/Claude/Build City Look")]
    public static void Build()
    {
        Directory.CreateDirectory(_textureFolder);
        Directory.CreateDirectory(_materialFolder);

        Material facade = CreateMaterial("OZ_Facade", BuildFacade(), 1f / 24f, new Color(1f, 1f, 1f) * 1.6f);
        facade.SetFloat("_RoofBlend", 1f);                                    // 上向きの面(屋上)は窓を出さない
        facade.SetColor("_RoofColor", new Color(0.045f, 0.047f, 0.055f));
        facade.SetFloat("_RoofSmoothness", 0.35f);
        Material pavement = CreateMaterial("OZ_Pavement", BuildPavement(), 1f / 4f, Color.black);
        Material asphalt = CreateMaterial("OZ_Asphalt", BuildAsphalt(new Color(0.11f, 0.112f, 0.12f), 0.35f, 7), 1f / 6f, Color.black);
        Material alley = CreateMaterial("OZ_Alley", BuildAsphalt(new Color(0.075f, 0.065f, 0.072f), 0.55f, 13), 1f / 6f, Color.black);
        Material concrete = CreateMaterial("OZ_Concrete", BuildConcrete(), 1f / 8f, Color.black);

        Assign("Bill", facade);
        Assign("Floor", pavement);
        Assign("Road", asphalt);
        Assign("BackAlley", alley);
        Assign("Wall", concrete);
        Assign("Stairs", concrete);
        Assign("Bridge", concrete);

        AssetDatabase.SaveAssets();
        Debug.Log("CSED_ClaudeCityLook: 街の見た目を作りました");
    }

    // ========================================
    // テクスチャ(色・マスク・発光)
    // ========================================

    private struct Maps
    {
        public string name;
        public Color[] color;
        public Color[] mask;       // R: 金属, G: AO, B: (未使用), A: つや
        public Color[] emission;   // 無ければnull
        public int size;
    }

    // 外壁: 1024px = 32m(1m = 32px)。階の高さ4m(128px)、窓の幅2m(64px)
    private static Maps BuildFacade()
    {
        const int size = 1024, floorPx = 128, columnPx = 64;
        Maps maps = NewMaps("Facade", size, true);
        System.Random random = new System.Random(2027);

        int columns = size / columnPx, floors = size / floorPx;
        Color[] lit = new Color[columns * floors];
        for (int i = 0; i < lit.Length; i++) lit[i] = PickWindowLight(random);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = x % columnPx, cy = y % floorPx;
                int index = y * size + x;
                float grime = Noise(x, y, 0.02f, 3) * 0.5f + Noise(x, y, 0.15f, 9) * 0.5f;

                bool isMullion = cx < 5 || cx >= columnPx - 5;                // 縦の枠
                bool isSpandrel = cy < 26 || cy >= floorPx - 14;              // 階と階の間の帯
                bool isWindow = !isMullion && !isSpandrel;

                if (isWindow)
                {
                    Color light = lit[(y / floorPx) * columns + x / columnPx];
                    // 窓の中のブラインド(上から少し下がっている窓がある)
                    float blind = Mathf.Repeat((x / columnPx) * 0.37f + (y / floorPx) * 0.61f, 1f);
                    bool isBlind = cy > floorPx - 14 - (int)(blind * 40f) && blind > 0.6f;
                    maps.color[index] = Tint(new Color(0.025f, 0.035f, 0.05f), grime, 0.15f);
                    maps.mask[index] = new Color(0.4f, 1f, 0f, isBlind ? 0.5f : 0.93f);
                    // 天井の照明の分だけ上が明るい。一部の窓はブラインドの横線が入る
                    float height01 = (cy - 26f) / (floorPx - 40f);
                    bool hasSlats = Hash(x / columnPx, y / floorPx + 77) > 0.55f;
                    float slat = hasSlats && cy % 6 < 2 ? 0.45f : 1f;
                    float glow = (0.55f + 0.45f * height01) * slat;
                    maps.emission[index] = isBlind ? light * 0.25f : light * glow;
                }
                else
                {
                    Color frame = isMullion ? new Color(0.09f, 0.095f, 0.11f) : new Color(0.06f, 0.063f, 0.075f);
                    maps.color[index] = Tint(frame, grime, 0.25f);
                    maps.mask[index] = new Color(isMullion ? 0.6f : 0f, 0.9f, 0f, isMullion ? 0.6f : 0.25f + grime * 0.15f);
                    // 階の帯の下端に、細い光のライン(未来のビルらしさ)
                    bool isStrip = isSpandrel && cy >= 2 && cy < 5 && (x / (columnPx * 4)) % 3 == 0;
                    maps.emission[index] = isStrip ? new Color(0.25f, 0.85f, 1f) * 0.6f : Color.black;
                }
            }
        }
        return maps;
    }

    // 窓の明かりの色(消えている窓が多い。暖色・白・まれにネオン)
    private static Color PickWindowLight(System.Random random)
    {
        double roll = random.NextDouble();
        float level = 0.45f + (float)random.NextDouble() * 0.55f;
        if (roll < 0.68) return Color.black;
        if (roll < 0.86) return new Color(1f, 0.74f, 0.45f) * level;    // 暖色
        if (roll < 0.95) return new Color(0.78f, 0.88f, 1f) * level;    // 白(オフィス)
        if (roll < 0.98) return new Color(0.25f, 0.9f, 1f) * level;     // 水色のネオン
        return new Color(1f, 0.25f, 0.75f) * level;                      // 紫のネオン
    }

    // 歩道: 512px = 4m。0.5mの敷石(64px)と目地、ところどころ水たまり
    private static Maps BuildPavement()
    {
        const int size = 512, tile = 64;
        Maps maps = NewMaps("Pavement", size, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                bool isGrout = x % tile < 3 || y % tile < 3;
                float tileShade = Hash(x / tile, y / tile) * 0.25f;
                float puddle = Mathf.Clamp01((Noise(x, y, 0.012f, 21) - 0.66f) * 6f);
                float grain = Noise(x, y, 0.4f, 4);

                Color baseColor = isGrout ? new Color(0.05f, 0.05f, 0.055f) : new Color(0.21f, 0.215f, 0.23f) * (0.85f + tileShade);
                maps.color[index] = Color.Lerp(Tint(baseColor, grain, 0.12f), baseColor * 0.9f, puddle * 0.6f);
                float smoothness = Mathf.Lerp(isGrout ? 0.2f : 0.4f + grain * 0.15f, 0.97f, puddle);
                maps.mask[index] = new Color(0f, isGrout ? 0.6f : 1f, 0f, smoothness);
            }
        }
        return maps;
    }

    // アスファルト: 512px。細かい粒と、濡れて光る部分
    private static Maps BuildAsphalt(Color baseColor, float wetness, int seed)
    {
        const int size = 512;
        Maps maps = NewMaps(seed == 7 ? "Asphalt" : "Alley", size, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                float grain = Hash(x, y + seed * 1000);
                float patch = Noise(x, y, 0.01f, seed);
                float wet = Mathf.Clamp01((Noise(x, y, 0.008f, seed + 50) - (1f - wetness * 0.7f)) * 4f);
                Color color = baseColor * (0.8f + grain * 0.4f) * (0.9f + patch * 0.2f);
                maps.color[index] = Color.Lerp(color, color * 0.8f, wet * 0.7f);
                maps.mask[index] = new Color(0f, 1f, 0f, Mathf.Lerp(0.3f + grain * 0.1f, 0.95f, wet));
            }
        }
        return maps;
    }

    // コンクリート: 512px = 8m。2mごとのパネルの継ぎ目
    private static Maps BuildConcrete()
    {
        const int size = 512, panel = 128;
        Maps maps = NewMaps("Concrete", size, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                bool isSeam = x % panel < 2 || y % panel < 2;
                float stain = Noise(x, y, 0.015f, 31);
                float grain = Noise(x, y, 0.3f, 32);
                Color color = new Color(0.16f, 0.165f, 0.18f) * (0.8f + stain * 0.35f) * (0.92f + grain * 0.16f);
                maps.color[index] = isSeam ? color * 0.45f : color;
                maps.mask[index] = new Color(0f, isSeam ? 0.5f : 1f, 0f, 0.28f + stain * 0.2f);
            }
        }
        return maps;
    }

    private static Maps NewMaps(string name, int size, bool hasEmission)
    {
        return new Maps
        {
            name = name,
            size = size,
            color = new Color[size * size],
            mask = new Color[size * size],
            emission = hasEmission ? new Color[size * size] : null,
        };
    }

    private static Color Tint(Color color, float noise, float amount)
    {
        float factor = 1f + (noise - 0.5f) * 2f * amount;
        return new Color(color.r * factor, color.g * factor, color.b * factor, 1f);
    }

    // 繰り返しても継ぎ目が出にくい、なめらかなノイズ(0〜1)
    private static float Noise(int x, int y, float frequency, int seed)
    {
        return Mathf.PerlinNoise(x * frequency + seed * 17.13f, y * frequency + seed * 3.71f);
    }

    private static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }

    // ========================================
    // マテリアル
    // ========================================

    private static Material CreateMaterial(string name, Maps maps, float worldScale, Color emissive)
    {
        Texture2D color = SaveTexture($"{name}_Color", maps.color, maps.size, true);
        Texture2D mask = SaveTexture($"{name}_Mask", maps.mask, maps.size, false);
        Texture2D emission = maps.emission != null ? SaveTexture($"{name}_Emission", maps.emission, maps.size, true) : null;

        string path = $"{_materialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(_shaderName));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find(_shaderName);

        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseColorMap", color);
        material.SetTexture("_MaskMap", mask);
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_Smoothness", 1f);
        material.SetFloat("_SmoothnessRemapMin", 0f);
        material.SetFloat("_SmoothnessRemapMax", 1f);
        material.SetFloat("_AORemapMin", 0f);
        material.SetFloat("_AORemapMax", 1f);
        material.EnableKeyword("_MASKMAP");

        // ワールド座標の3方向から貼る(Triplanar)
        material.SetFloat("_UVBase", 5f);
        material.SetFloat("_TexWorldScale", worldScale);
        material.EnableKeyword("_MAPPING_TRIPLANAR");
        material.DisableKeyword("_MAPPING_PLANAR");

        if (emission != null)
        {
            material.SetTexture("_EmissiveColorMap", emission);
            material.SetColor("_EmissiveColor", emissive);
            material.SetFloat("_UVEmissive", 6f);    // 色と同じ貼り方
            material.SetFloat("_TexWorldScaleEmissive", worldScale);
            material.EnableKeyword("_EMISSIVE_COLOR_MAP");
            material.EnableKeyword("_EMISSIVE_MAPPING_BASE");
        }
        else
        {
            material.SetTexture("_EmissiveColorMap", null);
            material.SetColor("_EmissiveColor", Color.black);
            material.DisableKeyword("_EMISSIVE_COLOR_MAP");
        }

        material.SetFloat("_DitherFade", 1f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D SaveTexture(string name, Color[] pixels, int size, bool isSrgb)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true, !isSrgb);
        texture.SetPixels(pixels);
        texture.Apply();

        string path = $"{_textureFolder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = isSrgb;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ========================================
    // プレハブ
    // ========================================

    private static void Assign(string prefabName, Material material)
    {
        string path = $"{_prefabFolder}/{prefabName}.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
