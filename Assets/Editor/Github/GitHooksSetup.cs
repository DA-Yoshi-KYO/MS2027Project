using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;

// Unityプロジェクトを開いたときに自動でGit hooksPathを設定するスクリプト
// 配置場所: Assets/Editor/GitHooksSetup.cs

[InitializeOnLoad]
public class GitHooksSetup
{
    private const string HooksDirName = ".githooks";
    private const string MarkerKey = "GitHooksSetup_AlreadyConfigured";

    static GitHooksSetup()
    {
        // 同じプロジェクトで何度も実行しないようにフラグ管理
        if (SessionState.GetBool(MarkerKey, false))
            return;

        SessionState.SetBool(MarkerKey, true);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string hooksPath = Path.Combine(projectRoot, HooksDirName);

        if (!Directory.Exists(hooksPath))
        {
            // .githooksフォルダがまだ無ければ何もしない
            return;
        }

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "config core.hooksPath " + HooksDirName,
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    UnityEngine.Debug.Log("[GitHooksSetup] Git hooks path を自動設定しました (.githooks)");
                }
                else
                {
                    string error = process.StandardError.ReadToEnd();
                    UnityEngine.Debug.LogWarning("[GitHooksSetup] Git hooks path の設定に失敗しました: " + error);
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("[GitHooksSetup] Gitコマンドの実行中にエラーが発生しました: " + e.Message);
        }
    }
}
