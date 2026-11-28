/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-11-02 | 巡回ルートの表示を、子オブジェクトからも描けるようにした(実際の巡回は配列のまま)
 * 2026-11-02 | 巡回ポイントの配列をシリアライズせず、開始時に子オブジェクトから作るようにした
 * 2026-11-02 | 巡回ルートの表示を、オン/オフの設定ではなく、選択している時だけ表示するようにした
 * 2026-11-09 | メンバーが隊列を組まず、離れて、同じルートをそれぞれ回る「離れて巡回」を追加した
 * 2026-11-28 | 軽量化のため、プレイヤーの近くを巡回している時だけ警察を出すようにした(出ていない間は、巡回の位置だけを進める)
 * ================================================ */

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 警察のグループ(1〜3人)を管理するクラス
/// ・指定した位置に、メンバーごとのステータスで警察を出現させる
/// ・巡回ルートと隊列(先頭がルートを進み、他の警察は先頭の通った道筋をたどる)を管理する
///   「離れて巡回」がオンなら、隊列は組まず、メンバーがそれぞれ、同じルートを、等間隔にずれた位置から回る
/// ・メンバーが見つけた標的をグループ内で共有する
/// ・詰まったメンバーを出現位置から出し直す(再スポーン)
/// ・軽量化: 巡回している位置がプレイヤーの近くに来た時だけ警察を出し、全員が遠ざかって何もしていなければ消す
///   出ていない間は、巡回の位置だけを、巡回の速さで進める(次に出る時は、その位置から巡回を続ける)
///   距離はCSO_PoliceAlertData(CS_PoliceAlertManager)で設定する。管理クラスが無いシーンでは、常に出す
/// 出現と判断はサーバー(またはオフライン)だけで行う
/// 巡回ルートのScene上の表示はCSED_PoliceSquadGizmoが行う
/// </summary>
public class CS_PoliceSquad : MonoBehaviour, IPoliceGroup
{
    // 先頭の回避の優先度(NavMeshAgentは値が小さいほど優先され、ぶつかった時に相手が道を譲る)
    // 後ろの警察ほど値を大きくし、先頭の進路を譲らせる
    private const int _leaderAvoidancePriority = 30;

    // プレイヤーとの距離を調べて、警察を出し入れする間隔(秒)
    private const float _rangeCheckInterval = 0.5f;

    // 巡回の位置から出す時に、近くの歩ける場所(NavMesh)を探す距離(m)
    private const float _spawnSampleDistance = 3.0f;

    // 出ていない間の、メンバーごとの巡回の位置(隊列で巡回する時は、0番目の先頭の位置だけを使う)
    private Vector3[] _awayPositions = new Vector3[0];

    // 次に距離を調べる時刻と、前に巡回の位置を進めた時刻
    private float _nextRangeCheckTime = 0.0f;
    private float _lastAdvanceTime = 0.0f;

    // グループのメンバー(0番目が先頭)
    private readonly List<CS_PoliceBrain> _members = new List<CS_PoliceBrain>();

    // 隊列(先頭の通った道筋)
    private readonly CS_PoliceFormation _formation = new CS_PoliceFormation();

    // メンバーが見つけた標的の共有情報(共有時間は設定値を使うためStartで作る)
    private CS_PoliceSharedTarget _sharedTarget = null;

    // グループ全員で合わせる巡回速度(メンバーの中で一番遅い巡回速度。隊列がばらけないように揃える)
    private float _groupPatrolSpeed = 0.0f;

    // 開始時の準備(巡回ポイント・ステータスの確認)が済んでいるか
    private bool _isReady = false;

    // 現在目指している巡回ポイントの番号(隊列で巡回する時の、先頭の番号)
    private int _patrolIndex = 0;

    // 離れて巡回する時の、メンバーごとの、現在目指している巡回ポイントの番号
    private int[] _memberPatrolIndices = new int[0];

    [Header("＝＝＝ 出現 ＝＝＝")]
    [SerializeField]
    [Tooltip("警察のプレハブ(CS_PoliceBrainが付いたもの)")]
    private CS_PoliceBrain _policePrefab = null;

