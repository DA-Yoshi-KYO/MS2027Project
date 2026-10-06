using UnityEngine;

/*
 * NPCの行動: 変身中に警察が近づいてきたら、変身を解いて逃げる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・警察は変身が完了しているプレイヤーしか狙わないので、変身完了中だけ選ぶ
 * ・警察と反対の方向へ_fleeDistanceだけ離れた位置を目指しつつ、変身を解く
 *   (必殺技中は解除できないので、終わり次第解く)
 * ・警察が近いほど価値が高い。急ぐ必要があるので、他の行動より高くなりやすいよう_urgencyを掛ける
 */
// ========================================

public class CS_NpcActionFleePolice : CS_NpcAction
{
    private const float _fleeDistance = 8f;     // 警察から離れる距離(m)
    private const float _urgency = 1.5f;        // 他の行動より優先しやすくする倍率

    public override string name => "警察から逃げる";

    public override float Evaluate(CS_NpcBrain brain)
    {
        if (!brain.transformation.isTransformed) return 0f;

        float range = brain.personality.policeSightRange;
        CS_PoliceBrain police = brain.sensor.FindNearestPolice(brain.position, range);
        if (police == null) return 0f;

        float distance = Vector3.Distance(brain.position, police.transform.position);
        return Closeness(distance, range) * brain.personality.fleePoliceWeight * _urgency;
    }

    public override void Tick(CS_NpcBrain brain)
    {
        brain.TryUntransform();

        CS_PoliceBrain police = brain.sensor.FindNearestPolice(brain.position, brain.personality.policeSightRange);
        if (police == null)
        {
            brain.StopMoving();
            return;
        }

        Vector3 away = brain.position - police.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = brain.player.transform.forward;

        brain.MoveTo(brain.position + away.normalized * _fleeDistance);
    }
}
