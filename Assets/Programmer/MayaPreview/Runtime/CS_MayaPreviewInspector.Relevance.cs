using System.Collections.Generic;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Relevance
 Summary: Which items of the toon / outline shaders mean something for the material being edited.
          - Parts  : SHG_NTE_Skin is face, body and nose in one shader (_SkinPart, chosen by the user).
                     Only the items of the chosen part are shown.
          - Hidden : also what is never read (the texture slot of an outline's settings).
                     Nothing is hidden by guessing what kind of part a mesh is.
          - Grey   : does nothing with the current values (GreyRules); the tooltip says what to change.
          - A (alpha) of a colour: only offered where the shader reads it.
          The other shaders (HDRP/Lit, effects ...) are left as they are.
          A rule's gate is never grey because of the item it greys, so nothing can get stuck.
 History: 2026/11/22 New
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private enum Gate
    {
        NoTexture,      // gate = a texture slot that is empty
        Zero,           // gate = a value that is 0
        One,            // gate = a value that is 1
        Filled,         // gate = a texture slot that is filled
        MapInUse,       // gate = the SDF map of a face, filled and with a strength above 0
    }

    private const float GateEpsilon = 0.0005f;

    // (item, gate, when it is grey). Only where the shader has both properties.
    private static readonly (string prop, string gate, Gate when)[] GreyRules =
    {
        ("_FaceSDFStrength", "_FaceSDFMap", Gate.NoTexture),
        ("_FaceSDFSoftness", "_FaceSDFMap", Gate.NoTexture),
        ("_FaceSDFFlip", "_FaceSDFMap", Gate.NoTexture),
        ("_FaceSDFSoftness", "_FaceSDFStrength", Gate.Zero),
        ("_FaceSDFFlip", "_FaceSDFStrength", Gate.Zero),
        ("_CelShadowThreshold", "_FaceSDFMap", Gate.MapInUse),
        ("_CelShadowSoftness", "_FaceSDFMap", Gate.MapInUse),
        ("_CelShadowStrength", "_FaceSDFMap", Gate.MapInUse),
        ("_SkyLight", "_FaceSDFMap", Gate.MapInUse),
        ("_BottomShade", "_FaceSDFMap", Gate.MapInUse),
        ("_HairHeightStrength", "_HairHeightMap", Gate.NoTexture),
        ("_EyeHighlightThreshold", "_EyeHighlightMask", Gate.Filled),
        ("_EyeHighlightColor", "_EyeHighlightPower", Gate.Zero),
        ("_NoseHideAngle", "_NoseSinkDepth", Gate.Zero),
        ("_EyeOverlayColor", "_EyeOverlayColorMap", Gate.NoTexture),
        ("_EyeOverlayStrength", "_EyeOverlayColorMap", Gate.NoTexture),
        ("_EyeOverlayFollow", "_EyeOverlayColorMap", Gate.NoTexture),
        ("_EyeOverlayColor", "_EyeOverlayStrength", Gate.Zero),
        ("_EyeOverlayFollow", "_EyeOverlayStrength", Gate.Zero),
        ("_EyeGlintColor", "_EyeGlintStrength", Gate.Zero),
        ("_EyeMirrorColor", "_EyeMirrorStrength", Gate.Zero),
        ("_EyeMirrorBlur", "_EyeMirrorStrength", Gate.Zero),
        ("_EyeGlintSize", "_EyeGlintStrength", Gate.Zero),
        ("_DeepShadowColor", "_DeepShadowStrength", Gate.Zero),
        ("_CelRimColor", "_CelRimWidth", Gate.Zero),
        ("_CelRimStrength", "_CelRimWidth", Gate.Zero),
        ("_CelRimColor", "_CelRimStrength", Gate.Zero),
        ("_CelSpecColor", "_CelSpecStrength", Gate.Zero),
        ("_CelSpecSize", "_CelSpecStrength", Gate.Zero),
        ("_DetailScale", "_DetailStrength", Gate.Zero),
        ("_HairHighlightColor", "_HairHighlightStrength", Gate.Zero),
        ("_HairHighlightPosition", "_HairHighlightStrength", Gate.Zero),
        ("_HairHighlightSharpness", "_HairHighlightStrength", Gate.Zero),
        ("_HairHighlightJitter", "_HairHighlightStrength", Gate.Zero),
        ("_HairHighlightStreaks", "_HairHighlightStrength", Gate.Zero),
        ("_HairHighlightJitterMap", "_HairHighlightStrength", Gate.Zero),
        ("_HairHighlightFlow", "_HairHighlightStrength", Gate.Zero),
        ("_HairHighlightStreaks", "_HairHighlightJitter", Gate.Zero),
        ("_HairHighlightJitterMap", "_HairHighlightJitter", Gate.Zero),
        ("_HairHighlightStreaks", "_HairHighlightJitterMap", Gate.Filled),
        ("_ClothThreadSize", "_ClothWeave", Gate.Zero),
        ("_ClothThreadAspect", "_ClothWeave", Gate.Zero),
        ("_ClothThreadBump", "_ClothWeave", Gate.Zero),
        ("_ClothThreadShade", "_ClothWeave", Gate.Zero),
        ("_ClothThreadSmoothness", "_ClothWeave", Gate.Zero),
        ("_ClothThreadMap", "_ClothWeave", Gate.Zero),
        ("_ClothFuzzColor", "_ClothFuzzStrength", Gate.Zero),
        ("_ClothFuzzMap", "_ClothFuzzStrength", Gate.Zero),
        ("_ClothGlossColor", "_ClothGlossStrength", Gate.Zero),
        ("_ClothSmoothness", "_ClothGlossStrength", Gate.Zero),
        ("_ClothThreadSmoothness", "_ClothGlossStrength", Gate.Zero),
        ("_ClothAnisotropy", "_ClothGlossStrength", Gate.Zero),
        ("_ClothSheenColor", "_ClothSheenStrength", Gate.Zero),
        ("_ClothStitchColor", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchLength", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchWidth", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchSpacing", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchOffset", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchAngle", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchTwin", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchGrid", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchMask", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchEdge", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchInset", "_ClothStitchStrength", Gate.Zero),
        ("_ClothStitchInset", "_ClothStitchEdge", Gate.Zero),
        ("_ClothStitchSpacing", "_ClothStitchEdge", Gate.One),
        ("_ClothStitchOffset", "_ClothStitchEdge", Gate.One),
        ("_ClothStitchAngle", "_ClothStitchEdge", Gate.One),
        ("_ClothStitchGrid", "_ClothStitchEdge", Gate.One),
        ("_ClothThreadSize", "_ClothWeave", Gate.Zero),
        ("_ClothThreadAspect", "_ClothWeave", Gate.Zero),
        ("_ClothThreadBump", "_ClothWeave", Gate.Zero),
        ("_ClothThreadShade", "_ClothWeave", Gate.Zero),
        ("_ClothThreadSmoothness", "_ClothWeave", Gate.Zero),
        ("_ClothThreadMap", "_ClothWeave", Gate.Zero),
        ("_OutlineTexDarken", "_OutlineTexBlend", Gate.Zero),
        ("_OutlineColor", "_OutlineTexBlend", Gate.One),
    };

    // The long half of the names: shown under the cursor, so the names fit a narrow panel.
    private static readonly Dictionary<string, string> LabelHints = new Dictionary<string, string>
    {
        { "_OutlineTexBlend", "線にテクスチャの色を混ぜる量" },
        { "_OutlineWidthMask", "黒 = 線なし（口元など）" },
        { "_OutlineDepthPush", "線の太さの何倍ぶん奥へ押すか" },
        { "_OutlineMaxPixels", "近づいた時の太さの上限。0 = 無制限" },
        { "_SkyLight", "空の光で上向きの面が明るくなる量" },
        { "_BackFaceShade", "スカートの内側など、裏から見た面の暗さ" },
        { "_NTESaturation", "1 = そのまま" },
        { "_DeepShadowStrength", "光から遠い面ほど「濃い影の色」になります" },
        { "_ClothThreadMap", "HDRP の Thread Map（無くても織り目は出ます）" },
        { "_ClothFuzzSize", "毛羽と色むらの大きさ" },
        { "_ClothFuzzMap", "HDRP の Fuzz Map（無くても毛羽は出ます）" },
        { "_EyeOverlayColorMap", "目に貼るテクスチャ（空など）" },
        { "_EyeOverlayFollow", "0 = 目に固定" },
        { "_EyeHighlightMask", "白 = 光る所" },
        { "_EyeHighlightThreshold", "マスクが無い時、これより白い所が光ります" },
        { "_EyeMirrorBulge", "反射の曲がり方。大きいほど広い範囲が映ります" },
        { "_HairHighlightJitterMap", "無くてもギザギザは出ます" },
        { "_HairHighlightStrength", "記事の Intensity" },
        { "_HairHighlightJitter", "記事の Jitter Intensity" },
        { "_NoseSinkDepth", "鼻の大きさの何倍ぶん沈むか" },
    };

    private static readonly string[] PartNames = { "顔", "体", "鼻" };

    private static readonly HashSet<string> FaceItems = new HashSet<string>
    {
        "_FaceSDFMap", "_FaceSDFStrength", "_FaceSDFSoftness", "_FaceSDFFlip"
    };

    private static readonly HashSet<string> NoseItems = new HashSet<string> { "_NoseHideAngle", "_NoseSinkDepth" };

    // All that does something on a nose (flat colour + sink + the shadow cast on it).
    private static readonly HashSet<string> NoseShows = new HashSet<string>
    {
        "_BaseMap", "_BaseColor", "_CelBrightness", "_NTESaturation", "_ShadowColor", "_CelCastShadowStrength",
        "_FaceAngle", "_NoseHideAngle", "_NoseSinkDepth"
    };

    // The only colours of these shaders whose alpha is read.
    private static readonly HashSet<string> AlphaColorProps = new HashSet<string> { "_EyeOverlayColor", "_EyeGlintColor" };

    private static bool IsToonShader(Shader shader) =>
        shader != null && (shader.name == CS_MayaPreviewOutline.ShaderName || shader.name.Contains("SHG_NTE_"));

    private static bool HasAlpha(Shader shader, string prop) => !IsToonShader(shader) || AlphaColorProps.Contains(prop);

    private bool IsPropertyShown(MaterialSlot slot, int index)
    {
        Material material = slot.material;
        string prop = material.shader.GetPropertyName(index);
        if (prop == CS_MayaPreviewMeshData.PartProp || prop == WeaveProp || prop == ClothKindProp) return false;     // the buttons above the groups
        int part = CS_MayaPreviewMeshData.PartOf(material);
        if (part == CS_MayaPreviewMeshData.PartFace && NoseItems.Contains(prop)) return false;
        if (part == CS_MayaPreviewMeshData.PartBody && (NoseItems.Contains(prop) || FaceItems.Contains(prop) || prop == "_FaceAngle"))
            return false;
        if (part == CS_MayaPreviewMeshData.PartNose && !NoseShows.Contains(prop)) return false;
        return prop != "_BaseMap" || !CS_MayaPreviewOutline.IsSettingsMaterial(material);
    }

    // Said once above the groups.
    private void DrawRelevanceHeader(MaterialSlot slot, CS_MayaPreviewMaterialLibrary library)
    {
        Material material = slot.material;
        if (!IsToonShader(material.shader)) return;
        int part = CS_MayaPreviewMeshData.PartOf(material);
        if (part >= 0)
        {
            int picked = ButtonGrid("パーツ", "同じシェーダーのまま、出る項目と計算が切り替わります", PartNames, part, PartNames.Length);
            if (picked >= 0 && picked != part) SetFloat(slot, library, CS_MayaPreviewMeshData.PartProp, picked);
        }
        DrawClothHeader(slot, library);
        Note("灰色 = 今の設定では効かない項目（カーソルを合わせると理由が出ます）");
    }

    // Why the item does nothing now, or null.
    private static string GreyReason(Material material, string prop)
    {
        Shader shader = material.shader;
        if (!IsToonShader(shader)) return null;
        foreach ((string item, string gate, Gate when) in GreyRules)
        {
            if (item != prop) continue;
            int index = shader.FindPropertyIndex(gate);
            if (index < 0) continue;
            string name = "「" + shader.GetPropertyDescription(index) + "」";
            switch (when)
            {
                case Gate.NoTexture:
                    if (!HasMap(material, gate)) return name + "に画像を入れると使えます";
                    break;
                case Gate.Zero:
                    if (material.GetFloat(gate) < GateEpsilon)
                        return gate == WeaveProp ? "織り方が「" + WeaveNames[0] + "」なので効きません" : name + "が 0 なので効きません";
                    break;
                case Gate.One:
                    if (material.GetFloat(gate) > 1f - GateEpsilon) return name + "が 1 なので効きません";
                    break;
                case Gate.Filled:
                    if (HasMap(material, gate)) return name + "を使っているので効きません";
                    break;
                case Gate.MapInUse:
                    if (CS_MayaPreviewMeshData.PartOf(material) == CS_MayaPreviewMeshData.PartFace
                        && HasMap(material, gate) && material.GetFloat("_FaceSDFStrength") >= GateEpsilon)
                        return name + "が影を決めているので効きません";
                    break;
            }
        }
        return null;
    }

    // Same test as NTE_HasMap: the shader's empty slot is a 4x4 texture.
    private static bool HasMap(Material material, string prop)
    {
        Texture texture = material.GetTexture(prop);
        return texture != null && Mathf.Max(texture.width, texture.height) > 4;
    }
}
