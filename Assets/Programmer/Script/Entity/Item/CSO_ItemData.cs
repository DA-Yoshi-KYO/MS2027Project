/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * ================================================ */

using UnityEngine;

/*
 * アイテムのデータ(ScriptableObject)の基底クラス
 * フィールド上のCS_ItemBaseがこれを参照し、拾われたときの処理を委譲する
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ■ 新しい入手形態を追加したいとき
 *   このクラスを継承し、Useをoverrideする
 *   例) 即時発動: CSO_ItemDataInstant(実装済み。効果をその場で適用して消費する)
 *       携帯型  : 今後CSO_ItemDataCarriableなどを追加予定(インベントリに追加する)
 */
// ========================================

public abstract class CSO_ItemData : ScriptableObject
{
    [Header("基本情報")]
    [SerializeField] private string _itemName = "";
    [SerializeField] private Sprite _icon;
    [SerializeField][TextArea] private string _description = "";

    public string itemName => _itemName;
    public Sprite icon => _icon;
    public string description => _description;

    // 拾われたときの処理。フィールドから消費して良いときはtrueを返す
    public abstract bool Use(GameObject picker);
}
