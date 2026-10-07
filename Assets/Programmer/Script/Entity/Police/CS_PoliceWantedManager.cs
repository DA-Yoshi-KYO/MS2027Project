/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-05 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// プレイヤーの手配度に応じて、警察(増援)を出現・削除するクラス(シーンに1つ置く)
/// ・手配度が上がったら、そのプレイヤーの周囲(外周)に増援を出現させ、プレイヤーへ向かわせる
/// ・手配度が下がったら、そのプレイヤーの増援の中からランダムに選んで消す(追跡中・戦闘中の警察は、落ち着くまで待つ)
/// ・増援の速度・視野距離は、持ち主のプレイヤーの今の手配度に合わせる(誰を追っていても持ち主の手配度のまま)
/// ・持ち主のプレイヤーが出した信号の場所へ、増援を駆け付けさせる
/// 手配度の値そのものはプレイヤー側が管理し、SetWantedLevelで知らせてもらう
/// 出現・削除はサーバー(またはオフライン)だけで行う
/// </summary>
public class CS_PoliceWantedManager : MonoBehaviour
{
    // シーンに置かれている管理クラス(静的メソッドから使う)
    private static CS_PoliceWantedManager _instance = null;

    // シーンに管理クラスが無いことを警告済みか(手配度が変わるたびに警告しないため)
    private static bool _hasWarnedNoInstance = false;

    // プレイヤーごとの増援のまとまり
    private readonly Dictionary<CS_PlayerHealth, CS_PoliceWantedGroup> _groups = new Dictionary<CS_PlayerHealth, CS_PoliceWantedGroup>();

    // 消せる警察を集める際に使い回すリスト
    private readonly List<CS_PoliceBrain> _removableBuffer = new List<CS_PoliceBrain>();

    // 持ち主がいなくなったまとまりを集める際に使い回すリスト
    private readonly List<CS_PlayerHealth> _leftOwnerBuffer = new List<CS_PlayerHealth>();

    // 出現位置からプレイヤーまで歩いて行けるかを調べる際に使い回す経路
    private NavMeshPath _reachablePath = null;

    // 次に人数を確認するまでの残り時間
    private float _refreshTimer = 0.0f;

    [Header("＝＝＝ 増援 ＝＝＝")]
    [SerializeField]
    [Tooltip("警察のプレハブ(CS_PoliceBrainが付いたもの)")]
    private CS_PoliceBrain _policePrefab = null;

    [SerializeField]
    [Tooltip("増援のステータス(視野距離は手配度データの値を使う)")]
    private CSO_PoliceStatus _status = null;

    [SerializeField]
    [Tooltip("速度倍率の基準にするプレイヤーのステータス(moveSpeedを基準速度にする)")]
    private CSO_PlayerStats _playerBaseStats = null;

    [SerializeField]
    [Tooltip("手配度ごとの増援の人数・強さ")]
    private CSO_PoliceWantedLevelData _levelData = null;

