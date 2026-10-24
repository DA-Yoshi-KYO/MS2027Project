using System;
using UnityEngine;

/*
 * 奇襲(気づかれていない悪人の背後・上から攻撃すると、一撃で倒せる)を判定するクラス
 * 通常攻撃(CS_PlayerAttack)が、1段目の判定を出す時にここへ問い合わせる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「奇襲」(Issue #275)。次の3つがそろった時に攻撃ボタンを押すと、奇襲になる
 *   ① 悪人がこちらに気づいていない: 臨戦態勢(CS_VillainCombat.isEngaged)でない、または自分以外を狙っている
 *   ② 一定距離まで近づいた: 悪人との水平距離が_range(既定3m)以内
 *   ③ 背後か上を取っている
 *      背後: 悪人の正面から見て、自分のいる方向が_backAngle度(既定120度)以上うしろ(真後ろを中心に左右60度ずつ)
 *      上  : 自分が悪人より_aboveHeight(既定1.5m)以上高い位置にいる
 * ・奇襲になった時は、その悪人の残りHP以上のダメージを与えて一撃で倒す(仕様「戦闘をすぐに終わらせる」)
 * ・奇襲は通常攻撃の1段目だけ(コンボの途中は、もう気づかれている想定)
 * ・判定はダメージと同じくサーバー(またはオフライン)で行う(悪人の臨戦態勢・標的はサーバーでしか分からないため)
 *   位置は、攻撃した本人の手元の位置(CS_PlayerAttackが送ってくるもの)を使う
 * ・奇襲が決まった時はonAmbush(倒した悪人)で知らせる(サーバー、またはオフラインのみ)。演出は未実装
 * ・壁越しの悪人は奇襲できない: プレイヤーの体の中心から悪人の体の中心までの間に、_obstacleLayersのもの(壁など)があれば奇襲にならない
 *   キャラクター(IDamageableを持つもの)は遮るものとして扱わない。飛び込みもこの線の上を進むので、壁をすり抜けて奇襲することもない
 * ・今奇襲できる悪人はFindAmbushTarget()で探せる(全クライアントで呼べる)。ロックオンUI(CS_PlayerLockOnUI)が使う
 * ・1段目を出した時に奇襲できる悪人がいれば、エイムアシスト(CS_PlayerAimAssist)がその悪人へ_rangeまで飛び込む
 *   なので_rangeを広げると、遠くから奇襲できるようになる(既定はPlayer.prefabで18m)
 *   悪人に気づかれているかは、CS_VillainCombat.IsTargeting(同期された標的)で判定するので、クライアントでも分かる
 */
// ========================================

public class CS_PlayerAmbush : MonoBehaviour
{
    [Header("奇襲の条件")]
    [SerializeField][Min(0f)] private float _range = 3f;                   // 悪人との水平距離がこれ以内(m)
    [SerializeField][Range(0f, 180f)] private float _backAngle = 120f;     // 悪人の正面から見て、これ以上うしろなら背後(度)
    [SerializeField][Min(0f)] private float _aboveHeight = 1.5f;           // 悪人よりこれ以上高ければ上を取っている(m)

    [Header("奇襲できる悪人を探す(ロックオン表示用)")]
    [SerializeField] private LayerMask _villainLayers = ~0;               // 悪人を探すレイヤー
    [SerializeField] private LayerMask _obstacleLayers = ~0;              // 悪人との間にあると奇襲できなくなるもの(壁など)のレイヤー

    private const int _searchBufferSize = 32;   // 一度に調べるコライダーの上限
    private readonly Collider[] _searchBuffer = new Collider[_searchBufferSize];
    private readonly RaycastHit[] _obstacleBuffer = new RaycastHit[_searchBufferSize];

    public float range => _range;   // 奇襲できる距離(m)。エイムアシストの飛び込み・攻撃判定の位置チェックにも使う

    public event Action<CS_VillainHealth> onAmbush;     // 奇襲が決まった時(サーバー、またはオフラインのみ)

