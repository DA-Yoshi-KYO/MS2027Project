/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-05 | 初回作成
 * 2026-11-01 | 出現位置を探す処理と、速度の表を作る処理を、内通フォンと増援で共通に使えるよう移した
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 警察の出現・削除をまとめたクラス
/// ネットワーク接続中(サーバー)はクライアントにも出現・削除させ、オフラインでは通常の生成・破棄を行う
/// </summary>
public static class CS_PoliceSpawnUtility
{
    // 出現位置を探す回数(歩ける場所・標的までたどり着ける場所が見つからなければ、出現させない)
    private const int _spawnPickAttempts = 8;

    // 出現位置の近くの歩ける場所(NavMesh)を探す距離
    private const float _sampleDistance = 3.0f;

    // 標的の足元の歩ける場所(NavMesh)を探す距離
    private const float _targetSampleDistance = 5.0f;

    // 出現位置が標的までたどり着けるかを調べる際に使い回す経路
    private static readonly NavMeshPath _path = new NavMeshPath();

    /// <summary>
    /// 警察を1人出現させるメソッド(サーバー、またはオフラインで呼ぶこと)
    /// </summary>
    /// <param name="prefab">警察のプレハブ</param>
    /// <param name="position">出現位置</param>
    /// <param name="rotation">出現時の向き</param>
    /// <returns>出現させた警察</returns>
    public static CS_PoliceBrain Spawn(CS_PoliceBrain prefab, Vector3 position, Quaternion rotation)
    {
        CS_PoliceBrain member = Object.Instantiate(prefab, position, rotation);

        // ネットワーク接続中は、クライアントにも出現させる
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
            && member.TryGetComponent(out NetworkObject networkObject))
        {
            networkObject.Spawn(true);
        }

        return member;
    }

    /// <summary>
    /// 警察を消すメソッド(サーバー、またはオフラインで呼ぶこと)
    /// </summary>
    /// <param name="police">消す警察</param>
    public static void Despawn(GameObject police)
    {
        // ネットワークに出現済みなら、クライアント側も含めて消す
        if (police.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned)
        {
            networkObject.Despawn(true);
            return;
        }

        Object.Destroy(police);
    }

    /// <summary>
    /// プレイヤーから決められた距離の範囲にある、歩いてプレイヤーの所まで行ける出現位置を探すメソッド
    /// 向きはランダムに選び、歩けない場所・標的までたどり着けない場所は避ける
    /// </summary>
    /// <param name="center">プレイヤーの位置</param>
    /// <param name="distanceA">プレイヤーからの距離の端(m)</param>
    /// <param name="distanceB">プレイヤーからの距離のもう一方の端(m。大小どちらでもよい)</param>
    /// <param name="position">見つけた出現位置</param>
    /// <returns>見つかったらtrue</returns>
    public static bool TryFindSpawnPosition(Vector3 center, float distanceA, float distanceB, out Vector3 position)
    {
        position = center;

        // 標的までの道のりは、標的の足元のNavMesh上の位置から調べる
        if (!NavMesh.SamplePosition(center, out NavMeshHit origin, _targetSampleDistance, NavMesh.AllAreas)) return false;

        float minDistance = Mathf.Min(distanceA, distanceB);
        float maxDistance = Mathf.Max(distanceA, distanceB);
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
    /// 呼ばれた・増援の警察の、移動状態ごとの速度を、プレイヤーの基準速度×倍率で作るメソッド
    /// 標的の現在地へ向かう(駆け付け)のは追跡と同じ速さ、探索・怪しんで近づくのは巡回の速さにする
    /// </summary>
    /// <param name="playerBaseStats">速度倍率の基準にするプレイヤーのステータス</param>
    /// <param name="policeStatus">警察のステータス</param>
    /// <returns>移動状態ごとの速度</returns>
    public static Dictionary<CSE_PoliceMoveState, float> CreateSpeedTable(CSO_PlayerStats playerBaseStats, CSO_PoliceStatus policeStatus)
    {
        float baseSpeed = playerBaseStats.moveSpeed;
        float searchSpeed = baseSpeed * policeStatus.patrolSpeedMultiplier;
        float chaseSpeed = baseSpeed * policeStatus.chaseSpeedMultiplier;

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
