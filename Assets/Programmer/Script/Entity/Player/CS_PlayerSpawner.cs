/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 接続したクライアントごとにプレイヤーを生成するクラス
/// </summary>
// ========================================
/*
 * メモ
 * ・生成の権威はサーバーのみが持つ
 *   オンライン: サーバー/ホストが、接続済みのクライアント全員分と、後から接続したクライアントの分を生成する
 *               (CS_ConnectionGateでCreatePlayerObjectをfalseにしているため、ここで生成する)
 *   オフライン(NetworkManagerが動いていないテストシーン): 自分用のプレイヤーを1体だけ生成する
 * ・生成位置は子オブジェクトのTransformを順番に使う(人数が子の数を超えたら先頭から使い回す)
 * ・切断したクライアントのプレイヤーはNetcodeが自動で破棄する
 * ・プレイヤーのプレハブはNetworkPrefabsList(DefaultNetworkPrefabs)に登録しておくこと
 */
// ========================================
public class CS_PlayerSpawner : MonoBehaviour
{
    [SerializeField][Tooltip("生成するプレイヤーのプレハブ")] private NetworkObject _playerPrefab;

    private Transform[] _spawnPoints;
    private int _spawnCount;    // 生成した人数(生成位置の選択に使う)
    private NetworkManager _networkManager;

    // このマシンが生成の権威を持つか(オフライン、またはサーバー/ホスト)
    private static bool HasAuthority =>
        NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

    private static bool IsOnline =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    void Start()
    {
        // クライアントはサーバーが生成したものが同期されてくるのを待つだけでよい
        if (!HasAuthority) return;

        if (_playerPrefab == null)
        {
            Debug.LogError("CS_PlayerSpawner: Player Prefab が未設定です", this);
            return;
        }

        _spawnPoints = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; ++i)
        {
            _spawnPoints[i] = transform.GetChild(i);
        }

        if (!IsOnline)
        {
            Spawn(null);
            return;
        }

        // 既に接続しているクライアント(ホスト自身、マッチングで集まったクライアント)の分を生成する
        _networkManager = NetworkManager.Singleton;
        foreach (ulong clientId in new List<ulong>(_networkManager.ConnectedClientsIds))
        {
            Spawn(clientId);
        }

        // 後から接続したクライアントの分
        _networkManager.OnClientConnectedCallback += OnClientConnected;
    }

    void OnDestroy()
    {
        if (_networkManager != null)
        {
            _networkManager.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Spawn(clientId);
    }

    /// <summary>
    /// プレイヤーを生成する
    /// </summary>
    /// <param name="clientId">操作するクライアントのID(オフラインの場合はnull)</param>
    private void Spawn(ulong? clientId)
    {
        // 同じクライアントのプレイヤーを二重に生成しない
        if (clientId.HasValue && _networkManager.SpawnManager.GetPlayerNetworkObject(clientId.Value) != null) return;

        Transform point = GetNextSpawnPoint();
        NetworkObject player = Instantiate(_playerPrefab, point.position, point.rotation);
        _spawnCount++;

        if (clientId.HasValue)
        {
            player.SpawnAsPlayerObject(clientId.Value, true);
        }
    }

    private Transform GetNextSpawnPoint()
    {
        if (_spawnPoints.Length == 0) return transform;

        return _spawnPoints[_spawnCount % _spawnPoints.Length];
    }
}
