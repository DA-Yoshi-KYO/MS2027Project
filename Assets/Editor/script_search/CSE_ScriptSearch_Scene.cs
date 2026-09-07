/*
+=====================================
 ファイル名 : CSE_ScriptSearch_Scene.cs
 概要     : ScriptSearchツールのScene選択GUI
 作者     : ヨシモト リョウ
 履歴     : 2026/02/15 新規作成
           2026/09/06 GUI構成整理・検索結果表示調整
           2026/09/07 コメント・履歴・UI微調整
=====================================+
*/

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ScriptSearch：Scene選択UI（partial）
/// </summary>
public partial class CSE_ScriptSearch
{
    // 選択中のScene（Project上の .unity アセット）
    private SceneAsset _targetSceneAsset;

    /// <summary>
    /// Scene選択フィールドを描画する（クリックで一覧/検索して選択できる）
    /// </summary>
    private void DrawTargetSceneField()
    {
            EditorGUILayout.LabelField("検索対象Scene", EditorStyles.boldLabel);
            GUILayout.Space(2.0f);

            _targetSceneAsset = (SceneAsset)EditorGUILayout.ObjectField(
                "  Target Scene",
                _targetSceneAsset,
                typeof(SceneAsset),
                false
            );
    }
}
#endif

