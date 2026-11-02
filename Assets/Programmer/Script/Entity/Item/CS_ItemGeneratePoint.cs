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

    private float _timer;       // 入れ替え・再生成までに数えた時間(秒。アイテムが拾われた時にリセットする)
    public float timer
    {
        get => _timer;
        set => _timer = value;
    }

    private bool _hadItem;      // 前のフレームで、アイテムが残っていたか(拾われた瞬間を調べるのに使う)
    public bool hadItem
    {
        get => _hadItem;
        set => _hadItem = value;
    }
}