    [SerializeField]
    [Tooltip("出現・再出現する位置")]
    private Transform _spawnPoint = null;

    [Header("＝＝＝ ステータス ＝＝＝")]
    [SerializeField]
    [Tooltip("メンバーごとのステータス(要素数がグループの人数になる。0番目が先頭。例: A, B, A)")]
    private CSO_PoliceStatus[] _memberStatuses = new CSO_PoliceStatus[2];

    [SerializeField]
    [Tooltip("速度倍率の基準にするプレイヤーのステータス(moveSpeedを基準速度にする)")]
    private CSO_PlayerStats _playerBaseStats = null;

    [Header("＝＝＝ 巡回 ＝＝＝")]
    // 巡回ルート(順番に回り、最後まで行ったら最初に戻る)。シリアライズせず、開始時にこのグループの子オブジェクト(出現位置は除く)から作る
    // 子オブジェクトの並んでいる順(Hierarchyの上から)が、巡回する順番になる
    private Transform[] _patrolPoints = new Transform[0];

    [SerializeField, Min(0f)]
    [Tooltip("先頭の警察が巡回ポイントにこの距離まで近づいたら、止まらずに次のポイントへ向かう")]
    private float _patrolPointPassDistance = 1.0f;

    [SerializeField]
    [Tooltip("オンにすると、メンバーは隊列を組まず、離れて、同じ巡回ルートをそれぞれ回る(出現も、ルートの上の、等間隔にずれたポイントから。見つけた標的の共有や、見つけた時の動きは、そのまま)")]
    private bool _spreadPatrol = false;

    [SerializeField, Min(0.5f)]
    [Tooltip("隊列の間隔(前の警察との距離。後ろの警察は先頭の通った道筋を一列でたどる)")]
    private float _formationSpacing = 1.5f;

    [Header("＝＝＝ 巡回ルートの表示(エディタのみ。グループを選択している時だけ表示する) ＝＝＝")]
    [SerializeField]
    [Tooltip("巡回ルートの線の色(始点のポイント→最後のポイント→始点に戻る、の順に変化する)")]
    private Gradient _routeGradient = CreateDefaultRouteGradient();

    [SerializeField, Min(1f)]
    [Tooltip("巡回ルートの線の太さ")]
    private float _routeLineThickness = 4.0f;

