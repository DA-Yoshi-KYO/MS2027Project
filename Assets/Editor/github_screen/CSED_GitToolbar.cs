/* ================================================
 * Unity上部にブランチ名とdevelopの最新状況を表示するクラス
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-20 | 初回作成
 * 2026-09-20 | ファイル名に合わせてクラス名をCSED_GitToolbarに変更
 *            | 日本語コメントを追加
 * ================================================ */
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
    internal static class CSED_GitToolbar
    {
        // 登録先と各PCのGit設定。既存設定を引き継ぐため保存キー名は維持する。
        private const string ToolbarPath = "Git/ブランチとdevelop";
        private const string StatusPath = "Git/developの最新状況";
        private const string GitPreference = "MS2027.GitToolbar.Executable";
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly CancellationTokenSource Lifetime = new CancellationTokenSource();
        // バックグラウンドの確認結果をUpdateで受け取り、画面へ反映する。
        private static Task<Snapshot> pending;
        private static Snapshot current = new Snapshot { Branch = "確認中…", Status = "develop: 確認中…" };
        private static double nextCheck;
        private static bool refreshRequested;
        private static string executable;
        // 通信は60秒間隔。前回確認したリモートのコミットと通信結果を保持する。
        private static string remoteHash;
        private static string remoteError;
        private static DateTime remoteChecked;
        private static readonly string VisibilityPreference = "MS2027.GitToolbar.VisibleOnce.v2." + ProjectRoot;
        private static bool visibilityInitialized = EditorPrefs.GetBool(VisibilityPreference, false);
        private static int visibilityAttempts;
        private static double nextVisibilityCheck;
        private static double nextLayoutCheck;

        /// <summary>
        /// Unity起動・再コンパイル時に更新処理と終了処理を登録する。
        /// </summary>
        static CSED_GitToolbar()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        /// <summary>
        /// 現在のブランチ名を左側のツールバー項目として生成する。
        /// </summary>
        [MainToolbarElement(ToolbarPath, defaultDockPosition = MainToolbarDockPosition.Left, defaultDockIndex = 100)]
        private static MainToolbarElement CreateToolbar()
        {
            return CreateLabel("Git: " + Escape(current.Branch));
        }

        /// <summary>
        /// developの取り込み状況を右側のツールバー項目として生成する。
        /// </summary>
        [MainToolbarElement(StatusPath, defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 0)]
        private static MainToolbarElement CreateStatus()
        {
            return CreateLabel(StatusText());
        }

        /// <summary>
        /// 最新は緑、未取り込み・確認できない状態は黄色の表示文字列にする。
        /// </summary>
        private static string StatusText()
        {
            string status = Escape(current.Status);
            if (current.Warning) status = "<color=#FFD54F>" + status + "</color>";
            else if (current.Latest) status = "<color=#81C784>" + status + "</color>";
            return status;
        }

        /// <summary>
        /// 表示ラベルに簡略ツールチップとGit専用メニューを設定する。
        /// </summary>
        private static MainToolbarElement CreateLabel(string text)
        {
            var label = new MainToolbarLabel(new MainToolbarContent(text, current.Detail ?? current.Status));
            label.populateContextMenu = PopulateGitMenu;
            return label;
        }

        /// <summary>
        /// 再確認とGit実行ファイルの設定だけをメニューへ追加する。
        /// </summary>
        private static void PopulateGitMenu(DropdownMenu menu)
        {
            menu.AppendAction("今すぐ再確認", _ => Refresh());
            menu.AppendAction("Gitの実行ファイルを指定…", _ => SelectGit());
            menu.AppendAction("Gitの自動検出に戻す", _ => ResetGit());
        }

        /// <summary>
        /// Unity標準のHide項目を追加させず、Git専用メニューへ置き換える。
        /// </summary>
        private static void OnGitContextMenu(ContextualMenuPopulateEvent evt)
        {
            // UnityのオーバーレイがHideを追加する前に、このメニューの処理を完了する。
            evt.menu.MenuItems().Clear();
            PopulateGitMenu(evt.menu);
            evt.StopImmediatePropagation();
        }

        /// <summary>
        /// ブランチ名の記号がリッチテキストのタグとして解釈されるのを防ぐ。
        /// </summary>
        private static string Escape(string text) => (text ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        /// <summary>
        /// 表示調整、確認結果の反映、約5秒ごとの非同期Git確認を進める。
        /// </summary>
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
                // 要素と計測済みの幅を維持する。作り直すと一瞬だけ既定の幅に戻り、
                // 隣のツール類が左右に揺れるため、文字だけを更新する。
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

        /// <summary>
        /// 文字の省略を解除し、左右の文字幅に左右されず再生ボタン類を中央に置く。
        /// </summary>
        private static void ApplyToolbarLayout()
        {
            // Unity 6.3の内部APIでGit表示だけを取得し、ラベルの既定幅制限を解除する。
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

            // 左右の文字量が変わっても、再生ボタン類の位置を画面中央に保つ。
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
                // ドラッグ領域や余白を除外し、見えているボタン群の中心で位置を計算する。
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

        /// <summary>
        /// 既存ラベルの文字と必要幅を更新し、縦位置・ツールチップ・メニューを整える。
        /// </summary>
        private static void StyleLabel(VisualElement root, string text)
        {
            root.UnregisterCallback<ContextualMenuPopulateEvent>(OnGitContextMenu, TrickleDown.TrickleDown);
            root.RegisterCallback<ContextualMenuPopulateEvent>(OnGitContextMenu, TrickleDown.TrickleDown);
            // Git表示だけ、Unityが付加するドラッグ操作の説明を簡略ツールチップに置き換える。
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
                // Unity標準の文字サイズと高さを使い、隣のツールと文字の縦位置を揃える。
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

        /// <summary>
        /// 初回だけGit表示を有効にする。Unity内部APIが変わった場合は処理を中止する。
        /// </summary>
        private static void EnsureInitialVisibility()
        {
            if (visibilityInitialized || visibilityAttempts >= 30 ||
                EditorApplication.timeSinceStartup < nextVisibilityCheck) return;
            nextVisibilityCheck = EditorApplication.timeSinceStartup + 1;
            visibilityAttempts++;
            // Unity 6.3には登録済みツールバーを初回表示する公開APIがないため内部APIを使う。
            // 自作項目だけを一度有効にし、以降はユーザーのレイアウト設定を維持する。
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
                // 内部APIが変更された場合も登録は残るため、ツールバーのメニューから表示できる。
                visibilityInitialized = true;
            }
        }

        /// <summary>
        /// 進行中の確認が完了した後で、キャッシュを破棄して再確認するよう予約する。
        /// </summary>
        [MenuItem("Tools/Git表示/今すぐ再確認")]
        private static void Refresh()
        {
            // 実行中の処理と競合しないよう、キャッシュの破棄はUpdate側で行う。
            refreshRequested = true;
        }

        /// <summary>
        /// 選択したGit実行ファイルを、このPCのEditorPrefsに保存する。
        /// </summary>
        [MenuItem("Tools/Git表示/Gitの実行ファイルを指定…")]
        private static void SelectGit()
        {
            string path = EditorUtility.OpenFilePanel("Gitの実行ファイルを選択", "", Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
            if (string.IsNullOrEmpty(path)) return;
            EditorPrefs.SetString(GitPreference, path);
            Refresh();
        }

        /// <summary>
        /// Git実行ファイルの手動指定を解除して、自動検出へ戻す。
        /// </summary>
        [MenuItem("Tools/Git表示/Gitの自動検出に戻す")]
        private static void ResetGit()
        {
            EditorPrefs.DeleteKey(GitPreference);
            Refresh();
        }

        /// <summary>
        /// 終了・再コンパイル時に、実行中のGit確認へキャンセルを通知する。
        /// </summary>
        private static void Stop() => Lifetime.Cancel();

        /// <summary>
        /// 作業ブランチがリモートdevelopの履歴を含むか確認する。バックグラウンド専用でUnityのUIには触れない。
        /// </summary>
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
                    // 古いリモート追跡参照ではなく、サーバーに直接問い合わせてdevelopを確認する。
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
                    // 履歴オブジェクトだけを取得し、ブランチ・リモート追跡参照・FETCH_HEADは変更しない。
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

        /// <summary>
        /// Gitのエラー文から通信失敗を判定し、認証・設定の失敗と区別する。
        /// </summary>
        private static bool IsNetworkError(string error)
        {
            string message = error.ToLowerInvariant();
            return message.Contains("could not resolve") || message.Contains("couldn't resolve") ||
                message.Contains("failed to connect") || message.Contains("unable to connect") ||
                message.Contains("network is unreachable") || message.Contains("network is down") ||
                message.Contains("connection timed out") || message.Contains("connection reset") ||
                message.Contains("no route to host") || message.Contains("could not connect");
        }

        /// <summary>
        /// 手動指定を優先し、未指定ならPATHと一般的なインストール先からGitを探す。
        /// </summary>
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

        /// <summary>
        /// Gitを非表示で実行して出力を回収する。約15秒のタイムアウトとキャンセルに対応する。
        /// </summary>
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
                    // 子プロセスが出力先を保持している場合も、読み取り完了を無期限には待たない。
                    if (!Task.WaitAll(new Task[] { output, error }, 1000))
                        return new CommandResult(-2, "", "Connection timed out");
                    return new CommandResult(process.ExitCode, output.Result.Trim(), error.Result.Trim());
                }
            }
            catch (System.ComponentModel.Win32Exception) { return new CommandResult(-1, "", "Git could not start"); }
        }

        /// <summary>
        /// 画面へ渡すブランチ名、状態、ツールチップ、表示色の判定結果。
        /// </summary>
        private sealed class Snapshot
        {
            public string Branch;
            public string Status;
            public string Detail;
            public bool Warning;
            public bool Latest;
        }

        /// <summary>
        /// Gitの終了コードと標準出力・標準エラー。負のコードは起動失敗やタイムアウトを表す。
        /// </summary>
        private struct CommandResult
        {
            public readonly int Code;
            public readonly string Output;
            public readonly string Error;
            public CommandResult(int code, string output, string error) { Code = code; Output = output; Error = error; }
        }
    }
}
