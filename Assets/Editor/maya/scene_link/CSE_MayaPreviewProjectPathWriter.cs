using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/*
+=====================================
 File   : CSE_MayaPreviewProjectPathWriter
 Summary: Writes MayaPreviewProject.txt (this project's Assets path) next to ANY Windows player
          whose Build Settings contain a Maya preview scene (Assets/.../Maya/...unity), so the preview's
          Export finds the project even when the exe was built with File > Build instead of
          Tools > Maya Preview > Build Preview Host. Game builds without such a scene get nothing.
 History: 2026/11/13 New
=====================================+
*/
public class CSE_MayaPreviewProjectPathWriter : IPostprocessBuildWithReport
{
    public int callbackOrder => 100;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64 && report.summary.platform != BuildTarget.StandaloneWindows) return;
        bool preview = false;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled && scene.path.Replace('\\', '/').Contains("/Maya/")) preview = true;
        // (Tools > Maya Preview > Build Preview Host writes the file itself.)
        if (!preview) return;

        try
        {
            string exe = report.summary.outputPath;
            string data = Path.Combine(Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe) + "_Data");
            string streaming = Path.Combine(data, "StreamingAssets");
            Directory.CreateDirectory(streaming);
            File.WriteAllText(Path.Combine(streaming, CS_MayaPreviewExport.ProjectFile), Application.dataPath);
        }
        catch (Exception exception) { Debug.LogWarning("Maya Preview: could not write the project path. " + exception.Message); }
    }
}