    [Header("＝＝＝ 情報共有 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("メンバーが見つけた標的を、見えなくなってからも共有し続ける時間(秒)")]
    private float _shareDuration = 0.5f;

    // 巡回ルートの表示用(CSED_PoliceSquadGizmoが参照する)
    public Gradient routeGradient => _routeGradient;
    public float routeLineThickness => _routeLineThickness;

    /// <summary>
    /// 巡回ルートの表示用の点を集めるメソッド(エディタのGizmo専用。毎回リストを作るので、ゲーム中の処理では使わない)
    /// 子オブジェクト(出現位置は除く)を、並んでいる順に返す
    /// </summary>
    /// <returns>表示する点</returns>
    public List<Transform> GetRouteDrawPoints()
    {
        return new List<Transform>(CollectChildPoints(transform, _spawnPoint));
    }

    /// <summary>
    /// 子オブジェクトを、並んでいる順に集めて、巡回ポイントの配列にするメソッド(巡回ドローンの出現クラスも使う)
    /// 実際の巡回は、処理の負荷を抑えるため、開始時にこのメソッドで作った配列を参照する
    /// </summary>
    /// <param name="parent">子オブジェクトを探す親</param>
    /// <param name="exclude">子オブジェクトから除くもの(出現位置など。無ければnull)</param>
    /// <returns>巡回ポイントの配列(子オブジェクトが無ければ空)</returns>
    public static Transform[] CollectChildPoints(Transform parent, Transform exclude)
    {
        List<Transform> points = new List<Transform>();
        foreach (Transform child in parent)
        {
            if (child != exclude) points.Add(child);
        }
        return points.ToArray();
    }

    /// <summary>
    /// ネットワーク接続中のクライアント(サーバーではない)かを判定するメソッド
    /// 警察の出現と判断はサーバーだけで行うため、クライアントでは処理を止めるのに使う
    /// </summary>
    /// <returns>クライアントならtrue(オフライン・サーバーならfalse)</returns>
    public static bool IsNetworkClientOnly()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager != null && manager.IsListening && !manager.IsServer;
    }

    private void Start()
    {
        if (IsNetworkClientOnly()) return;

        // 巡回ポイントは、子オブジェクトから作る(シーンに配列を登録しなくてよい)
        _patrolPoints = CollectChildPoints(transform, _spawnPoint);

        if (!IsSettingValid())
        {
            Debug.LogError("CS_PoliceSquad: 未設定の項目があります(ステータス・巡回ポイントに None が無いかも確認してください)", this);
            return;
        }

        if (_memberStatuses.Length < 1 || _memberStatuses.Length > 3)
        {
            Debug.LogWarning($"CS_PoliceSquad: グループの人数は1〜3人の想定です(現在 {_memberStatuses.Length} 人)", this);
        }

        _sharedTarget = new CS_PoliceSharedTarget(_shareDuration);
        _groupPatrolSpeed = CalculateGroupPatrolSpeed();
        InitializeSpreadPatrol();
        ResetAwayPatrol();
        _isReady = true;

        // ソロ: 今のエリアの部隊だけ出す。エリアが変わるたびに出す・消すを切り替える
        if (CS_SoloAreaProgression.instance != null) CS_SoloAreaProgression.instance.onAreaChanged += HandleAreaChanged;
        Sync();

        // 全グループが同じフレームで距離を調べないよう、最初の時刻をずらす
        _nextRangeCheckTime = Time.time + UnityEngine.Random.Range(0.0f, _rangeCheckInterval);
    }

    private void OnDestroy()
    {
        if (CS_SoloAreaProgression.instance != null) CS_SoloAreaProgression.instance.onAreaChanged -= HandleAreaChanged;
    }

    private void HandleAreaChanged(CS_CrimeArea area)
    {
        Sync();
    }

    /// <summary>
    /// 今の状況に合わせて、警察を出す・消すを切り替えるメソッド
    /// ソロで、出現位置が今のエリアでなければ、常に消す。それ以外は、プレイヤーの近くを巡回している時だけ出す
    /// </summary>
    private void Sync()
    {
        if (!_isReady) return;

        if (!CS_SoloAreaProgression.IsActiveAt(_spawnPoint.position))
        {
            if (_members.Count > 0) DespawnMembers();
            ResetAwayPatrol();
            return;
        }

        bool isRanged = CS_PoliceAlertManager.TryGetSquadRange(out float spawnDistance, out float despawnDistance);
        if (_members.Count == 0)
        {
            AdvanceAwayPatrol();
            if (!isRanged || IsAnyAwayPositionNearPlayer(spawnDistance)) SpawnMembers();
        }
        else if (isRanged && CanLeave(despawnDistance))
        {
            RememberAwayPatrol();
            DespawnMembers();
        }
    }

    // 全員を、出ていない間に進めた巡回の位置から出す
    private void SpawnMembers()
    {
        _formation.Clear();
        for (int i = 0; i < _memberStatuses.Length; i++)
        {
            _members.Add(SpawnMember(i, GetResumePosition(i), GetResumeRotation(i)));
        }
    }

    private void DespawnMembers()
    {
        foreach (CS_PoliceBrain member in _members)
        {
            if (member != null) CS_PoliceSpawnUtility.Despawn(member.gameObject);
        }
        _members.Clear();
        _formation.Clear();
    }

    // 巡回を最初の状態に戻す(巡回の位置は出現位置。離れて巡回する時は、それぞれの最初のポイント)
    private void ResetAwayPatrol()
    {
        _patrolIndex = 0;
        InitializeSpreadPatrol();
        _awayPositions = new Vector3[_memberStatuses.Length];
        for (int i = 0; i < _awayPositions.Length; i++)
        {
            _awayPositions[i] = IsSpreadPatrol() ? _patrolPoints[_memberPatrolIndices[i]].position : _spawnPoint.position;
        }
        _lastAdvanceTime = Time.time;
    }

    // 出ていない間、巡回の速さで、巡回の位置を進める(ポイントを順に、まっすぐ進んだことにする)
    private void AdvanceAwayPatrol()
    {
        float travel = _groupPatrolSpeed * (Time.time - _lastAdvanceTime);
        _lastAdvanceTime = Time.time;
        if (_patrolPoints.Length < 2 || travel <= 0.0f) return;

        if (!IsSpreadPatrol())
        {
            _awayPositions[0] = AdvanceAlongRoute(_awayPositions[0], ref _patrolIndex, travel);
            return;
        }

        for (int i = 0; i < _awayPositions.Length; i++)
        {
            _awayPositions[i] = AdvanceAlongRoute(_awayPositions[i], ref _memberPatrolIndices[i], travel);
        }
    }

    // positionから、目指しているポイント(targetIndex)の順に、travelの長さだけ進んだ位置を返す
    private Vector3 AdvanceAlongRoute(Vector3 position, ref int targetIndex, float travel)
    {
        // ポイントが重なっていても終わるように、進むポイントの数に上限を付ける
        for (int i = 0; i < _patrolPoints.Length * 4 && travel > 0.0f; i++)
        {
            Vector3 target = _patrolPoints[targetIndex].position;
            float distance = Vector3.Distance(position, target);
            if (distance > travel) return Vector3.MoveTowards(position, target, travel);

            travel -= distance;
            position = target;
            targetIndex = (targetIndex + 1) % _patrolPoints.Length;
        }
        return position;
    }

    private bool IsAnyAwayPositionNearPlayer(float distance)
    {
        if (!IsSpreadPatrol()) return IsNearAnyPlayer(_awayPositions[0], distance);

        foreach (Vector3 position in _awayPositions)
        {
            if (IsNearAnyPlayer(position, distance)) return true;
        }
        return false;
    }

    // どれかのプレイヤー(NPCを含む)が、水平方向で、距離以内にいるか
    private static bool IsNearAnyPlayer(Vector3 position, float distance)
    {
        foreach (CS_Player player in CS_Player.activePlayers)
        {
            if (player == null) continue;

            Vector3 offset = player.transform.position - position;
            offset.y = 0.0f;
            if (offset.sqrMagnitude <= distance * distance) return true;
        }
        return false;
    }

    // 全員が、どのプレイヤーからも離れていて、巡回・待機しているだけ(追跡・駆け付け・探索・攻撃をしていない)なら、消してよい
    private bool CanLeave(float distance)
    {
        foreach (CS_PoliceBrain member in _members)
        {
            if (member == null || !member.isIdle || IsNearAnyPlayer(member.transform.position, distance)) return false;
        }
        return true;
    }

    // 消す前に、今の位置を、出ていない間の巡回の位置にする
    private void RememberAwayPatrol()
    {
        for (int i = 0; i < _members.Count && i < _awayPositions.Length; i++)
        {
            _awayPositions[i] = _members[i].transform.position;
        }
        _lastAdvanceTime = Time.time;
    }

    // 出す位置: 巡回の位置(隊列の時は、先頭の位置から横にずらす)。近くの歩ける場所に合わせる
    private Vector3 GetResumePosition(int slot)
    {
        bool isSpread = IsSpreadPatrol();
        Vector3 basePosition = _awayPositions[isSpread ? slot : 0];

        // 巡回の位置は、ポイントの間をまっすぐ進めているので、建物の中などになった時は、目指しているポイントに出す
        if (NavMesh.SamplePosition(basePosition, out NavMeshHit baseHit, _spawnSampleDistance, NavMesh.AllAreas)) basePosition = baseHit.position;
        else if (_patrolPoints.Length > 0) basePosition = _patrolPoints[isSpread ? _memberPatrolIndices[slot] : _patrolIndex].position;
        if (isSpread) return basePosition;

        Vector3 position = basePosition + GetResumeRotation(slot) * Vector3.right * ((slot - 1) * _formationSpacing);
        return NavMesh.SamplePosition(position, out NavMeshHit hit, _formationSpacing, NavMesh.AllAreas) ? hit.position : basePosition;
    }

    // 出す時の向き: 次に目指すポイントの方向
    private Quaternion GetResumeRotation(int slot)
    {
        if (_patrolPoints.Length == 0) return _spawnPoint.rotation;

        bool isSpread = IsSpreadPatrol();
        Vector3 direction = _patrolPoints[isSpread ? _memberPatrolIndices[slot] : _patrolIndex].position - _awayPositions[isSpread ? slot : 0];
        direction.y = 0.0f;
        return direction.sqrMagnitude > 0.01f ? Quaternion.LookRotation(direction) : _spawnPoint.rotation;
    }

    private void Update()
    {
        if (_isReady && Time.time >= _nextRangeCheckTime)
        {
            _nextRangeCheckTime = Time.time + _rangeCheckInterval;
            Sync();
        }

        // マルチのシーン遷移では、警察(NetworkObject)が先に破棄され、このグループはシーンが切り替わるまで数フレーム残る
        // 破棄された警察を触らないよう、先頭が破棄されていたら何もしない
        if (_members.Count == 0 || _members[0] == null) return;

        // 離れて巡回する時は、隊列を組まないので、先頭の道筋は記録しない
        if (IsSpreadPatrol()) return;

        // 一番後ろの警察がたどる距離の分だけ、先頭の道筋を記録しておく
        _formation.RecordLeaderPosition(_members[0].transform.position, _formationSpacing * (_members.Count - 1));
    }

    /// <summary>
    /// 指定したメンバーがグループの先頭かを判定するメソッド
    /// </summary>
    /// <param name="member">判定するメンバー</param>
    /// <returns>先頭ならtrue</returns>
    public bool IsLeader(CS_PoliceBrain member)
    {
        // 離れて巡回する時は、全員が、自分で巡回ルートを目指す(先頭と同じ扱い)
        if (IsSpreadPatrol()) return true;

        return _members.Count > 0 && _members[0] == member;
    }

    /// <summary>
    /// 離れて巡回するか(設定がオンで、巡回ルートがある)を判定するメソッド
    /// </summary>
    private bool IsSpreadPatrol()
    {
        return _spreadPatrol && _patrolPoints.Length > 0;
    }

    /// <summary>
    /// 離れて巡回する時の、メンバーごとの最初の巡回ポイントを、ルートの上に等間隔に決めるメソッド
    /// </summary>
    private void InitializeSpreadPatrol()
    {
        _memberPatrolIndices = new int[_memberStatuses.Length];
        if (!IsSpreadPatrol()) return;

        for (int i = 0; i < _memberPatrolIndices.Length; i++)
        {
            _memberPatrolIndices[i] = Mathf.RoundToInt(i * (float)_patrolPoints.Length / _memberPatrolIndices.Length) % _patrolPoints.Length;
        }
    }

    /// <summary>
    /// グループの巡回ルート(先頭以外は隊列の位置)に沿って移動させるメソッド
    /// </summary>
    /// <param name="member">巡回させるメンバー</param>
    /// <param name="move">メンバーの移動</param>
    /// <returns>メンバーの状態(巡回)</returns>
    public CSE_PoliceMoveState MoveIdle(CS_PoliceBrain member, CS_PoliceMove move)
    {
        if (IsSpreadPatrol()) return MoveSpread(member, move);

        move.SetDestination(GetPatrolDestination(member), CSE_PoliceMoveState.Patrol);

        // 先頭の警察は巡回ポイントの少し手前で次のポイントへ向かい直す
        // (ポイントで減速して止まると、後ろの警察が追い付いてぶつかるため)
        if (IsLeader(member) && move.IsNearDestination(_patrolPointPassDistance))
        {
            AdvancePatrolPoint();
            move.SetDestination(GetPatrolDestination(member), CSE_PoliceMoveState.Patrol);
        }

        return CSE_PoliceMoveState.Patrol;
    }

    /// <summary>
    /// 離れて巡回する時に、メンバーを、自分の次の巡回ポイントへ移動させるメソッド(巡回ポイントの少し手前で、次のポイントへ向かい直す)
    /// </summary>
    private CSE_PoliceMoveState MoveSpread(CS_PoliceBrain member, CS_PoliceMove move)
    {
        int slot = _members.IndexOf(member);
        if (slot < 0 || slot >= _memberPatrolIndices.Length) return CSE_PoliceMoveState.Patrol;

        move.SetDestination(_patrolPoints[_memberPatrolIndices[slot]].position, CSE_PoliceMoveState.Patrol);
        if (move.IsNearDestination(_patrolPointPassDistance))
        {
            _memberPatrolIndices[slot] = (_memberPatrolIndices[slot] + 1) % _patrolPoints.Length;
            move.SetDestination(_patrolPoints[_memberPatrolIndices[slot]].position, CSE_PoliceMoveState.Patrol);
        }
        return CSE_PoliceMoveState.Patrol;
    }

    /// <summary>
    /// 巡回中のメンバーが目指す位置を取得するメソッド
    /// 先頭は巡回ポイント、それ以外は先頭の通った道筋を一定間隔あけてたどった位置を目指す
    /// </summary>
    /// <param name="member">巡回中のメンバー</param>
    /// <returns>目指す位置</returns>
    private Vector3 GetPatrolDestination(CS_PoliceBrain member)
    {
        int slot = _members.IndexOf(member);
        if (slot > 0)
        {
            // 先頭が破棄されている(シーン遷移中など)間は、その場で待つ
            if (_members[0] == null) return member.transform.position;

            // 道筋がまだ短い(出現直後など)場合は、先頭が十分進むまでその場で待つ
            bool hasPosition = _formation.TryGetPositionBehind(_members[0].transform.position, _formationSpacing * slot, out Vector3 formationPosition);
            return hasPosition ? formationPosition : member.transform.position;
        }

        // 巡回ルートが無い場合は、出現位置で待機する
        if (_patrolPoints.Length == 0) return _spawnPoint.position;
        return _patrolPoints[_patrolIndex].position;
    }

    /// <summary>
    /// 次の巡回ポイントへ進めるメソッド(先頭のメンバーが巡回ポイントを通過した時に呼ぶ)
    /// </summary>
    private void AdvancePatrolPoint()
    {
        if (_patrolPoints.Length == 0) return;

        _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
    }

    /// <summary>
    /// メンバーが見つけた標的をグループに報告するメソッド
    /// </summary>
    /// <param name="target">見つけた標的</param>
    /// <param name="priority">標的の優先度</param>
    public void ReportTarget(Transform target, int priority)
    {
        _sharedTarget.Report(target, priority);
    }

    /// <summary>
    /// グループ内で共有している標的を取得するメソッド
    /// </summary>
    /// <param name="priority">標的の優先度</param>
    /// <returns>共有中の標的(いなければnull)</returns>
    public Transform GetSharedTarget(out int priority)
    {
        return _sharedTarget.Get(out priority);
    }

    /// <summary>
    /// 異常事態になったメンバーを消し、出現位置から出し直すメソッド
    /// </summary>
    /// <param name="member">出し直すメンバー</param>
    public void RequestRespawn(CS_PoliceBrain member)
    {
        int slot = _members.IndexOf(member);
        if (slot < 0) return;

        CS_PoliceSpawnUtility.Despawn(member.gameObject);
        _members[slot] = SpawnMember(slot, GetStartPosition(slot), _spawnPoint.rotation);

        // 先頭が出し直された場合、前の先頭の道筋は使えないので記録し直す
        if (slot == 0) _formation.Clear();
    }

    /// <summary>
    /// 必要な項目がすべて設定されているかを判定するメソッド
    /// </summary>
    /// <returns>設定されていればtrue</returns>
    private bool IsSettingValid()
    {
        if (_policePrefab == null || _spawnPoint == null || _playerBaseStats == null) return false;
        if (_memberStatuses.Length == 0) return false;

        // 配列の途中に未設定(None)の要素があると実行中にエラーになるので、出現前に弾く
        if (Array.Exists(_memberStatuses, status => status == null)) return false;
        return !Array.Exists(_patrolPoints, point => point == null);
    }

    /// <summary>
    /// グループ全員で合わせる巡回速度(メンバーの中で一番遅い巡回速度)を計算するメソッド
    /// </summary>
    /// <returns>グループの巡回速度</returns>
    private float CalculateGroupPatrolSpeed()
    {
        float slowestMultiplier = float.MaxValue;
        foreach (CSO_PoliceStatus status in _memberStatuses)
        {
            slowestMultiplier = Mathf.Min(slowestMultiplier, status.patrolSpeedMultiplier);
        }

        return _playerBaseStats.moveSpeed * slowestMultiplier;
    }

    /// <summary>
    /// メンバーの移動状態ごとの速度を、プレイヤーの基準速度×倍率で作るメソッド
    /// </summary>
    /// <param name="status">メンバーのステータス</param>
    /// <returns>移動状態ごとの速度</returns>
    private Dictionary<CSE_PoliceMoveState, float> CreateSpeedTable(CSO_PoliceStatus status)
    {
        float baseSpeed = _playerBaseStats.moveSpeed;
        float searchSpeed = baseSpeed * status.patrolSpeedMultiplier;
        float chaseSpeed = baseSpeed * status.chaseSpeedMultiplier;

        // 巡回はグループで揃えた速さ、駆け付けは追跡と同じ速さ、探索・怪しんで近づくのは自分の巡回の速さにする
        return new Dictionary<CSE_PoliceMoveState, float>
        {
            { CSE_PoliceMoveState.Patrol, _groupPatrolSpeed },
            { CSE_PoliceMoveState.Chase, chaseSpeed },
            { CSE_PoliceMoveState.Rush, chaseSpeed },
            { CSE_PoliceMoveState.Search, searchSpeed },
            { CSE_PoliceMoveState.Suspect, searchSpeed },
        };
    }

    /// <summary>
    /// 出し直す(詰まった)メンバーの位置を取得するメソッド
    /// 同じ位置に重ならないよう、出現位置を横にずらす(離れて巡回する時は、ルートの上の、自分の今のポイントに出現する)
    /// </summary>
    private Vector3 GetStartPosition(int slot)
    {
        return IsSpreadPatrol() && slot < _memberPatrolIndices.Length
            ? _patrolPoints[_memberPatrolIndices[slot]].position
            : _spawnPoint.position + _spawnPoint.right * ((slot - 1) * _formationSpacing);
    }

    /// <summary>
    /// 指定した位置にメンバーを1人出現させるメソッド
    /// </summary>
    /// <param name="slot">メンバーの番号(ステータスの選択と、回避の優先度に使う)</param>
    /// <returns>出現させたメンバー</returns>
    private CS_PoliceBrain SpawnMember(int slot, Vector3 position, Quaternion rotation)
    {
        CS_PoliceBrain member = CS_PoliceSpawnUtility.Spawn(_policePrefab, position, rotation);

        // ぶつかった時は後ろの警察が道を譲るよう、先頭ほど回避の優先度を高くする
        member.GetComponent<NavMeshAgent>().avoidancePriority = _leaderAvoidancePriority + slot;

        CSO_PoliceStatus status = _memberStatuses[slot];
        member.Setting(this, CreateSpeedTable(status), status);
        return member;
    }

    /// <summary>
    /// 巡回ルートの線の色の初期値(水色→赤)を作るメソッド
    /// </summary>
    /// <returns>線の色のグラデーション</returns>
    private static Gradient CreateDefaultRouteGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.2f, 0.8f, 1.0f), 0.0f), new GradientColorKey(new Color(1.0f, 0.3f, 0.3f), 1.0f) },
            new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) });
        return gradient;
    }
}
