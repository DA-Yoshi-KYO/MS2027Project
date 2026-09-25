/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 警察の攻撃判定オブジェクト
/// 生成された位置に一定時間残り、その間に判定の球に入った相手(IDamageable)に1回ずつダメージを与える
/// </summary>
public class CS_PoliceAttackHitbox : MonoBehaviour
{
    // 一度に判定できるコライダーの上限
    private const int _hitBufferSize = 16;

    // 判定の球に入ったコライダーを集める際に使い回すバッファ
    private readonly Collider[] _hitBuffer = new Collider[_hitBufferSize];

    // この攻撃判定で既にダメージを与えた相手(同じ相手に何度も当たらないようにする)
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

    // 与えるダメージ
    private float _damage = 0.0f;

    // 判定の球の半径
    private float _radius = 0.0f;

    // 判定が消えるまでの残り時間
    private float _remainingTime = 0.0f;

    // 攻撃を出した位置(警察の中心)。ここから相手までの間に壁などがあれば当てない
    private Vector3 _origin = Vector3.zero;

    // 初期化処理を行わずに判定するのを防ぐためのフラグ
    private bool _isInitialized = false;

    /// <summary>
    /// 指定した位置に攻撃判定オブジェクトを生成するメソッド
    /// </summary>
    /// <param name="position">判定の中心</param>
    /// <param name="origin">攻撃を出した位置(ここから壁越しになる相手には当たらない)</param>
    /// <param name="damage">与えるダメージ</param>
    /// <param name="radius">判定の球の半径</param>
    /// <param name="lifetime">判定が残る時間(秒)</param>
    /// <returns>生成した攻撃判定</returns>
    public static CS_PoliceAttackHitbox Create(Vector3 position, Vector3 origin, float damage, float radius, float lifetime)
    {
        GameObject hitboxObject = new GameObject("PoliceAttackHitbox");
        hitboxObject.transform.position = position;

        CS_PoliceAttackHitbox hitbox = hitboxObject.AddComponent<CS_PoliceAttackHitbox>();
        hitbox._origin = origin;
        hitbox._damage = damage;
        hitbox._radius = radius;
        hitbox._remainingTime = lifetime;
        hitbox._isInitialized = true;
        return hitbox;
    }

    private void FixedUpdate()
    {
        if (!_isInitialized) return;

        // 残っている間は毎回判定し、新しく入ってきた相手にもダメージを与える
        HitTargetsInRange();

        _remainingTime -= Time.fixedDeltaTime;
        if (_remainingTime <= 0.0f) Destroy(gameObject);
    }

    /// <summary>
    /// 判定の球に入っている相手のうち、まだダメージを与えていない相手にダメージを与えるメソッド
    /// </summary>
    private void HitTargetsInRange()
    {
        // ダメージを与える相手はEntityレイヤーのキャラクターの中から探す
        int count = Physics.OverlapSphereNonAlloc(transform.position, _radius, _hitBuffer, CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = _hitBuffer[i];
            IDamageable target = hitCollider.GetComponentInParent<IDamageable>();
            if (target == null || _hitTargets.Contains(target)) continue;

            // 攻撃を出した位置から壁越しになる相手には当てない
            // (ここでは当てずに、壁の陰から判定の中へ回り込んできたら当たるようにする)
            Transform targetRoot = ((Component)target).transform;
            if (!CS_PoliceLineOfSight.IsClear(_origin, hitCollider.bounds.center, targetRoot)) continue;

            _hitTargets.Add(target);
            target.TakeDamage(_damage);
        }
    }

    private void OnDrawGizmos()
    {
        // Sceneビューで攻撃判定の範囲を確認できるようにする
        Gizmos.color = new Color(1.0f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}
