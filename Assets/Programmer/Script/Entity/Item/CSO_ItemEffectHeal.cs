/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * ================================================ */

using UnityEngine;

/*
 * 対象のHPを即時回復させる効果(ScriptableObject)
 * targetがIHealableを実装していない場合は何もしない(警告ログのみ)
 *
 * 制作者：　吉田京志郎
 */

[CreateAssetMenu(fileName = "DB_ItemEffectHeal", menuName = "Item/Item Effect/Heal")]
public class CSO_ItemEffectHeal : CSO_ItemEffect
{
    [SerializeField][Tooltip("回復量")][Min(0f)] private float _healAmount = 10f;

    public override void Apply(GameObject target)
    {
        IHealable healable = target.GetComponentInParent<IHealable>();
        if (healable == null)
        {
            Debug.LogWarning($"CSO_ItemEffectHeal: {target.name} はIHealableを実装していないため回復できません", target);
            return;
        }

        healable.Heal(_healAmount);
    }
}
