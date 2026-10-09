using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの所持アイテムのアイコンをアイテムスロットUI(CS_UIItemSlotModel)へ反映するクラス
 * CS_PlayerItemSlotから所持アイテムの変化を受け取り、UI側のModelにアイコンを渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・UIの使い方自体はClaudeDocs/UIModelの使い方.md参照。ここでは実際のプレイヤーへの組み込みのみ行う
 * ・アイテムスロットUIは画面に1つだけのため、自分(Owner)のプレイヤーだけがBindする
 *   (他人のプレイヤーがBindすると、自分のスロットが他人のアイテムで上書きされてしまう)
 * ・アイコンはアイテムデータ(CSO_ItemData.icon)のものを使う。アイコン未設定のアイテムはスロットが空の表示になる
 * ・所持アイテムはCS_PlayerItemSlotが同期しているため、リモートクライアントでも自分のアイテムが表示される
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く(自分のプレイヤー扱い)
 */
// ========================================

[RequireComponent(typeof(CS_PlayerItemSlot))]
public class CS_PlayerItemSlotUI : NetworkBehaviour
{
    private CS_PlayerItemSlot _itemSlot;
    private CS_UIItemSlotModel _itemModel;

    private void Awake()
    {
        _itemSlot = GetComponent<CS_PlayerItemSlot>();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        BindItemSlotUI();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        BindItemSlotUI();
    }

    public override void OnNetworkDespawn()
    {
        UnbindItemSlotUI();
    }

    public override void OnDestroy()
    {
        UnbindItemSlotUI();
        base.OnDestroy();
    }

    // スロットUIのModelを作り、現在の所持アイテムのアイコンで公開する
    private void BindItemSlotUI()
    {
        _itemModel = new CS_UIItemSlotModel(GetIcon(_itemSlot.heldItem));
        _itemModel.Bind();

        _itemSlot.onItemChanged += HandleItemChanged;
    }

    private void UnbindItemSlotUI()
    {
        if (_itemModel == null) return;

        _itemSlot.onItemChanged -= HandleItemChanged;
        _itemModel.Dispose();
        _itemModel = null;
    }

    // 拾った・使った時にアイコンを差し替える(nullならスロットは非表示)
    private void HandleItemChanged(CSO_ItemDataCarriable item)
    {
        _itemModel.SetIcon(GetIcon(item));
    }

    private static Sprite GetIcon(CSO_ItemDataCarriable item)
    {
        return item != null ? item.icon : null;
    }
}
