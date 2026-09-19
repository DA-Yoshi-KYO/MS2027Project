using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2027.EditorTools
{
    [InitializeOnLoad]
    internal static class ProtectedBranchWarning
    {
        private const string ConfigAsset = "Assets/Editor/github_screen/ProtectedBranches.json";
        private const string LastBranchKey = "MS2027.ProtectedBranch.LastBranch";
        private const string WarnedKey = "MS2027.ProtectedBranch.Warned";
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly HashSet<VisualElement> Roots = new HashSet<VisualElement>();
        private static readonly HashSet<string> ProtectedBranches = new HashSet<string>(StringComparer.Ordinal);
        private static string configContents;
        private static string queuedBranch;
        private static double nextPoll;

        [Serializable]
        private sealed class Settings
        {
            public string[] protectedBranches = Array.Empty<string>();
        }

        static ProtectedBranchWarning()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting += Cleanup;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            ReloadSettings();
            ObserveBranch();
            Roots.RemoveWhere(root => root.panel == null);
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var root = window.rootVisualElement;
                if (!Roots.Add(root)) continue;
                // Capture before Scene, Inspector, Project, and toolbar controls handle the click.
                root.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            }
        }

        private static void ReloadSettings()
        {
            try
            {
                string path = Path.Combine(ProjectRoot, ConfigAsset);
                string contents = File.Exists(path) ? File.ReadAllText(path) : "";
                if (contents == configContents) return;
                configContents = contents;
                var settings = string.IsNullOrWhiteSpace(contents) ? null : JsonUtility.FromJson<Settings>(contents);
                ProtectedBranches.Clear();
                if (settings?.protectedBranches == null) return;
                foreach (string branch in settings.protectedBranches)
                    if (!string.IsNullOrWhiteSpace(branch)) ProtectedBranches.Add(branch.Trim());
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Debug.LogWarning("[Git表示] ProtectedBranches.jsonを読み込めません。JSONの形式とアクセス権を確認してください。");
            }
        }

        private static string ObserveBranch()
        {
            string branch = ReadBranch(ProjectRoot);
            if (branch == null) return null;
            // SessionState survives script recompiles, but resets when this Unity session closes.
            if (SessionState.GetString(LastBranchKey, "") != branch)
            {
                SessionState.SetString(LastBranchKey, branch);
                SessionState.SetBool(WarnedKey, false);
            }
            return branch;
        }

        private static void OnPointerDown(PointerDownEvent evt)
        {
            if (queuedBranch != null) return;
            ReloadSettings();
            // Read HEAD at click time as well, so a recent switch cannot use stale toolbar status.
            string branch = ObserveBranch();
            if (branch == null || !ProtectedBranches.Contains(branch) || SessionState.GetBool(WarnedKey, false)) return;
            SessionState.SetBool(WarnedKey, true);
            queuedBranch = branch;
            evt.StopImmediatePropagation();
#pragma warning disable CS0618
            evt.PreventDefault();
#pragma warning restore CS0618
            EditorApplication.delayCall += ShowWarning;
        }

        private static void ShowWarning()
        {
            string branch = queuedBranch;
            queuedBranch = null;
            if (branch == null || ObserveBranch() != branch || !ProtectedBranches.Contains(branch)) return;
            EditorUtility.DisplayDialog("作業ブランチの確認",
                branch + "\n\n作業禁止のブランチですがよろしいでしょうか？", "続行", "キャンセル");
            // This is a once-per-entry warning. Neither choice replays the intercepted click.
        }

        private static string ReadBranch(string projectRoot)
        {
            try
            {
                for (var directory = new DirectoryInfo(projectRoot); directory != null; directory = directory.Parent)
                {
                    string gitPath = Path.Combine(directory.FullName, ".git");
                    if (File.Exists(gitPath))
                    {
                        string link = File.ReadAllText(gitPath).Trim();
                        if (!link.StartsWith("gitdir:", StringComparison.Ordinal)) return null;
                        gitPath = Path.GetFullPath(Path.Combine(directory.FullName, link.Substring(7).Trim()));
                    }
                    else if (!Directory.Exists(gitPath)) continue;
                    string head = File.ReadAllText(Path.Combine(gitPath, "HEAD")).Trim();
                    const string prefix = "ref: refs/heads/";
                    return head.StartsWith(prefix, StringComparison.Ordinal) ? head.Substring(prefix.Length) : "(detached HEAD)";
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        private static void Cleanup()
        {
            EditorApplication.update -= Update;
            EditorApplication.delayCall -= ShowWarning;
            foreach (var root in Roots)
                root.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            Roots.Clear();
        }
    }
}
