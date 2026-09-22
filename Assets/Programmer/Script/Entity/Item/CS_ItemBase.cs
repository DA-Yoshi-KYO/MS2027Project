/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * ================================================ */

using UnityEngine;

/*
 * フィールド上に配置されるアイテムの実体
 * 触れた相手にCSO_ItemDataの処理を委譲し、消費されたら自身を破棄する
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・コライダーはIs Triggerをオンにしておく(Rigidbodyはプレイヤー側が持っている)
 * ・拾える対象はPickup Layersで絞る(現状プレイヤー側にタグ等の目印がないため)
 * ・ネットワーク対戦での拾得の権威(誰が確定させるか)は未対応
 */
// ========================================

[RequireComponent(typeof(Collider))]
public class CS_ItemBase : MonoBehaviour
{
    [SerializeField][Tooltip("このアイテムのデータ")] private CSO_ItemData _itemData;
    [SerializeField][Tooltip("拾える対象のレイヤー")] private LayerMask _pickupLayers;

    public CSO_ItemData itemData => _itemData;

    private void OnTriggerEnter(Collider other)
    {
        if (_itemData == null) return;
        if ((_pickupLayers.value & (1 << other.gameObject.layer)) == 0) return;

        if (_itemData.Use(other.gameObject))
        {
            Destroy(gameObject);
        }
    }
}
