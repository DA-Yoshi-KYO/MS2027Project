/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-10-14 | 4人に満たない枠をNPCで埋めるようにした(秋野)
 * 2026-10-23 | 全員のシーン読み込みが終わってから生成するようにした(秋野、Issue #290)
 * 2026-10-29 | エリア選択で選んだエリアの開始地点から生成するようにした(Issue #342)
 * 2026-10-29 | エリアがある時は、エリア選択の結果が無くても(デバッグ起動)エリアから生成するようにした(四隅の4地点をやめた)
 * 2026-10-30 | エリア選択の画面がエリアの位置を読めるように、_areaSpawnRootsを読むだけのプロパティを追加
 * ================================================ */

using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 接続したクライアントごとにプレイヤーを生成し、4人に満たない枠はNPCで埋めるクラス
/// </summary>
// ========================================
/*
 * メモ
 * ・生成の権威はサーバーのみが持つ
 *   オンライン: サーバー/ホストが、接続済みのクライアント全員分と、後から接続したクライアントの分を生成する
 *               (CS_ConnectionGateでCreatePlayerObjectをfalseにしているため、ここで生成する)
 *   オフライン(NetworkManagerが動いていないテストシーン): 自分用のプレイヤーを1体だけ生成する
 * ・プレイヤー番号(0〜3。HP UIなどが表示先を決めるのに使う)は、空いている一番小さい番号を割り当てる
 *   NetworkObjectをSpawnする前にCS_Player.AssignPlayerNumber()で設定する(スポーン時に全員へ同期される)
 *   生成位置は、下の開始エリアの開始地点(マルチ)か、プレイヤー番号と同じ番号の子オブジェクトのTransform(ソロ。子の数を超えたら折り返す)
 * ・NPC(仕様書「NPC(他プレイヤー)」、Issue #164)
 *   4人で遊ぶ前提なので、人が4人に満たない枠をNPCで埋める(ソロなら3体、3人なら1体)
 *   NPCはサーバーが生成・所有し、判断(CS_PlayerNpcBrain)もサーバーで動く。人と同じプレイヤーのプレハブを使う
 *   性格は_npcPersonalitiesの順に割り当てる(1体目は[0]、2体目は[1]…。足りなければ先頭から使い回す)
 *   後から人が接続して枠が足りない時は、NPCを1体消してその番号を人に譲る
 *   人が切断して枠が空いた時は、またNPCで埋める
 * ・生成するタイミング(Issue #290)
 *   オンラインでは、全員のゲームシーンの読み込みが終わった時(NetworkSceneManager.OnLoadEventCompleted)に生成する
 *   (読み込みの遅いクライアントのプレイヤーが、床ができる前に落ち始めて床の下へ抜けるのを防ぐ)
 *   ホスト1人だけの時はStartより前に完了が届くことがあるため、購読はAwakeで行い、届いていればStartで生成する
 *   シーン管理を使わずに始めた時など完了が届かない場合は、_loadWaitTimeout秒後に生成する
 *   読み込みが時間切れになったクライアントの分も生成する(落ちた場合はCS_Playerの落下の保険でスポーン位置へ戻る)
 * ・開始エリア(仕様「各プレイヤーが開始前に3つのエリアから好きな地点を選びスタート」、Issue #342)
 *   エリア選択シーン(CS_AreaSelectController)を通ってきた時は、CS_SpawnAreaStoreに入っている選んだエリアの開始地点から生成する
 *   開始地点は_areaSpawnRootsの子のTransform(エリアごとに、そのエリアで生成した順に使う。子の数を超えたら折り返す)
 *   NPCと、選択が入っていない人(後から接続した人、デバッグ用の起動シーンから始めた時など)は、ランダムなエリアにする
 *   _areaSpawnRootsが空の時(ソロのPlayerSpawner)は、上のプレイヤー番号ごとの地点(子オブジェクト)を使う
 *   ゲームシーンを直接再生した時はソロになる(CS_GameModeStore)ので、ソロのPlayerSpawnerから生成する
 * ・切断したクライアントのプレイヤーはNetcodeが自動で破棄する
 * ・プレイヤーのプレハブはNetworkPrefabsList(DefaultNetworkPrefabs)に登録しておくこと
 */
// ========================================
public class CS_PlayerSpawner : MonoBehaviour
{
    [SerializeField][Tooltip("生成するプレイヤーのプレハブ")] private NetworkObject _playerPrefab;
    [SerializeField][Tooltip("エリアごとの開始地点の親(エリアの番号順。子を開始地点として使う。空ならプレイヤー番号ごとの地点を使う)")] private Transform[] _areaSpawnRoots;

    [Header("NPC")]
    [SerializeField][Tooltip("4人に満たない枠をNPCで埋める")] private bool _fillWithNpc = true;
    [SerializeField][Tooltip("オフライン(テストシーン)でもNPCで埋める")] private bool _fillWithNpcOffline = false;
    [SerializeField][Tooltip("空き枠に入れるNPCの性格(入れる順)")] private CSO_NpcPersonality[] _npcPersonalities;

    [Header("生成のタイミング")]
    [SerializeField][Tooltip("全員のシーン読み込みの完了を待つ最大秒数(届かない時はこの秒数後に生成する)")] private float _loadWaitTimeout = 10f;

    private const int _playerNumberCap = 4;   // プレイヤー番号(0〜3)の数(最大人数)

    private readonly List<CS_Player> _players = new List<CS_Player>();   // 生成したプレイヤー(人・NPC)
    private Transform[] _spawnPoints;
    private int _spawnCount;    // 生成した人数(番号が足りない時の予備)
    private int[] _areaSpawnCounts; // エリアごとに生成した人数(エリア内の開始地点を順に使うため)
    private NetworkManager _networkManager;

    private bool _isWaitingLoad;        // 全員のシーン読み込みの完了を待っているか(購読中か)
    private bool _isLoadCompleted;      // 全員のシーン読み込みが終わったか
    private bool _isReady;              // Startで生成の準備(生成位置の取得)が終わったか
    private bool _hasSpawnedPlayers;    // 最初の生成を済ませたか

    /// <summary>
    /// エリアごとの開始地点の親(エリアの番号順。エリア選択の画面が、プレハブからエリアの位置を読むのに使う)
    /// </summary>
    public IReadOnlyList<Transform> areaSpawnRoots => _areaSpawnRoots;

    // このマシンが生成の権威を持つか(オフライン、またはサーバー/ホスト)
    private static bool HasAuthority =>
        NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

    private static bool IsOnline =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    // 全員のシーン読み込みの完了は、ホスト1人だとStartより前に届くことがあるため、ここで購読しておく
    void Awake()
    {
        if (!IsOnline || !HasAuthority) return;

        _networkManager = NetworkManager.Singleton;
        if (_networkManager.SceneManager == null || !_networkManager.NetworkConfig.EnableSceneManagement) return;

        _isWaitingLoad = true;
        _networkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
    }

    void Start()
    {
        // クライアントはサーバーが生成したものが同期されてくるのを待つだけでよい
        if (!HasAuthority) return;

        if (_playerPrefab == null)
        {
            Debug.LogError("CS_PlayerSpawner: Player Prefab が未設定です", this);
            StopWaitingLoad();
            return;
        }

        _spawnPoints = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; ++i)
        {
            _spawnPoints[i] = transform.GetChild(i);
        }
        _areaSpawnCounts = new int[_areaSpawnRoots != null ? _areaSpawnRoots.Length : 0];
        _isReady = true;

        if (!IsOnline)
        {
            Spawn(null);
            if (_fillWithNpcOffline) FillWithNpcs();
            return;
        }

        _networkManager = NetworkManager.Singleton;

        // シーン管理を使っていない、またはもう全員の読み込みが終わっていれば、すぐに生成する
        if (!_isWaitingLoad || _isLoadCompleted)
        {
            SpawnPlayers();
            return;
        }

        StartCoroutine(SpawnPlayersAfterTimeout());
    }

    void OnDestroy()
    {
        StopWaitingLoad();

        if (_networkManager != null)
        {
            _networkManager.OnClientConnectedCallback -= OnClientConnected;
            _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // 全員(時間切れのクライアントを除く)のシーン読み込みが終わった時(サーバーで呼ばれる)
    private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        // このスポナーがいるシーン以外の読み込みは関係ない
        if (sceneName != gameObject.scene.name) return;

        _isLoadCompleted = true;
        if (_isReady) SpawnPlayers();
    }

    // 完了が届かないまま_loadWaitTimeout秒経ったら、待つのをやめて生成する
    private IEnumerator SpawnPlayersAfterTimeout()
    {
        yield return new WaitForSecondsRealtime(_loadWaitTimeout);

        if (_hasSpawnedPlayers) yield break;

        Debug.LogWarning("CS_PlayerSpawner: 全員のシーン読み込みの完了が届かないため、待たずにプレイヤーを生成します", this);
        SpawnPlayers();
    }

    private void StopWaitingLoad()
    {
        if (!_isWaitingLoad) return;

        _isWaitingLoad = false;
        if (_networkManager != null && _networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }
    }

    // 接続中の全員の分を生成し、空いた枠をNPCで埋める(オンラインで最初の1回だけ)
    private void SpawnPlayers()
    {
        if (_hasSpawnedPlayers) return;

        _hasSpawnedPlayers = true;
        StopWaitingLoad();

        // 既に接続しているクライアント(ホスト自身、マッチングで集まったクライアント)の分を生成する
        foreach (ulong clientId in new List<ulong>(_networkManager.ConnectedClientsIds))
        {
            Spawn(clientId);
        }

        // 人が4人に満たない枠をNPCで埋める
        FillWithNpcs();

        // 後から接続・切断したクライアントの分
        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        Spawn(clientId);
    }

    // 切断したプレイヤーはNetcodeが破棄するので、破棄が終わってから空いた枠をNPCで埋める
    private void OnClientDisconnected(ulong clientId)
    {
        if (!_networkManager.IsServer) return;

        StartCoroutine(FillWithNpcsNextFrame());
    }

    private IEnumerator FillWithNpcsNextFrame()
    {
        yield return null;

        // 通信の終了中(ゲーム終了など)は埋めない
        if (_networkManager == null || !_networkManager.IsServer || _networkManager.ShutdownInProgress) yield break;

        FillWithNpcs();
    }

    /// <summary>
    /// 人のプレイヤーを生成する
    /// </summary>
    /// <param name="clientId">操作するクライアントのID(オフラインの場合はnull)</param>
    private void Spawn(ulong? clientId)
    {
        // 同じクライアントのプレイヤーを二重に生成しない
        if (clientId.HasValue && _networkManager.SpawnManager.GetPlayerNetworkObject(clientId.Value) != null) return;

        // 枠が埋まっていれば、NPCを1体消して人に譲る
        int number = FindFreeNumber();
        if (number < 0 && TryRemoveNpc())
        {
            number = FindFreeNumber();
        }

        // オフラインの自分はサーバーと同じIDでエリアを選んでいる
        NetworkObject player = Create(number, clientId ?? NetworkManager.ServerClientId);

        if (clientId.HasValue)
        {
            player.SpawnAsPlayerObject(clientId.Value, true);
        }
    }

    /// <summary>
    /// 空いている枠をNPCで埋める(人とNPCを合わせて4人になるまで)
    /// </summary>
    private void FillWithNpcs()
    {
        if (!_fillWithNpc) return;

        int number;
        while ((number = FindFreeNumber()) >= 0)
        {
            SpawnNpc(number);
        }
    }

    private void SpawnNpc(int number)
    {
        int npcIndex = CountNpcs();
        NetworkObject npc = Create(number, null, isNpc: true);

        CS_PlayerNpcBrain brain = npc.GetComponent<CS_PlayerNpcBrain>();
        if (brain != null && _npcPersonalities != null && _npcPersonalities.Length > 0)
        {
            brain.Setup(_npcPersonalities[npcIndex % _npcPersonalities.Length]);
        }

        // NPCはサーバーが所有する
        if (IsOnline)
        {
            npc.Spawn(true);
        }
    }

    // プレイヤーのプレハブを生成し、番号(とNPCの印)を付ける。Spawnは呼び出し側で行う
    // clientIdは選んだエリアを探すのに使う(NPCはnull)
    private NetworkObject Create(int number, ulong? clientId, bool isNpc = false)
    {
        // 番号が足りない時(5人目以降)は、これまで通り生成順で折り返す
        if (number < 0) number = _spawnCount % _playerNumberCap;
        _spawnCount++;

        Transform point = GetAreaSpawnPoint(clientId);
        if (point == null) point = GetSpawnPoint(number);
        NetworkObject instance = Instantiate(_playerPrefab, point.position, point.rotation);

        // NetworkVariableなので、Spawnする前に設定しておく(Spawn時に全員へ同期される)
        CS_Player player = instance.GetComponent<CS_Player>();
        if (player != null)
        {
            player.AssignPlayerNumber(number);
            if (isNpc) player.MarkAsNpc();
            _players.Add(player);
        }

        return instance;
    }

    // NPCを1体消す(番号の大きいものから)。消せたらtrue
    private bool TryRemoveNpc()
    {
        RemoveDestroyedPlayers();

        CS_Player target = null;
        foreach (CS_Player player in _players)
        {
            if (!player.isNpc) continue;
            if (target == null || player.playerNumber > target.playerNumber) target = player;
        }

        if (target == null) return false;

        _players.Remove(target);
        if (IsOnline && target.NetworkObject.IsSpawned)
        {
            target.NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(target.gameObject);
        }
        return true;
    }

    // 空いている一番小さいプレイヤー番号(空きが無ければ-1)
    private int FindFreeNumber()
    {
        RemoveDestroyedPlayers();

        for (int number = 0; number < _playerNumberCap; number++)
        {
            if (!_players.Exists(player => player.playerNumber == number)) return number;
        }
        return -1;
    }

    private int CountNpcs()
    {
        RemoveDestroyedPlayers();
        return _players.FindAll(player => player.isNpc).Count;
    }

    // 切断などで破棄されたプレイヤーを一覧から外す
    private void RemoveDestroyedPlayers()
    {
        _players.RemoveAll(player => player == null);
    }

    // 選んだエリアの開始地点(エリアが無い時はnull)
    private Transform GetAreaSpawnPoint(ulong? clientId)
    {
        if (_areaSpawnCounts.Length == 0) return null;

        // NPCと、選択が入っていない人はランダムなエリアにする
        int area;
        if (!clientId.HasValue || !CS_SpawnAreaStore.TryGetArea(clientId.Value, out area))
        {
            area = Random.Range(0, _areaSpawnCounts.Length);
        }
        area %= _areaSpawnCounts.Length;

        Transform root = _areaSpawnRoots[area];
        if (root == null) return null;
        if (root.childCount == 0) return root;

        // 同じエリアを選んだ人が重ならないように、エリア内の開始地点を順に使う
        Transform point = root.GetChild(_areaSpawnCounts[area] % root.childCount);
        _areaSpawnCounts[area]++;
        return point;
    }

    private Transform GetSpawnPoint(int number)
    {
        if (_spawnPoints.Length == 0) return transform;

        return _spawnPoints[number % _spawnPoints.Length];
    }
}
