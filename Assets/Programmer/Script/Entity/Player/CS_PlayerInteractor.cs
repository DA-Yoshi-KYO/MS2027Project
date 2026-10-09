using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーが近くの操作できるもの(IInteractable、ケアパッケージなど)を、ボタンの長押しで操作するクラス
 * 自分のプレイヤー(Owner)がボタンを押している間、サーバーへ操作の開始・終了を伝える
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・ボタンはアイテム使用ボタン(UseItem: E / RB)を使う
 *   操作できるものの近くで押した時は、アイテムは使わない(CS_PlayerItemSlotがhasInteractTargetを見て使用をやめる)
 * ・流れ(Ownerの手元)
 *   1. 操作できる範囲の中でボタンを押した瞬間に、一番近いものの操作を始める
 *   2. ボタンを離す・動けなくなる(倒れたなど)と、操作をやめる
 *   3. 操作される側が中断した(離れた・ダメージを受けたなど)時は、ボタンを押し直すまで始めない
 *      (IInteractable.IsInteractingがfalseになったら中断とみなす。開始の依頼が届くまでの遅れは待つ)
 * ・開始・終了はサーバーへRPCで伝え、サーバーでIInteractable.BeginInteract / EndInteractを呼ぶ
 *   (オフラインのテストシーンでは、その場で呼ぶ)
 * ・操作できるものはNetworkObjectを持っていること(RPCで相手を指定するため)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerInteractor : NetworkBehaviour
{
    private const float _startTimeout = 1f;   // 開始を依頼してから、相手側で操作が始まるのを待つ時間(秒)

    private CS_Player _player;
    private IInteractable _current;   // 今操作しているもの(Ownerの手元)
    private bool _hasStarted;         // 相手側で操作が始まったのを確認したか(依頼がサーバーに届くまで少しかかる)
    private float _waitTime;          // 開始を依頼してからの時間

    // 今ボタンを押したら操作が始まる相手がいるか(Ownerの手元。CS_PlayerItemSlotがアイテム使用をやめる判定に使う)
    public bool hasInteractTarget => FindTarget() != null;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
    }

    private void Update()
    {
        if (!IsLocalOwner()) return;

        if (_current != null)
        {
            UpdateInteracting();
            return;
        }

        if (!_player.canAct || !_player.useItemAction.WasPressedThisFrame()) return;

        IInteractable target = FindTarget();
        if (target == null) return;

        _current = target;
        _hasStarted = false;
        _waitTime = 0f;
        RequestBegin(target);
    }

    private void OnDisable()
    {
        if (_current != null) StopInteract();
    }

    // 操作中。ボタンを離した・動けなくなったらやめる。相手側で中断された・相手が消えた時は、押し直すまで待つ
    private void UpdateInteracting()
    {
        if (IsGone(_current) || IsInterrupted())
        {
            _current = null;
            return;
        }

        if (_player.canAct && _player.useItemAction.IsPressed()) return;

        StopInteract();
    }

    // 相手側で中断されたか(一度始まったのを確認してから判定する。始まらないまま待ち時間を過ぎた時も中断扱い)
    private bool IsInterrupted()
    {
        bool isInteracting = _current.IsInteracting(gameObject);
        if (isInteracting)
        {
            _hasStarted = true;
            return false;
        }

        if (_hasStarted) return true;

        _waitTime += Time.deltaTime;
        return _waitTime >= _startTimeout;
    }

    private void StopInteract()
    {
        if (!IsGone(_current)) RequestEnd(_current);
        _current = null;
    }

    private IInteractable FindTarget()
    {
        if (!_player.canAct) return null;

        return CS_InteractableRegistry.FindNearest(transform.position, gameObject);
    }

    // ---- サーバーへの依頼 ----

    private void RequestBegin(IInteractable target)
    {
        if (!IsSpawned)
        {
            target.BeginInteract(gameObject);
            return;
        }

        if (target is NetworkBehaviour behaviour) BeginInteractRpc(behaviour.NetworkObject);
    }

    private void RequestEnd(IInteractable target)
    {
        if (!IsSpawned)
        {
            target.EndInteract(gameObject);
            return;
        }

        if (target is NetworkBehaviour behaviour && behaviour.IsSpawned) EndInteractRpc(behaviour.NetworkObject);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void BeginInteractRpc(NetworkObjectReference targetReference)
    {
        if (TryGetInteractable(targetReference, out IInteractable target)) target.BeginInteract(gameObject);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void EndInteractRpc(NetworkObjectReference targetReference)
    {
        if (TryGetInteractable(targetReference, out IInteractable target)) target.EndInteract(gameObject);
    }

    private static bool TryGetInteractable(NetworkObjectReference reference, out IInteractable interactable)
    {
        interactable = null;
        if (!reference.TryGet(out NetworkObject networkObject)) return false;

        interactable = networkObject.GetComponent<IInteractable>();
        return interactable != null;
    }

    // ---- 補助 ----

    // 自分が操作しているプレイヤーか(オフラインのテストシーンでは常にtrue)
    private bool IsLocalOwner()
    {
        if (!IsSpawned) return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;

        return IsOwner;
    }

    // DestroyされたUnityオブジェクトはインターフェース越しだとnullにならないので、Objectとして確認する
    private static bool IsGone(IInteractable interactable)
    {
        return interactable == null || (interactable is Object unityObject && unityObject == null);
    }
}
