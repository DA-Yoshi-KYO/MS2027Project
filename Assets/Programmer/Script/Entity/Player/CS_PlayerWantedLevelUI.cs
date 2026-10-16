using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの手配度を手配度UI(CS_UIWantedLevelModel)へ反映するクラス
 * CS_PlayerWantedLevelから手配度の変化を受け取り、UI側のModelを更新するだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・UIの使い方自体はClaudeDocs/手配度UIの使い方.md参照。ここでは実際のプレイヤーへの組み込みのみ行う
 * ・手配度UIは画面に1つだけ。Modelは全クライアントで全プレイヤー分をCS_Player.playerNumberでBindし、
 *   画面に出るのは自分の番号の分だけ(UI側のPresenterがlocalPlayerNumberと一致する番号だけを表示する)
 *   自分のプレイヤーはBindより先にSetLocalPlayerNumberで番号を登録する(逆だとPresenterがBindの通知を見逃す)
 * ・自分かどうかはIsOwnerではなくCS_Player.isLocalPlayerで判定する(NPCはホストでIsOwnerになるため)
 * ・手配度の実体はCS_PlayerWantedLevel側のNetworkVariableにあり、ここは表示専用。数値の変更は一切行わない
 * ・Modelは手配度0で始まるため、作った直後に今の手配度(途中参加などで0でないことがある)へ合わせる
 * ・最大手配度(星の数)はCS_PlayerWantedLevel.maxLevelを使う(UI側は渡された数だけ星を作る)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く(playerNumberは既定値の0になる)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerWantedLevel))]
public class CS_PlayerWantedLevelUI : NetworkBehaviour
{
    private CS_Player _player;
    private CS_PlayerWantedLevel _wantedLevel;
    private CS_UIWantedLevelModel _wantedModel;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _wantedLevel = GetComponent<CS_PlayerWantedLevel>();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        BindWantedLevelUI(_player.isLocalPlayer);
    }

    public override void OnNetworkSpawn()
    {
        BindWantedLevelUI(_player.isLocalPlayer);   // NPCはホストでIsOwnerになるため、isLocalPlayerで判定する
    }

    public override void OnNetworkDespawn()
    {
        UnbindWantedLevelUI();
    }

    public override void OnDestroy()
    {
        UnbindWantedLevelUI();
        base.OnDestroy();
    }

    // 手配度UIのModelを作り、自分のplayerNumberで公開する
    private void BindWantedLevelUI(bool isLocalPlayer)
    {
        // 自分の番号を先に登録しておく(Bindの通知を受けた時点でPresenterが自分の分だと判断できるように)
        if (isLocalPlayer)
        {
            CS_UIWantedLevelModel.SetLocalPlayerNumber(_player.playerNumber);
        }

        _wantedModel = new CS_UIWantedLevelModel(_wantedLevel.maxLevel);
        _wantedModel.SetLevel(_wantedLevel.level);   // 途中参加などで、すでに手配度がある場合に合わせる
        _wantedModel.Bind(_player.playerNumber);

        _wantedLevel.onLevelChanged += HandleLevelChanged;
    }

    private void UnbindWantedLevelUI()
    {
        if (_wantedModel == null) return;

        _wantedLevel.onLevelChanged -= HandleLevelChanged;
        _wantedModel.Dispose();
        _wantedModel = null;
    }

    // 手配度が変わるたびにUI側のModelへ反映する
    private void HandleLevelChanged(int level)
    {
        _wantedModel.SetLevel(level);
    }
}
