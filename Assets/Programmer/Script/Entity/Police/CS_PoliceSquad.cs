/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 警察のグループ(2〜3人)を管理するクラス
/// ・指定した位置に、メンバーごとのステータスで警察を出現させる
/// ・巡回ルートと隊列(先頭がルートを進み、他の警察は先頭の通った道筋をたどる)を管理する
/// ・メンバーが見つけた標的をグループ内で共有する
/// ・警備エリア内で起きた事件をメンバーに伝え、現場へ駆け付けさせる
/// ・詰まったメンバーを出現位置から出し直す(再スポーン)
/// 出現と判断はサーバー(またはオフライン)だけで行う
/// 巡回ルートのScene上の表示はCSED_PoliceSquadGizmoが行う
/// </summary>
public class CS_PoliceSquad : MonoBehaviour
{
    // 出現中の全グループ(事件の通知を全グループに届けるため)
    private static readonly List<CS_PoliceSquad> _activeSquads = new List<CS_PoliceSquad>();

    // 先頭の回避の優先度(NavMeshAgentは値が小さいほど優先され、ぶつかった時に相手が道を譲る)
    // 後ろの警察ほど値を大きくし、先頭の進路を譲らせる
    private const int _leaderAvoidancePriority = 30;

    // グループのメンバー(0番目が先頭)
    private readonly List<CS_PoliceBrain> _members = new List<CS_PoliceBrain>();

    // 隊列(先頭の通った道筋)
    private readonly CS_PoliceFormation _formation = new CS_PoliceFormation();

    // メンバーが見つけた標的の共有情報(共有時間は設定値を使うためStartで作る)
    private CS_PoliceSharedTarget _sharedTarget = null;

    // グループ全員で合わせる巡回速度(メンバーの中で一番遅い巡回速度。隊列がばらけないように揃える)
    private float _groupPatrolSpeed = 0.0f;

    // 現在目指している巡回ポイントの番号
    private int _patrolIndex = 0;

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
    [SerializeField]
    [Tooltip("巡回ルート(順番に回り、最後まで行ったら最初に戻る)")]
    private Transform[] _patrolPoints = new Transform[0];

    [SerializeField]
    [Tooltip("警備エリア(この中で起きた事件に駆け付ける。Trigger推奨)")]
    private Collider _guardArea = null;

    [SerializeField, Min(0f)]
    [Tooltip("先頭の警察が巡回ポイントにこの距離まで近づいたら、止まらずに次のポイントへ向かう")]
    private float _patrolPointPassDistance = 1.0f;

    [SerializeField, Min(0.5f)]
    [Tooltip("隊列の間隔(前の警察との距離。後ろの警察は先頭の通った道筋を一列でたどる)")]
    private float _formationSpacing = 1.5f;

    [Header("＝＝＝ 巡回ルートの表示(エディタのみ) ＝＝＝")]
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

    // 先頭の警察が巡回ポイントを通過したとみなす距離
    public float patrolPointPassDistance => _patrolPointPassDistance;

    // 巡回ルートの表示用(CSED_PoliceSquadGizmoが参照する)
    public IReadOnlyList<Transform> patrolPoints => _patrolPoints;
    public Gradient routeGradient => _routeGradient;
    public float routeLineThickness => _routeLineThickness;

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

    /// <summary>
    /// 事件(プレイヤーの変身など)が起きたことを全グループに知らせるメソッド
    /// 警備エリア内で起きた場合のみ、そのグループが現場へ駆け付ける(サーバーで呼ぶこと)
    /// </summary>
    /// <param name="position">事件の現場</param>
    public static void NotifyIncident(Vector3 position)
    {
        foreach (CS_PoliceSquad squad in _activeSquads)
        {
            squad.ReceiveIncident(position);
        }
    }

    private void Start()
    {
        if (IsNetworkClientOnly()) return;

        if (!IsSettingValid())
        {
            Debug.LogError("CS_PoliceSquad: 未設定の項目があります(ステータス・巡回ポイントに None が無いかも確認してください)", this);
            return;
        }

        if (_memberStatuses.Length < 2 || _memberStatuses.Length > 3)
        {
            Debug.LogWarning($"CS_PoliceSquad: グループの人数は2〜3人の想定です(現在 {_memberStatuses.Length} 人)", this);
        }

        _sharedTarget = new CS_PoliceSharedTarget(_shareDuration);
        _groupPatrolSpeed = CalculateGroupPatrolSpeed();
        for (int i = 0; i < _memberStatuses.Length; i++)
        {
            _members.Add(SpawnMember(i));
        }

        _activeSquads.Add(this);
    }

