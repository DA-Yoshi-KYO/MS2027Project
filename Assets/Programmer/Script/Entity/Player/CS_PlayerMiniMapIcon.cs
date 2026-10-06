using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの位置をミニマップ(CS_MiniMapController)へ登録するクラス
 * 登録と解除だけを行い、位置・向きの更新はCS_MiniMapController側が自動で行う
 * ミニマップ側はこのクラス・CS_Playerの存在を知らない
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・使い方はClaudeDocs/ミニマップの使い方.md参照。ここでは実際のプレイヤーへの組み込みのみ行う
 * ・自分のプレイヤー(IsOwnerかつNPCでない): RegisterLocalPlayer(ミニマップの中心になる。解除のAPIは無い)
 *   他人のプレイヤー・NPC               : RegisterAlly(味方アイコン。消える時にUnregisterAlly)
 *   (NPCはサーバーがOwnerなので、ホストではIsOwnerだけだと自分扱いになってしまう)
 *   全クライアントがそれぞれ自分のミニマップに登録する(NetworkTransformで他人の位置が動く)
 * ・idは種類をまたいで重複させない(悪人・警察と混ざらないよう"Player_"を付ける)
 * ・シーンにCS_MiniMapControllerが無ければ何もしない(ミニマップの無いテストシーンでも動く)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでは、唯一のプレイヤーとして自分扱いで登録する
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerMiniMapIcon : NetworkBehaviour
{
    private CS_MiniMapController _miniMap;
    private string _allyId;     // 他人のプレイヤーとして登録した時のid(自分の場合はnull)

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        Register(!GetComponent<CS_Player>().isNpc, $"Player_{GetInstanceID()}");
    }

    public override void OnNetworkSpawn()
    {
        Register(IsOwner && !GetComponent<CS_Player>().isNpc, $"Player_{NetworkObjectId}");
    }

    public override void OnNetworkDespawn()
    {
        Unregister();
    }

    public override void OnDestroy()
    {
        Unregister();
        base.OnDestroy();
    }

    private void Register(bool isLocal, string id)
    {
        _miniMap = FindAnyObjectByType<CS_MiniMapController>();
        if (_miniMap == null) return;

        if (isLocal)
        {
            _miniMap.RegisterLocalPlayer(transform);
            return;
        }

        _allyId = id;
        _miniMap.RegisterAlly(_allyId, transform);
    }

    // 他人のプレイヤーだけ解除する(自分には解除のAPIが無い)
    private void Unregister()
    {
        if (_miniMap == null || _allyId == null) return;

        _miniMap.UnregisterAlly(_allyId);
        _allyId = null;
    }
}
