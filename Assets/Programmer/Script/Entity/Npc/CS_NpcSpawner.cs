using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * 人のプレイヤーが4人に満たない時、空いている枠(プレイヤー番号)にNPCを生成するクラス
 * NPCは人のプレイヤーと同じプレハブを使い、頭脳(CS_NpcBrain)を付けて入力の出どころにする
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・生成の権威はサーバーのみ(オフラインのテストシーンでは自分で生成する)。NPCはサーバーが所有し、サーバーが動かす
 * ・人のプレイヤーはCS_PlayerSpawnerが生成する。それを待つため、_startDelay秒後に空き枠を調べて埋める
 *   空き枠 = プレイヤー番号(0〜3)のうち、どのプレイヤーにも使われていない番号
 * ・どの枠にどの性格のNPCを入れるかは_slotPersonalities(要素番号 = プレイヤー番号)で決める
 *   空(None)の枠にはNPCを入れない
 * ・NPCにもプレイヤー番号を割り当てるので、HP UI・スコア・リザルトでは人のプレイヤーと同じように扱われる
 *   NPCかどうかはCS_Player.isNpc、性格はCS_NpcBrainで判別できる
 * ・後から人が接続した場合: CS_PlayerSpawnerがその人に番号を割り当てた後、同じ番号のNPCがいれば消して枠を譲る
 * ・生成位置は_spawnPointsの子のTransformを、プレイヤー番号の順に使う(CS_PlayerSpawnerのオブジェクトを指定すれば同じ位置を使える)
 * ・プレイヤーのプレハブはNetworkPrefabsListに登録済みのものを使う(人と同じプレハブ)
 */
// ========================================

public class CS_NpcSpawner : MonoBehaviour
{
    private const int _maxPlayers = 4;  // プレイヤー番号(0〜3)の数

    [SerializeField][Tooltip("生成するプレイヤーのプレハブ(人と同じもの)")] private NetworkObject _playerPrefab;
    [SerializeField][Tooltip("生成位置の親(子のTransformを番号順に使う。空なら自分の子)")] private Transform _spawnPoints;
    [SerializeField][Tooltip("枠(プレイヤー番号)ごとのNPCの性格。空の枠にはNPCを入れない")]
    private CSO_NpcPersonality[] _slotPersonalities = new CSO_NpcPersonality[_maxPlayers];
    [SerializeField][Tooltip("人のプレイヤーの生成を待つ時間(秒)")] private float _startDelay = 0.5f;

    private readonly List<CS_Player> _npcs = new List<CS_Player>();
    private NetworkManager _networkManager;

    // このマシンが生成の権威を持つか(オフライン、またはサーバー/ホスト)
    private static bool HasAuthority =>
        NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

    private static bool IsOnline =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    private void Start()
    {
        if (!HasAuthority) return;

        if (_playerPrefab == null)
        {
            Debug.LogError("CS_NpcSpawner: Player Prefab が未設定です", this);
            return;
        }

        if (_spawnPoints == null) _spawnPoints = transform;

        if (IsOnline)
        {
            _networkManager = NetworkManager.Singleton;
            _networkManager.OnClientConnectedCallback += HandleClientConnected;
        }

        StartCoroutine(FillEmptySlotsAfterDelay());
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
        {
            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private IEnumerator FillEmptySlotsAfterDelay()
    {
        yield return new WaitForSeconds(_startDelay);

        FillEmptySlots();
    }

    // 使われていないプレイヤー番号に、設定された性格のNPCを生成する
    private void FillEmptySlots()
    {
        HashSet<int> usedNumbers = new HashSet<int>();
        foreach (CS_Player player in FindObjectsByType<CS_Player>(FindObjectsSortMode.None))
        {
            usedNumbers.Add(player.playerNumber);
        }

        for (int number = 0; number < _maxPlayers; number++)
        {
            if (usedNumbers.Contains(number)) continue;
            if (number >= _slotPersonalities.Length || _slotPersonalities[number] == null) continue;

            SpawnNpc(number, _slotPersonalities[number]);
        }
    }

    private void SpawnNpc(int playerNumber, CSO_NpcPersonality personality)
    {
        Transform point = GetSpawnPoint(playerNumber);
        NetworkObject npcObject = Instantiate(_playerPrefab, point.position, point.rotation);
        npcObject.name = $"NPC_{playerNumber}_{personality.displayName}";

        // 頭脳はサーバー(またはオフライン)にだけ付ける。Spawn前に設定しておく
        npcObject.gameObject.AddComponent<CS_NpcBrain>().Setup(personality);

        // NetworkVariableなので、Spawnする前に設定しておく(Spawn時に全員へ同期される)
        CS_Player npc = npcObject.GetComponent<CS_Player>();
        npc.AssignPlayerNumber(playerNumber);
        npc.AssignAsNpc();
        _npcs.Add(npc);

        if (IsOnline)
        {
            npcObject.Spawn(true);
        }
    }

    // 後から人が接続した時、CS_PlayerSpawnerがその人に番号を割り当てるのを待ってから、同じ番号のNPCを消す
    private void HandleClientConnected(ulong clientId)
    {
        StartCoroutine(YieldSlotAfterFrame());
    }

    private IEnumerator YieldSlotAfterFrame()
    {
        yield return null;

        HashSet<int> humanNumbers = new HashSet<int>();
        foreach (CS_Player player in FindObjectsByType<CS_Player>(FindObjectsSortMode.None))
        {
            if (!player.isNpc) humanNumbers.Add(player.playerNumber);
        }

        for (int i = _npcs.Count - 1; i >= 0; i--)
        {
            CS_Player npc = _npcs[i];
            if (npc != null && !humanNumbers.Contains(npc.playerNumber)) continue;

            _npcs.RemoveAt(i);
            if (npc == null) continue;

            DespawnNpc(npc);
        }
    }

    private void DespawnNpc(CS_Player npc)
    {
        NetworkObject npcObject = npc.GetComponent<NetworkObject>();
        if (npcObject.IsSpawned)
        {
            npcObject.Despawn(true);
            return;
        }

        Destroy(npc.gameObject);
    }

    private Transform GetSpawnPoint(int playerNumber)
    {
        if (_spawnPoints.childCount == 0) return _spawnPoints;

        return _spawnPoints.GetChild(playerNumber % _spawnPoints.childCount);
    }
}
