/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 使用者の正面へ陽動ホログラムの弾(CS_ThrownHologram)を投げる効果
/// 展開する位置・ホログラムの時間は、投げた弾・ホログラム側で決める
/// </summary>
/// <remarks>
/// ・CSO_ItemDataCarriableの効果として使う(アイテム使用ボタンで発動)
/// ・Apply(target)のtargetは使用者(プレイヤー)。使用者の向いている方向へ山なりに投げる
/// ・生成はサーバー(またはオフライン)で行う(CS_PlayerItemSlotの使用処理がサーバーで実行されるため)
/// </remarks>
[CreateAssetMenu(fileName = "DB_ItemEffectHologram", menuName = "Item/Item Effect/Hologram")]
public class CSO_ItemEffectHologram : CSO_ItemEffect
{
    [SerializeField]
    [Tooltip("投げるホログラムの弾のプレハブ")]
    private CS_ThrownHologram _projectilePrefab = null;

    [SerializeField, Min(0f)]
    [Tooltip("前方向の初速")]
    private float _forwardSpeed = 10.0f;

    [SerializeField, Min(0f)]
    [Tooltip("上方向の初速")]
    private float _upwardSpeed = 4.0f;

    [SerializeField]
    [Tooltip("投げ始める位置(使用者から見た前方向と上方向のずれ)")]
    private Vector2 _spawnOffset = new Vector2(0.8f, 1.2f);

    public override void Apply(GameObject target)
    {
        if (_projectilePrefab == null)
        {
            Debug.LogError("CSO_ItemEffectHologram: Projectile Prefab が未設定です", this);
            return;
        }

        Transform thrower = target.transform;
        Vector3 position = thrower.position + thrower.forward * _spawnOffset.x + Vector3.up * _spawnOffset.y;
        CS_ThrownHologram projectile = Instantiate(_projectilePrefab, position, thrower.rotation);

        // オンライン時は、クライアントにも弾を出す
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            projectile.NetworkObject.Spawn();
        }

        Vector3 velocity = thrower.forward * _forwardSpeed + Vector3.up * _upwardSpeed;
        projectile.Launch(velocity, target);
    }
}
