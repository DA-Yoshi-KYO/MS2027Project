using System;
using System.Runtime.InteropServices;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewMemory
 Summary: Keeps the preview exe small in memory.
          - Before the first scene loads: applies the saved quality level and texture size
            (Graphics panel), so the heavy HDRP asset / full-size textures are never loaded
            when the light setup is in use.
          - Trim(): when the exe goes behind Maya, unused assets are unloaded, the GC runs and
            Windows takes back the pages of the working set (they come back by themselves
            when the exe is used again). Task Manager "Memory" drops accordingly.
 History: 2026/11/05 New
=====================================+
*/
public static class CS_MayaPreviewMemory
{
    public const string QualityPrefsKey = "MayaPreview.Quality";
    public const string TextureLimitPrefsKey = "MayaPreview.TextureLimit";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedSettings()
    {
        if (Application.isEditor) return;   // never change the editor's quality settings
        int quality = PlayerPrefs.GetInt(QualityPrefsKey, -1);
        if (quality >= 0 && quality < QualitySettings.names.Length && quality != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(quality, true);
        int limit = PlayerPrefs.GetInt(TextureLimitPrefsKey, -1);
        if (limit >= 0) QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(limit, 0, 3);
    }

    private static AsyncOperation s_unload;

    public static void Trim()
    {
        if (s_unload != null && !s_unload.isDone) return;
        s_unload = Resources.UnloadUnusedAssets();
        s_unload.completed += _ =>
        {
            GC.Collect();
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { SetProcessWorkingSetSize(GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1)); }
            catch { /* not critical */ }
#endif
        };
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimum, IntPtr maximum);
#endif
}
