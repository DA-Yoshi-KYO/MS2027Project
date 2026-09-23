/* ================================================
 * ScriptSearchツールの検索実行処理
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-02-15 | 初回作成
 * 2026-09-06 | GUI構成整理・検索結果表示調整
 * 2026-09-07 | コメント・履歴・UI微調整
 * 2026-09-23 | CSED_へ改名・コメント形式を統一
 * ================================================ */

#if UNITY_EDITOR

/// <summary>
/// ScriptSearchツールの検索実行処理。
/// </summary>
public partial class CSED_ScriptSearch
{
    /// <summary>
    /// Hierarchy / Prefab の検索をまとめて実行する。
    /// SearchボタンとProject右クリック検索の両方から利用する。
    /// </summary>
    private void ExecuteSearch()
    {
        ExecuteHierarchySearch();

        ExecuteAssetsSearch();

        Repaint();
    }


    /// <summary>
    /// Searchボタン押下時の処理。
    /// </summary>
    partial void OnClickSearchButton()
    {
        ExecuteSearch();
    }
}

#endif
