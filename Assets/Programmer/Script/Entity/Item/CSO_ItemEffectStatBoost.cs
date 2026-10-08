using UnityEngine;

/*
 * 使用者(拾った人)のステータスを上げる効果
 * ランダムイベント「支援物資」のアイテムで使う
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・CSO_ItemDataInstant(拾ったらすぐ効くアイテム)の効果として使う
 * ・各ステータスを、今の値 × 倍率 にする(1なら変えない)。効果はゲーム終了まで続く
 *   何度も拾うと倍率が重ねて掛かる(例: 攻撃力×1.2 を2回 → ×1.44)
 * ・ステータスの変更はCS_PlayerStatsを通す(書き込みはサーバーのみ。アイテムの拾得もサーバーで処理される)
 */
// ========================================

[CreateAssetMenu(fileName = "DB_ItemEffectStatBoost", menuName = "Item/Item Effect/Stat Boost")]
public class CSO_ItemEffectStatBoost : CSO_ItemEffect
{
    [SerializeField, Min(0f)]
    [Tooltip("HP上限の倍率(1なら変えない)")]
    private float _maxHpRate = 1f;

    [SerializeField, Min(0f)]
    [Tooltip("攻撃力の倍率(1なら変えない)")]
    private float _attackPowerRate = 1.2f;

    [SerializeField, Min(0f)]
    [Tooltip("移動速度の倍率(1なら変えない)")]
    private float _moveSpeedRate = 1f;

    [SerializeField, Min(0f)]
    [Tooltip("必殺技の威力の倍率(1なら変えない)")]
    private float _specialAttackPowerRate = 1f;

    public override void Apply(GameObject target)
    {
        CS_PlayerStats stats = target.GetComponentInParent<CS_PlayerStats>();
        if (stats == null)
        {
            Debug.LogWarning($"CSO_ItemEffectStatBoost: {target.name} はCS_PlayerStatsを持たないため、ステータスを上げられません", target);
            return;
        }

        if (!Mathf.Approximately(_maxHpRate, 1f)) stats.SetMaxHp(stats.maxHp * _maxHpRate);
        if (!Mathf.Approximately(_attackPowerRate, 1f)) stats.SetAttackPower(stats.attackPower * _attackPowerRate);
        if (!Mathf.Approximately(_moveSpeedRate, 1f)) stats.SetMoveSpeed(stats.moveSpeed * _moveSpeedRate);
        if (!Mathf.Approximately(_specialAttackPowerRate, 1f)) stats.SetSpecialAttackPower(stats.specialAttackPower * _specialAttackPowerRate);
    }
}
