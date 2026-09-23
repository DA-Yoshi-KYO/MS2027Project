/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * ================================================ */

using UnityEngine;

/*
 * アイテムの効果1個分(ScriptableObject)の基底クラス
 * CSO_ItemDataInstantなどから呼び出され、対象に効果を適用する
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ■ 新しい効果を追加したいとき
 *   このクラスを継承し、Applyをoverrideする
 *   例) CSO_ItemEffectHeal          : 即時回復(実装済み)
 *       CSO_ItemEffectHealOverTime : 継続回復(今後追加予定)
 *       CSO_ItemEffectAttackUp     : 攻撃力アップ(今後追加予定)
 */
// ========================================

public abstract class CSO_ItemEffect : ScriptableObject
{
    // targetに対して効果を適用する
    public abstract void Apply(GameObject target);
}