    // この相手に、attackerPositionから攻撃した時に奇襲になるか(サーバー、またはオフラインで呼ぶ)
    public bool CanAmbush(IDamageable target, Vector3 attackerPosition, out CS_VillainHealth villain)
    {
        villain = null;

        CS_VillainHealth candidate = target as CS_VillainHealth;
        if (candidate == null || candidate.isDefeated) return false;
        if (!MeetsAmbushCondition(candidate, attackerPosition)) return false;

        villain = candidate;
        return true;
    }

    // 今の位置から奇襲できる悪人のうち、一番近い悪人を探す(いなければnull。全クライアントで呼べる)
    public CS_VillainHealth FindAmbushTarget()
    {
        Vector3 position = transform.position;
        int count = Physics.OverlapSphereNonAlloc(position, _range, _searchBuffer, _villainLayers, QueryTriggerInteraction.Ignore);

        CS_VillainHealth nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            CS_VillainHealth villain = _searchBuffer[i].GetComponentInParent<CS_VillainHealth>();
            if (villain == null || villain == nearest) continue;

            // isDefeatedはサーバーのみなので、クライアントでも分かるHPでも見る
            if (villain.isDefeated || villain.currentHp <= 0f) continue;
            if (!MeetsAmbushCondition(villain, position)) continue;

            float sqr = (villain.transform.position - position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            nearest = villain;
            nearestSqr = sqr;
        }
        return nearest;
    }

    // 奇襲が決まったことを知らせる(CS_PlayerAttackが、一撃で倒すダメージを与えた後に呼ぶ)
    public void NotifyAmbush(CS_VillainHealth villain)
    {
        onAmbush?.Invoke(villain);
    }

    // ②距離 ①気づかれていない ③背後か上 ④間に壁が無い、がそろっているか
    private bool MeetsAmbushCondition(CS_VillainHealth villain, Vector3 attackerPosition)
    {
        Transform villainTransform = villain.transform;
        Vector3 toAttacker = attackerPosition - villainTransform.position;
        Vector3 flat = new Vector3(toAttacker.x, 0f, toAttacker.z);

        if (flat.magnitude > _range) return false;
        if (IsNoticed(villain)) return false;
        if (!IsAbove(toAttacker.y) && !IsBehind(villainTransform.forward, flat)) return false;
        return IsClearLine(attackerPosition, villainTransform);
    }

    // fromから悪人の体の中心までの間に、壁など(_obstacleLayers)が無いか
    // 悪人自身・プレイヤー・他の悪人などのキャラクター(IDamageableを持つもの)は遮るものとして扱わない
    private bool IsClearLine(Vector3 from, Transform villain)
    {
        Vector3 toVillain = villain.position - from;
        float distance = toVillain.magnitude;
        if (distance <= 0.0001f) return true;

        int count = Physics.RaycastNonAlloc(from, toVillain / distance, _obstacleBuffer, distance, _obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = _obstacleBuffer[i].collider;
            if (hit.transform.IsChildOf(villain) || hit.transform.IsChildOf(transform)) continue;
            if (hit.GetComponentInParent<IDamageable>() != null) continue;

            return false;
        }
        return true;
    }

    // 悪人がこちらに気づいているか(臨戦態勢で、自分を狙っている)
    private bool IsNoticed(CS_VillainHealth villain)
    {
        CS_VillainCombat combat = villain.GetComponent<CS_VillainCombat>();
        if (combat == null) return false;

        return combat.IsTargeting(transform.root);
    }

    private bool IsAbove(float heightDifference)
    {
        return heightDifference >= _aboveHeight;
    }

    // 悪人の正面から見て、自分が背後の範囲にいるか
    private bool IsBehind(Vector3 villainForward, Vector3 flatToAttacker)
    {
        villainForward.y = 0f;
        if (villainForward.sqrMagnitude < 0.0001f || flatToAttacker.sqrMagnitude < 0.0001f) return true;   // 重なっている時は背後扱い

        return Vector3.Angle(villainForward, flatToAttacker) >= _backAngle;
    }

    // 選択中に、奇襲できる範囲をシーンビューへ表示する(調整用)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, _range);
    }
}
