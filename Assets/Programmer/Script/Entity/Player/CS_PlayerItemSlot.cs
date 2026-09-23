using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーが持てる携帯型アイテムのスロット(1つ)を管理するクラス
 * ICarriableItemHolderを実装し、CSO_ItemDataCarriableを拾えるようにする
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・スロットは1つ。空いていなければ拾えない(アイテムはフィールドに残る)
 * ・拾得はCS_ItemBase.OnTriggerEnterがサーバー以外の判定を無視するため、
 *   TryStoreItem()は常にサーバー(またはオフライン)でしか呼ばれない
 * ・使用(使用ボタン)はクライアントからサーバーへRPCで依頼し、サーバー側で
 *   CSO_ItemDataCarriable.Activate()を呼んでスロットを空にする
 *   (CS_PlayerAttackなどと同じ「依頼→サーバーで確定」の形)
 * ・held(現在持っているアイテム)はNetworkVariable化していない
 *   (CSO_ItemDataCarriableはアセット参照のため、そのままNetworkVariableには乗らない)
 *   → onItemChangedは自分自身(サーバー/ホスト、またはオフライン)でのHUD表示には使えるが、
 *      リモートクライアントの手元では「自分が何を持っているか」を知る手段がまだない
 *   → 他クライアントにも見せたい場合は、アイテムをIDで持つレジストリ的な仕組みが必要
 *      (ClaudeUsers/ItemSlot実装ガイド.md参照。アイテム担当と要相談)
 * ・使用ボタン(UseItem)はデザイナーからのキーバインド仕様に無かったため、
 *   暫定でF / コントローラーRBに割り当てている(要確認)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerItemSlot : NetworkBehaviour, ICarriableItemHolder
{
    private CS_Player _player;
    private CSO_ItemDataCarriable _heldItem;

    public bool hasItem => _heldItem != null;
    public CSO_ItemDataCarriable heldItem => _heldItem;

    public event Action<CSO_ItemDataCarriable> onItemChanged;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
    }

    private void Update()
    {
        if (!_player.canAct) return;
        if (!hasItem) return;
        if (!_player.useItemAction.WasPressedThisFrame()) return;

        RequestUseItem();
    }

    // ICarriableItemHolder実装。スロットに格納できたらtrueを返す
    public bool TryStoreItem(CSO_ItemDataCarriable item)
    {
        if (IsSpawned && !IsServer) return false;
        if (hasItem) return false;

        _heldItem = item;
        onItemChanged?.Invoke(_heldItem);
        return true;
    }

    // アイテムの使用を依頼する
    private void RequestUseItem()
    {
        // オフライン(テストシーン)では、その場で使用する
        if (!IsSpawned)
        {
            ExecuteUseItem();
            return;
        }

        UseItemRpc();
    }

    // Ownerからサーバーへ、使用の実行を依頼する
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void UseItemRpc()
    {
        ExecuteUseItem();
    }

    // スロットのアイテムを発動して空にする(サーバー、またはオフラインで実行される)
    private void ExecuteUseItem()
    {
        if (!hasItem) return;

        CSO_ItemDataCarriable item = _heldItem;
        _heldItem = null;
        onItemChanged?.Invoke(null);

        item.Activate(gameObject);
    }
}
