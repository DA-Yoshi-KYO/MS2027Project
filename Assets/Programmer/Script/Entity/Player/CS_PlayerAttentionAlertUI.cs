using UnityEngine;

/*
 * 警戒状態(CS_PlayerAlert)を、警戒状態の赤帯UI(CS_UIAttentionAlertModel)へ渡すクラス
 * CS_PlayerAlertの状態が変わったら、警戒中かどうかをUI側のModelに渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様はUI仕様書「警戒状態時のUI」(Issue #375)。警察陣営に見つかって警戒状態の間、画面の上下に赤帯を出してループさせる
 * ・赤帯は自分の画面にだけ出すため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   CS_PlayerAlert.onAlertChangedは全クライアントで全プレイヤー分発生するため、他人の警戒状態で自分の画面に出ないようにする
 *   isLocalPlayerはスポーン後に決まるため、毎フレーム確かめて、自分のプレイヤーになった時にBindする
 * ・発見中・捜索中のどちらでも赤帯を出す(CS_PlayerAlert.isAlerted)
 * ・CS_PlayerAlertのロジックはこのクラスを知らない(外しても警戒状態は動く)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerAlert))]
[RequireComponent(typeof(CS_Player))]
public class CS_PlayerAttentionAlertUI : MonoBehaviour
{
    private CS_Player _player;
    private CS_PlayerAlert _alert;
    private CS_UIAttentionAlertModel _alertModel;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _alert = GetComponent<CS_PlayerAlert>();
    }

    private void OnDisable()
    {
        UnbindAlertUI();
    }

    private void LateUpdate()
    {
        if (!_player.isLocalPlayer)
        {
            UnbindAlertUI();    // NPC・他人のプレイヤーは赤帯を出さない
            return;
        }

        if (_alertModel == null) BindAlertUI();
    }

    // 赤帯UIのModelを作って公開する
    private void BindAlertUI()
    {
        _alertModel = new CS_UIAttentionAlertModel();
        _alertModel.SetAlerted(_alert.isAlerted);   // Bind前に今の状態を入れておく(Bind直後に違う表示にならないように)
        _alertModel.Bind();

        _alert.onAlertChanged += HandleAlertChanged;
    }

    private void UnbindAlertUI()
    {
        if (_alertModel == null) return;

        _alert.onAlertChanged -= HandleAlertChanged;
        _alertModel.Dispose();
        _alertModel = null;
    }

    // 警戒状態が変わったら、警戒中(なし以外)かどうかをModelに渡す
    private void HandleAlertChanged(CSE_PlayerAlertState state)
    {
        _alertModel?.SetAlerted(state != CSE_PlayerAlertState.None);
    }
}
