using UnityEngine;
using UnityEngine.AI;

/*
 * NPCの行動: 当てもなくうろつく(他にやることが無い時)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・価値は性格の重み(wanderWeight)のまま。他の行動が全部0の時に選ばれる、いわば待機
 * ・近くのNavMesh上の点をランダムに選んで向かい、着いたら次の点を選ぶ(歩き回るうちに悪人などを見つける)
 */
// ========================================

public class CS_NpcActionWander : CS_NpcAction
{
    private const float _wanderRadius = 12f;     // うろつく範囲(m)
    private const float _arriveDistance = 1f;    // 目的地にこの距離まで近づいたら次へ(m)

    public override string name => "うろつく";

    public override float Evaluate(CS_NpcBrain brain)
    {
        return brain.personality.wanderWeight;
    }

    public override void OnEnter(CS_NpcBrain brain)
    {
        PickNextPoint(brain);
    }

    public override void Tick(CS_NpcBrain brain)
    {
        // 目的地は次に選ぶまで保たれるので、着いた時だけ選び直す
        if (brain.HasArrived(_arriveDistance))
        {
            PickNextPoint(brain);
        }
    }

    private static void PickNextPoint(CS_NpcBrain brain)
    {
        Vector2 random = Random.insideUnitCircle * _wanderRadius;
        Vector3 candidate = brain.position + new Vector3(random.x, 0f, random.y);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, _wanderRadius, NavMesh.AllAreas))
        {
            candidate = hit.position;
        }

        brain.MoveTo(candidate);
    }
}
