using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Cloth
 Summary: The buttons above the groups of SHG_NTE_Cloth.
          - 布の材質 : sets the thread / fuzz / gloss sliders to a kind of cloth (the values of Unity's
                       HDRP fabric samples, shaded as toon). A start: every slider stays editable.
                       Colours, maps, the thread's angle and the stitches are left as they are.
          - 織り方   : the thread pattern (_ClothWeave).
 History: 2026/11/22 New
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private const string WeaveProp = "_ClothWeave";
    private const int ClothButtonsPerRow = 4;

    private static readonly string[] WeaveNames = { "無地", "ジャージ", "綾織", "平織", "格子", "リブ" };     // by the value of _ClothWeave

    private static readonly string[] PresetProps =
    {
        WeaveProp, "_ClothThreadSize", "_ClothThreadBump", "_ClothThreadShade", "_ClothFuzzStrength", "_ClothFuzzSize", "_ClothMottle",
        "_ClothSmoothness", "_ClothThreadSmoothness", "_ClothGlossStrength", "_ClothAnisotropy", "_ClothSheenStrength"
    };

    private static readonly (string name, float[] values)[] ClothPresets =
    {
        //                         weave size  bump  shade fuzz   size mottle smooth diff  gloss  aniso  sheen
        ("綿",       new[] { 1f, 2.0f, 0.7f, 0.65f, 0.09f, 6f, 0.05f, 0.40f, 0.20f, 0.05f,  0.00f, 0.15f }),
        ("麻",       new[] { 3f, 2.0f, 0.9f, 0.80f, 0.06f, 4f, 0.35f, 0.30f, 0.05f, 0.03f,  0.00f, 0.10f }),
        ("デニム",   new[] { 2f, 1.5f, 1.0f, 0.90f, 0.20f, 4f, 0.30f, 0.30f, 0.05f, 0.04f,  0.00f, 0.15f }),
        ("ウール",   new[] { 5f, 3.5f, 1.4f, 0.90f, 0.40f, 3f, 0.10f, 0.25f, 0.40f, 0.03f,  0.00f, 0.40f }),
        ("ナイロン", new[] { 4f, 1.5f, 0.6f, 0.25f, 0.02f, 6f, 0.00f, 0.60f, 0.08f, 0.25f, -0.30f, 0.05f }),
        ("シルク",   new[] { 2f, 0.8f, 0.2f, 0.25f, 0.00f, 6f, 0.00f, 0.58f, 0.10f, 0.40f, -0.75f, 0.20f }),
        ("ベルベット", new[] { 0f, 2.0f, 0.0f, 0.00f, 0.03f, 5f, 0.10f, 0.30f, 0.00f, 0.03f,  0.00f, 0.90f }),
    };

    private void DrawClothHeader(MaterialSlot slot, CS_MayaPreviewMaterialLibrary library)
    {
        Material material = slot.material;
        if (!material.HasProperty(WeaveProp)) return;

        for (int start = 0; start < ClothPresets.Length; start += ClothButtonsPerRow)
        {
            BeginRow(start == 0 ? "布の材質" : "", "押すと、糸・毛羽・光沢の値がその布になります。その後で細かく変えられます");
            for (int i = start; i < Mathf.Min(start + ClothButtonsPerRow, ClothPresets.Length); i++)
            {
                if (!GUILayout.Button(ClothPresets[i].name, IsPreset(material, ClothPresets[i].values) ? _buttonOn : _button, GUILayout.Height(RowHeight)))
                    continue;
                for (int p = 0; p < PresetProps.Length; p++) SetFloat(slot, library, PresetProps[p], ClothPresets[i].values[p]);
                ClearFields();
                GUI.FocusControl(null);
            }
            EndRow();
        }

        int weave = Mathf.RoundToInt(material.GetFloat(WeaveProp));
        for (int start = 0; start < WeaveNames.Length; start += ClothButtonsPerRow)
        {
            BeginRow(start == 0 ? "織り方" : "", "糸の模様。大きさは「織り目」の「糸の太さ・細かさ」で変えられます");
            for (int i = start; i < Mathf.Min(start + ClothButtonsPerRow, WeaveNames.Length); i++)
            {
                if (!GUILayout.Button(WeaveNames[i], i == weave ? _buttonOn : _button, GUILayout.Height(RowHeight))) continue;
                if (i != weave) SetFloat(slot, library, WeaveProp, i);
                GUI.FocusControl(null);
            }
            EndRow();
        }
    }

    private static bool IsPreset(Material material, float[] values)
    {
        for (int p = 0; p < PresetProps.Length; p++)
            if (!material.HasProperty(PresetProps[p]) || Mathf.Abs(material.GetFloat(PresetProps[p]) - values[p]) > 0.0005f) return false;
        return true;
    }
}
