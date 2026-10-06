using UnityEngine;

/*
 * NPCの行動: 他のプレイヤーの周りをうろついて邪魔する(妨害)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・一番近い他のプレイヤーの周りを、_orbitRadiusの円を描くように回る
 * ・今は「近くをうろつく」だけ(体で道をふさぐ程度)。攻撃や妨害アイテムを使うなどは仕様が決まったら足す
 */
// ========================================

public class CS_NpcActionHarass : CS_NpcAction
{
    private const float _orbitRadius = 2.5f;     // 相手の周りを回る半径(m)
    private const float _orbitSpeed = 60f;       // 回る速さ(度/秒)

    public override string name => "妨害する";

    public override float Evaluate(CS_NpcBrain brain)
    {
        float range = brain.personality.playerSightRange;
        CS_Player other = brain.sensor.FindNearestOtherPlayer(brain.player, range);
        if (other == null) return 0f;

        float distance = Vector3.Distance(brain.position, other.transform.position);
        return Closeness(distance, range) * brain.personality.harassWeight;
    }

    public override void Tick(CS_NpcBrain brain)
    {
        CS_Player other = brain.sensor.FindNearestOtherPlayer(brain.player, brain.personality.playerSightRange);
        if (other == null)
        {
            brain.StopMoving();
            return;
        }

        // 相手を中心に、時間とともに回る位置を目指す
        float angle = Time.time * _orbitSpeed + brain.player.playerNumber * 90f;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _orbitRadius;
        brain.MoveTo(other.transform.position + offset);
    }
}
