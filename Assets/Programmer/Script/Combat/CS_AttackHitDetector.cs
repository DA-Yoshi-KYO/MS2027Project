using System.Collections.Generic;
using UnityEngine;

/*
 * 攻撃の当たり判定(正面 hitRange 先、半径 hitRadius の球状範囲)を行う共通処理
 * CS_PlayerAttack(コンボ攻撃)とCS_PlayerSpecialAttack(必殺技)の両方から使う
 * hitRangeを0にすると自分を中心にした全方位の範囲攻撃になる(必殺技はこの設定を使用)
 *
 * 制作者：　秋野翔太
 */

public static class CS_AttackHitDetector
{
    // originの正面 hitRange 先、半径 hitRadius の球にいるIDamageableをresultsへ集める(同じ相手は1回だけ、自分自身は除く)
    // hitRangeが0なら自分の位置が中心になる(全方位)
    public static void FindTargets(
        Transform origin, CSO_AttackData step, LayerMask targetLayers,
        Collider[] buffer, HashSet<IDamageable> results)
    {
        results.Clear();

        Vector3 center = origin.position + origin.forward * step.hitRange;
        int count = Physics.OverlapSphereNonAlloc(
            center, step.hitRadius, buffer, targetLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (buffer[i].transform.IsChildOf(origin)) continue;

            IDamageable target = buffer[i].GetComponentInParent<IDamageable>();
            if (target != null)
            {
                results.Add(target);
            }
        }
    }
}
