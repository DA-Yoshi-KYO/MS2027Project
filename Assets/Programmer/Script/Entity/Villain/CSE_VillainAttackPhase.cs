/*
 * 悪人の攻撃の段階
 * CS_VillainCombatが管理し、CS_VillainAttackVisualが見た目の切り替えに使う
 *
 * 制作者：　中出峻輔
 */

public enum CSE_VillainAttackPhase : byte
{
    None,       // 攻撃していない
    Charge,     // 溜め中(まだ当たらない)
    Hit,        // 攻撃判定が出ている
}