    [Header("＝＝＝ 出現位置 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("手配度を上げたプレイヤーから、この距離だけ離れた外周に出現させる")]
    private float _spawnRadius = 20.0f;

    [SerializeField, Min(1)]
    [Tooltip("出現できる位置を探す回数(見つからなければ、次の確認の時にもう一度探す)")]
    private int _spawnAttempts = 10;

    [SerializeField, Min(0.1f)]
    [Tooltip("外周の位置から、歩ける場所(NavMesh)を探す距離")]
    private float _navMeshSearchDistance = 2.0f;

    [Header("＝＝＝ 管理 ＝＝＝")]
    [SerializeField, Min(0.05f)]
    [Tooltip("増援の人数を確認する間隔(秒)。追跡中で消せなかった警察も、この間隔で消せるか確認し直す")]
    private float _refreshInterval = 0.2f;

    [SerializeField, Min(0f)]
    [Tooltip("増援が見つけた標的を、見えなくなってからも増援同士で共有し続ける時間(秒)")]
    private float _shareDuration = 0.5f;

    /// <summary>
    /// プレイヤーの手配度が変わったことを知らせるメソッド(サーバーで呼ぶこと)
    /// </summary>
    /// <param name="player">手配度が変わったプレイヤー</param>
    /// <param name="level">変わった後の手配度(0〜最大)</param>
    public static void SetWantedLevel(CS_PlayerHealth player, int level)
    {
        if (!TryGetInstance(out CS_PoliceWantedManager manager)) return;

        manager.ChangeWantedLevel(player, level);
    }

    /// <summary>
    /// プレイヤーが出した信号の場所へ、そのプレイヤーの増援を駆け付けさせるメソッド
    /// (CS_PoliceSquad.NotifyIncidentから呼ばれる)
    /// </summary>
    /// <param name="player">信号を出したプレイヤー</param>
    /// <param name="position">信号を出した位置</param>
    public static void ReceiveSignal(CS_PlayerHealth player, Vector3 position)
    {
        if (player == null || _instance == null || CS_PoliceSquad.IsNetworkClientOnly()) return;
        if (!_instance._groups.TryGetValue(player, out CS_PoliceWantedGroup group)) return;

        foreach (CS_PoliceBrain member in group.members)
        {
            if (member != null) member.RequestRush(position);
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("CS_PoliceWantedManager: シーンに2つ以上置かれています(後から置かれた方は使われません)", this);
            return;
        }

        _instance = this;
        _reachablePath = new NavMeshPath();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (CS_PoliceSquad.IsNetworkClientOnly()) return;

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer > 0.0f) return;
        _refreshTimer = _refreshInterval;

        foreach (CS_PoliceWantedGroup group in _groups.Values)
        {
            RefreshMembers(group);
            if (group.owner == null) _leftOwnerBuffer.Add(group.owner);
        }

        // 持ち主がいなくなった(切断など)まとまりは、増援を消し終えたので片付ける
        foreach (CS_PlayerHealth owner in _leftOwnerBuffer)
        {
            _groups.Remove(owner);
        }
        _leftOwnerBuffer.Clear();
    }

    /// <summary>
    /// 増援を演出なしですぐに消すメソッド
    /// 詰まった増援もこれで消す(足りなくなった分は、次の人数の確認で持ち主の近くに出現させる)
    /// </summary>
    /// <param name="group">増援のまとまり</param>
    /// <param name="member">消す増援</param>
    public void DespawnMember(CS_PoliceWantedGroup group, CS_PoliceBrain member)
    {
        group.RemoveMember(member);
        CS_PoliceSpawnUtility.Despawn(member.gameObject);
    }

    /// <summary>
    /// シーンに置かれている管理クラスを取得するメソッド
    /// </summary>
    /// <param name="manager">管理クラス</param>
    /// <returns>使える状態ならtrue(クライアントでは出現・削除を行わないのでfalse)</returns>
    private static bool TryGetInstance(out CS_PoliceWantedManager manager)
    {
        manager = _instance;
        if (CS_PoliceSquad.IsNetworkClientOnly()) return false;
        if (manager != null) return true;

        if (!_hasWarnedNoInstance)
        {
            Debug.LogWarning("CS_PoliceWantedManager: シーンに置かれていないため、手配度による警察の増援は出現しません");
            _hasWarnedNoInstance = true;
        }
        return false;
    }

    /// <summary>
    /// プレイヤーの手配度を変更し、増援の強さと人数を合わせるメソッド
    /// </summary>
    /// <param name="player">手配度が変わったプレイヤー</param>
    /// <param name="level">変わった後の手配度</param>
    private void ChangeWantedLevel(CS_PlayerHealth player, int level)
    {
        if (player == null || !IsSettingValid()) return;

        if (!_groups.TryGetValue(player, out CS_PoliceWantedGroup group))
        {
            group = new CS_PoliceWantedGroup(this, player, _shareDuration);
            _groups.Add(player, group);
        }

        level = Mathf.Clamp(level, 0, _levelData.maxLevel);
        if (group.level == level) return;

        group.SetLevel(level);

        // 手配度が上がっても下がっても、そのプレイヤーの増援は全員今の手配度の強さにする
        foreach (CS_PoliceBrain member in group.members)
        {
            if (member != null) ApplyAbility(member, level);
        }

        RefreshMembers(group);
    }

    /// <summary>
    /// 手配度に応じた人数になるよう、増援を出現させる・消すメソッド
    /// </summary>
    /// <param name="group">増援のまとまり</param>
    private void RefreshMembers(CS_PoliceWantedGroup group)
    {
        group.RemoveDestroyedMembers();

        // 持ち主がいなくなった(切断など)場合は、追跡中でもすぐに全員消す
        if (group.owner == null)
        {
            while (group.members.Count > 0)
            {
                DespawnMember(group, group.members[0]);
            }
            return;
        }

        int requiredCount = _levelData.GetPoliceCount(group.level);
        while (group.members.Count < requiredCount)
        {
            if (!TrySpawnMember(group)) break;
        }

        int excessCount = group.members.Count - requiredCount;
        if (excessCount > 0) RemoveExcessMembers(group, excessCount);
    }

    /// <summary>
    /// 持ち主の周囲(外周)に増援を1人出現させ、持ち主へ向かわせるメソッド
    /// </summary>
    /// <param name="group">増援のまとまり</param>
    /// <returns>出現させられたらtrue(出現できる位置が無ければfalse)</returns>
    private bool TrySpawnMember(CS_PoliceWantedGroup group)
    {
        Vector3 ownerPosition = group.owner.transform.position;
        if (!TryFindSpawnPosition(ownerPosition, out Vector3 spawnPosition)) return false;

        // 持ち主の方を向いて出現させる
        Vector3 toOwner = ownerPosition - spawnPosition;
        toOwner.y = 0.0f;
        Quaternion rotation = toOwner.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toOwner) : Quaternion.identity;

        CS_PoliceBrain member = CS_PoliceSpawnUtility.Spawn(_policePrefab, spawnPosition, rotation);
        member.Setting(group, CreateSpeedTable(group.level), _status);
        ApplyAbility(member, group.level);

        // 仕様: 増援は追跡状態から始まる(持ち主の位置を追いながら向かい、見つけたら追跡に切り替わる)
        member.RequestPursuit(group.owner);

        group.AddMember(member);
        return true;
    }

