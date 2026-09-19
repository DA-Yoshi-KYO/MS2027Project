/* ================================================
 * JSONで指定した作業禁止ブランチへの切り替えを警告するクラス
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-20 | 初回作成
 * 2026-09-20 | ファイル名に合わせてクラス名をCSED_ProtectedBranchWarningに変更
 *            | 日本語コメントを追加
 * ================================================ */
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2027.EditorTools
{
    [InitializeOnLoad]
    internal static class CSED_ProtectedBranchWarning
    {
        // JSONは共有設定、SessionStateは今回のUnity起動中だけ保持する警告履歴。
        private const string ConfigAsset = "Assets/Editor/github_screen/ProtectedBranches.json";
        private const string LastBranchKey = "MS2027.ProtectedBranch.LastBranch";
        private const string WarnedKey = "MS2027.ProtectedBranch.Warned";
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        // ウィンドウへのイベント重複登録を防ぎ、ブランチ名は大文字・小文字を区別する。
        private static readonly HashSet<VisualElement> Roots = new HashSet<VisualElement>();
        private static readonly HashSet<string> ProtectedBranches = new HashSet<string>(StringComparer.Ordinal);
        private static string configContents;
        private static string queuedBranch;
        private static double nextPoll;

        /// <summary>
        /// ProtectedBranches.jsonのprotectedBranches配列を読み込むための型。
        /// </summary>
        [Serializable]
        private sealed class Settings
        {
            public string[] protectedBranches = Array.Empty<string>();
        }

        /// <summary>
        /// 警告監視を開始し、再コンパイル・終了時の解除処理を登録する。
        /// </summary>
        static CSED_ProtectedBranchWarning()
        {
            EditorApplication.update += Update;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting += Cleanup;
        }

        /// <summary>
        /// CSの再コンパイル完了時に警告済み状態を解除し、次のクリックで再確認する。
        /// </summary>
        private static void OnCompilationFinished(object context)
        {
            // SessionStateへ保存することで、この直後のアセンブリ再読み込みにも引き継ぐ。
            // 再生モードへの移行だけではコンパイルされないため、警告を増やさない。
            SessionState.SetBool(WarnedKey, false);
        }

        /// <summary>
        /// 約0.5秒ごとに設定とブランチを確認し、新しく開いたEditorウィンドウにもクリック監視を登録する。
        /// </summary>
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
                // Scene・Inspector・Project・ツールバー内の操作より先にクリックを受け取る。
                root.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            }
        }

        /// <summary>
        /// JSONの内容が変わったときだけ警告対象を更新する。空または未配置なら対象なしとする。
        /// </summary>
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

        /// <summary>
        /// ブランチ変更を検出したら警告済み状態を解除する。読み取り失敗時は前回の記録を維持する。
        /// </summary>
        private static string ObserveBranch()
        {
            string branch = ReadBranch(ProjectRoot);
            if (branch == null) return null;
            // ブランチ変更時にも解除する。再コンパイル時の解除はOnCompilationFinishedで行う。
            if (SessionState.GetString(LastBranchKey, "") != branch)
            {
                SessionState.SetString(LastBranchKey, branch);
                SessionState.SetBool(WarnedKey, false);
            }
            return branch;
        }

        /// <summary>
        /// 対象ブランチの最初のクリックを止め、イベント処理後に確認ダイアログを表示する。
        /// </summary>
        private static void OnPointerDown(PointerDownEvent evt)
        {
            if (queuedBranch != null) return;
            ReloadSettings();
            // クリック時にもHEADを読み直し、切り替え直後に古いブランチで判定しないようにする。
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

        /// <summary>
        /// 表示直前にブランチを再確認し、切り替え前の古い警告を出さないようにする。
        /// </summary>
        private static void ShowWarning()
        {
            string branch = queuedBranch;
            queuedBranch = null;
            if (branch == null || ObserveBranch() != branch || !ProtectedBranches.Contains(branch)) return;
            EditorUtility.DisplayDialog("作業ブランチの確認",
                branch + "\n\n作業禁止のブランチですがよろしいでしょうか？", "続行", "キャンセル");
            // 起動・ブランチ変更・再コンパイル後に1回。どちらを選んでも、止めたクリックは再実行しない。
        }

        /// <summary>
        /// 親フォルダーをたどってGitのHEADを読む。worktreeの.gitファイルとdetached HEADにも対応する。
        /// </summary>
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

        /// <summary>
        /// 更新・表示予約・クリック監視を解除し、再コンパイル後の重複実行を防ぐ。
        /// </summary>
        private static void Cleanup()
        {
            EditorApplication.update -= Update;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            EditorApplication.delayCall -= ShowWarning;
            foreach (var root in Roots)
                root.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            Roots.Clear();
        }
    }
}
