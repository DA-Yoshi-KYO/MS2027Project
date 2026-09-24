using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * 悪人をグループ単位でフィールドに生成するクラス
 * フィールド上のグループ数が「プレイヤーの数 + 追加グループ数」を下回らないように維持し、
 * 時間経過でもグループを追加する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・生成の権威はサーバーのみが持つ
 *   オンライン: サーバー/ホスト以外は何もしない(生成したNetworkObjectが同期されてくる)
 *   オフライン(NetworkManagerが動いていないテストシーン): その場で生成する
 * ・スポーン位置はシーン上のCS_VillainSpawnPointを起動時に1回だけ取得する
 *   使用中(グループが残っている)のスポーン位置には生成しない
 * ・1グループの人数は minMembers ～ maxMembers 人(両端を含む)からランダム
 *   各メンバーの種類は villainPrefabs からランダムに選ぶ
 * ・グループのメンバーが全員いなくなったら(撃退・逃走でDestroyされたら)、そのスポーン位置は空く
 * ・生成する悪人のプレハブはNetworkPrefabsList(DefaultNetworkPrefabs)に登録しておくこと
 */
// ========================================

public class CS_VillainSpawner : MonoBehaviour
{
    // 生成済みグループ1つ分の情報
    private class VillainGroup
    {
        private readonly CS_VillainSpawnPoint _spawnPoint;
        private readonly List<NetworkObject> _members = new List<NetworkObject>();

        public CS_VillainSpawnPoint spawnPoint => _spawnPoint;
        public List<NetworkObject> members => _members;

        // Destroyされたメンバーはnull扱いになるので、1人でも残っていれば生存
        public bool isAlive => _members.Exists(member => member != null);

        public VillainGroup(CS_VillainSpawnPoint spawnPoint)
        {
            _spawnPoint = spawnPoint;
        }
    }

    private const float _checkInterval = 0.5f;   // グループ数を確認する間隔(秒)

    [Header("生成する悪人")]
    [SerializeField]
    [Tooltip("生成する悪人の種類。メンバーごとにランダムで選ばれる")]
    private NetworkObject[] _villainPrefabs;

    [Header("グループ")]
    [SerializeField, Min(1)]
    [Tooltip("1グループの最少人数")]
    private int _minMembers = 5;

    [SerializeField, Min(1)]
    [Tooltip("1グループの最多人数")]
    private int _maxMembers = 6;

    [SerializeField, Min(0)]
    [Tooltip("プレイヤーの数に加えて、常に存在させるグループ数")]
    private int _extraGroupCount = 1;

    [Header("時間経過による追加")]
    [SerializeField, Min(0f)]
    [Tooltip("グループを追加する間隔(秒)。0で時間経過による追加をしない")]
    private float _spawnInterval = 60f;

    [SerializeField, Min(0)]
    [Tooltip("時間経過で追加する場合のグループ数の上限")]
    private int _maxGroupCount = 8;

    [Header("オフライン用")]
    [SerializeField, Min(1)]
    [Tooltip("NetworkManagerが動いていないテストシーンで想定するプレイヤーの数")]
    private int _offlinePlayerCount = 1;

    private CS_VillainSpawnPoint[] _spawnPoints;
    private readonly List<VillainGroup> _groups = new List<VillainGroup>();
    private float _checkTimer;
    private float _spawnTimer;
    private bool _isRunning;
    private bool _hasWarnedNoPoint;   // 空きポイント不足の警告を毎回出さないためのフラグ

    public int groupCount => _groups.Count;

    // このマシンが生成の権威を持つか(オフライン、またはサーバー/ホスト)
    private static bool hasAuthority =>
        NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

    // 常に存在させるグループ数(プレイヤーの数 + 追加グループ数)
    private int requiredGroupCount => GetPlayerCount() + _extraGroupCount;