    /// <summary>
    /// 多すぎる増援を、消せる状態(追跡・戦闘をしていない)の中からランダムに選んで消すメソッド
    /// 消せなかった分は、次の人数の確認でもう一度消せるか確認する
    /// </summary>
    /// <param name="group">増援のまとまり</param>
    /// <param name="excessCount">多すぎる人数</param>
    private void RemoveExcessMembers(CS_PoliceWantedGroup group, int excessCount)
    {
        _removableBuffer.Clear();
        foreach (CS_PoliceBrain member in group.members)
        {
            if (member.isIdle) _removableBuffer.Add(member);
        }

        for (int i = 0; i < excessCount && _removableBuffer.Count > 0; i++)
        {
            int index = Random.Range(0, _removableBuffer.Count);
            CS_PoliceBrain member = _removableBuffer[index];

            // 選んだ警察は末尾の警察と入れ替えてから外す(リストの詰め直しをしないため)
            _removableBuffer[index] = _removableBuffer[_removableBuffer.Count - 1];
            _removableBuffer.RemoveAt(_removableBuffer.Count - 1);

            group.RemoveMember(member);
            FadeOutMember(member);
        }
    }

    /// <summary>
    /// 増援を消える演出の後に消すメソッド(演出が無い場合はすぐ消す)
    /// </summary>
    /// <param name="member">消す増援</param>
    private void FadeOutMember(CS_PoliceBrain member)
    {
        if (member.TryGetComponent(out CS_PoliceFadeOut fadeOut))
        {
            fadeOut.StartFadeOut();
            return;
        }

        CS_PoliceSpawnUtility.Despawn(member.gameObject);
    }

