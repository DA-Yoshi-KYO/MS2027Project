using UnityEngine;

/*
 * NPCの行動: 悪人を探して近づき、変身して倒す(必殺技も使う)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・近くに悪人がいるほど価値が高い。強さの調整(悪人を狙う積極性)も掛ける
 * ・雑魚の性格が人のスコアを上回りそうな間(CS_NpcDifficulty.suppressScoring)は選ばない
 * ・攻撃は変身中しかできないので、近づきながら変身する(警察が近い間は変身しない)
 * ・必殺技は、ゲージが満タンで、範囲に悪人がspecialMinTargets人以上いる時に使う
 */
// ========================================

public class CS_NpcActionHuntVillain : CS_NpcAction
{
    public override string name => "悪人を倒す";

    public override float Evaluate(CS_NpcBrain brain)
    {
        if (brain.difficulty.suppressScoring) return 0f;

        CSO_NpcPersonality personality = brain.personality;
        CS_VillainHealth villain = brain.sensor.FindNearestVillain(brain.position, personality.villainSightRange);
        if (villain == null) return 0f;

        float distance = Vector3.Distance(brain.position, villain.transform.position);
        return Closeness(distance, personality.villainSightRange)
            * personality.huntVillainWeight
            * personality.GetHuntAggressiveness(brain.difficulty.strength);
    }

    public override void Tick(CS_NpcBrain brain)
    {
        CSO_NpcPersonality personality = brain.personality;
        CS_VillainHealth villain = brain.sensor.FindNearestVillain(brain.position, personality.villainSightRange);
        if (villain == null)
        {
            brain.StopMoving();
            return;
        }

        brain.TryTransform();

        Vector3 target = villain.transform.position;
        if (Vector3.Distance(brain.position, target) > personality.attackRange)
        {
            brain.MoveTo(target);
        }
        else
        {
            brain.FaceTowards(target);
            brain.PressAttack();
        }

        if (brain.sensor.CountVillains(brain.position, personality.specialRange) >= personality.specialMinTargets)
        {
            brain.PressSpecial();
        }
    }
}
