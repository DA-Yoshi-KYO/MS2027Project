using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーのコンポーネントで共通に使う、ネットワークまわりの判定をまとめたクラス
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・どのコンポーネントにも同じ判定が散らばっていたため、ここにまとめた(書き方を揃えて、読み間違いを防ぐため)
 * ・IsServerOrOffline: 値を決める(NetworkVariableに書く・判定を確定する)のは、サーバーか、オフライン(スポーン前・テストシーン)だけ
 *   スポーン済みのクライアントはfalse(同期された値を読むだけ)
 * ・GetSyncedTime: 全員で揃う時刻。スポーン中はサーバーの時刻(NetworkManager.ServerTime)、オフラインではTime.timeAsDouble
 *   終わる時刻をNetworkVariableで配る処理(復活・スタン・必殺技の効果など)は、これで比べる
 * ・isOnline: NetworkManagerが動いているか(オフラインのテストシーンでは、Startで自分から初期化するのに使う)
 * ・NetworkBehaviourの拡張メソッドなので、使う側では this.IsServerOrOffline() のように書く
 *   (NetworkBehaviourのHasAuthorityと同じ名前を付けると、警告(CS0108)が出て紛らわしいため、名前を変えている)
 */
// ========================================

public static class CS_PlayerNetwork
{
    // NetworkManagerが動いているか
    public static bool isOnline => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    // 値を決めてよいか(サーバー、またはオフライン)
    public static bool IsServerOrOffline(this NetworkBehaviour behaviour)
    {
        return !behaviour.IsSpawned || behaviour.IsServer;
    }

    // 全員で揃う時刻(スポーン中はサーバーの時刻、オフラインではTime.timeAsDouble)
    public static double GetSyncedTime(this NetworkBehaviour behaviour)
    {
        return behaviour.IsSpawned ? behaviour.NetworkManager.ServerTime.Time : Time.timeAsDouble;
    }
}
