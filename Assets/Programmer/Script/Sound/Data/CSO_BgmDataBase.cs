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
/// BGM設定をIDごとに管理するデータベース。
/// InspectorではList、実行時はDictionaryとして使用する。
/// </summary>
[CreateAssetMenu(
    fileName = "DB_BgmDatabase",
    menuName = "Sound/BGM Database"
)]
public class CSO_BgmDataBase : ScriptableObject
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("BGMデータ一覧")]
    [SerializeField]
    private List<CS_BgmData> _bgmList = new();

    // =========================================================
    // 内部フィールド
    // =========================================================

    private Dictionary<CSE_BgmId, CS_BgmData> _bgmTable;

    // =========================================================
    // データ取得
    // =========================================================

    public bool TryGetData(
        CSE_BgmId id,
        out CS_BgmData data)
    {
        if (_bgmTable == null)
            BuildTable();

        return _bgmTable.TryGetValue(id, out data);
    }

    // =========================================================
    // Dictionary構築
    // =========================================================

    private void BuildTable()
    {
        _bgmTable =
            new Dictionary<CSE_BgmId, CS_BgmData>();

        foreach (CS_BgmData bgmData in _bgmList)
        {
            if (bgmData == null)
                continue;

            if (bgmData.id == CSE_BgmId.None)
            {
                Debug.LogWarning(
                    "[CSO_BgmDatabase] " +
                    "Noneが設定されたBGMデータがあります。",
                    this
                );

                continue;
            }

            if (_bgmTable.ContainsKey(bgmData.id))
            {
                Debug.LogWarning(
                    $"[CSO_BgmDatabase] " +
                    $"BGM IDが重複しています：{bgmData.id}",
                    this
                );

                continue;
            }

            _bgmTable.Add(
                bgmData.id,
                bgmData
            );
        }
    }

    private void OnValidate()
    {
        _bgmTable = null;
    }
}