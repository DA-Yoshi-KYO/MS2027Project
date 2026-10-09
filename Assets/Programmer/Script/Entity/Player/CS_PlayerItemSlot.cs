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
 * ・所持アイテムの同期(HUDのアイコン表示と、リモートクライアントの使用ボタン判定に使う)
 *   CSO_ItemDataCarriableはアセット参照のため、そのままNetworkVariableには乗らない
 *   → Inspectorの_syncableItems(持てるアイテムの一覧)の何番目かをNetworkVariable<int>で同期し、
 *      各クライアントは番号からアセットを引き直す(-1 = 何も持っていない)
 *   → 新しい携帯型アイテムを追加したら、_syncableItemsにも登録すること
 *      (未登録のアイテムはホスト・オフラインでは使えるが、リモートクライアントでは「持っていない」扱いになる)
 *   → onItemChangedは全クライアントで発生する(HUD表示用)。効果の発動はサーバーのみ
 * ・使用ボタンはケアパッケージなどの操作(CS_PlayerInteractor)と同じボタン。操作できるものの近くでは使用しない
 * ・使用ボタン(UseItem)はデザイナーからのキーバインド仕様に無かったため、
 *   暫定でF / コントローラーRBに割り当てている(要確認)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerItemSlot : NetworkBehaviour, ICarriableItemHolder
{
    private const int _noItemIndex = -1;

    [Header("同期")]
    [SerializeField] private CSO_ItemDataCarriable[] _syncableItems;   // 持てるアイテムの一覧(この番号で所持アイテムを同期する)

    private CS_Player _player;
    private CS_PlayerInteractor _interactor;   // ケアパッケージなどの操作(同じボタンを使う)。付いていなければnull
    private CSO_ItemDataCarriable _heldItem;    // サーバー(またはオフライン)が持つ本体

    // 書き込みはサーバーのみ。_syncableItemsの何番目を持っているか(-1 = 何も持っていない)
    private readonly NetworkVariable<int> _heldItemIndex = new NetworkVariable<int>(_noItemIndex);

    public bool hasItem => heldItem != null;
    public CSO_ItemDataCarriable heldItem => IsSpawned && !IsServer ? FindSyncableItem(_heldItemIndex.Value) : _heldItem;

    public event Action<CSO_ItemDataCarriable> onItemChanged;   // 全クライアントで発生する(HUD表示用)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _interactor = GetComponent<CS_PlayerInteractor>();
    }

    public override void OnNetworkSpawn()
    {
        _heldItemIndex.OnValueChanged += HandleHeldItemIndexChanged;
    }

    public override void OnNetworkDespawn()
    {
        _heldItemIndex.OnValueChanged -= HandleHeldItemIndexChanged;
    }

    private void Update()
    {
        if (!_player.canAct) return;
        if (!hasItem) return;
        if (!_player.useItemAction.WasPressedThisFrame()) return;
        if (_interactor != null && _interactor.hasInteractTarget) return;   // 操作できるもの(ケアパッケージなど)の近くでは、アイテムを使わず操作を優先する

        RequestUseItem();
    }

    // ICarriableItemHolder実装。スロットに格納できたらtrueを返す
    public bool TryStoreItem(CSO_ItemDataCarriable item)
    {
        if (IsSpawned && !IsServer) return false;
        if (hasItem) return false;

        SetHeldItem(item);
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
        SetHeldItem(null);

        item.Activate(gameObject);
    }

    // 所持アイテムを変更し、同期用の番号も更新する(サーバー、またはオフラインで実行される)
    private void SetHeldItem(CSO_ItemDataCarriable item)
    {
        _heldItem = item;

        if (IsSpawned)
        {
            _heldItemIndex.Value = FindSyncableIndex(item);
        }

        // サーバー・オフラインは本体を直接通知する(未登録のアイテムでも自分のHUDには出せるように)
        onItemChanged?.Invoke(_heldItem);
    }

    // リモートクライアントで、同期された番号が変わった時にHUDへ通知する
    private void HandleHeldItemIndexChanged(int previous, int current)
    {
        if (IsServer) return;

        onItemChanged?.Invoke(FindSyncableItem(current));
    }

    private int FindSyncableIndex(CSO_ItemDataCarriable item)
    {
        if (item == null) return _noItemIndex;

        int index = _syncableItems == null ? _noItemIndex : Array.IndexOf(_syncableItems, item);
        if (index < 0)
        {
            Debug.LogWarning($"CS_PlayerItemSlot: {item.name} がSyncable Itemsに登録されていないため、リモートクライアントには同期されません", this);
        }
        return index;
    }

    private CSO_ItemDataCarriable FindSyncableItem(int index)
    {
        if (_syncableItems == null) return null;
        if (index < 0 || index >= _syncableItems.Length) return null;

        return _syncableItems[index];
    }
}