    /// <summary>
    /// プレイヤーの周囲(外周)で、出現できる位置をランダムに探すメソッド
    /// </summary>
    /// <param name="center">プレイヤーの位置</param>
    /// <param name="position">出現できる位置</param>
    /// <returns>見つかればtrue</returns>
    private bool TryFindSpawnPosition(Vector3 center, out Vector3 position)
    {
        for (int i = 0; i < _spawnAttempts; i++)
        {
            float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
            Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * _spawnRadius;

            // 建物の中など、歩ける場所(NavMesh)が近くに無い位置には出現させない
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _navMeshSearchDistance, NavMesh.AllAreas)) continue;

            // 屋根の上・閉じた区画など、プレイヤーまで歩いて行けない場所には出現させない
            if (!CanReach(hit.position, center)) continue;

            position = hit.position;
            return true;
        }

        position = center;
        return false;
    }

    /// <summary>
    /// 出現位置からプレイヤーの位置まで、NavMesh上を歩いて行けるかを判定するメソッド
    /// </summary>
    /// <param name="from">出現位置</param>
    /// <param name="playerPosition">プレイヤーの位置</param>
    /// <returns>歩いて行ければtrue(プレイヤーが空中にいるなどで判定できない場合もtrue)</returns>
    private bool CanReach(Vector3 from, Vector3 playerPosition)
    {
        // プレイヤーはジャンプ中などでNavMeshから離れていることがあるので、少し広めに足元を探す
        if (!NavMesh.SamplePosition(playerPosition, out NavMeshHit playerHit, _navMeshSearchDistance * 2.0f, NavMesh.AllAreas)) return true;

        return NavMesh.CalculatePath(from, playerHit.position, NavMesh.AllAreas, _reachablePath)
            && _reachablePath.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>
    /// 増援の速度・視野距離を、手配度に合わせて変更するメソッド
    /// </summary>
    /// <param name="member">変更する増援</param>
    /// <param name="level">持ち主の手配度</param>
    private void ApplyAbility(CS_PoliceBrain member, int level)
    {
        // 手配度0(消えるのを待っている間)は、データのある一番低い手配度の強さにする
        member.ChangeAbility(CreateSpeedTable(level), _levelData.GetLevel(level).viewDistance);
    }

    /// <summary>
    /// 増援の移動状態ごとの速度を、プレイヤーの基準速度×ステータスの倍率×手配度の倍率で作るメソッド
    /// </summary>
    /// <param name="level">持ち主の手配度</param>
    /// <returns>移動状態ごとの速度</returns>
    private Dictionary<CSE_PoliceMoveState, float> CreateSpeedTable(int level)
    {
        float baseSpeed = _playerBaseStats.moveSpeed;
        float walkSpeed = baseSpeed * _status.patrolSpeedMultiplier;
        float chaseSpeed = baseSpeed * _status.chaseSpeedMultiplier * _levelData.GetLevel(level).chaseSpeedMultiplier;

        // 追跡・駆け付けだけ手配度で速くし、探索・待機は巡回の速さにする(増援は巡回しないが、念のため設定する)
        return new Dictionary<CSE_PoliceMoveState, float>
        {
            { CSE_PoliceMoveState.Patrol, walkSpeed },
            { CSE_PoliceMoveState.Chase, chaseSpeed },
            { CSE_PoliceMoveState.Rush, chaseSpeed },
            { CSE_PoliceMoveState.Search, walkSpeed },
            { CSE_PoliceMoveState.Wait, walkSpeed },
        };
    }

    /// <summary>
    /// 必要な項目がすべて設定されているかを判定するメソッド
    /// </summary>
    /// <returns>設定されていればtrue</returns>
    private bool IsSettingValid()
    {
        if (_policePrefab != null && _status != null && _playerBaseStats != null
            && _levelData != null && _levelData.maxLevel > 0)
        {
            return true;
        }

        Debug.LogError("CS_PoliceWantedManager: 未設定の項目があります(手配度データの要素数が0になっていないかも確認してください)", this);
        return false;
    }
}
