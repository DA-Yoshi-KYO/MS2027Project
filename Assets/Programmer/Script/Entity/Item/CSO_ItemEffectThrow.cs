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
 */
// ========================================

[CreateAssetMenu(fileName = "DB_ItemEffectThrow", menuName = "Item/Item Effect/Throw")]
public class CSO_ItemEffectThrow : CSO_ItemEffect
{
    [SerializeField][Tooltip("投げる爆弾のプレハブ")] private CS_ThrownBomb _projectilePrefab;
    [SerializeField][Tooltip("前方向の初速")][Min(0f)] private float _forwardSpeed = 10f;
    [SerializeField][Tooltip("上方向の初速")][Min(0f)] private float _upwardSpeed = 4f;
    [SerializeField][Tooltip("投げ始める位置(使用者から見た前方向と上方向のずれ)")] private Vector2 _spawnOffset = new Vector2(0.8f, 1.2f);

    public override void Apply(GameObject target)
    {
        if (_projectilePrefab == null)
        {
            Debug.LogError("CSO_ItemEffectThrow: Projectile Prefab が未設定です", this);
            return;
        }

        Transform thrower = target.transform;
        Vector3 position = thrower.position + thrower.forward * _spawnOffset.x + Vector3.up * _spawnOffset.y;
        CS_ThrownBomb bomb = Instantiate(_projectilePrefab, position, thrower.rotation);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            bomb.NetworkObject.Spawn();
        }

        Vector3 velocity = thrower.forward * _forwardSpeed + Vector3.up * _upwardSpeed;
        bomb.Launch(velocity, target);
    }
}
