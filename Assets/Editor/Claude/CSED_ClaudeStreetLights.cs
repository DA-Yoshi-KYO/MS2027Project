using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/*
 * 道路に沿って街灯(ライトと光る灯具)を並べるエディタ拡張(画づくり用)
 * Tools/Claude/Place Street Lights から実行する。Unity MCPのexecute_menu_itemからも呼べる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・街全体を_spacing mおきの格子で上から調べ、上が開けている歩道・道路(OZ_Pavement / OZ_Asphalt)の上に青白い街灯を、
 *   路地(OZ_Alley)の上に紫寄りの暗い明かりを置く(大通りと裏道を明るさ・色で見分けられるように)。高さは地面から_height m
 *   調べる範囲は、OZ_のマテリアルが付いた物全体の範囲。格子は少しずらして、規則正しく並びすぎないようにする
 * ・置いた街灯は、シーン直下の「OZ_StreetLights」の下にまとめる。実行するたびに作り直す(消す時はこのオブジェクトを消すだけ)
 * ・ライトは影なしの点光源(数が多いため)。光る灯具は小さな箱に発光するマテリアル(OZ_LampHead)
 */
// ========================================

public static class CSED_ClaudeStreetLights
{
    private const string _rootName = "OZ_StreetLights";
    private const string _lampMaterialPath = "Assets/Programmer/Material/OrderZero/OZ_LampHead.mat";

    private const float _spacing = 15f;          // 街灯の間隔(m)
    private const float _alleyIntensity = 600f;  // 路地の明かりの明るさ(ルーメン)
    private const float _alleyRange = 13f;
    private static readonly Color _alleyColor = new Color(1f, 0.3f, 0.65f);    // 路地の紫寄りの明かり
    private const float _height = 6.5f;          // 道路の上面からの高さ(m)
    private const float _range = 20f;            // 光の届く距離(m)
    private const float _intensity = 1000f;      // 歩道の明るさ(ルーメン)
    private const float _roadIntensity = 1400f;  // 車道の明るさ(ルーメン)
    private static readonly Color _lightColor = new Color(0.72f, 0.86f, 1f);   // 青白いLED
    private static readonly Color _roadLightColor = new Color(1f, 0.72f, 0.45f);  // 車道は暖色(ナトリウム灯風)。歩道の青白い光と対比させる

    [MenuItem("Tools/Claude/Place Street Lights")]
    public static void Place()
    {
        GameObject old = GameObject.Find(_rootName);
        if (old != null) Object.DestroyImmediate(old);

        GameObject root = new GameObject(_rootName);
        Material lampMaterial = GetLampMaterial();
        int count = 0;

        Bounds area = GetFieldBounds();
        for (float x = area.min.x + _spacing * 0.5f; x < area.max.x; x += _spacing)
        {
            for (float z = area.min.z + _spacing * 0.5f; z < area.max.z; z += _spacing)
            {
                // 規則正しく並びすぎないよう、少しずらす
                Vector3 jitter = new Vector3(Mathf.Sin(x * 12.9f + z * 78.2f), 0f, Mathf.Cos(x * 39.3f + z * 11.7f)) * (_spacing * 0.2f);
                Vector3 origin = new Vector3(x, area.max.y + 20f, z) + jitter;
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, area.size.y + 40f, ~0, QueryTriggerInteraction.Ignore)) continue;

                Renderer hitRenderer = hit.collider.GetComponent<Renderer>();
                string materialName = hitRenderer != null && hitRenderer.sharedMaterial != null ? hitRenderer.sharedMaterial.name : "";
                bool isAlley = materialName == "OZ_Alley";
                if (!isAlley && materialName != "OZ_Pavement" && materialName != "OZ_Asphalt") continue;   // 建物の上などには置かない

                _currentIsRoad = materialName == "OZ_Asphalt";
                CreateLamp(root.transform, hit.point + Vector3.up * _height, lampMaterial, count++, isAlley);
            }
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"CSED_ClaudeStreetLights: 街灯を{count}本置きました");
    }

    // OZ_のマテリアルが付いた物(街)全体の範囲
    private static Bounds GetFieldBounds()
    {
        Bounds bounds = new Bounds();
        bool hasAny = false;
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            Material material = renderer.sharedMaterial;
            if (material == null || !material.name.StartsWith("OZ_") || material.name == "OZ_LampHead") continue;

            if (hasAny) bounds.Encapsulate(renderer.bounds);
            else { bounds = renderer.bounds; hasAny = true; }
        }
        return bounds;
    }

    private static void CreateLamp(Transform parent, Vector3 position, Material lampMaterial, int index, bool isAlley)
    {
        GameObject lamp = new GameObject($"StreetLight_{index:000}");
        lamp.transform.SetParent(parent, false);
        lamp.transform.position = position;

        Light light = lamp.AddComponent<Light>();
        // HDRP用のデータは付いた時に明るさを初期値(600ルーメン)へ戻すので、先に付けてから値を入れる
        if (!lamp.TryGetComponent(out UnityEngine.Rendering.HighDefinition.HDAdditionalLightData hdLight))
        {
            hdLight = lamp.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        }
        hdLight.volumetricDimmer = 0.25f;   // 霧の中で光りすぎないように
        light.type = LightType.Point;
        bool isRoad = !isAlley && _currentIsRoad;
        light.color = isAlley ? _alleyColor : (isRoad ? _roadLightColor : _lightColor);
        light.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        light.intensity = isAlley ? _alleyIntensity : (isRoad ? _roadIntensity : _intensity);
        light.range = isAlley ? _alleyRange : _range;
        light.shadows = LightShadows.None;

        // 光っている灯具(光源が見えるように)
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        Object.DestroyImmediate(head.GetComponent<Collider>());
        head.transform.SetParent(lamp.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        head.transform.localScale = new Vector3(0.9f, 0.12f, 0.35f);
        MeshRenderer renderer = head.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = isAlley ? GetAlleyLampMaterial() : lampMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private const string _alleyLampMaterialPath = "Assets/Programmer/Material/OrderZero/OZ_AlleyLampHead.mat";
    private static Material _alleyLamp;
    private static bool _currentIsRoad;   // 今置いている街灯が車道の上か(色を変える)

    private static Material GetAlleyLampMaterial()
    {
        if (_alleyLamp != null) return _alleyLamp;

        _alleyLamp = AssetDatabase.LoadAssetAtPath<Material>(_alleyLampMaterialPath);
        if (_alleyLamp == null)
        {
            _alleyLamp = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(_alleyLamp, _alleyLampMaterialPath);
        }
        _alleyLamp.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f));
        _alleyLamp.SetColor("_EmissiveColor", _alleyColor * 30f);
        UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(_alleyLamp);
        EditorUtility.SetDirty(_alleyLamp);
        return _alleyLamp;
    }

    private static Material GetLampMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(_lampMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(material, _lampMaterialPath);
        }

        material.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f));
        material.SetColor("_EmissiveColor", _lightColor * 40f);
        material.EnableKeyword("_EMISSIVE_COLOR_MAP");
        UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
        EditorUtility.SetDirty(material);
        return material;
    }
}
