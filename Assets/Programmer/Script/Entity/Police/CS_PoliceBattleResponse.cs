/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-28 | 初回作成(Issue #569)
 * ================================================ */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 悪人グループとプレイヤーの戦闘が始まったら、少し離れた見えない場所に警察を出して、現場へ向かわせるクラス
/// CS_PoliceAlertManagerが持ち、サーバー(またはオフライン)だけで毎フレーム呼ぶ。設定はCSO_PoliceAlertData
/// ・戦闘の始まり: プレイヤーが悪人を攻撃した(CS_VillainHealth.onDamagedBy) / 悪人がプレイヤーを狙って攻撃を始めた(CS_VillainCombat.isAttacking)の、早い方
/// ・きっかけは悪人グループごと。同じ悪人グループには、1回だけ出す(グループに入っていない悪人は、1人で1グループとして扱う)
/// ・戦闘が始まってから、決めた時間(初期値5秒)が経ってから出す。戦闘の場所は、始まった時の場所
/// ・出す場所: 戦闘の場所から決めた距離の、歩いて現場へ行ける場所のうち、どのプレイヤーからも壁などで見えない所
///   見つからなければ、少しの間は探し直し、それでも無ければ出さない
/// 悪人は、プレイヤーの近くにいる物だけを調べる(シーン全体は探さない)
/// </summary>
public class CS_PoliceBattleResponse
{
    // 近くの悪人を調べる間隔(秒)
    private const float _scanInterval = 0.2f;

    // 出現場所が見つからない時に、探し直す間隔と、あきらめるまでの時間(秒)
    private const float _retryInterval = 0.5f;
    private const float _retryDuration = 5.0f;

    // 出現場所を、1回の探索で試す回数と、後半の試行で距離を伸ばす倍率
    private const int _pickRounds = 6;
    private const float _widenDistanceScale = 1.5f;

    // 同時に出す警察同士を、これより近くに出さない(m)
    private const float _minSpacing = 2.0f;

    // 見えるかを調べる高さ(足元から。m): プレイヤーの目と、出現する警察の頭・胴
    private const float _eyeHeight = 1.6f;
    private static readonly float[] _bodyHeights = { 1.6f, 0.8f };

    /// <summary>
    /// 出現場所を探している途中の、警察の出現の予約
    /// </summary>
    private class PendingSpawn
    {
        public Vector3 site;
        public CS_PoliceGatherPoint point;     // 同じ戦闘の警察は、同じインスタンスを目印にする(現場の周りに、分かれて立つ)
        public Func<bool> isBattleActive;
        public int remaining;
        public float deadline;
        public float nextTryTime;
        public readonly List<Vector3> usedPositions = new List<Vector3>();
    }

    private readonly CSO_PoliceAlertData _data = null;

    // 見張っている悪人(攻撃されたことを知るために、イベントを登録している)
    private readonly Dictionary<CS_VillainHealth, Action<GameObject, float>> _watched = new Dictionary<CS_VillainHealth, Action<GameObject, float>>();
    private readonly List<CS_VillainHealth> _removeBuffer = new List<CS_VillainHealth>();

    // すでに警察を出した悪人グループ(グループが無い悪人は、悪人自身)
    private readonly HashSet<object> _responded = new HashSet<object>();

    private readonly List<PendingSpawn> _pending = new List<PendingSpawn>();
    private readonly Collider[] _overlapBuffer = new Collider[128];
    private float _scanTimer = 0.0f;

    public CS_PoliceBattleResponse(CSO_PoliceAlertData data)
    {
        _data = data;
    }

    /// <summary>
    /// 戦闘の始まりを調べ、予約している警察を出すメソッド(サーバー、またはオフラインで毎フレーム呼ぶこと)
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!_data.hasBattlePolice) return;

        _scanTimer -= deltaTime;
        if (_scanTimer <= 0.0f)
        {
            _scanTimer = _scanInterval;
            RemoveGoneVillains();
            WatchNearbyVillains();
            CheckVillainAttacks();
        }

        SpawnPending();
    }

    /// <summary>
    /// イベントの登録を外し、覚えていることを捨てるメソッド
    /// </summary>
    public void Clear()
    {
        foreach (KeyValuePair<CS_VillainHealth, Action<GameObject, float>> pair in _watched)
        {
            if (pair.Key != null) pair.Key.onDamagedBy -= pair.Value;
        }
        _watched.Clear();
        _responded.Clear();
        _pending.Clear();
    }

    // 消えた悪人を、見張りから外す
    private void RemoveGoneVillains()
    {
        _removeBuffer.Clear();
        foreach (CS_VillainHealth villain in _watched.Keys)
        {
            if (villain == null) _removeBuffer.Add(villain);
        }
        foreach (CS_VillainHealth villain in _removeBuffer) _watched.Remove(villain);
    }

    // プレイヤーの近くにいる悪人を、見張りに加える(攻撃される前に、イベントを登録しておく)
    private void WatchNearbyVillains()
    {
        foreach (CS_Player player in CS_Player.activePlayers)
        {
            if (player == null) continue;

            int count = Physics.OverlapSphereNonAlloc(player.transform.position, _data.battleWatchRadius, _overlapBuffer,
                CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                CS_VillainHealth villain = _overlapBuffer[i].GetComponentInParent<CS_VillainHealth>();
                if (villain == null || villain.isDefeated || _watched.ContainsKey(villain)) continue;

                CS_VillainHealth watchedVillain = villain;
                Action<GameObject, float> handler = (attacker, damage) => HandleVillainDamaged(watchedVillain, attacker);
                villain.onDamagedBy += handler;
                _watched.Add(villain, handler);
            }
        }
    }

    // プレイヤーが悪人を攻撃した時
    private void HandleVillainDamaged(CS_VillainHealth villain, GameObject attacker)
    {
        if (villain == null || attacker == null || !_data.hasBattlePolice) return;

        CS_PlayerHealth player = attacker.GetComponentInParent<CS_PlayerHealth>();
        if (player == null) return;

        StartBattle(villain);
    }

    // 悪人がプレイヤーを狙って攻撃を始めているかを調べる
    private void CheckVillainAttacks()
    {
        foreach (CS_VillainHealth villain in _watched.Keys)
        {
            if (villain == null || villain.isDefeated || !villain.TryGetComponent(out CS_VillainCombat combat)) continue;
            if (!combat.isAttacking || combat.target == null || combat.target.GetComponentInParent<CS_PlayerHealth>() == null) continue;

            StartBattle(villain);
        }
    }

    // 戦闘が始まった。このグループにまだ警察を出していなければ、出現を予約する
    private void StartBattle(CS_VillainHealth villain)
    {
        CS_VillainCombat combat = villain.GetComponent<CS_VillainCombat>();
        CS_VillainGroup group = combat != null ? combat.group : null;
        object key = group != null ? (object)group : villain;
        if (!_responded.Add(key)) return;

        Func<bool> isBattleActive;
        if (group != null) isBattleActive = () => IsAnyMemberEngaged(group);
        else isBattleActive = () => IsEngaged(combat);

        _pending.Add(new PendingSpawn
        {
            site = villain.transform.position,
            point = new CS_PoliceGatherPoint(villain.transform.position),   // 信号の主は指定しない(駆け付けているだけでは、プレイヤーを警戒状態にしない)
            isBattleActive = isBattleActive,
            remaining = _data.battlePoliceCount,
            deadline = Time.time + _data.battleSpawnDelay + _retryDuration,   // 探し直しの期限は、出し始めてから数える
            nextTryTime = Time.time + _data.battleSpawnDelay,
        });
    }

    private static bool IsAnyMemberEngaged(CS_VillainGroup group)
    {
        foreach (CS_VillainCrime member in group.members)
        {
            if (member != null && !member.isDefeated && IsEngaged(member.GetComponent<CS_VillainCombat>())) return true;
        }
        return false;
    }

    private static bool IsEngaged(CS_VillainCombat combat)
    {
        return combat != null && combat.enabled && combat.isEngaged;
    }

    // 予約している警察を出す(場所が見つからなければ、期限まで探し直す)
    private void SpawnPending()
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            PendingSpawn pending = _pending[i];
            if (Time.time < pending.nextTryTime) continue;

            pending.nextTryTime = Time.time + _retryInterval;
            while (pending.remaining > 0 && TryFindHiddenPosition(pending, out Vector3 position))
            {
                Spawn(pending, position);
            }

            if (pending.remaining <= 0 || Time.time >= pending.deadline) _pending.RemoveAt(i);
        }
    }

    // 戦闘の場所から決めた距離の、歩いて行けて、どのプレイヤーからも見えない場所を探す
    private bool TryFindHiddenPosition(PendingSpawn pending, out Vector3 position)
    {
        float distance = _data.battleSpawnDistance;
        for (int round = 0; round < _pickRounds; round++)
        {
            // 前半は決めた距離ちょうど、後半は少し遠くまで探す
            float farthest = round < _pickRounds / 2 ? distance : distance * _widenDistanceScale;
            if (!CS_PoliceSpawnUtility.TryFindSpawnPosition(pending.site, distance, farthest, out position)) continue;

            // ソロ: 今のエリアの扇形の外には出さない
            if (!CS_SoloAreaProgression.IsActiveAt(position)) continue;
            if (IsNearUsedPosition(pending, position) || IsSeenByAnyPlayer(position)) continue;

            return true;
        }

        position = pending.site;
        return false;
    }

    private static bool IsNearUsedPosition(PendingSpawn pending, Vector3 position)
    {
        foreach (Vector3 used in pending.usedPositions)
        {
            if ((used - position).sqrMagnitude < _minSpacing * _minSpacing) return true;
        }
        return false;
    }

    // どれかのプレイヤーの目から、その場所に立つ警察の体が、壁などに遮られずに見えるか
    private static bool IsSeenByAnyPlayer(Vector3 position)
    {
        foreach (CS_Player player in CS_Player.activePlayers)
        {
            if (player == null) continue;

            Vector3 eye = player.transform.position + Vector3.up * _eyeHeight;
            foreach (float height in _bodyHeights)
            {
                if (CS_PoliceLineOfSight.IsClear(eye, position + Vector3.up * height, player.transform)) return true;
            }
        }
        return false;
    }

    // 警察を1人出して、戦闘の場所へ向かわせる
    private void Spawn(PendingSpawn pending, Vector3 position)
    {
        pending.remaining--;
        pending.usedPositions.Add(position);

        Vector3 toSite = pending.site - position;
        toSite.y = 0.0f;
        Quaternion rotation = toSite.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toSite) : Quaternion.identity;

        CS_PoliceBrain police = CS_PoliceSpawnUtility.Spawn(_data.reinforcePolice, position, rotation);
        police.Setting(new CS_PoliceBattleGroup(pending.isBattleActive),
            CS_PoliceSpawnUtility.CreateSpeedTable(_data.playerBaseStats, _data.reinforceStatus), _data.reinforceStatus);

        police.RequestRush(pending.point);
    }
}
