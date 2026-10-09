using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーのHPをHP UI(CS_UIPlayerHpModel)へ反映するクラス
 * CS_PlayerHealthからHPの状態を受け取り、UI側のModelを更新するだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・UIの使い方自体はClaudeDocs/PlayerHpUIの使い方.md参照。ここでは実際のプレイヤーへの組み込みのみ行う
 * ・どの番号(0〜3)のゲージに表示するかはCS_Player.playerNumberを使う
 *   (CS_PlayerSpawnerが生成時に、接続順で0,1,2,3...(4人を超えたら再び0から)割り当てる)
 * ・Modelの生成・Bindは全クライアントで全プレイヤー分行うが、画面に出すのは自分(Owner)の番号だけ
 *   (UI側のPresenterがCS_UIPlayerHpModel.localPlayerNumberと一致する番号だけを表示するため、
 *    自分のプレイヤーはBindより先にSetLocalPlayerNumberで番号を登録する)
 *   (HPの実体はCS_PlayerHealth側のNetworkVariableにあり、ここは表示専用。数値の変更は一切行わない)
 * ・CS_PlayerHealthのオフライン初期化(Start)より後に現在HPを読む必要があるため、
 *   CS_PlayerVisualと同じ実行順(2)にしている
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く(playerNumberは既定値の0になる)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerHealth))]
[DefaultExecutionOrder(2)] // CS_PlayerHealth.Start(オフライン時のHP初期化)の後に、基準となるHPを読むため
public class CS_PlayerHpUI : NetworkBehaviour
{
    private CS_Player _player;
    private CS_PlayerHealth _health;
    private CS_UIPlayerHpModel _hpModel;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _health = GetComponent<CS_PlayerHealth>();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        BindHpUI(true);
    }

    public override void OnNetworkSpawn()
    {
        BindHpUI(IsOwner);
    }

    public override void OnNetworkDespawn()
    {
        UnbindHpUI();
    }

    public override void OnDestroy()
    {
        UnbindHpUI();
        base.OnDestroy();
    }

    // HP UIのModelを作り、自分のplayerNumberで公開する
    private void BindHpUI(bool isLocalPlayer)
    {
        // 自分の番号を先に登録しておく(Bindの通知を受けた時点でPresenterが自分の分だと判断できるように)
        if (isLocalPlayer)
        {
            CS_UIPlayerHpModel.SetLocalPlayerNumber(_player.playerNumber);
        }

        _hpModel = new CS_UIPlayerHpModel((int)_health.maxHp, (int)_health.currentHp);
        _hpModel.Bind(_player.playerNumber);

        _health.onHpChanged += HandleHpChanged;
    }

    private void UnbindHpUI()
    {
        if (_hpModel == null) return;

        _health.onHpChanged -= HandleHpChanged;
        _hpModel.Dispose();
        _hpModel = null;
    }

    // HPが変わるたびにUI側のModelへ反映する(最大値も一緒に渡ってくるので、変更があれば追従する)
    private void HandleHpChanged(float current, float max)
    {
        _hpModel.SetMaxHp((int)max);
        _hpModel.SetHp((int)current);
    }
}
