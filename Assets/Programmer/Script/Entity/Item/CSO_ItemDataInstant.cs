/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * ================================================ */

using UnityEngine;

/*
 * 即時発動アイテムのデータ(ScriptableObject)
 * 拾った瞬間に登録された効果(CSO_ItemEffect)を全て適用し、消費される
 *
 * 制作者：　吉田京志郎
 */

[CreateAssetMenu(fileName = "DB_ItemDataInstant", menuName = "Item/Item Data (Instant)")]
public class CSO_ItemDataInstant : CSO_ItemData
{
    [Header("効果")]
    [SerializeField] private CSO_ItemEffect[] _effects;

    public override bool OnPickup(GameObject picker)
    {
        if (_effects == null) return true;

        foreach (CSO_ItemEffect effect in _effects)
        {
            if (effect == null) continue;

            effect.Apply(picker);
        }

        return true;
    }
}
