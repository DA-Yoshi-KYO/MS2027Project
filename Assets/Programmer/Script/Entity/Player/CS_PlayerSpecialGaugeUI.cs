using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの必殺ゲージを必殺技ゲージUI(CS_UISpecialGaugeModel)へ反映するクラス
 * CS_PlayerSpecialGaugeからゲージの状態を受け取り、UI側のModelを更新するだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・UIの使い方自体はClaudeDocs/UIModelの使い方.md参照。ここでは実際のプレイヤーへの組み込みのみ行う
 * ・Modelは全プレイヤー分をCS_Player.playerNumberでBindし、画面に出すのは自分(Owner)の番号だけ
 *   (UI側のPresenterがCS_UISpecialGaugeModel.localPlayerNumberと一致する番号だけを表示するため、
 *    自分のプレイヤーはBindより先にSetLocalPlayerNumberで番号を登録する)
 * ・ゲージの実体はCS_PlayerSpecialGauge側のNetworkVariableにあり、ここは表示専用。数値の変更は一切行わない
 * ・CS_UISpecialGaugeModelは最大値を後から変えられないため、上限(CS_PlayerStats.maxGauge)が変わったら
 *   Modelを作り直してBindし直す
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く(自分のプレイヤー扱い、playerNumberは0)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerSpecialGauge))]
[DefaultExecutionOrder(2)] // CS_PlayerStats.Start(オフライン時のステータス初期化)の後に、ゲージの上限を読むため
public class CS_PlayerSpecialGaugeUI : NetworkBehaviour
{
    private CS_Player _player;
    private CS_PlayerSpecialGauge _gauge;
    private CS_UISpecialGaugeModel _gaugeModel;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _gauge = GetComponent<CS_PlayerSpecialGauge>();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        BindGaugeUI(true);
    }

    public override void OnNetworkSpawn()
    {
        BindGaugeUI(IsOwner);
    }

    public override void OnNetworkDespawn()
    {
        UnbindGaugeUI();
    }

    public override void OnDestroy()
    {
        UnbindGaugeUI();
        base.OnDestroy();
    }

    // ゲージUIのModelを作り、自分のplayerNumberで公開する
    private void BindGaugeUI(bool isLocalPlayer)
    {
        // 自分の番号を先に登録しておく(Bindの通知を受けた時点でPresenterが自分の分だと判断できるように)
        if (isLocalPlayer)
        {
            CS_UISpecialGaugeModel.SetLocalPlayerNumber(_player.playerNumber);
        }

        CreateModel(_gauge.maxGauge, _gauge.currentGauge);
        _gauge.onGaugeChanged += HandleGaugeChanged;
    }

    private void UnbindGaugeUI()
    {
        if (_gaugeModel == null) return;

        _gauge.onGaugeChanged -= HandleGaugeChanged;
        _gaugeModel.Dispose();
        _gaugeModel = null;
    }

    private void CreateModel(float max, float current)
    {
        _gaugeModel?.Dispose();

        // 上限が0だと表示で0除算になるため、最低でも1にしておく
        _gaugeModel = new CS_UISpecialGaugeModel(Mathf.Max(1f, max), current);
        _gaugeModel.Bind(_player.playerNumber);
    }

    // ゲージが変わるたびにUI側のModelへ反映する(上限が変わっていたらModelを作り直す)
    private void HandleGaugeChanged(float current, float max)
    {
        if (!Mathf.Approximately(_gaugeModel.maxGauge, Mathf.Max(1f, max)))
        {
            CreateModel(max, current);
            return;
        }

        _gaugeModel.SetGauge(current);
    }
}
