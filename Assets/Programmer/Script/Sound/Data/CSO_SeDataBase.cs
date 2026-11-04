/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SEの設定をIDごとに管理するデータベース。
/// InspectorではList、実行時はDictionaryとして使用する。
/// </summary>
[CreateAssetMenu(
    fileName = "DB_SeDatabase",
    menuName = "Sound/SE Database"
)]
public class CSO_SeDataBase : ScriptableObject
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("SEデータ一覧")]
    [SerializeField]
    private List<CS_SeData> _seList = new();

    // =========================================================
    // 内部フィールド
    // =========================================================

    private Dictionary<CSE_SeId, CS_SeData> _seTable;

    // =========================================================
    // データ取得
    // =========================================================

    /// <summary>
    /// 指定したSE IDのデータを取得する。
    /// 登録がない場合はfalseを返す。
    /// </summary>
    public bool TryGetData(
        CSE_SeId id,
        out CS_SeData data)
    {
        if (_seTable == null)
            BuildTable();

        return _seTable.TryGetValue(
            id,
            out data
        );
    }

    // =========================================================
    // Dictionary構築
    // =========================================================

    private void BuildTable()
    {
        _seTable =
            new Dictionary<CSE_SeId, CS_SeData>();

        foreach (CS_SeData seData in _seList)
        {
            if (seData == null)
                continue;

            if (seData.id == CSE_SeId.None)
            {
                Debug.LogWarning(
                    "[CSO_SeDatabase] " +
                    "Noneが設定されたSEデータがあります。",
                    this
                );

                continue;
            }

            if (_seTable.ContainsKey(seData.id))
            {
                Debug.LogWarning(
                    $"[CSO_SeDatabase] " +
                    $"SE IDが重複しています：{seData.id}",
                    this
                );

                continue;
            }

            _seTable.Add(
                seData.id,
                seData
            );
        }
    }

    // =========================================================
    // Inspector更新
    // =========================================================

    /// <summary>
    /// Inspectorでデータを編集した場合、
    /// 次回取得時にDictionaryを作り直す。
    /// </summary>
    private void OnValidate()
    {
        _seTable = null;
    }
}
