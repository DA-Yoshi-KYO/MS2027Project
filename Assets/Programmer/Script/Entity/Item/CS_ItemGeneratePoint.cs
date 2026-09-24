/* ================================================
 * 
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 生成したアイテムの登録
/// </summary>
public class CS_ItemGeneratePoint : MonoBehaviour
{
    private CS_ItemBase _item;
    public CS_ItemBase item
    {
        get { return _item; }
        set { _item = value; }
    }
}
