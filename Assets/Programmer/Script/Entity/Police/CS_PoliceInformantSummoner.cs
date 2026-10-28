/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 内通フォンの効果として、使用者以外のプレイヤーの近くに警察を呼ぶクラス
/// ・生きているプレイヤー(人・NPC)のうち、使用者以外のそれぞれの近くに、データで決めた人数の警察を出現させる
/// ・呼ばれた警察は、プレイヤーを追跡する(CS_PoliceInformantGroupが決める)
/// 出現はサーバー(またはオフライン)だけで行う
/// </summary>
public static class CS_PoliceInformantSummoner
{
    // 出現位置を探す回数(歩ける場所・標的までたどり着ける場所が見つからなければ、そのプレイヤーには出現させない)
    private const int _spawnPickAttempts = 8;

    // 出現位置の近くの歩ける場所(NavMesh)を探す距離
    private const float _sampleDistance = 3.0f;

    // 標的の足元の歩ける場所(NavMesh)を探す距離
    private const float _targetSampleDistance = 5.0f;

    // 出現位置が標的までたどり着けるかを調べる際に使い回す経路
    private static readonly NavMeshPath _path = new NavMeshPath();

    /// <summary>
    /// 使用者以外のプレイヤーの近くに、警察を呼ぶメソッド(サーバー、またはオフラインで呼ぶこと)
    /// </summary>
    /// <param name="user">アイテムの使用者(プレイヤー)。使用者の近くには呼ばない</param>
    /// <param name="data">内通フォンのデータ</param>
    /// <returns>呼んだ警察の人数</returns>
    public static int Summon(GameObject user, CSO_PoliceInformantData data)
    {
        if (CS_PoliceSquad.IsNetworkClientOnly()) return 0;

        if (!IsDataValid(data))
        {
            Debug.LogError("CS_PoliceInformantSummoner: 内通フォンのデータの未設定の項目があります(警察のプレハブ・ステータス・プレイヤーの基準ステータス)", data);
            return 0;
        }

        CS_Player userPlayer = user != null ? user.GetComponentInParent<CS_Player>() : null;
        Dictionary<CSE_PoliceMoveState, float> speedTable = CreateSpeedTable(data);

        int summonedCount = 0;
        foreach (CS_Player player in CS_Player.activePlayers)
        {
            if (player == null || player == userPlayer) continue;
            if (!player.TryGetComponent(out CS_PlayerHealth health) || health.isDead) continue;

            summonedCount += SummonAround(health, data, speedTable);
        }

        return summonedCount;
    }

    /// <summary>
    /// 必要な項目がすべて設定されているかを判定するメソッド
    /// </summary>
    private static bool IsDataValid(CSO_PoliceInformantData data)
    {
        return data != null && data.policePrefab != null && data.policeStatus != null && data.playerBaseStats != null;
    }

    /// <summary>
    /// 1人のプレイヤーの近くに、警察を呼ぶメソッド
    /// </summary>
    /// <returns>呼んだ警察の人数</returns>
    private static int SummonAround(CS_PlayerHealth target, CSO_PoliceInformantData data, Dictionary<CSE_PoliceMoveState, float> speedTable)
    {
        int summonedCount = 0;
        for (int i = 0; i < data.policeCountPerPlayer; i++)
        {
            if (!TryFindSpawnPosition(target.transform.position, data, out Vector3 position)) continue;

            SpawnOne(target, data, speedTable, position);
            summonedCount++;
        }

        return summonedCount;
    }

    /// <summary>
    /// 警察を1人出現させ、標的を追跡させるメソッド
    /// </summary>
    private static void SpawnOne(CS_PlayerHealth target, CSO_PoliceInformantData data, Dictionary<CSE_PoliceMoveState, float> speedTable, Vector3 position)
    {
        // 出現した時に、標的の方を向いている
        Vector3 toTarget = target.transform.position - position;
        toTarget.y = 0.0f;
        Quaternion rotation = toTarget.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toTarget) : Quaternion.identity;

        CS_PoliceBrain police = CS_PoliceSpawnUtility.Spawn(data.policePrefab, position, rotation);
        police.Setting(new CS_PoliceInformantGroup(target, data.homingDuration), speedTable, data.policeStatus);
    }

    /// <summary>
    /// プレイヤーから決められた距離の範囲にある、歩いてプレイヤーの所まで行ける出現位置を探すメソッド
    /// </summary>
    /// <param name="center">プレイヤーの位置</param>
    /// <param name="data">内通フォンのデータ</param>
    /// <param name="position">見つけた出現位置</param>
    /// <returns>見つかったらtrue</returns>
    private static bool TryFindSpawnPosition(Vector3 center, CSO_PoliceInformantData data, out Vector3 position)
    {
        position = center;

        // 標的までの道のりは、標的の足元のNavMesh上の位置から調べる
        if (!NavMesh.SamplePosition(center, out NavMeshHit origin, _targetSampleDistance, NavMesh.AllAreas)) return false;

        float minDistance = Mathf.Min(data.spawnDistanceMin, data.spawnDistanceMax);
        float maxDistance = Mathf.Max(data.spawnDistanceMin, data.spawnDistanceMax);
        for (int i = 0; i < _spawnPickAttempts; i++)
        {
            Vector3 direction = Quaternion.Euler(0.0f, Random.Range(0.0f, 360.0f), 0.0f) * Vector3.forward;
            Vector3 candidate = center + direction * Random.Range(minDistance, maxDistance);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _sampleDistance, NavMesh.AllAreas)) continue;

            // 壁の向こうなど、歩いて標的の所まで行けない位置には出現させない(追い付けずに消えるだけになる)
            if (!NavMesh.CalculatePath(hit.position, origin.position, NavMesh.AllAreas, _path)) continue;
            if (_path.status != NavMeshPathStatus.PathComplete) continue;

            position = hit.position;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 警察の移動状態ごとの速度を、プレイヤーの基準速度×倍率で作るメソッド
    /// </summary>
    private static Dictionary<CSE_PoliceMoveState, float> CreateSpeedTable(CSO_PoliceInformantData data)
    {
        float baseSpeed = data.playerBaseStats.moveSpeed;
        float searchSpeed = baseSpeed * data.policeStatus.patrolSpeedMultiplier;
        float chaseSpeed = baseSpeed * data.policeStatus.chaseSpeedMultiplier;

        // 標的の現在地へ向かう(駆け付け)のは追跡と同じ速さ、探索・怪しんで近づくのは巡回の速さにする
        return new Dictionary<CSE_PoliceMoveState, float>
        {
            { CSE_PoliceMoveState.Patrol, searchSpeed },
            { CSE_PoliceMoveState.Chase, chaseSpeed },
            { CSE_PoliceMoveState.Rush, chaseSpeed },
            { CSE_PoliceMoveState.Search, searchSpeed },
            { CSE_PoliceMoveState.Suspect, searchSpeed },
        };
    }
}
