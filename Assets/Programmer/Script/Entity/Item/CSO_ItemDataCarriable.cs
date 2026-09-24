/* ================================================
 *
 * ================================================
 * 制作者：KR
 * ------------------------------------------------
 * 2026-09-23 | 初回作成
 * ================================================ */

using UnityEngine;

/*
 * 携帯型アイテムのデータ(ScriptableObject)
 * 拾った瞬間はICarriableItemHolder(プレイヤーなど)のスロットに格納されるだけで、
 * 効果の発動(Activate)は任意のタイミングでプレイヤー側から呼び出す
 *
 * 制作者：　KR
 */

// ========================================
/*
 * メモ
 * ・OnPickup(picker)は「格納できたか」を返す。格納できなければアイテムはフィールドに残る
 *   (ICarriableItemHolder未実装、またはスロットが埋まっている場合)
 * ・効果の適用はOnPickup()の中では行わない。プレイヤー側がスロットのアイテムを使うタイミングで
 *   Activate(user)を呼び出すこと
 * ・アイテムスロット本体(ICarriableItemHolderの実装)はプレイヤー側の担当のため、ここでは未実装
 */
// ========================================

[CreateAssetMenu(fileName = "DB_ItemDataCarriable", menuName = "Item/Item Data (Carriable)")]
public class CSO_ItemDataCarriable : CSO_ItemData
{
    [Header("効果")]
    [SerializeField] private CSO_ItemEffect[] _effects;

    public override bool OnPickup(GameObject picker)
    {
        ICarriableItemHolder holder = picker.GetComponentInParent<ICarriableItemHolder>();
        if (holder == null)
        {
            Debug.LogWarning($"CSO_ItemDataCarriable: {picker.name} はICarriableItemHolderを実装していないため所持できません", picker);
            return false;
        }

        return holder.TryStoreItem(this);
    }

    // スロットに格納されたアイテムを発動する。効果(_effects)を全て対象に適用する
    public void Activate(GameObject user)
    {
        if (_effects == null) return;

        foreach (CSO_ItemEffect effect in _effects)
        {
            if (effect == null) continue;

            effect.Apply(user);
        }
    }
}
