using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

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
 * ・ゲームの経過時間による変化(timeScaling)
 *   設定されていれば、生成時の経過時間の段階で、1グループの人数・HP・犯罪完遂時間・攻撃力を決める
 *   (HPなどは生成する悪人のCS_VillainStatsに、Spawnより前に設定する)
 *   段階が変わったら、既にフィールドにいる悪人のHP上限・犯罪完遂時間・攻撃力も変える(グループの人数は変えない)
 *   HP上限が増えた分は現在HPも増やす(受けたダメージはそのまま残る)
 *   経過時間はCS_TimerController(ゲームのタイマー)から取る。シーンに無ければ、スポナーの起動からの時間を使う
 * ・timeScalingが未設定なら、1グループの人数は minMembers ～ maxMembers 人(両端を含む)からランダム
 *   各メンバーの種類は villainPrefabs からランダムに選ぶ
 * ・グループのうち同時に攻撃してくるのは maxAttackersPerGroup 人まで(残りは様子見。CS_VillainGroup参照)
 * ・グループのメンバーが全員いなくなったら(撃退・逃走でDestroyされたら)、そのスポーン位置は空く
 * ・グループがいなくなった(全員撃退された、または犯罪を完遂して逃走した)位置には、respawnCooldown 秒間リスポーンしない
 *   ただし「プレイヤーの数 + 追加グループ数」を保てない場合は、待ち中の位置にも生成する
 *   (待ち中の位置からは、待ちが早く終わる位置を選ぶ)
 *   時間経過による追加では、待ち中の位置には生成しない
 * ・ランダムイベント(大量発生)用に、スポーン位置を使わずにグループを生成する SpawnEventGroup がある
 *   イベントのグループは通常のグループ数に数えず、犯罪も進めない(時間経過の段階の反映は通常と同じ)
 * ・グループ単位の犯罪の進行は、生成したCS_VillainGroupのTickを毎フレーム呼んで進める
 *   進行度はスポーン位置の犯罪完遂ゲージ(CS_VillainCrimeGauge)にも毎フレーム渡す(付いていなければ何もしない)
 * ・生成する悪人のプレハブはNetworkPrefabsList(DefaultNetworkPrefabs)に登録し、CS_VillainCrimeを付けておくこと
 */
// ========================================

public class CS_VillainSpawner : MonoBehaviour
{
    private const float _checkInterval = 0.5f;   // グループ数を確認する間隔(秒)
    private const float _navMeshSampleRadius = 2f;   // イベントの生成位置の近くでNavMeshを探す半径(m)
    private const float _bodyCenterHeight = 1f;      // 足元から悪人の体の中心(transform.position)までの高さ(m)

    [Header("生成する悪人")]
    [SerializeField]
    [Tooltip("生成する悪人の種類。メンバーごとにランダムで選ばれる")]
    private NetworkObject[] _villainPrefabs;

    [Header("時間経過による変化")]
    [SerializeField]
    [Tooltip("経過時間ごとの人数・HP・犯罪完遂時間(DB_VillainTimeScaling)。未設定なら下の最少・最多人数を使う")]
    private CSO_VillainTimeScaling _timeScaling;

    [Header("グループ")]
    [SerializeField, Min(1)]
    [Tooltip("1グループの最少人数(Time Scalingが未設定の時だけ使う)")]
    private int _minMembers = 5;

    [SerializeField, Min(1)]
    [Tooltip("1グループの最多人数(Time Scalingが未設定の時だけ使う)")]
    private int _maxMembers = 6;

    [SerializeField, Min(1)]
    [Tooltip("グループのうち、同時に攻撃してくる人数。残りのメンバーは距離を取って様子を見る")]
    private int _maxAttackersPerGroup = 2;

    [SerializeField, Min(0)]
    [Tooltip("プレイヤーの数に加えて、常に存在させるグループ数")]
    private int _extraGroupCount = 1;

