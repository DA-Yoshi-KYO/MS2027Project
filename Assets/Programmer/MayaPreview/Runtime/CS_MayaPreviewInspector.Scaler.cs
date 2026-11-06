using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Scaler
 Summary: MayaScaler (toolbar check box left of Outline).
          Maya sends its values in centimetres and the mesh is built 1 Maya unit = 1 Unity unit.
          Off (default): real size. Maya_Model_Root is scaled 0.01 (cm -> m, like an FBX import),
                         so a 170 cm character in Maya is 1.7 m here.
          On           : 100x the real size (Maya_Model_Root scale 1: 1 cm in Maya = 1 m here).
          Only the view: Export still writes the Maya values unchanged.
          The choice is remembered between runs (PlayerPrefs).
 History: 2026/11/05 New
          2026/11/06 Off was 1 cm = 1 m (100x too big) and On another 100x on top: Off is now
                     the real size and On is 100x
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    // New key: the old one stored "On" as the default, which is not the real size any more.
    private const string MayaScalerPrefsKey = "MayaPreview.MayaScaler.v2";
    private const float MayaScalerFactor = 100f;
    private const float MayaCentimetresToMetres = 0.01f;

    private bool _mayaScaler;
    private Rect _mayaScalerButton;

    private void LoadMayaScaler()
    {
        _mayaScaler = PlayerPrefs.GetInt(MayaScalerPrefsKey, 0) != 0;
        ApplyMayaScaler();
    }

    private void SetMayaScaler(bool on)
    {
        _mayaScaler = on;
        PlayerPrefs.SetInt(MayaScalerPrefsKey, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMayaScaler();
        Flash(on ? "MayaScaler: x" + MayaScalerFactor + " (1 cm in Maya = 1 m)" : "MayaScaler: real size (cm -> m)");
    }

    // Also called from Scan (the root can be created after this component).
    private void ApplyMayaScaler()
    {
        Transform root = _scanModelRoot != null ? _scanModelRoot : CS_MayaPreviewExport.ModelRoot();
        if (root == null) return;
        Vector3 scale = Vector3.one * (MayaCentimetresToMetres * (_mayaScaler ? MayaScalerFactor : 1f));
        if (root.localScale != scale) root.localScale = scale;
    }
}
