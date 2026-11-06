using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/*
+=====================================
 File   : CSE_MayaPreviewHostBuild
 Summary: Builds the Maya preview player (MS2027.exe) from HDRP_Maya_Toon_Scene.
          - Menu: Tools > Maya Preview > Build Preview Host
          - Command line (the project must not be open in another editor):
            Unity.exe -batchmode -quit -projectPath <project>
                      -executeMethod CSE_MayaPreviewHostBuild.BuildFromCommandLine
                      [-mpOutput <folder>] -logFile <log>
          Default output: <Desktop>/Unity_Maya_PreviewHost/MS2027.exe
          The project's Assets path is written next to the player
          (MS2027_Data/StreamingAssets/MayaPreviewProject.txt): the player's Export writes
          into <project>/Assets/Designer by default.
 History: 2026/10/11 New
          2026/10/11 Project path for the export
          2026/11/05 Built with DirectX 11 (less memory), project setting restored afterwards
=====================================+
*/
public static class CSE_MayaPreviewHostBuild
{
    private const string Scene = "Assets/Programmer/Scene/Test/Maya/HDRP_Maya_Toon_Scene.unity";

    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Unity_Maya_PreviewHost");

    [MenuItem("Tools/Maya Preview/Build Preview Host")]
    private static void BuildMenu()
    {
        string folder = DefaultFolder;
        if (!EditorUtility.DisplayDialog("Maya Preview", "Build the preview player to\n" + folder + "\n\nClose MS2027.exe first.", "Build", "Cancel")) return;
        BuildReport report = Build(folder);
        EditorUtility.DisplayDialog("Maya Preview", "Build " + report.summary.result + "\n" + folder, "OK");
    }

    public static void BuildFromCommandLine()
    {
        string folder = DefaultFolder;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-mpOutput") folder = args[i + 1];
        BuildReport report = Build(folder);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    public static BuildReport Build(string folder)
    {
        Directory.CreateDirectory(folder);
        var options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = Path.Combine(folder, "MS2027.exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };
        // The preview player uses DirectX 11: much less driver memory than DirectX 12 and no
        // ray tracing structures (ray tracing is off in the preview anyway). The project's own
        // setting (DX12 first) is put back right after the build.
        GraphicsDeviceType[] previousApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
        bool previousAuto = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
        BuildReport report;
        try
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, previousApis);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, previousAuto);
        }
        if (report.summary.result == BuildResult.Succeeded)
        {
            try
            {
                string streaming = Path.Combine(folder, "MS2027_Data", "StreamingAssets");
                Directory.CreateDirectory(streaming);
                File.WriteAllText(Path.Combine(streaming, CS_MayaPreviewExport.ProjectFile), Application.dataPath);
            }
            catch (Exception exception) { Debug.LogWarning("Maya Preview Host build: could not write the project path. " + exception.Message); }
        }
        Debug.Log("Maya Preview Host build: " + report.summary.result + "  errors " + report.summary.totalErrors +
                  "  " + options.locationPathName);
        return report;
    }
}
