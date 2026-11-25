using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Cloth
 Summary: The buttons above the groups of SHG_NTE_Cloth.
          - 布の材質 : sets the thread / fuzz / gloss sliders to a kind of cloth (the values of Unity's
                       HDRP fabric samples, shaded as toon). A start: every slider stays editable.
                       Colours, maps, the thread's angle and the stitches are left as they are.
          - 織り方   : the thread pattern (_ClothWeave). A weave the chosen cloth does not exist in is
                       grey (velvet has a pile, denim is a twill ...).
          Buttons sit in a grid of one width, in as many columns as fit every name; the two kinds have a
          space between them.
 History: 2026/11/22 New
          2026/11/22 One width, space, threads of the presets visible from a normal distance
          2026/11/22 No name cut off, weaves the cloth cannot have are grey
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private const string WeaveProp = "_ClothWeave";
    private const int ClothButtonsPerRow = 4;
    private const float ClothBlockSpace = 12f;

    private static readonly string[] WeaveNames = { "無地", "ジャージ", "綾織", "平織", "格子", "リブ" };     // by the value of _ClothWeave

    private static readonly string[] PresetProps =
    {
        WeaveProp, "_ClothThreadSize", "_ClothThreadBump", "_ClothThreadShade", "_ClothFuzzStrength", "_ClothFuzzSize", "_ClothMottle",
        "_ClothSmoothness", "_ClothThreadSmoothness", "_ClothGlossStrength", "_ClothAnisotropy", "_ClothSheenStrength"
    };

    private static readonly (string name, float[] values)[] ClothPresets =
    {
        //                         weave size  bump  shade fuzz   size mottle smooth diff  gloss  aniso  sheen
        ("綿",       new[] { 1f, 4.0f, 1.0f, 0.90f, 0.09f, 6f, 0.05f, 0.40f, 0.20f, 0.05f,  0.00f, 0.15f }),
        ("麻",       new[] { 3f, 4.0f, 1.2f, 1.00f, 0.06f, 4f, 0.35f, 0.30f, 0.05f, 0.03f,  0.00f, 0.10f }),
        ("デニム",   new[] { 2f, 3.0f, 1.3f, 1.10f, 0.20f, 4f, 0.30f, 0.30f, 0.05f, 0.04f,  0.00f, 0.15f }),
        ("ウール",   new[] { 5f, 6.0f, 1.6f, 1.10f, 0.40f, 3f, 0.10f, 0.25f, 0.40f, 0.03f,  0.00f, 0.40f }),
        ("ナイロン", new[] { 4f, 3.0f, 0.9f, 0.50f, 0.02f, 6f, 0.00f, 0.60f, 0.08f, 0.25f, -0.30f, 0.05f }),
        ("シルク",   new[] { 2f, 2.0f, 0.4f, 0.40f, 0.00f, 6f, 0.00f, 0.58f, 0.10f, 0.40f, -0.75f, 0.20f }),
        ("ベルベット", new[] { 0f, 2.0f, 0.0f, 0.00f, 0.03f, 5f, 0.10f, 0.30f, 0.00f, 0.03f,  0.00f, 0.90f }),
    };

    private static readonly string[] PresetNames = System.Array.ConvertAll(ClothPresets, preset => preset.name);

    private const string ClothKindProp = "_ClothMaterial";      // the cloth picked last: stays while the sliders are changed

    // Which weaves each cloth really exists in (by WeaveNames).
    private static readonly int[][] WeavesOfCloth =
    {
        new[] { 0, 1, 2, 3, 4, 5 },     // 綿
        new[] { 0, 2, 3, 4 },           // 麻: 編み物（ジャージ・リブ）は無い
        new[] { 2 },                    // デニム: 綾織の生地の名前
        new[] { 0, 1, 2, 3, 4, 5 },     // ウール
        new[] { 0, 1, 2, 3, 4 },        // ナイロン: リブ編みは無い
        new[] { 0, 1, 2, 3 },           // シルク: 格子・リブは無い
        new[] { 0 },                    // ベルベット: 表が毛足なので織り目は見えない
    };

    private static string CannotWeave(int cloth, int weave)
    {
        if (cloth < 0 || System.Array.IndexOf(WeavesOfCloth[cloth], weave) >= 0) return null;
        string can = string.Join("・", System.Array.ConvertAll(WeavesOfCloth[cloth], w => WeaveNames[w]));
        return "「" + PresetNames[cloth] + "」では選べません（選べるのは " + can + "）";
    }

    private void DrawClothHeader(MaterialSlot slot, CS_MayaPreviewMaterialLibrary library)
    {
        Material material = slot.material;
        if (!material.HasProperty(WeaveProp)) return;

        int columns = FittingColumns(PresetNames, WeaveNames, ClothButtonsPerRow);
        int cloth = System.Array.FindIndex(ClothPresets, preset => IsPreset(material, preset.values));
        if (cloth < 0 && material.HasProperty(ClothKindProp))
            cloth = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat(ClothKindProp)), 0, ClothPresets.Length - 1);
        int picked = ButtonBlock("布の材質", "押すと、糸・毛羽・光沢の値がその布になります。その後で細かく変えられます",
                                 PresetNames, cloth, columns);
        if (picked >= 0)
        {
            for (int p = 0; p < PresetProps.Length; p++) SetFloat(slot, library, PresetProps[p], ClothPresets[picked].values[p]);
            if (material.HasProperty(ClothKindProp)) SetFloat(slot, library, ClothKindProp, picked);
            ClearFields();
        }

        GUILayout.Space(ClothBlockSpace);

        int weave = Mathf.RoundToInt(material.GetFloat(WeaveProp));
        picked = ButtonBlock("織り方", "糸の模様。大きさは「織り目」の「糸の太さ・細かさ」で変えられます",
                             WeaveNames, weave, columns, i => CannotWeave(cloth, i));
        if (picked >= 0 && picked != weave) SetFloat(slot, library, WeaveProp, picked);
        GUILayout.Space(ClothBlockSpace * 0.5f);
    }

    private static bool IsPreset(Material material, float[] values)
    {
        for (int p = 0; p < PresetProps.Length; p++)
            if (!material.HasProperty(PresetProps[p]) || Mathf.Abs(material.GetFloat(PresetProps[p]) - values[p]) > 0.0005f) return false;
        return true;
    }
}
