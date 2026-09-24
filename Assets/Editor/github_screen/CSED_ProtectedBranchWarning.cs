/* ================================================
 * 作業禁止ブランチでの編集をチェックボックスで許可するクラス
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-20 | 初回作成
 * 2026-09-23 | 警告ダイアログを作業許可チェックへ変更
 * ================================================ */
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2027.EditorTools
{
    [InitializeOnLoad]
    internal static class CSED_ProtectedBranchWarning
    {
        private const string ConfigAsset = "Assets/Editor/github_screen/ProtectedBranches.json";
        private const string LastBranchKey = "MS2027.ProtectedBranch.LastBranch";
        private const string AllowedKey = "MS2027.ProtectedBranch.WorkAllowed";
        private const string ConsentName = "ms2027-branch-work-consent";
        private const string BlockerName = "ms2027-branch-input-blocker";
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly HashSet<VisualElement> Roots = new HashSet<VisualElement>();
        private static readonly HashSet<VisualElement> ContentRoots = new HashSet<VisualElement>();
        private static readonly Dictionary<VisualElement, VisualElement> Blockers = new Dictionary<VisualElement, VisualElement>();
        private static readonly HashSet<VisualElement> PlayInspectionPanels = new HashSet<VisualElement>();
        private static readonly Dictionary<VisualElement, EditorWindow> ConsolePanels = new Dictionary<VisualElement, EditorWindow>();
        private static readonly HashSet<VisualElement> ConsoleInputRoots = new HashSet<VisualElement>();
        private static readonly HashSet<string> ProtectedBranches = new HashSet<string>(StringComparer.Ordinal);
        private static string configContents;
        private static double nextPoll;
        private static bool warningPending;

        /// <summary>
        /// JSONから読み込む作業禁止ブランチの完全一致リスト。
        /// </summary>
        [Serializable]
        private sealed class Settings
        {
            public string[] protectedBranches = Array.Empty<string>();
        }

        /// <summary>
        /// 編集入力と再生状態を監視する。許可はSessionStateに保存し、再コンパイルでは解除しない。
        /// </summary>
        static CSED_ProtectedBranchWarning()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting += Cleanup;
        }

        /// <summary>
        /// 現在のブランチと設定を確認し、未許可の禁止ブランチであればtrueを返す。
        /// </summary>
        internal static bool IsLocked
        {
            get
            {
                ReloadSettings();
                string branch = ObserveBranch();
                return ProtectedBranches.Contains(branch) && !SessionState.GetBool(AllowedKey, false);
            }
        }

        /// <summary>
        /// 設定とブランチを監視し、新しく開いたEditorウィンドウにも入力制限を登録する。
        /// </summary>
        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.25;
            ReloadSettings();
            ObserveBranch();
            bool locked = IsLocked;
            PlayInspectionPanels.Clear();
            ConsolePanels.Clear();
            var workPanels = new Dictionary<VisualElement, VisualElement>();
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var root = window.rootVisualElement;
                if (Roots.Add(root)) RegisterInput(root, true);
                // IMGUIも覆うためパネルに配置するが、タブを除いたコンテンツ領域だけを制限する。
                var panelRoot = root.panel?.visualTree;
                if (panelRoot == null) continue;
                if (window.GetType().FullName == "UnityEditor.ConsoleWindow")
                {
                    ConsolePanels[panelRoot] = window;
                    // ConsoleのIMGUIへ届く前に、ログ項目のクリックだけを監視する。
                    if (ConsoleInputRoots.Add(panelRoot)) RegisterConsoleInput(panelRoot, true);
                }
                if (CanInspectDuringPlay(window.GetType())) PlayInspectionPanels.Add(panelRoot);
                if (window.GetType().Name == "MainToolbarWindow") continue;
                var contentRoot = root;
                while (contentRoot.parent != null && contentRoot.parent != panelRoot)
                    contentRoot = contentRoot.parent;
                if (ContentRoots.Add(contentRoot))
                    contentRoot.RegisterCallback<GeometryChangedEvent>(OnContentGeometryChanged);
                workPanels[panelRoot] = contentRoot;
            }
            foreach (var panel in workPanels)
                UpdateBlocker(panel.Key, panel.Value,
                    locked && !ConsolePanels.ContainsKey(panel.Key) &&
                    !(EditorApplication.isPlaying && PlayInspectionPanels.Contains(panel.Key)));
        }

        /// <summary>
        /// 再生中の操作を許可する標準Hierarchy・Inspector・Game・Sceneを判定する。
        /// </summary>
        private static bool CanInspectDuringPlay(Type type)
        {
            for (; type != null; type = type.BaseType)
                if (type.FullName == "UnityEditor.SceneHierarchyWindow" ||
                    type.FullName == "UnityEditor.InspectorWindow" ||
                    type.FullName == "UnityEditor.GameView" ||
                    type.FullName == "UnityEditor.SceneView")
                    return true;
            return false;
        }

        /// <summary>
        /// タブを除くウィンドウの最前面でクリックを受け取り、IMGUIを含む背後への操作を遮断する。
        /// </summary>
        private static void UpdateBlocker(VisualElement panelRoot, VisualElement contentRoot, bool locked)
        {
            // 許可チェックを含むパネルは覆わず、従来の入力監視でチェック以外を制限する。
            if (panelRoot.Q<Toggle>(ConsentName) != null) locked = false;
            if (!Blockers.TryGetValue(panelRoot, out var blocker))
            {
                if (!locked) return;
                blocker = new VisualElement { name = BlockerName, pickingMode = PickingMode.Position, focusable = true };
                blocker.StretchToParentSize();
                blocker.style.backgroundColor = Color.clear;
                RegisterInput(blocker, true);
                panelRoot.Add(blocker);
                Blockers.Add(panelRoot, blocker);
            }
            UpdateBlockerTop(blocker, contentRoot);
            blocker.style.display = locked ? DisplayStyle.Flex : DisplayStyle.None;
            if (locked)
            {
                blocker.BringToFront();
                // 入力欄が以前からフォーカス中でも、キー操作が裏側へ届かないようにする。
                var focused = panelRoot.panel?.focusController?.focusedElement as VisualElement;
                if (focused != null && focused != blocker && panelRoot.Contains(focused)) blocker.Focus();
            }
        }

        /// <summary>
        /// Unityがコンテンツに設定した上余白を使い、タブ列を入力遮断の範囲から外す。
        /// </summary>
        private static void UpdateBlockerTop(VisualElement blocker, VisualElement contentRoot)
        {
            // 固定ピクセル数にせず、フローティング・最大化時のタブ高さにも追従する。
            float top = contentRoot.resolvedStyle.top;
            blocker.style.top = float.IsNaN(top) ? 0f : Mathf.Max(0f, top);
        }

        /// <summary>
        /// タブの移動やレイアウト変更に合わせ、入力を遮断する領域を更新する。
        /// </summary>
        private static void OnContentGeometryChanged(GeometryChangedEvent evt)
        {
            var contentRoot = evt.target as VisualElement;
            var panelRoot = contentRoot?.panel?.visualTree;
            if (panelRoot != null && Blockers.TryGetValue(panelRoot, out var blocker))
                UpdateBlockerTop(blocker, contentRoot);
            nextPoll = 0;
        }

        /// <summary>
        /// ブランチ名の横へチェックを追加する。禁止対象以外では非表示にする。
        /// </summary>
        internal static void UpdateConsent(VisualElement branchRoot)
        {
            ReloadSettings();
            string branch = ObserveBranch();
            var text = branchRoot.Q<TextElement>("EditorToolbarButtonText");
            if (text == null) return;
            var toggle = branchRoot.Q<Toggle>(ConsentName);
            if (toggle == null)
            {
                toggle = new Toggle("作業を許可") { name = ConsentName };
                // ブランチ名側に確保済みの余白を使い、左余白を二重に加えない。
                toggle.style.marginLeft = 0;
                toggle.style.flexShrink = 0;
                toggle.style.alignSelf = Align.Center;
                toggle.tooltip = "禁止ブランチでの作業を許可";
                // Inspector向けのラベル幅を解除し、文字とチェックの間隔だけを確保する。
                toggle.labelElement.style.minWidth = 0;
                toggle.labelElement.style.width = StyleKeyword.Auto;
                toggle.labelElement.style.flexBasis = StyleKeyword.Auto;
                toggle.labelElement.style.flexGrow = 0;
                toggle.labelElement.style.marginRight = 4;
                var input = toggle.Q(className: Toggle.inputUssClassName);
                if (input != null)
                {
                    input.style.flexGrow = 0;
                    input.style.marginLeft = 0;
                }
                // コールバックに表示時点のブランチを捕捉せず、変更直前のHEADを確認する。
                toggle.RegisterValueChangedCallback(evt =>
                {
                    string displayedBranch = toggle.userData as string;
                    string activeBranch = ObserveBranch();
                    if (displayedBranch == activeBranch && ProtectedBranches.Contains(activeBranch))
                        SessionState.SetBool(AllowedKey, evt.newValue);
                    toggle.SetValueWithoutNotify(SessionState.GetBool(AllowedKey, false));
                    UpdateConsentColor(toggle);
                });
                text.parent.Add(toggle);
            }
            toggle.userData = branch;
            toggle.style.display = ProtectedBranches.Contains(branch) ? DisplayStyle.Flex : DisplayStyle.None;
            toggle.SetValueWithoutNotify(SessionState.GetBool(AllowedKey, false));
            UpdateConsentColor(toggle);
        }

        /// <summary>
        /// 許可中だけ青色で表示する。未チェック時はUnity標準の色へ戻す。
        /// </summary>
        private static void UpdateConsentColor(Toggle toggle)
        {
            var color = toggle.value ? new StyleColor(new Color(0.3f, 0.65f, 1f)) : new StyleColor(StyleKeyword.Null);
            toggle.style.color = color;
            toggle.labelElement.style.color = color;
        }

        /// <summary>
        /// JSONを変更時だけ解析する。解析失敗時は直前の設定を保持する。
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
                // 読み込み失敗時は直前の設定を維持し、コンソールには出力しない。
            }
        }

        /// <summary>
        /// ブランチを切り替えるたびに許可を解除する。再起動でもSessionState自体がリセットされる。
        /// </summary>
        private static string ObserveBranch()
        {
            string branch = ReadBranch(ProjectRoot);
            // HEAD書き換え中などの一時的な読み取り失敗では、以前のロック状態を維持する。
            if (branch == null) return SessionState.GetString(LastBranchKey, "");
            if (SessionState.GetString(LastBranchKey, "") != branch)
            {
                SessionState.SetString(LastBranchKey, branch);
                SessionState.SetBool(AllowedKey, false);
            }
            return branch;
        }

        /// <summary>
        /// 許可チェックと再生ボタン類、再生中のHierarchy・Inspector・Game・Scene以外は未許可時に入力を止める。
        /// </summary>
        private static void BlockInput(EventBase evt)
        {
            if (!IsLocked) return;
            var eventTarget = evt.target as VisualElement;
            var panelRoot = eventTarget?.panel?.visualTree;
            if (panelRoot != null && ConsolePanels.TryGetValue(panelRoot, out var console))
            {
                if (!IsConsoleLogInput(evt, console)) return;
            }
            else if (EditorApplication.isPlaying && eventTarget?.panel != null &&
                PlayInspectionPanels.Contains(eventTarget.panel.visualTree)) return;
            for (var target = evt.target as VisualElement; target != null; target = target.parent)
                // Unity 6.3標準の再生・停止・一時停止・ステップを含むオーバーレイ。
                if (target.name == ConsentName || target.name == "PlayMode") return;
            evt.StopImmediatePropagation();
#pragma warning disable CS0618
            evt.PreventDefault();
#pragma warning restore CS0618
            // 押下時だけ案内する。移動・キー入力・ボタンを離す操作では表示しない。
            if (evt is PointerDownEvent || evt is MouseDownEvent) QueueWarning();
        }

        /// <summary>
        /// Consoleの検索・絞り込み・スクロールを許可し、ログ行へのクリックだけを制限する。
        /// </summary>
        private static bool IsConsoleLogInput(EventBase evt, EditorWindow console)
        {
            // 検索欄への文字入力は許可するが、選択済みログをEnterで開く操作は止める。
            if (evt is KeyDownEvent key)
                return key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter;
            Vector2 position;
            if (evt is PointerDownEvent pointerDown) position = pointerDown.position;
            else if (evt is PointerUpEvent pointerUp) position = pointerUp.position;
            else if (evt is MouseDownEvent mouseDown) position = mouseDown.mousePosition;
            else if (evt is MouseUpEvent mouseUp) position = mouseUp.mousePosition;
            else return false;

            // Unity標準Consoleの一覧状態を参照し、空白やスクロールバーをログ行と誤判定しない。
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = console.GetType();
            var list = type.GetField("m_ListView", flags)?.GetValue(console);
            if (list == null) return false;
            var listType = list.GetType();
            int rows = (int)(listType.GetField("totalRows", flags)?.GetValue(list) ?? 0);
            int rowHeight = (int)(listType.GetField("rowHeight", flags)?.GetValue(list) ?? 0);
            var scroll = (Vector2)(listType.GetField("scrollPos", flags)?.GetValue(list) ?? Vector2.zero);
            int listHeight = (int)(type.GetField("ms_LVHeight", flags)?.GetValue(console) ?? 0);
            var root = console.rootVisualElement;
            Vector2 local = root.WorldToLocal(position);
            float toolbarHeight = EditorStyles.toolbar.fixedHeight;
            float y = local.y - toolbarHeight;
            float scrollbarWidth = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).verticalScrollbar.fixedWidth;
            if (scrollbarWidth <= 0f) scrollbarWidth = 16f;
            float width = root.resolvedStyle.width - (rows * rowHeight > listHeight ? scrollbarWidth : 0f);
            return local.x >= 0f && local.x < width && y >= 0f && y < listHeight &&
                rowHeight > 0 && y + scroll.y < rows * rowHeight;
        }

        /// <summary>
        /// Consoleと同じ位置に別のタブが表示されても、タブ切り替え自体は制限しない。
        /// </summary>
        private static void OnConsoleInput<T>(T evt) where T : EventBase<T>, new()
        {
            var root = (evt.currentTarget as VisualElement)?.panel?.visualTree;
            if (root != null && ConsolePanels.ContainsKey(root)) BlockInput(evt);
        }

        /// <summary>
        /// Console用のクリック・キー監視を登録または解除する。
        /// </summary>
        private static void RegisterConsoleInput(VisualElement root, bool add)
        {
            if (add)
            {
                root.RegisterCallback<PointerDownEvent>(OnConsoleInput, TrickleDown.TrickleDown);
                root.RegisterCallback<PointerUpEvent>(OnConsoleInput, TrickleDown.TrickleDown);
                root.RegisterCallback<MouseDownEvent>(OnConsoleInput, TrickleDown.TrickleDown);
                root.RegisterCallback<MouseUpEvent>(OnConsoleInput, TrickleDown.TrickleDown);
                root.RegisterCallback<KeyDownEvent>(OnConsoleInput, TrickleDown.TrickleDown);
            }
            else
            {
                root.UnregisterCallback<PointerDownEvent>(OnConsoleInput, TrickleDown.TrickleDown);
                root.UnregisterCallback<PointerUpEvent>(OnConsoleInput, TrickleDown.TrickleDown);
                root.UnregisterCallback<MouseDownEvent>(OnConsoleInput, TrickleDown.TrickleDown);
                root.UnregisterCallback<MouseUpEvent>(OnConsoleInput, TrickleDown.TrickleDown);
                root.UnregisterCallback<KeyDownEvent>(OnConsoleInput, TrickleDown.TrickleDown);
            }
        }

        /// <summary>
        /// クリックの処理終了後に警告を予約し、同じクリックによる重複表示を防ぐ。
        /// </summary>
        private static void QueueWarning()
        {
            if (warningPending) return;
            warningPending = true;
            EditorApplication.delayCall += ShowWarning;
        }

        /// <summary>
        /// 未許可なら警告を表示する。閉じても許可せず、次のクリックでも再び案内する。
        /// </summary>
        private static void ShowWarning()
        {
            try
            {
                if (IsLocked)
                    EditorUtility.DisplayDialog("作業禁止ブランチ",
                        "作業禁止ブランチです。作業許可にチェックを入れてください", "OK");
            }
            finally
            {
                warningPending = false;
            }
        }

        /// <summary>
        /// マウス・キー・ドラッグ・編集コマンドを、各コントロールへ届く前に監視する。
        /// </summary>
        private static void RegisterInput(VisualElement root, bool add)
        {
            Bind<PointerDownEvent>(root, add);
            Bind<PointerUpEvent>(root, add);
            Bind<PointerMoveEvent>(root, add);
            Bind<MouseDownEvent>(root, add);
            Bind<MouseUpEvent>(root, add);
            Bind<KeyDownEvent>(root, add);
            Bind<KeyUpEvent>(root, add);
            Bind<WheelEvent>(root, add);
            Bind<DragUpdatedEvent>(root, add);
            Bind<DragPerformEvent>(root, add);
            Bind<ExecuteCommandEvent>(root, add);
            Bind<ValidateCommandEvent>(root, add);
            Bind<ContextualMenuPopulateEvent>(root, add);
        }

        /// <summary>
        /// 入力イベントの登録・解除を対称に行い、再コンパイル後の重複監視を防ぐ。
        /// </summary>
        private static void Bind<T>(VisualElement root, bool add) where T : EventBase<T>, new()
        {
            if (add) root.RegisterCallback<T>(OnInput, TrickleDown.TrickleDown);
            else root.UnregisterCallback<T>(OnInput, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// 各入力イベントを共通の許可判定へ渡す。
        /// </summary>
        private static void OnInput<T>(T evt) where T : EventBase<T>, new() => BlockInput(evt);

        /// <summary>
        /// 再生・停止の切り替え直後に、Hierarchy・Inspector・Game・Sceneの操作制限を更新する。
        /// </summary>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            nextPoll = 0;
            Update();
        }

        /// <summary>
        /// 通常リポジトリとworktreeのHEADから現在のブランチ名を読み取る。
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
        /// イベントを解除する。許可状態は再コンパイル後にも引き継ぐため消去しない。
        /// </summary>
        private static void Cleanup()
        {
            EditorApplication.update -= Update;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall -= ShowWarning;
            warningPending = false;
            foreach (var blocker in Blockers.Values)
            {
                RegisterInput(blocker, false);
                blocker.RemoveFromHierarchy();
            }
            Blockers.Clear();
            PlayInspectionPanels.Clear();
            foreach (var root in ConsoleInputRoots) RegisterConsoleInput(root, false);
            ConsoleInputRoots.Clear();
            ConsolePanels.Clear();
            foreach (var root in ContentRoots)
                root.UnregisterCallback<GeometryChangedEvent>(OnContentGeometryChanged);
            ContentRoots.Clear();
            foreach (var root in Roots) RegisterInput(root, false);
            Roots.Clear();
        }
    }

    /// <summary>
    /// 未許可の禁止ブランチでは、Unityからのアセット・シーン保存も止める。
    /// </summary>
    internal sealed class CSED_ProtectedBranchSaveGuard : AssetModificationProcessor
    {
        /// <summary>
        /// ロック中は保存対象を返さない。外部エディターやGitからのファイル変更は対象外。
        /// </summary>
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (!CSED_ProtectedBranchWarning.IsLocked) return paths;
            return Array.Empty<string>();
        }
    }
}