    [SerializeField, Min(0f)]
    [Tooltip("グループがいなくなった(全滅・犯罪完遂)位置に、次のグループを生成しない時間(秒)。最低グループ数を保てない場合は例外")]
    private float _respawnCooldown = 20f;

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
    private readonly List<CS_VillainGroup> _groups = new List<CS_VillainGroup>();
    private readonly List<CS_VillainGroup> _eventGroups = new List<CS_VillainGroup>();   // ランダムイベントで生成したグループ
    private float _checkTimer;
    private float _spawnTimer;
    private bool _isRunning;
    private bool _hasWarnedNoPoint;   // 空きポイント不足の警告を毎回出さないためのフラグ
    private CS_TimerController _timer;   // ゲームのタイマー(経過時間の取得用)。テストシーンなどで無ければnull
    private float _startTime;            // スポナーが動き始めた時刻(タイマーが無い時の経過時間に使う)
    private CSO_VillainTimeScaling.Stage _currentStage;   // 今の経過時間の段階(変わったら既にいる悪人にも反映する)

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

        if (!HasValidPrefabs()) return;

        _spawnPoints = FindObjectsByType<CS_VillainSpawnPoint>(FindObjectsSortMode.None);
        if (_spawnPoints.Length == 0)
        {
            Debug.LogError("CS_VillainSpawner: シーンにCS_VillainSpawnPointがありません", this);
            return;
        }

        _timer = FindAnyObjectByType<CS_TimerController>();
        _startTime = Time.time;

