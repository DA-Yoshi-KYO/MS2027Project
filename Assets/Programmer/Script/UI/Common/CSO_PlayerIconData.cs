/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-09 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 
/// </summary>
[CreateAssetMenu(fileName = "CSO_PlayerIconData", menuName = "Scriptable Objects/CSO_PlayerIconData")]
public class CSO_PlayerIconData : ScriptableObject
{
    [Header("プレイヤーアイコン（playerNumber をインデックスに使う）")]
    [SerializeField] private Sprite[] _playerIcons;

    /// <summary>
    /// playerNumber に対応するアイコンを取得する
    /// 範囲外の場合は null を返す
    /// </summary>
    public Sprite GetIcon(int playerNumber)
    {
        if (_playerIcons == null
            || playerNumber < 0
            || playerNumber >= _playerIcons.Length)
            return null;

        return _playerIcons[playerNumber];
    }
}
