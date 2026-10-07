/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-05 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 警察の出現・削除をまとめたクラス
/// ネットワーク接続中(サーバー)はクライアントにも出現・削除させ、オフラインでは通常の生成・破棄を行う
/// </summary>
public static class CS_PoliceSpawnUtility
{
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
}
