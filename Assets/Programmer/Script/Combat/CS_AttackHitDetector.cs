using System.Collections.Generic;
using UnityEngine;

/*
 * 攻撃の当たり判定(正面 hitRange 先、半径 hitRadius の球状範囲)を行う共通処理
 * CS_PlayerAttack(コンボ攻撃)とCS_PlayerSpecialAttack(必殺技)の両方から使う
 * hitRangeを0にすると自分を中心にした全方位の範囲攻撃になる(必殺技はこの設定を使用)
 * TryKnockbackで、当たった相手(IKnockbackable)を攻撃した位置から離れる方向へ下がらせる
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
        FindTargets(origin, origin.position, origin.forward, step, targetLayers, buffer, results);
    }

    private const float _maxPoseError = 3f;   // 送られてきた位置が、サーバー上の位置からこれ以上離れていたら信用しない(m)

    // 攻撃した本人から送られてきた位置・向きを確かめる(サーバーで使う)
    // サーバー上の位置から大きく離れている時(不正・大きな遅延)は、サーバー上の位置・向きを使う。向きは水平にそろえる
    public static void ResolvePose(Transform origin, ref Vector3 position, ref Vector3 forward)
    {
        if ((position - origin.position).sqrMagnitude > _maxPoseError * _maxPoseError)
        {
            position = origin.position;
            forward = origin.forward;
        }

        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : origin.forward;
    }

    // 位置と向きを指定して判定する(originは自分自身を除くためだけに使う)
    // プレイヤーの攻撃は、攻撃した本人の手元の位置・向き(エイムアシストで補正済み)をサーバーへ送って判定するため、こちらを使う
    public static void FindTargets(
        Transform origin, Vector3 position, Vector3 forward, CSO_AttackData step, LayerMask targetLayers,
        Collider[] buffer, HashSet<IDamageable> results)
    {
        results.Clear();

        Vector3 center = position + forward * step.hitRange;
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

    // 当たった相手がノックバックを受けられるなら、sourcePosition(攻撃した位置)から離れる方向へ下がらせる
    // IDamageable(例: CS_VillainHealth)とIKnockbackable(例: CS_VillainKnockback)は別コンポーネントのことが多いので、
    // 同じオブジェクト上のコンポーネントも探す。ダメージの後で呼ぶ(サーバー、またはオフライン)
    public static void TryKnockback(IDamageable target, Vector3 sourcePosition, float power = 1f)
    {
        IKnockbackable knockbackable = null;

        if (target is Component component)
        {
            if (component == null) return;   // 撃退されて破棄済み

            component.TryGetComponent(out knockbackable);
        }
        else
        {
            knockbackable = target as IKnockbackable;
        }

        knockbackable?.Knockback(sourcePosition, power);
    }
}
