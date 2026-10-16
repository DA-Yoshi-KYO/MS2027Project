using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの頭上の順位UI(CS_UIPlayerRankOverhead)をBindするクラス
 * 「このプレイヤーの番号」と「画面を見ている自分(ローカルプレイヤー)のカメラ」を渡すだけ
 * UI側はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・UIの使い方自体はClaudeDocs/頭上順位UIの使い方.md参照。ここでは実際のプレイヤーへの組み込みのみ行う
 * ・順位UI本体(PlayerRank.prefab)はPlayer.prefabの子に置いてあるので、GetComponentInChildrenで取る
 * ・他人の頭上に出すUIのため、全クライアントで全プレイヤー分Bindする
 *   (自分の頭上はUI側が自動で隠す)
 * ・カメラは、どのプレイヤーのUIにも「ローカルプレイヤーのカメラ」を渡す
 *   (他人のプレイヤーのカメラはCS_Playerが無効化しているため、それを渡すと正しい方向を向かない)
 *   → 他人のプレイヤーが先にスポーンした場合(後から参加したクライアントなど)は、その時点でローカルのカメラが無いため、
 *      ローカルプレイヤーのスポーン時に、出現中の全プレイヤー分をBindし直す
 * ・Unbindは、UI側がOnNetworkDespawn / OnDestroyで自動で行う
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く(自分のカメラでBindする。playerNumberは0)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerRankOverheadUI : NetworkBehaviour
{
    private static readonly List<CS_PlayerRankOverheadUI> _activePlayers = new List<CS_PlayerRankOverheadUI>();
    private static Camera _localCamera;     // 画面を見ている自分(ローカルプレイヤー)のカメラ

    private CS_Player _player;
    private CS_UIPlayerRankOverhead _rankOverhead;

    // Domain Reloadを切っている場合に、前回の再生の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activePlayers.Clear();
        _localCamera = null;
    }

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _rankOverhead = GetComponentInChildren<CS_UIPlayerRankOverhead>(true);
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        Register(_player.isLocalPlayer);
    }

    public override void OnNetworkSpawn()
    {
        Register(_player.isLocalPlayer);   // NPCはホストでIsOwnerになるため、isLocalPlayerで判定する
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

    private void Register(bool isLocalPlayer)
    {
        if (!_activePlayers.Contains(this))
        {
            _activePlayers.Add(this);
        }

        if (!isLocalPlayer)
        {
            BindRankOverhead();
            return;
        }

        // 自分のカメラがそろったので、先にスポーンしていた他プレイヤーの分も含めて全員Bindし直す
        _localCamera = _player.playerCamera;
        foreach (CS_PlayerRankOverheadUI player in _activePlayers)
        {
            player.BindRankOverhead();
        }
    }

    private void Unregister()
    {
        _activePlayers.Remove(this);

        // 自分のプレイヤーが消えたら、そのカメラはもう使えない
        if (_localCamera != null && _localCamera == _player.playerCamera)
        {
            _localCamera = null;
        }
    }

    // playerNumberは「このプレイヤー」、カメラは「ローカルプレイヤー」のものを渡す
    private void BindRankOverhead()
    {
        if (_rankOverhead == null) return;
        if (_localCamera == null) return;   // ローカルプレイヤーのスポーン時にまとめてBindされる

        // 自分の頭上は隠す。NPCはホストでIsOwnerになるため、自分かどうかもプレイヤー側から渡す
        _rankOverhead.Bind(_player.playerNumber, _localCamera, _player.isLocalPlayer);
    }
}