        _isRunning = true;
        FillRequiredGroups();
    }

    private void Update()
    {
        if (!_isRunning) return;

        // 犯罪の進行は毎フレーム行う
        foreach (CS_VillainGroup group in _groups)
        {
            group.Tick(Time.deltaTime);

            // グループの進行度を、スポーン位置の犯罪完遂ゲージへ渡す(完遂したらゲージは消す)
            SetGauge(group.spawnPoint, group.crimeProgress, !group.isCrimeCompleted);
        }

        _spawnTimer += Time.deltaTime;
        _checkTimer += Time.deltaTime;
        if (_checkTimer < _checkInterval) return;
        _checkTimer = 0f;

        RemoveDeadGroups();
        UpdateStage();
        FillRequiredGroups();
        SpawnByInterval();
    }

    // 経過時間の段階が変わったら、既にフィールドにいる悪人のステータスを新しい段階の値にする
    private void UpdateStage()
    {
        if (_timeScaling == null) return;

        CSO_VillainTimeScaling.Stage stage = _timeScaling.GetStage(GetElapsedTime());
        if (stage == _currentStage) return;

        _currentStage = stage;
        ApplyStageToGroups(_groups, stage);
        ApplyStageToGroups(_eventGroups, stage);
    }

    private static void ApplyStageToGroups(List<CS_VillainGroup> groups, CSO_VillainTimeScaling.Stage stage)
    {
        foreach (CS_VillainGroup group in groups)
        {
            if (!group.usesTimeScaling) continue;   // レイドのボスなど、固定のステータスのグループ

            foreach (CS_VillainCrime member in group.members)
            {
                if (member != null) ApplyStage(member, stage);
            }
        }
    }

    private static void ApplyStage(CS_VillainCrime member, CSO_VillainTimeScaling.Stage stage)
    {
        if (!member.TryGetComponent(out CS_VillainStats stats)) return;

        float previousMaxHp = stats.maxHp;
        stats.UpdateSpawnOverrides(stage.maxHp, stage.attackPower, stage.crimeCompleteTime);

        // HP上限が増えた分だけ現在HPも増やす(受けたダメージはそのまま残す)
        if (member.TryGetComponent(out CS_VillainHealth health)) health.ApplyMaxHpChange(previousMaxHp);
    }

    private void OnValidate()
    {
        _maxMembers = Mathf.Max(_minMembers, _maxMembers);
    }

    // 全員いなくなったグループを外し、スポーン位置を空ける
    // いなくなった(全滅・犯罪完遂の逃走)グループの位置は、しばらくリスポーンを待たせる
    private void RemoveDeadGroups()
    {
        for (int i = _groups.Count - 1; i >= 0; i--)
        {
            CS_VillainGroup group = _groups[i];
            if (group.isAlive) continue;

            group.spawnPoint.isOccupied = false;
            group.spawnPoint.StartCooldown(_respawnCooldown);
            SetGauge(group.spawnPoint, 0f, false);   // ゲージを隠して0に戻す
            _groups.RemoveAt(i);
        }

        // イベントのグループはスポーン位置を持たないので、一覧から外すだけ
        _eventGroups.RemoveAll(group => !group.isAlive);
    }

    // ランダムイベントから呼ぶ(サーバー、またはオフライン)。スポーン位置を使わずに、centerの周りにグループを生成する
    // 通常のグループ数(プレイヤーの数 + 追加グループ数)には数えず、犯罪も進めない(完遂はイベント側でCompleteCrimeを呼ぶ)
    // memberCount: 1グループの人数。0以下なら、経過時間の段階の人数(段階が無ければ minMembers 〜 maxMembers)
    // prefab: 生成する悪人のプレハブ(nullなら villainPrefabs からランダム。レイドのボスなどは専用のプレハブを渡す)
    // usesTimeScaling: 時間経過の段階で人数・ステータスを変えるか(falseならプレハブのBase Statsのまま)
    public CS_VillainGroup SpawnEventGroup(Vector3 center, float memberRadius, int memberCount,
        NetworkObject prefab = null, bool usesTimeScaling = true)
    {
        if (!hasAuthority) return null;
        if (prefab == null && !HasValidPrefabs()) return null;

        CS_VillainGroup group = new CS_VillainGroup(null, _maxAttackersPerGroup, usesTimeScaling);
        CSO_VillainTimeScaling.Stage stage = usesTimeScaling && _timeScaling != null ? _timeScaling.GetStage(GetElapsedTime()) : null;
        if (memberCount <= 0) memberCount = stage != null ? stage.memberCount : Random.Range(_minMembers, _maxMembers + 1);

        for (int i = 0; i < memberCount; i++)
        {
            Vector3 position = center;
            if (memberCount > 1)
            {
                float angle = 360f / memberCount * i;
                position += Quaternion.Euler(0f, angle, 0f) * Vector3.forward * memberRadius;
            }
            NetworkObject villain = SpawnVillain(SnapToNavMesh(position), center, stage, prefab);
            group.AddMember(villain.GetComponent<CS_VillainCrime>());
        }

        _eventGroups.Add(group);
        return group;
    }

    // 生成位置をNavMeshの上(悪人の体の中心の高さ)に合わせる。近くにNavMeshが無ければそのまま
    private static Vector3 SnapToNavMesh(Vector3 position)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, _navMeshSampleRadius, NavMesh.AllAreas)) return position;

        return hit.position + Vector3.up * _bodyCenterHeight;
    }

    // スポーン位置に犯罪完遂ゲージ(CS_VillainCrimeGauge)が付いていれば、値と表示を設定する
    private static void SetGauge(CS_VillainSpawnPoint point, float progress, bool isVisible)
    {
        if (point.TryGetComponent(out CS_VillainCrimeGauge gauge)) gauge.SetProgress(progress, isVisible);
    }

    // 最低グループ数を下回っていたら、空きがある限り生成する(足りなければリスポーン待ちの位置も使う)
    private void FillRequiredGroups()
    {
        while (_groups.Count < requiredGroupCount)
        {
            if (!TrySpawnGroup(true)) return;
        }
    }

    // 一定時間ごとに、上限までグループを1つ追加する
    private void SpawnByInterval()
    {
        if (_spawnInterval <= 0f) return;
        if (_spawnTimer < _spawnInterval) return;

        _spawnTimer = 0f;
        if (_groups.Count >= _maxGroupCount) return;

        TrySpawnGroup(false);
    }

    // 空いているスポーン位置から1つ選び、グループを生成する
    // allowCoolingDown: リスポーン待ちの位置しか空いていない時に、そこへ生成してよいか
    private bool TrySpawnGroup(bool allowCoolingDown)
    {
        CS_VillainSpawnPoint point = GetFreePoint(allowCoolingDown);
        if (point == null)
        {
            // 時間経過による追加(allowCoolingDownがfalse)は、空きが無ければ見送るだけなので警告しない
            if (allowCoolingDown && !_hasWarnedNoPoint)
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

    private CS_VillainGroup SpawnGroup(CS_VillainSpawnPoint point)
    {
        CS_VillainGroup group = new CS_VillainGroup(point, _maxAttackersPerGroup);

        // 経過時間の段階があれば、その人数・ステータスで生成する
        CSO_VillainTimeScaling.Stage stage = _timeScaling != null ? _timeScaling.GetStage(GetElapsedTime()) : null;
        int memberCount = stage != null ? stage.memberCount : Random.Range(_minMembers, _maxMembers + 1);

        for (int i = 0; i < memberCount; i++)
        {
            Vector3 position = point.GetMemberPosition(i, memberCount);
            NetworkObject villain = SpawnVillain(position, point.transform.position, stage);
            group.AddMember(villain.GetComponent<CS_VillainCrime>());
        }

        point.isOccupied = true;
        return group;
    }

    // 悪人を1人生成する。グループの中心(犯罪を行う場所)を向かせる
    // prefabがnullなら villainPrefabs からランダムに選ぶ
    private NetworkObject SpawnVillain(Vector3 position, Vector3 center, CSO_VillainTimeScaling.Stage stage, NetworkObject prefab = null)
    {
        if (prefab == null) prefab = _villainPrefabs[Random.Range(0, _villainPrefabs.Length)];

        Vector3 lookDirection = center - position;
        lookDirection.y = 0f;
        Quaternion rotation = lookDirection.sqrMagnitude > 0f ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

        NetworkObject villain = Instantiate(prefab, position, rotation);

        // ステータスの初期化(Spawn時、オフラインはStart時)より前に、経過時間の段階の値を設定する
        if (stage != null && villain.TryGetComponent(out CS_VillainStats stats))
        {
            stats.SetSpawnOverrides(stage.maxHp, stage.attackPower, stage.crimeCompleteTime);
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            villain.Spawn(true);
        }
        return villain;
    }

    // 空いていてリスポーン待ちでない位置からランダムに選ぶ
    // 無ければ、allowCoolingDownの時だけ、リスポーン待ちの位置のうち待ちが一番早く終わる位置を選ぶ
    private CS_VillainSpawnPoint GetFreePoint(bool allowCoolingDown)
    {
        List<CS_VillainSpawnPoint> readyPoints = new List<CS_VillainSpawnPoint>();
        CS_VillainSpawnPoint earliestCoolingPoint = null;
        foreach (CS_VillainSpawnPoint point in _spawnPoints)
        {
            if (point == null || point.isOccupied) continue;

            if (!point.isCoolingDown)
            {
                readyPoints.Add(point);
                continue;
            }

            if (earliestCoolingPoint == null || point.cooldownEndTime < earliestCoolingPoint.cooldownEndTime)
            {
                earliestCoolingPoint = point;
            }
        }

        if (readyPoints.Count > 0) return readyPoints[Random.Range(0, readyPoints.Count)];
        return allowCoolingDown ? earliestCoolingPoint : null;
    }

    // プレハブが設定されていて、全てにCS_VillainCrimeが付いているか
    // (付いていないとグループの生存判定ができず、生成し続けてしまうため)
    private bool HasValidPrefabs()
    {
        if (_villainPrefabs == null || _villainPrefabs.Length == 0)
        {
            Debug.LogError("CS_VillainSpawner: Villain Prefabs が未設定です", this);
            return false;
        }

        foreach (NetworkObject prefab in _villainPrefabs)
        {
            if (prefab != null && prefab.GetComponent<CS_VillainCrime>() != null) continue;

            Debug.LogError("CS_VillainSpawner: Villain Prefabs に空の要素、またはCS_VillainCrimeが付いていないプレハブがあります", this);
            return false;
        }
        return true;
    }

    private int GetPlayerCount()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return _offlinePlayerCount;

        return NetworkManager.Singleton.ConnectedClientsIds.Count;
    }

    // ゲームの経過時間(秒)。タイマーが無いシーンでは、スポナーが動き始めてからの時間
    // オンラインでタイマーがまだSpawnされていない間は、終了時刻が未設定で正しい値が取れないので使わない
    private float GetElapsedTime()
    {
        bool isOnline = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (_timer != null && (_timer.IsSpawned || !isOnline)) return _timer.GetElapsedTime();

        return Time.time - _startTime;
    }
}