    private void Start()
    {
        // クライアントはサーバーが生成したものが同期されてくるのを待つだけでよい
        if (!hasAuthority) return;

        if (_villainPrefabs == null || _villainPrefabs.Length == 0)
        {
            Debug.LogError("CS_VillainSpawner: Villain Prefabs が未設定です", this);
            return;
        }

        _spawnPoints = FindObjectsByType<CS_VillainSpawnPoint>(FindObjectsSortMode.None);
        if (_spawnPoints.Length == 0)
        {
            Debug.LogError("CS_VillainSpawner: シーンにCS_VillainSpawnPointがありません", this);
            return;
        }

        _isRunning = true;
        FillRequiredGroups();
    }

    private void Update()
    {
        if (!_isRunning) return;

        _spawnTimer += Time.deltaTime;
        _checkTimer += Time.deltaTime;
        if (_checkTimer < _checkInterval) return;
        _checkTimer = 0f;

        RemoveDeadGroups();
        FillRequiredGroups();
        SpawnByInterval();
    }

    private void OnValidate()
    {
        _maxMembers = Mathf.Max(_minMembers, _maxMembers);
    }

    // 全員いなくなったグループを外し、スポーン位置を空ける
    private void RemoveDeadGroups()
    {
        for (int i = _groups.Count - 1; i >= 0; i--)
        {
            if (_groups[i].isAlive) continue;

            _groups[i].spawnPoint.isOccupied = false;
            _groups.RemoveAt(i);
        }
    }

    // 最低グループ数を下回っていたら、空きがある限り生成する
    private void FillRequiredGroups()
    {
        while (_groups.Count < requiredGroupCount)
        {
            if (!TrySpawnGroup()) return;
        }
    }

    // 一定時間ごとに、上限までグループを1つ追加する
    private void SpawnByInterval()
    {
        if (_spawnInterval <= 0f) return;
        if (_spawnTimer < _spawnInterval) return;

        _spawnTimer = 0f;
        if (_groups.Count >= _maxGroupCount) return;

        TrySpawnGroup();
    }

    // 空いているスポーン位置からランダムに1つ選び、グループを生成する
    private bool TrySpawnGroup()
    {
        CS_VillainSpawnPoint point = GetRandomFreePoint();
        if (point == null)
        {
            if (!_hasWarnedNoPoint)
            {
                Debug.LogWarning("CS_VillainSpawner: 空いているスポーン位置が足りないため、グループを生成できません", this);
                _hasWarnedNoPoint = true;
            }
            return false;
        }

        _hasWarnedNoPoint = false;
        _groups.Add(SpawnGroup(point));
        return true;
    }

    private VillainGroup SpawnGroup(CS_VillainSpawnPoint point)
    {
        VillainGroup group = new VillainGroup(point);
        int memberCount = Random.Range(_minMembers, _maxMembers + 1);

        for (int i = 0; i < memberCount; i++)
        {
            Vector3 position = point.GetMemberPosition(i, memberCount);
            group.members.Add(SpawnVillain(position, point.transform.position));
        }

        point.isOccupied = true;
        return group;
    }

    // 悪人を1人生成する。グループの中心(犯罪を行う場所)を向かせる
    private NetworkObject SpawnVillain(Vector3 position, Vector3 center)
    {
        NetworkObject prefab = _villainPrefabs[Random.Range(0, _villainPrefabs.Length)];

        Vector3 lookDirection = center - position;
        lookDirection.y = 0f;
        Quaternion rotation = lookDirection.sqrMagnitude > 0f ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

        NetworkObject villain = Instantiate(prefab, position, rotation);
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            villain.Spawn(true);
        }
        return villain;
    }

    private CS_VillainSpawnPoint GetRandomFreePoint()
    {
        List<CS_VillainSpawnPoint> freePoints = new List<CS_VillainSpawnPoint>();
        foreach (CS_VillainSpawnPoint point in _spawnPoints)
        {
            if (point != null && !point.isOccupied) freePoints.Add(point);
        }

        if (freePoints.Count == 0) return null;
        return freePoints[Random.Range(0, freePoints.Count)];
    }

    private int GetPlayerCount()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return _offlinePlayerCount;

        return NetworkManager.Singleton.ConnectedClientsIds.Count;
    }
}
