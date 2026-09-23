/* ================================================
 * ScriptSearchツールのEditorWindow本体
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-02-15 | 初回作成
 * 2026-09-06 | GUI構成整理・検索結果表示調整
 * 2026-09-07 | コメント・履歴・UI微調整
 * 2026-09-23 | CSED_へ改名・コメント形式を統一
 * ================================================ */

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ScriptSearchツール本体。
/// GUIの詳細はpartial classで分割して管理する。
/// </summary>
public partial class CSED_ScriptSearch : EditorWindow
{
    /// <summary>
    /// Toolsメニューから検索ウィンドウを開き、既存ウィンドウがあれば再利用する。
    /// </summary>
    [MenuItem("Tools/ScriptSearch")]
    public static void ShowWindow()
    {
        GetOrCreateWindow();
    }


    /// <summary>
    /// ScriptSearchウィンドウを取得して表示する。
    /// </summary>
    private static CSED_ScriptSearch GetOrCreateWindow()
    {
        CSED_ScriptSearch window =
            GetWindow<CSED_ScriptSearch>("ScriptSearch");

        window.minSize = new Vector2(
            500.0f,
            400.0f
        );

        window.Show();

        window.Focus();

        return window;
    }


    /// <summary>
    /// 指定ScriptをTarget Scriptへ設定して、そのまま検索を実行する。
    /// </summary>
    public static void OpenAndSearch(
        MonoScript targetScript
    )
    {
        if (targetScript == null)
        {
            return;
        }

        CSED_ScriptSearch window =
            GetOrCreateWindow();

        window._targetScript =
            targetScript;

        window.ExecuteSearch();
    }

    /// <summary>
    /// ウィンドウ有効化時の初期化用。現在は追加の初期化処理を行わない。
    /// </summary>
    private void OnEnable()
    {
    }

    /// <summary>
    /// 検索設定・結果・ヘルプの共通レイアウトを描画する。
    /// </summary>
    private void OnGUI()
    {
        DrawMainLayout();
    }
}
#endif
