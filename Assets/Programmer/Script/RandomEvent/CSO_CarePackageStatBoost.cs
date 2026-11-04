using UnityEngine;

/*
 * ケアパッケージの中身「ステータスアップ」
 * 開封したプレイヤーのステータスを、開けた瞬間に上げる(アイテムは出ない)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・効果はアイテムの効果(CSO_ItemEffect)をそのまま使う。初期は DB_ItemEffectStatBoost(攻撃力×1.2、ゲーム終了まで続く)
 *   上げるステータス・量を変える時は、効果のアセットを変える(または別の効果のアセットを作って差し替える)
 * ・開けてすぐ終わる(IsFinishedは初期状態のtrue)
 */
// ========================================

[CreateAssetMenu(fileName = "DB_CarePackageStatBoost", menuName = "RandomEvent/CarePackage/Stat Boost")]
public class CSO_CarePackageStatBoost : CSO_CarePackageContent
{
    [SerializeField]
    [Tooltip("開けたプレイヤーにかける効果")]
    private CSO_ItemEffect[] _effects = new CSO_ItemEffect[0];

    public override void Open(CS_CarePackageOpening opening)
    {
        if (opening.opener == null) return;

        foreach (CSO_ItemEffect effect in _effects)
        {
            if (effect != null) effect.Apply(opening.opener);
        }
    }
}
