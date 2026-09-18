#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Auto setup for Teto_GameReady.fbx.
// Target: Unity 6.x + HDRP. No UniVRM dependency is required.
public sealed class TetoGameReadyImporter : AssetPostprocessor
{
    private const string TargetFile = "Teto_GameReady.fbx";
    private const string SetupMarker = "TetoGameReady_HDRP_v5";

    private struct MaterialSpec
    {
        public string sourceName, baseTex, normalTex, emissionTex, alphaMode;
        public Color baseColor, emissionColor;
        public float metallic, roughness, alphaCutoff;
        public bool doubleSided;
        public MaterialSpec(string n,string b,string no,string e,Color c,float m,float r,Color ec,string a,float ac,bool d)
        { sourceName=n;baseTex=b;normalTex=no;emissionTex=e;baseColor=c;metallic=m;roughness=r;emissionColor=ec;alphaMode=a;alphaCutoff=ac;doubleSided=d; }
    }

    private static readonly MaterialSpec[] Specs = new MaterialSpec[]
    {
        new MaterialSpec("N00_000_00_FaceMouth_00_FACE (Instance)", "00__01.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, false),
        new MaterialSpec("N00_000_00_EyeIris_00_EYE (Instance)", "03__02.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "BLEND", 0.5f, false),
        new MaterialSpec("N00_000_00_Face_00_SKIN (Instance)", "04__04.png", "05__05.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_000_00_EyeWhite_00_EYE (Instance)", "06__06.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, false),
        new MaterialSpec("N00_000_00_FaceBrow_00_FACE (Instance)", "07__07.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "BLEND", 0.5f, true),
        new MaterialSpec("N00_000_00_FaceEyelash_00_FACE (Instance)", "08__08.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "BLEND", 0.5f, true),
        new MaterialSpec("N00_000_00_FaceEyeline_00_FACE (Instance)", "09__09.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "BLEND", 0.5f, true),
        new MaterialSpec("N00_000_00_Body_00_SKIN (Instance)", "10__10.png", "11__11.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, false),
        new MaterialSpec("N00_000_00_HairBack_00_HAIR (Instance)", "12__12.png", "13_N00_000_00_HairBack_00_nml.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0.858823538f,0.694117665f,0.694117665f,1f), "MASK", 0.5f, false),
        new MaterialSpec("N00_008_01_Shoes_01_CLOTH_01 (Instance)", "14__13.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_007_01_Tops_01_CLOTH_01 (Instance)", "15__14.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_002_03_Tops_01_CLOTH_01 (Instance)", "16__15.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_002_03_Tops_01_CLOTH_02 (Instance)", "17__16.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_002_03_Tops_01_CLOTH_03 (Instance)", "18__17.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_008_01_Shoes_01_CLOTH_02 (Instance)", "19__18.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_010_01_Onepiece_00_CLOTH (Instance)", "20__19.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_007_01_Tops_01_CLOTH_02 (Instance)", "21__20.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_007_01_Tops_01_CLOTH_03 (Instance)", "22__21.png", "02_Shader_NoneNormal.png", "01_Shader_NoneBlack.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_000_Hair_00_HAIR_01 (Instance)", "23__22.png", "25_N00_000_Hair_00_nml_01.png", "24__23.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_000_Hair_00_HAIR_03 (Instance)", "26__26.png", "28_N00_000_Hair_00_nml_03.png", "27__27.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
        new MaterialSpec("N00_000_Hair_00_HAIR_04 (Instance)", "29__28.png", "31_N00_000_Hair_00_nml_04.png", "30__29.png", new Color(1f,1f,1f,1f), 0f, 0.9f, new Color(0f,0f,0f,1f), "MASK", 0.5f, true),
    };

    private static readonly HashSet<string> NormalTextureFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "02_Shader_NoneNormal.png",
        "05__05.png",
        "11__11.png",
        "13_N00_000_00_HairBack_00_nml.png",
        "25_N00_000_Hair_00_nml_01.png",
        "28_N00_000_Hair_00_nml_03.png",
        "31_N00_000_Hair_00_nml_04.png",
    };

    private static readonly Dictionary<string,string> HumanBoneMap = new Dictionary<string,string>
    {
        { "Hips", "J_Bip_C_Hips" },
        { "LeftUpperLeg", "J_Bip_L_UpperLeg" },
        { "RightUpperLeg", "J_Bip_R_UpperLeg" },
        { "LeftLowerLeg", "J_Bip_L_LowerLeg" },
        { "RightLowerLeg", "J_Bip_R_LowerLeg" },
        { "LeftFoot", "J_Bip_L_Foot" },
        { "RightFoot", "J_Bip_R_Foot" },
        { "Spine", "J_Bip_C_Spine" },
        { "Chest", "J_Bip_C_Chest" },
        { "Neck", "J_Bip_C_Neck" },
        { "Head", "J_Bip_C_Head" },
        { "LeftShoulder", "J_Bip_L_Shoulder" },
        { "RightShoulder", "J_Bip_R_Shoulder" },
        { "LeftUpperArm", "J_Bip_L_UpperArm" },
        { "RightUpperArm", "J_Bip_R_UpperArm" },
        { "LeftLowerArm", "J_Bip_L_LowerArm" },
        { "RightLowerArm", "J_Bip_R_LowerArm" },
        { "LeftHand", "J_Bip_L_Hand" },
        { "RightHand", "J_Bip_R_Hand" },
        { "LeftToes", "J_Bip_L_ToeBase" },
        { "RightToes", "J_Bip_R_ToeBase" },
        { "LeftEye", "J_Adj_L_FaceEye" },
        { "RightEye", "J_Adj_R_FaceEye" },
        { "LeftThumbProximal", "J_Bip_L_Thumb1" },
        { "LeftThumbIntermediate", "J_Bip_L_Thumb2" },
        { "LeftThumbDistal", "J_Bip_L_Thumb3" },
        { "LeftIndexProximal", "J_Bip_L_Index1" },
        { "LeftIndexIntermediate", "J_Bip_L_Index2" },
        { "LeftIndexDistal", "J_Bip_L_Index3" },
        { "LeftMiddleProximal", "J_Bip_L_Middle1" },
        { "LeftMiddleIntermediate", "J_Bip_L_Middle2" },
        { "LeftMiddleDistal", "J_Bip_L_Middle3" },
        { "LeftRingProximal", "J_Bip_L_Ring1" },
        { "LeftRingIntermediate", "J_Bip_L_Ring2" },
        { "LeftRingDistal", "J_Bip_L_Ring3" },
        { "LeftLittleProximal", "J_Bip_L_Little1" },
        { "LeftLittleIntermediate", "J_Bip_L_Little2" },
        { "LeftLittleDistal", "J_Bip_L_Little3" },
        { "RightThumbProximal", "J_Bip_R_Thumb1" },
        { "RightThumbIntermediate", "J_Bip_R_Thumb2" },
        { "RightThumbDistal", "J_Bip_R_Thumb3" },
        { "RightIndexProximal", "J_Bip_R_Index1" },
        { "RightIndexIntermediate", "J_Bip_R_Index2" },
        { "RightIndexDistal", "J_Bip_R_Index3" },
        { "RightMiddleProximal", "J_Bip_R_Middle1" },
        { "RightMiddleIntermediate", "J_Bip_R_Middle2" },
        { "RightMiddleDistal", "J_Bip_R_Middle3" },
        { "RightRingProximal", "J_Bip_R_Ring1" },
        { "RightRingIntermediate", "J_Bip_R_Ring2" },
        { "RightRingDistal", "J_Bip_R_Ring3" },
        { "RightLittleProximal", "J_Bip_R_Little1" },
        { "RightLittleIntermediate", "J_Bip_R_Little2" },
        { "RightLittleDistal", "J_Bip_R_Little3" },
        { "UpperChest", "J_Bip_C_UpperChest" },
    };

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Teto_GameReady_UnityHDRP/")) return;
        string file = Path.GetFileName(assetPath);
        if (NormalTextureFiles.Contains(file))
        {
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.NormalMap;
            ti.sRGBTexture = false;
        }
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
        {
            if (Path.GetFileName(path).Equals(TargetFile, StringComparison.OrdinalIgnoreCase))
            {
                string p = path;
                EditorApplication.delayCall += () => Setup(p, false);
                break;
            }
        }
    }

    [MenuItem("Tools/Teto GameReady/Rebuild HDRP Materials")]
    private static void RebuildMenu()
    {
        string[] guids = AssetDatabase.FindAssets("Teto_GameReady t:Model");
        if (guids.Length == 0) { Debug.LogError("Teto_GameReady.fbx が見つかりません。"); return; }
        Setup(AssetDatabase.GUIDToAssetPath(guids[0]), true);
    }

    private static void Setup(string fbxPath, bool force)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;
        if (!force && importer.userData.Contains(SetupMarker)) return;

        string packageRoot = Path.GetDirectoryName(Path.GetDirectoryName(fbxPath)).Replace('\\','/');
        string texRoot = packageRoot + "/Textures";
        string matRoot = packageRoot + "/Materials";
        if (!AssetDatabase.IsValidFolder(matRoot)) AssetDatabase.CreateFolder(packageRoot, "Materials");

        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) { Debug.LogError("HDRP/Lit が見つかりません。HDRPプロジェクトで使用してください。"); return; }

        foreach (var s in Specs)
        {
            string safe = MakeSafeFileName(s.sourceName);
            string matPath = matRoot + "/" + safe + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); mat.name = s.sourceName; AssetDatabase.CreateAsset(mat, matPath); }
            else mat.shader = shader;

            Texture2D baseTex = LoadTex(texRoot, s.baseTex);
            Texture2D normalTex = LoadTex(texRoot, s.normalTex);
            Texture2D emissionTex = LoadTex(texRoot, s.emissionTex);
            if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", baseTex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", s.baseColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", s.metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f - s.roughness);
            if (normalTex != null && mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", normalTex);
            if (emissionTex != null && mat.HasProperty("_EmissiveColorMap")) mat.SetTexture("_EmissiveColorMap", emissionTex);
            if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", s.emissionColor);

            ConfigureSurface(mat, s);
            EditorUtility.SetDirty(mat);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), s.sourceName), mat);
        }

        importer.importBlendShapes = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

        // Material setup must not force a Humanoid Avatar.
        // The FBX keeps the original skin/bone hierarchy, but Humanoid retargeting is a
        // separate concern and can fail when Unity cannot infer an Avatar from the FBX.
        // Import as Generic by default so material/texture setup never breaks model import.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.optimizeBones = false;

        importer.userData = SetupMarker;
        importer.SaveAndReimport();
        AssetDatabase.SaveAssets();
        
        // Verify what Unity actually created from the FBX after the reimport.
        GameObject importedModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (importedModel != null)
        {
            var renderers = importedModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int blendShapeCount = 0;
            foreach (var r in renderers)
                if (r.sharedMesh != null) blendShapeCount += r.sharedMesh.blendShapeCount;

            if (renderers.Length == 0)
                Debug.LogError("Teto_GameReady: FBX は読み込まれましたが SkinnedMeshRenderer が 0 個です。FBX構造を確認してください。");
            else
                Debug.Log($"Teto_GameReady: Import OK / SkinnedMeshRenderer={renderers.Length} / BlendShapes={blendShapeCount} / Transforms={importedModel.GetComponentsInChildren<Transform>(true).Length}");
        }
        Debug.Log("Teto_GameReady: HDRP Materials / Textures / Generic Rig setup 完了");
    }

    private static Texture2D LoadTex(string root, string file)
    { return string.IsNullOrEmpty(file) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/" + file); }

    private static void ConfigureSurface(Material mat, MaterialSpec s)
    {
        bool transparent = s.alphaMode == "BLEND";
        bool cutout = s.alphaMode == "MASK";
        if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", transparent ? 1f : 0f);
        if (mat.HasProperty("_AlphaCutoffEnable")) mat.SetFloat("_AlphaCutoffEnable", cutout ? 1f : 0f);
        if (mat.HasProperty("_AlphaCutoff")) mat.SetFloat("_AlphaCutoff", s.alphaCutoff);
        if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", s.doubleSided ? 1f : 0f);
        if (mat.HasProperty("_CullMode")) mat.SetFloat("_CullMode", s.doubleSided ? 0f : 2f);
        if (mat.HasProperty("_CullModeForward")) mat.SetFloat("_CullModeForward", s.doubleSided ? 0f : 2f);

        if (transparent)
        {
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
        }
        else
        {
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (cutout) { mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.EnableKeyword("_ALPHATEST_ON"); }
            else { mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry; mat.SetOverrideTag("RenderType", "Opaque"); mat.DisableKeyword("_ALPHATEST_ON"); }
        }
        if (s.doubleSided) mat.EnableKeyword("_DOUBLESIDED_ON"); else mat.DisableKeyword("_DOUBLESIDED_ON");
        if (mat.GetTexture("_NormalMap") != null) mat.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
    }

    private static string MakeSafeFileName(string name)
    { foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_'); return name; }
}
#endif
