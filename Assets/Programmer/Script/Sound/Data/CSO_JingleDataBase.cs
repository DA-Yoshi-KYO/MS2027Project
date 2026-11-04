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
/// ジングル設定をIDごとに管理するデータベース。
/// </summary>
[CreateAssetMenu(
    fileName = "DB_JingleDatabase",
    menuName = "Sound/Jingle Database"
)]
public class CSO_JingleDataBase : ScriptableObject
{
    [Header("ジングルデータ一覧")]
    [SerializeField] private List<CS_JingleData> _jingleList = new();

    private Dictionary<CSE_JingleId, CS_JingleData> _jingleTable;

    public bool TryGetData(
        CSE_JingleId id,
        out CS_JingleData data)
    {
        if (_jingleTable == null)
            BuildTable();

        return _jingleTable.TryGetValue(id, out data);
    }

    private void BuildTable()
    {
        _jingleTable = new Dictionary<CSE_JingleId, CS_JingleData>();

        foreach (CS_JingleData jingleData in _jingleList)
        {
            if (jingleData == null)
                continue;

            if (jingleData.id == CSE_JingleId.None)
            {
                Debug.LogWarning(
                    "[CSO_JingleDatabase] Noneが設定されています。",
                    this
                );
                continue;
            }

            if (!_jingleTable.TryAdd(jingleData.id, jingleData))
            {
                Debug.LogWarning(
                    $"[CSO_JingleDatabase] IDが重複しています: {jingleData.id}",
                    this
                );
            }
        }
    }

    private void OnValidate()
    {
        _jingleTable = null;
    }
}