    private void Update()
    {
        if (_members.Count == 0) return;

        // 一番後ろの警察がたどる距離の分だけ、先頭の道筋を記録しておく
        _formation.RecordLeaderPosition(_members[0].transform.position, _formationSpacing * (_members.Count - 1));
    }

    private void OnDestroy()
    {
        _activeSquads.Remove(this);
    }

    /// <summary>
    /// 指定したメンバーがグループの先頭かを判定するメソッド
    /// </summary>
    /// <param name="member">判定するメンバー</param>
    /// <returns>先頭ならtrue</returns>
    public bool IsLeader(CS_PoliceBrain member)
    {
        return _members.Count > 0 && _members[0] == member;
    }

    /// <summary>
    /// 巡回中のメンバーが目指す位置を取得するメソッド
    /// 先頭は巡回ポイント、それ以外は先頭の通った道筋を一定間隔あけてたどった位置を目指す
    /// </summary>
    /// <param name="member">巡回中のメンバー</param>
    /// <returns>目指す位置</returns>
    public Vector3 GetPatrolDestination(CS_PoliceBrain member)
    {
        int slot = _members.IndexOf(member);
        if (slot > 0)
        {
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
    public void AdvancePatrolPoint()
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

        DespawnMember(member);
        _members[slot] = SpawnMember(slot);

        // 先頭が出し直された場合、前の先頭の道筋は使えないので記録し直す
        if (slot == 0) _formation.Clear();
    }

    /// <summary>
    /// 事件の通知を受け取り、警備エリア内ならメンバーを駆け付けさせるメソッド
    /// </summary>
    /// <param name="position">事件の現場</param>
    private void ReceiveIncident(Vector3 position)
    {
        // ClosestPointは、位置がコライダーの内側ならその位置をそのまま返す
        if (_guardArea == null || _guardArea.ClosestPoint(position) != position) return;

        foreach (CS_PoliceBrain member in _members)
        {
            member.RequestRush(position);
        }
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

        // 巡回はグループで揃えた速さ、駆け付けは追跡と同じ速さ、探索は自分の巡回の速さにする
        return new Dictionary<CSE_PoliceMoveState, float>
        {
            { CSE_PoliceMoveState.Patrol, _groupPatrolSpeed },
            { CSE_PoliceMoveState.Chase, chaseSpeed },
            { CSE_PoliceMoveState.Rush, chaseSpeed },
            { CSE_PoliceMoveState.Search, searchSpeed },
        };
    }

    /// <summary>
    /// 出現位置にメンバーを1人出現させるメソッド
    /// </summary>
    /// <param name="slot">メンバーの番号(ステータスの選択と、出現位置を横にずらすのに使う)</param>
    /// <returns>出現させたメンバー</returns>
    private CS_PoliceBrain SpawnMember(int slot)
    {
        // 同じ位置に重ならないよう、出現位置を横にずらす
        Vector3 position = _spawnPoint.position + _spawnPoint.right * ((slot - 1) * _formationSpacing);
        CS_PoliceBrain member = Instantiate(_policePrefab, position, _spawnPoint.rotation);

        // ぶつかった時は後ろの警察が道を譲るよう、先頭ほど回避の優先度を高くする
        member.GetComponent<NavMeshAgent>().avoidancePriority = _leaderAvoidancePriority + slot;

        // ネットワーク接続中は、クライアントにも出現させる
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
            && member.TryGetComponent(out NetworkObject networkObject))
        {
            networkObject.Spawn(true);
        }

        CSO_PoliceStatus status = _memberStatuses[slot];
        member.Setting(this, CreateSpeedTable(status), status);
        return member;
    }

    /// <summary>
    /// メンバーを消すメソッド
    /// </summary>
    /// <param name="member">消すメンバー</param>
    private void DespawnMember(CS_PoliceBrain member)
    {
        // ネットワークに出現済みなら、クライアント側も含めて消す
        if (member.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned)
        {
            networkObject.Despawn(true);
            return;
        }

        Destroy(member.gameObject);
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
