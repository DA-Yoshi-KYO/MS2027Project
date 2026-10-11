using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
 History: 2026/10/11 New
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
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("Maya Preview Host build: " + report.summary.result + "  errors " + report.summary.totalErrors +
                  "  " + options.locationPathName);
        return report;
    }
}
