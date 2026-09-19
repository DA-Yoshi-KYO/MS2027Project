using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2027.EditorTools
{
    [InitializeOnLoad]
    internal static class GitToolbar
    {
        private const string ToolbarPath = "Git/ブランチとdevelop";
        private const string StatusPath = "Git/developの最新状況";
        private const string GitPreference = "MS2027.GitToolbar.Executable";
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly CancellationTokenSource Lifetime = new CancellationTokenSource();
        private static Task<Snapshot> pending;
        private static Snapshot current = new Snapshot { Branch = "確認中…", Status = "develop: 確認中…" };
        private static double nextCheck;
        private static bool refreshRequested;
        private static string executable;
        private static string remoteHash;
        private static string remoteError;
        private static DateTime remoteChecked;
        private static readonly string VisibilityPreference = "MS2027.GitToolbar.VisibleOnce.v2." + ProjectRoot;
        private static bool visibilityInitialized = EditorPrefs.GetBool(VisibilityPreference, false);
        private static int visibilityAttempts;
        private static double nextVisibilityCheck;
        private static double nextLayoutCheck;

        static GitToolbar()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        [MainToolbarElement(ToolbarPath, defaultDockPosition = MainToolbarDockPosition.Left, defaultDockIndex = 100)]
        private static MainToolbarElement CreateToolbar()
        {
            return CreateLabel("Git: " + Escape(current.Branch));
        }

        [MainToolbarElement(StatusPath, defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 0)]
        private static MainToolbarElement CreateStatus()
        {
            return CreateLabel(StatusText());
        }

        private static string StatusText()
        {
            string status = Escape(current.Status);
            if (current.Warning) status = "<color=#FFD54F>" + status + "</color>";
            else if (current.Latest) status = "<color=#81C784>" + status + "</color>";
            return status;
        }

        private static MainToolbarElement CreateLabel(string text)
        {
            var label = new MainToolbarLabel(new MainToolbarContent(text, current.Detail ?? current.Status));
            label.populateContextMenu = PopulateGitMenu;
            return label;
        }

        private static void PopulateGitMenu(DropdownMenu menu)
        {
            menu.AppendAction("今すぐ再確認", _ => Refresh());
            menu.AppendAction("Gitの実行ファイルを指定…", _ => SelectGit());
            menu.AppendAction("Gitの自動検出に戻す", _ => ResetGit());
        }

        private static void OnGitContextMenu(ContextualMenuPopulateEvent evt)
        {
            // Own this menu before Unity's overlay handlers append their Hide action.
            evt.menu.MenuItems().Clear();
            PopulateGitMenu(evt.menu);
            evt.StopImmediatePropagation();
        }

        private static string Escape(string text) => (text ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static void Update()
        {
            if (Lifetime.IsCancellationRequested) return;
            EnsureInitialVisibility();
            if (EditorApplication.timeSinceStartup >= nextLayoutCheck)
            {
                nextLayoutCheck = EditorApplication.timeSinceStartup + 1;
                ApplyToolbarLayout();
            }
            if (pending != null)
            {
                if (!pending.IsCompleted) return;
                current = pending.Status == TaskStatus.RanToCompletion ? pending.Result :
                    new Snapshot { Branch = current.Branch, Status = "Git: 確認できません", Warning = true };
                pending = null;
                // Keep the existing elements and their measured widths; rebuilding briefly
                // restores Unity's default width and makes the neighbouring tools jump.
                ApplyToolbarLayout();
            }
            if (refreshRequested)
            {
                executable = null;
                remoteChecked = DateTime.MinValue;
                nextCheck = 0;
                refreshRequested = false;
            }
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 5;
            string configured = EditorPrefs.GetString(GitPreference, "");
            pending = Task.Run(() => ReadStatus(configured, Lifetime.Token));
        }

        private static void ApplyToolbarLayout()
        {
            // Unity's label has a default width limit. Remove it only for this Git display.
            var method = typeof(MainToolbar).GetMethod("TryGetOverlay", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) return;
            var arguments = new object[] { ToolbarPath, null };
            if (!(bool)method.Invoke(null, arguments)) return;
            var overlay = arguments[1] as UnityEditor.Overlays.Overlay;
            if (overlay == null) return;
            var root = overlay.rootVisualElement;
            StyleLabel(root, "Git: " + Escape(current.Branch));
            var statusArguments = new object[] { StatusPath, null };
            if ((bool)method.Invoke(null, statusArguments) && statusArguments[1] is UnityEditor.Overlays.Overlay statusOverlay)
                StyleLabel(statusOverlay.rootVisualElement, StatusText());

            // Anchor the playback section to the window centre, independent of either side's text.
            for (var container = root.parent; container != null; container = container.parent)
            {
                var type = container.GetType();
                if (type.Name != "MainToolbarOverlayContainer") continue;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var left = type.GetField("m_LeftSection", flags)?.GetValue(container) as VisualElement;
                var right = type.GetField("m_RightSection", flags)?.GetValue(container) as VisualElement;
                var middle = type.GetField("m_MiddleSection", flags)?.GetValue(container) as VisualElement;
                if (left == null || right == null || middle == null) return;
                foreach (var side in new[] { left, right })
                {
                    side.style.flexBasis = 0;
                    side.style.flexGrow = 1;
                    side.style.flexShrink = 1;
                    side.style.minWidth = 0;
                }
                right.style.justifyContent = Justify.FlexEnd;
                right.contentContainer.style.justifyContent = Justify.FlexEnd;
                middle.style.position = Position.Absolute;
                middle.style.flexGrow = 0;
                middle.style.flexShrink = 0;
                middle.style.width = StyleKeyword.Auto;
                // Centre the visible controls, excluding the overlay's dragger/padding.
                Rect controls = default;
                bool foundControls = false;
                middle.Query<VisualElement>().ForEach(element =>
                {
                    if (!(element is Button) && !(element is Toggle)) return;
                    Rect bounds = element.worldBound;
                    if (element.resolvedStyle.display == DisplayStyle.None || bounds.width <= 0 || bounds.height <= 0) return;
                    controls = !foundControls ? bounds : Rect.MinMaxRect(Mathf.Min(controls.xMin, bounds.xMin),
                        Mathf.Min(controls.yMin, bounds.yMin), Mathf.Max(controls.xMax, bounds.xMax), Mathf.Max(controls.yMax, bounds.yMax));
                    foundControls = true;
                });
                if (foundControls && middle.panel != null)
                {
                    Vector2 desired = new Vector2(middle.panel.visualTree.worldBound.center.x, container.worldBound.center.y);
                    Vector2 target = middle.parent.WorldToLocal(desired);
                    Vector2 offset = middle.WorldToLocal(controls.center);
                    middle.style.translate = new Translate(0, 0);
                    middle.style.left = Mathf.Round(target.x - offset.x);
                    middle.style.top = Mathf.Round(target.y - offset.y);
                }
                return;
            }
        }

        private static void StyleLabel(VisualElement root, string text)
        {
            root.UnregisterCallback<ContextualMenuPopulateEvent>(OnGitContextMenu, TrickleDown.TrickleDown);
            root.RegisterCallback<ContextualMenuPopulateEvent>(OnGitContextMenu, TrickleDown.TrickleDown);
            // Replace Unity's automatically appended drag instructions on our own elements only.
            root.tooltip = current.Detail ?? current.Status;
            root.Query<VisualElement>().ForEach(element =>
            {
                if (!string.IsNullOrEmpty(element.tooltip)) element.tooltip = root.tooltip;
            });
            root.Query<TextElement>().ForEach(label =>
            {
                if (label.name != "EditorToolbarButtonText") return;
                if (label.text != text) label.text = text;
                label.enableRichText = true;
                // Keep Unity's standard text metrics so the baseline matches adjacent tools.
                label.style.fontSize = StyleKeyword.Null;
                label.style.height = StyleKeyword.Null;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.textOverflow = TextOverflow.Clip;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                float width = Mathf.Ceil(label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined,
                    0, VisualElement.MeasureMode.Undefined).x) + 12;
                label.style.minWidth = width;
                for (VisualElement element = label; element != null; element = element.parent)
                {
                    element.style.maxWidth = StyleKeyword.None;
                    element.style.flexShrink = 0;
                    if (element == root) break;
                }
            });

        }

        private static void EnsureInitialVisibility()
        {
            if (visibilityInitialized || visibilityAttempts >= 30 ||
                EditorApplication.timeSinceStartup < nextVisibilityCheck) return;
            nextVisibilityCheck = EditorApplication.timeSinceStartup + 1;
            visibilityAttempts++;
            // Unity 6.3 has no public API for initially showing a registered toolbar overlay.
            // Only enable our own element once; respect subsequent user layout changes.
            try
            {
                var method = typeof(MainToolbar).GetMethod("TryGetOverlay", BindingFlags.Static | BindingFlags.NonPublic);
                if (method == null) { visibilityInitialized = true; return; }
                var arguments = new object[] { ToolbarPath, null };
                if (!(bool)method.Invoke(null, arguments)) return;
                var overlay = arguments[1] as UnityEditor.Overlays.Overlay;
                if (overlay == null) return;
                overlay.displayed = true;
                var statusArguments = new object[] { StatusPath, null };
                if (!(bool)method.Invoke(null, statusArguments)) return;
                if (statusArguments[1] is UnityEditor.Overlays.Overlay statusOverlay) statusOverlay.displayed = true;
                EditorPrefs.SetBool(VisibilityPreference, true);
                visibilityInitialized = true;
            }
            catch (Exception)
            {
                // Registration still works; use the toolbar context menu if Unity changes this API.
                visibilityInitialized = true;
            }
        }

        [MenuItem("Tools/Git表示/今すぐ再確認")]
        private static void Refresh()
        {
            // The main update loop resets the cache after the worker finishes.
            refreshRequested = true;
        }

        [MenuItem("Tools/Git表示/Gitの実行ファイルを指定…")]
        private static void SelectGit()
        {
            string path = EditorUtility.OpenFilePanel("Gitの実行ファイルを選択", "", Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
            if (string.IsNullOrEmpty(path)) return;
            EditorPrefs.SetString(GitPreference, path);
            Refresh();
        }

        [MenuItem("Tools/Git表示/Gitの自動検出に戻す")]
        private static void ResetGit()
        {
            EditorPrefs.DeleteKey(GitPreference);
            Refresh();
        }

        private static void Stop() => Lifetime.Cancel();

        private static Snapshot ReadStatus(string configured, CancellationToken token)
        {
            var state = new Snapshot { Branch = "不明", Warning = true };
            try
            {
                if (executable == null) executable = FindGit(configured, token);
                if (executable == null)
                {
                    state.Status = "Gitが見つかりません";
                    state.Detail = "Tools > Git表示 > Gitの実行ファイルを指定… から設定できます。";
                    return state;
                }
                var repository = Run(executable, "rev-parse --show-toplevel", token);
                if (repository.Code != 0)
                {
                    state.Status = "Gitリポジトリを確認できません";
                    return state;
                }
                var branch = Run(executable, "symbolic-ref --quiet --short HEAD", token);
                state.Branch = branch.Code == 0 ? branch.Output : "detached HEAD: " + Run(executable, "rev-parse --short HEAD", token).Output;
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    remoteChecked = DateTime.MinValue;
                    state.Status = "ネットワークがありません。";
                    return state;
                }
                if ((DateTime.UtcNow - remoteChecked).TotalSeconds >= 60)
                {
                    remoteHash = null;
                    remoteError = null;
                    // Query the server directly; do not trust stale remote-tracking refs.
                    var remote = Run(executable, "ls-remote --exit-code origin refs/heads/develop", token);
                    remoteChecked = DateTime.UtcNow;
                    if (remote.Code == 0 && !string.IsNullOrWhiteSpace(remote.Output))
                        remoteHash = remote.Output.Split(new[] { '\t', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    else if (remote.Code == 2) remoteError = "originにdevelopがありません";
                    else if (IsNetworkError(remote.Error) || remote.Code == -2)
                        remoteError = "ネットワークがありません。";
                    else remoteError = "リモートを確認できません（認証・接続設定）";
                }
                state.Detail = "比較先: origin/develop\n確認: " + remoteChecked.ToLocalTime().ToString("HH:mm:ss");
                if (remoteError != null || remoteHash == null)
                {
                    state.Status = remoteError ?? "develop: 確認できません";
                    return state;
                }
                var head = Run(executable, "rev-parse --verify HEAD", token);
                if (head.Code != 0)
                {
                    state.Status = "develop: 比較できません（HEADなし）";
                    return state;
                }
                if (Run(executable, "cat-file -e " + remoteHash + "^{commit}", token).Code != 0)
                {
                    // Obtain ancestry without changing branches, remote-tracking refs, or FETCH_HEAD.
                    var fetch = Run(executable, "fetch --no-tags --no-write-fetch-head --refmap= origin " + remoteHash, token);
                    if (fetch.Code != 0)
                    {
                        state.Status = IsNetworkError(fetch.Error) || fetch.Code == -2
                            ? "ネットワークがありません。" : "develop: 履歴を確認できません";
                        return state;
                    }
                }
                var ancestor = Run(executable, "merge-base --is-ancestor " + remoteHash + " " + head.Output, token);
                if (ancestor.Code == 0)
                {
                    state.Status = "develop: 最新（取り込み済み）";
                    state.Warning = false;
                    state.Latest = true;
                }
                else if (ancestor.Code == 1)
                {
                    if (Run(executable, "rev-parse --is-shallow-repository", token).Output == "true")
                        state.Status = "develop: 判定できません（履歴不足）";
                    else
                    {
                        var missing = Run(executable, "rev-list --count " + head.Output + ".." + remoteHash, token);
                        state.Status = missing.Code == 0 && int.TryParse(missing.Output, out int count)
                            ? "develop: 未取り込み " + count + "件（最新ではありません）"
                            : "develop: 未取り込みあり（最新ではありません）";
                    }
                }
                else state.Status = "develop: 比較できません";
                return state;
            }
            catch (OperationCanceledException) { return state; }
            catch (Exception)
            {
                state.Status = "Git: 確認できません";
                state.Detail = "Gitの実行ファイルとリポジトリへのアクセスを確認してください。";
                return state;
            }
        }

        private static bool IsNetworkError(string error)
        {
            string message = error.ToLowerInvariant();
            return message.Contains("could not resolve") || message.Contains("couldn't resolve") ||
                message.Contains("failed to connect") || message.Contains("unable to connect") ||
                message.Contains("network is unreachable") || message.Contains("network is down") ||
                message.Contains("connection timed out") || message.Contains("connection reset") ||
                message.Contains("no route to host") || message.Contains("could not connect");
        }

        private static string FindGit(string configured, CancellationToken token)
        {
            if (!string.IsNullOrEmpty(configured))
                return Run(configured, "--version", token).Code == 0 ? configured : null;
            var candidates = new List<string> { "git" };
            foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
            {
                if (string.IsNullOrEmpty(root)) continue;
                candidates.Add(Path.Combine(root, "Git", "cmd", "git.exe"));
                candidates.Add(Path.Combine(root, "Programs", "Git", "cmd", "git.exe"));
            }
            candidates.Add("/usr/bin/git");
            candidates.Add("/usr/local/bin/git");
            candidates.Add("/opt/homebrew/bin/git");
            foreach (string candidate in candidates)
            {
                token.ThrowIfCancellationRequested();
                if (candidate != "git" && !File.Exists(candidate)) continue;
                if (Run(candidate, "--version", token).Code == 0) return candidate;
            }
            return null;
        }

        private static CommandResult Run(string git, string arguments, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var start = new ProcessStartInfo
            {
                FileName = git, Arguments = arguments, WorkingDirectory = ProjectRoot,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            start.EnvironmentVariables["GCM_INTERACTIVE"] = "Never";
            start.EnvironmentVariables["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes -o ConnectTimeout=10 -o StrictHostKeyChecking=yes";
            start.EnvironmentVariables["LC_ALL"] = "C";
            try
            {
                using (var process = Process.Start(start))
                {
                    if (process == null) return new CommandResult(-1, "", "Git could not start");
                    var output = process.StandardOutput.ReadToEndAsync();
                    var error = process.StandardError.ReadToEndAsync();
                    var timer = Stopwatch.StartNew();
                    while (!process.WaitForExit(100))
                    {
                        if (!token.IsCancellationRequested && timer.ElapsedMilliseconds < 15000) continue;
                        try { process.Kill(); } catch (InvalidOperationException) { }
                        token.ThrowIfCancellationRequested();
                        return new CommandResult(-2, "", "Connection timed out");
                    }
                    // A spawned helper may still own a redirected pipe; bound that wait too.
                    if (!Task.WaitAll(new Task[] { output, error }, 1000))
                        return new CommandResult(-2, "", "Connection timed out");
                    return new CommandResult(process.ExitCode, output.Result.Trim(), error.Result.Trim());
                }
            }
            catch (System.ComponentModel.Win32Exception) { return new CommandResult(-1, "", "Git could not start"); }
        }

        private sealed class Snapshot
        {
            public string Branch;
            public string Status;
            public string Detail;
            public bool Warning;
            public bool Latest;
        }

        private struct CommandResult
        {
            public readonly int Code;
            public readonly string Output;
            public readonly string Error;
            public CommandResult(int code, string output, string error) { Code = code; Output = output; Error = error; }
        }
    }
}
