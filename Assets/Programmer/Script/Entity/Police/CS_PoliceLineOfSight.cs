/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警察の視線・攻撃が壁などに遮られていないかを判定するクラス
/// 視界(CS_PoliceVision)・チャージ開始(CS_PoliceAttack)・攻撃判定(CS_PoliceAttackHitbox)で共通して使う
/// </summary>
public static class CS_PoliceLineOfSight
{
    // 一度に調べられる当たりの上限
    private const int _hitBufferSize = 16;

    // 線上の当たりを集める際に使い回すバッファ(判定のたびに配列を確保しないため)
    private static readonly RaycastHit[] _hitBuffer = new RaycastHit[_hitBufferSize];

    /// <summary>
    /// 2点の間に、視線・攻撃を遮るものが無いかを判定するメソッド
    /// 遮るものはCS_PoliceLayers.obstacleLayers(Entity以外のステージなど)に属するもの
    /// 標的自身と、プレイヤー・悪人などのキャラクター(IDamageableを持つもの)は遮るものとして扱わない
    /// (Entityレイヤーが付いていないキャラクターがいても、悪人の後ろにいるプレイヤーを狙えるようにするため)
    /// </summary>
    /// <param name="from">視線・攻撃の始点</param>
    /// <param name="to">標的の位置</param>
    /// <param name="targetRoot">標的の本体</param>
    /// <returns>遮るものが無ければtrue</returns>
    public static bool IsClear(Vector3 from, Vector3 to, Transform targetRoot)
    {
        Vector3 toTarget = to - from;
        float distance = toTarget.magnitude;
        if (distance <= 0.0f) return true;

        // 始点から標的までの線上の当たりをすべて調べる(標的より奥のものは含まれない)
        int count = Physics.RaycastNonAlloc(from, toTarget / distance, _hitBuffer, distance, CS_PoliceLayers.obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!IsIgnorable(_hitBuffer[i].collider, targetRoot)) return false;
        }
        return true;
    }

    /// <summary>
    /// 線上で当たったコライダーを、遮るものとして扱わなくてよいかを判定するメソッド
    /// </summary>
    /// <param name="hitCollider">当たったコライダー</param>
    /// <param name="targetRoot">標的の本体</param>
    /// <returns>標的自身やキャラクターならtrue</returns>
    private static bool IsIgnorable(Collider hitCollider, Transform targetRoot)
    {
        // hit.transformはRigidbodyの付いたオブジェクトを返すことがあるので、当たったコライダー自体で判定する
        if (hitCollider.transform.IsChildOf(targetRoot)) return true;
        return hitCollider.GetComponentInParent<IDamageable>() != null;
    }
}
