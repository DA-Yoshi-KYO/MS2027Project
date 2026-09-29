/* ================================================
 *
 * ================================================
 * 制作者：KR
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/*
 * 対象の周囲に範囲ダメージを与える効果(ScriptableObject)
 * targetの位置を中心に球状の判定を行い、当たったIDamageableにTakeDamageを呼ぶ
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・爆弾アイテムでは、投げた爆弾(CS_ThrownBomb)が着弾後に爆弾自身をtargetにして呼ぶ
 *   (拾う → スロットに格納 → 使用ボタンで投げる(CSO_ItemEffectThrow) → 着弾してn秒後にこれ)
 */
// ========================================

[CreateAssetMenu(fileName = "DB_ItemEffectExplode", menuName = "Item/Item Effect/Explode")]
public class CSO_ItemEffectExplode : CSO_ItemEffect
{
    [SerializeField][Tooltip("ダメージ量")][Min(0f)] private float _damage = 3f;
    [SerializeField][Tooltip("爆発の範囲半径")][Min(0f)] private float _radius = 3f;
    [SerializeField][Tooltip("ダメージを与える対象のレイヤー")] private LayerMask _targetLayers;

    private const int _hitBufferSize = 16;

    public override void Apply(GameObject target)
    {
        Collider[] hitBuffer = new Collider[_hitBufferSize];
        int count = Physics.OverlapSphereNonAlloc(
            target.transform.position, _radius, hitBuffer, _targetLayers, QueryTriggerInteraction.Ignore);

        HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        for (int i = 0; i < count; i++)
        {
            IDamageable damageable = hitBuffer[i].GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                hitTargets.Add(damageable);
            }
        }

        foreach (IDamageable damageable in hitTargets)
        {
            damageable.TakeDamage(_damage);
        }
    }
}
