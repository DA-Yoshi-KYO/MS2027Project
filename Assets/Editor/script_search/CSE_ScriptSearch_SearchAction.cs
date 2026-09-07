/*
+=====================================
 ファイル名 : CSE_ScriptSearch_SearchAction.cs
 概要     : ScriptSearchツールの検索実行処理
 作者     : ヨシモト リョウ
 履歴     : 2026/02/15 新規作成
           2026/09/06 GUI構成整理・検索結果表示調整
           2026/09/07 コメント・履歴・UI微調整
=====================================+
*/

#if UNITY_EDITOR

public partial class CSE_ScriptSearch
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
