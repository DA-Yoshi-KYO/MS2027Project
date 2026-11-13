/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 位相置換機の実体
/// 使われてから一定時間後に、使用者とランダムな他のプレイヤーの位置を入れ替える
/// </summary>
/// <remarks>
/// ・CSO_ItemEffectPhaseSwapが生成する(サーバー、またはオフラインで実行される)
/// ・入れ替える相手は、入れ替える時(使われた時ではなく、時間が過ぎた時)に決める。生きている使用者以外のプレイヤー(人・NPC)からランダムに選ぶ
/// ・時間が過ぎた時に、使用者が死亡していたり、相手がいなければ、何も起きない
/// ・プレイヤーの位置は、操作している側が権威を持つ(NetworkTransformのAuthority Mode: Owner)ので、サーバーが直接動かせない
///   そのため、全員へRpcで知らせ、そのプレイヤーを操作している側(NPCならサーバー)だけが、自分のプレイヤーを動かす
/// ・入れ替えるのは位置だけで、向きは変えない
/// ・NetworkPrefabsList(DefaultNetworkPrefabs)にこのプレハブを登録しておくこと
/// </remarks>
[RequireComponent(typeof(NetworkObject))]
public class CS_PhaseSwap : NetworkBehaviour
{
    // 入れ替えが終わってから、このオブジェクトを消すまでの時間(秒)(Rpcが全員に届いてから消すため)
    private const float _removeDelay = 1.0f;

    // 入れ替える相手の候補を集める際に使い回すリスト
    private static readonly List<CS_Player> _candidates = new List<CS_Player>();

    // 位置を入れ替える使用者
    private CS_Player _user = null;

    // 入れ替えるまでの残り時間
    private float _remainingTime = 0.0f;

    // 使用者が決まっているか(Beginが呼ばれたか)
    private bool _isArmed = false;

    // 入れ替えの処理(中止も含む)を終えたか
    private bool _isFinished = false;

    // 入れ替えを終えてから、消すまでの残り時間
    private float _removeTimer = 0.0f;

    /// <summary>
    /// 入れ替えを始めるメソッド(サーバー、またはオフラインで呼ぶこと)
    /// </summary>
    /// <param name="user">位置を入れ替える使用者</param>
    /// <param name="delay">使ってから入れ替えるまでの時間(秒)</param>
    public void Begin(CS_Player user, float delay)
    {
        _user = user;
        _remainingTime = delay;
        _isArmed = true;
    }

    private void Update()
    {
        // 入れ替えと消去は、サーバー(またはオフライン)だけで行う
        if (!_isArmed || (IsSpawned && !IsServer)) return;

        if (_isFinished)
        {
            _removeTimer -= Time.deltaTime;
            if (_removeTimer <= 0.0f) Remove();
            return;
        }

        _remainingTime -= Time.deltaTime;
        if (_remainingTime > 0.0f) return;

        Swap();
        _isFinished = true;
        _removeTimer = _removeDelay;
    }

    /// <summary>
    /// 使用者と、ランダムな他のプレイヤーの位置を入れ替えるメソッド(使用者が死亡している・相手がいない時は何もしない)
    /// </summary>
    private void Swap()
    {
        CS_Player target = PickTarget();
        if (!IsAlive(_user) || target == null) return;

        Vector3 userPosition = _user.transform.position;
        Vector3 targetPosition = target.transform.position;

        // オフラインは、その場で動かす
        if (!IsSpawned)
        {
            Teleport(_user, targetPosition);
            Teleport(target, userPosition);
            return;
        }

        SwapRpc(_user.NetworkObjectId, targetPosition, target.NetworkObjectId, userPosition);
    }

    /// <summary>
    /// 入れ替える相手を、生きている使用者以外のプレイヤーからランダムに選ぶメソッド
    /// </summary>
    /// <returns>選んだプレイヤー(いなければnull)</returns>
    private CS_Player PickTarget()
    {
        _candidates.Clear();
        foreach (CS_Player player in CS_Player.activePlayers)
        {
            if (player != _user && IsAlive(player)) _candidates.Add(player);
        }

        return _candidates.Count > 0 ? _candidates[Random.Range(0, _candidates.Count)] : null;
    }

    /// <summary>
    /// プレイヤーが生きているかを判定するメソッド
    /// </summary>
    private static bool IsAlive(CS_Player player)
    {
        return player != null && player.TryGetComponent(out CS_PlayerHealth health) && !health.isDead;
    }

    /// <summary>
    /// 2人のプレイヤーを、それぞれの行き先へ動かすよう、全員へ知らせるメソッド
    /// </summary>
    [Rpc(SendTo.Everyone)]
    private void SwapRpc(ulong firstId, Vector3 firstDestination, ulong secondId, Vector3 secondDestination)
    {
        TeleportIfOwner(firstId, firstDestination);
        TeleportIfOwner(secondId, secondDestination);
    }

    /// <summary>
    /// 指定したプレイヤーを、自分が操作している(権威を持っている)場合だけ、行き先へ動かすメソッド
    /// </summary>
    private static void TeleportIfOwner(ulong networkObjectId, Vector3 destination)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject)) return;
        if (!networkObject.IsOwner || !networkObject.TryGetComponent(out CS_Player player)) return;

        Teleport(player, destination);
    }

    /// <summary>
    /// プレイヤーを行き先へ動かすメソッド(他の人の画面でも、補間せずにその場へ移す)
    /// </summary>
    private static void Teleport(CS_Player player, Vector3 destination)
    {
        player.SnapPosition(destination);

        if (player.IsSpawned && player.TryGetComponent(out NetworkTransform networkTransform))
        {
            networkTransform.Teleport(destination, player.transform.rotation, player.transform.localScale);
        }
    }

    /// <summary>
    /// このオブジェクトを消すメソッド(オンラインならネットワーク越しにDespawn、オフラインならDestroy)
    /// </summary>
    private void Remove()
    {
        _isArmed = false;
        if (IsSpawned)
        {
            NetworkObject.Despawn();
            return;
        }

        Destroy(gameObject);
    }
}
