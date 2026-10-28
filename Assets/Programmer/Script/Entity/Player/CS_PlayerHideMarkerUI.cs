using UnityEngine;

/*
 * 今ダッシュボタンを押したら飛べるハイドポイント(CS_PlayerHide.currentCandidate)を、ハイドマーカーUI(CS_UIHideMarkerModel)へ渡すクラス
 * CS_PlayerHideに飛べるハイドポイントを問い合わせ、UI側のModelに目印の位置を渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・目印の見た目と、立つ位置からの高さはUICanvas側(HideMarker)のView・Presenterで決める
 * ・ハイドマーカーは自分の画面に1つだけのため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   isLocalPlayerはスポーン後に決まるため、毎フレーム確かめて、自分のプレイヤーになった時にBindする
 * ・CS_PlayerHideのロジックはこのクラスを知らない(外してもハイドは動く)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHide))]
[RequireComponent(typeof(CS_Player))]
public class CS_PlayerHideMarkerUI : MonoBehaviour
{
    private CS_Player _player;
    private CS_PlayerHide _hide;
    private CS_UIHideMarkerModel _markerModel;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _hide = GetComponent<CS_PlayerHide>();
    }

    private void OnDisable()
    {
        UnbindMarkerUI();
    }

    private void LateUpdate()
    {
        if (!_player.isLocalPlayer)
        {
            UnbindMarkerUI();   // NPC・他人のプレイヤーは目印を出さない
            return;
        }

        if (_markerModel == null) BindMarkerUI();

        CS_HidePoint candidate = _hide.currentCandidate;
        _markerModel.SetTarget(candidate != null ? candidate.standTransform : null);
    }

    // ハイドマーカーのModelを作って公開する
    private void BindMarkerUI()
    {
        _markerModel = new CS_UIHideMarkerModel();
        _markerModel.Bind();
    }

    private void UnbindMarkerUI()
    {
        if (_markerModel == null) return;

        _markerModel.Dispose();
        _markerModel = null;
    }
}
