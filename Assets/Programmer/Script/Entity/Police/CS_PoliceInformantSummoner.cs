/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * 2026-11-01 | 出現位置を探す処理と速度の表を作る処理を、CS_PoliceSpawnUtilityへ移した
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 内通フォンの効果として、使用者以外のプレイヤーの近くに警察を呼ぶクラス
/// ・生きているプレイヤー(人・NPC)のうち、使用者以外のそれぞれの近くに、データで決めた人数の警察を出現させる
/// ・呼ばれた警察は、プレイヤーを追跡する(CS_PoliceInformantGroupが決める)
/// 出現はサーバー(またはオフライン)だけで行う
/// </summary>
public static class CS_PoliceInformantSummoner
{
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
        Dictionary<CSE_PoliceMoveState, float> speedTable = CS_PoliceSpawnUtility.CreateSpeedTable(data.playerBaseStats, data.policeStatus);

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
            if (!CS_PoliceSpawnUtility.TryFindSpawnPosition(target.transform.position, data.spawnDistanceMin, data.spawnDistanceMax, out Vector3 position)) continue;

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
}
