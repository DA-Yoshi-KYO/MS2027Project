/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/*
 * フィールド上に配置されるアイテムの実体
 * 触れた相手にCSO_ItemDataの処理を委譲し、消費されたら自身を破棄(Despawn)する
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・コライダーはIs Triggerをオンにしておく(Rigidbodyはプレイヤー側が持っている)
 * ・拾える対象はPickup Layersで絞る(現状プレイヤー側にタグ等の目印がないため)
 * ・拾得の確定はサーバーのみが行う(クライアントの判定は無視する)
 *   オンライン: サーバー/ホストのみ処理する(IsServer)
 *   オフライン(NetworkManagerが動いていないテストシーン): その場で処理する
 * ・NetworkPrefabsList(DefaultNetworkPrefabs)にこのプレハブを登録しておくこと
 */
// ========================================

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class CS_ItemBase : NetworkBehaviour
{
    [SerializeField][Tooltip("このアイテムのデータ")] private CSO_ItemData _itemData;
    [SerializeField][Tooltip("拾える対象のレイヤー")] private LayerMask _pickupLayers;

    public CSO_ItemData itemData => _itemData;

    private void OnTriggerEnter(Collider other)
    {
        if (_itemData == null) return;
        if ((_pickupLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // オンライン時は、サーバー/ホスト以外の判定を無視する(拾得はサーバーだけが確定させる)
        if (IsSpawned && !IsServer) return;

        if (_itemData.OnPickup(other.gameObject))
        {
            Consume();
        }
    }

    // フィールドから自身を取り除く(オンラインならネットワーク越しにDespawn、オフラインならDestroy)
    private void Consume()
    {
        if (IsSpawned)
        {
            NetworkObject.Despawn();
            return;
        }

        Destroy(gameObject);
    }
}
