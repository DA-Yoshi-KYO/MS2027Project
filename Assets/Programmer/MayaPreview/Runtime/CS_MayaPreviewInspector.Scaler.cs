using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Scaler
 Summary: MayaScaler (toolbar check box left of Outline).
          On (default): everything received from Maya is shown 100x (Maya_Model_Root scale 100).
          Off         : as received (scale 1).
          Only the view: Export still writes the Maya values unchanged.
          The choice is remembered between runs (PlayerPrefs).
 History: 2026/11/05 New
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private const string MayaScalerPrefsKey = "MayaPreview.MayaScaler";
    private const float MayaScalerFactor = 100f;

    private bool _mayaScaler = true;
    private Rect _mayaScalerButton;

    private void LoadMayaScaler()
    {
        _mayaScaler = PlayerPrefs.GetInt(MayaScalerPrefsKey, 1) != 0;
        ApplyMayaScaler();
    }

    private void SetMayaScaler(bool on)
    {
        _mayaScaler = on;
        PlayerPrefs.SetInt(MayaScalerPrefsKey, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMayaScaler();
        Flash(on ? "MayaScaler: x" + MayaScalerFactor : "MayaScaler: x1");
    }

    // Also called from Scan (the root can be created after this component).
    private void ApplyMayaScaler()
    {
        Transform root = _scanModelRoot != null ? _scanModelRoot : CS_MayaPreviewExport.ModelRoot();
        if (root == null) return;
        Vector3 scale = Vector3.one * (_mayaScaler ? MayaScalerFactor : 1f);
        if (root.localScale != scale) root.localScale = scale;
    }
}
