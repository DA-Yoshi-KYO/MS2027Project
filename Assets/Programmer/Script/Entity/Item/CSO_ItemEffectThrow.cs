/* ================================================
 *
 * ================================================
 * 制作者：KR
 * ------------------------------------------------
 * 2026-09-28 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/*
 * 使用者の正面へ爆弾(CS_ThrownBomb)を投げる効果(ScriptableObject)
 * 爆発のタイミングやダメージは投げた爆弾側(CS_ThrownBomb / CSO_ItemEffectExplode)で決める
 *
 * 制作者：　KR
 */

// ========================================
/*
 * メモ
 * ・CSO_ItemDataCarriableの効果として使う(アイテム使用ボタンで発動)
 * ・Apply(target)のtargetは使用者(プレイヤー)。使用者の向いている方向へ山なりに投げる
 * ・生成はサーバー(またはオフライン)で行う
 *   CS_PlayerItemSlotの使用処理がサーバーで実行されるため、ここもサーバーで呼ばれる
 * ・ホーミング: 投げた時に、_homingRange以内で一番近い悪人か、使用者以外のプレイヤーを狙い、その相手へ向かって飛ばす
 *   爆弾は、重力と回転を切って、爆発するまで、その相手の体の高さへ向かって飛び続ける
 *   警察は狙わない。相手がいない時は、今まで通り正面へ投げる
 */
// ========================================

[CreateAssetMenu(fileName = "DB_ItemEffectThrow", menuName = "Item/Item Effect/Throw")]
public class CSO_ItemEffectThrow : CSO_ItemEffect
{
    [SerializeField][Tooltip("投げる爆弾のプレハブ")] private CS_ThrownBomb _projectilePrefab;
    [SerializeField][Tooltip("前方向の初速")][Min(0f)] private float _forwardSpeed = 10f;
    [SerializeField][Tooltip("上方向の初速")][Min(0f)] private float _upwardSpeed = 4f;
    [SerializeField][Tooltip("投げ始める位置(使用者から見た前方向と上方向のずれ)")] private Vector2 _spawnOffset = new Vector2(0.8f, 1.2f);
    [SerializeField][Tooltip("ホーミングで狙う範囲(m)。この範囲内で一番近い悪人か、使用者以外のプレイヤーを狙う。0なら狙わない")][Min(0f)] private float _homingRange = 5f;
    [SerializeField][Tooltip("ホーミングで曲がる速さ(度/秒)")][Min(0f)] private float _homingTurnSpeed = 360f;
    [SerializeField][Tooltip("ホーミング中に飛ぶ速さ(m/秒)")][Min(0f)] private float _homingSpeed = 6f;

    private const int _hitBufferSize = 32;

    public override void Apply(GameObject target)
    {
        if (_projectilePrefab == null)
        {
            Debug.LogError("CSO_ItemEffectThrow: Projectile Prefab が未設定です", this);
            return;
        }

        Transform thrower = target.transform;
        Transform homingTarget = FindHomingTarget(thrower);

        Vector3 position = thrower.position + thrower.forward * _spawnOffset.x + Vector3.up * _spawnOffset.y;
        CS_ThrownBomb bomb = Instantiate(_projectilePrefab, position, thrower.rotation);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            bomb.NetworkObject.Spawn();
        }

        // 狙う相手がいる時は、その相手の方向(水平)へ投げる。いない時は、正面へ投げる
        Vector3 direction = GetThrowDirection(thrower, position, homingTarget);
        Vector3 velocity = direction * _forwardSpeed + Vector3.up * _upwardSpeed;
        bomb.Launch(velocity, target, homingTarget, _homingTurnSpeed, _homingSpeed);
    }

    // 投げる水平方向を求める(狙う相手がいればその方向、いなければ使用者の正面)
    private static Vector3 GetThrowDirection(Transform thrower, Vector3 from, Transform homingTarget)
    {
        if (homingTarget == null) return thrower.forward;

        Vector3 toTarget = homingTarget.position - from;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : thrower.forward;
    }

    // 範囲内で一番近い、悪人か使用者以外のプレイヤー(生きているもの)を探す(警察は狙わない)
    private Transform FindHomingTarget(Transform thrower)
    {
        if (_homingRange <= 0f) return null;

        Collider[] hitBuffer = new Collider[_hitBufferSize];
        int count = Physics.OverlapSphereNonAlloc(
            thrower.position, _homingRange, hitBuffer, CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);

        Transform nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Transform candidate = GetHomingCandidate(hitBuffer[i], thrower);
            if (candidate == null) continue;

            float sqr = (candidate.position - thrower.position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            nearest = candidate;
            nearestSqr = sqr;
        }

        return nearest;
    }

    // コライダーが、狙える相手(生きている悪人、または使用者以外の生きているプレイヤー)なら、その本体を返す
    private static Transform GetHomingCandidate(Collider collider, Transform thrower)
    {
        CS_VillainHealth villain = collider.GetComponentInParent<CS_VillainHealth>();
        if (villain != null) return villain.isDefeated ? null : villain.transform;

        CS_PlayerHealth player = collider.GetComponentInParent<CS_PlayerHealth>();
        if (player == null || player.isDead) return null;
        if (player.transform.IsChildOf(thrower) || thrower.IsChildOf(player.transform)) return null;

        return player.transform;
    }
}
