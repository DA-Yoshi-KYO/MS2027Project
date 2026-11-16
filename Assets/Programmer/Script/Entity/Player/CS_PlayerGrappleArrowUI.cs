using UnityEngine;

/*
 * 今狙っているグラップルのSAポイントへ飛んでいく道筋の矢印(Issue #505)を、矢印UI(CS_UIGrappleArrowModel)へ渡すクラス
 * CS_PlayerHideに狙っているSAポイントを問い合わせ、UI側のModelに道筋の始点・終点を渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出す条件: 狙っているSAポイント(CS_PlayerHide.currentCandidate)がグラップル(CS_GrappleSAPoint)で、飛んでいない時
 *   狙いを外した時・飛び始めた時(onFlyBegin)・飛んでいる最中は出さない(矢印UI側が短いフェードで消す)
 * ・道筋: 始点はプレイヤーの足元から_startHeight上(体の中心あたり)、終点は立つ位置(CS_SAPoint.standPosition。引っ張られる先)
 *   毎フレーム今のプレイヤーの位置から決めるので、プレイヤーが動いても道筋が追従する
 *   山形を並べる区間(始点から少し先〜終点の少し手前)や数・流れる速さは、UICanvas側(GrappleArrow)のViewで決める
 * ・矢印は自分の画面に1つだけのため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   isLocalPlayerはスポーン後に決まるため、毎フレーム確かめて、自分のプレイヤーになった時にBindする
 * ・CS_PlayerHideのロジックはこのクラスを知らない(外してもグラップルは動く)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く。通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHide))]
[RequireComponent(typeof(CS_Player))]
public class CS_PlayerGrappleArrowUI : MonoBehaviour
{
    [Header("道筋の始点")]
    [SerializeField][Min(0f)] private float _startHeight = 1f;     // 道筋の始点の高さ(プレイヤーの足元から。m。体の中心あたり)

    private CS_Player _player;
    private CS_PlayerHide _hide;
    private CS_UIGrappleArrowModel _arrowModel;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _hide = GetComponent<CS_PlayerHide>();
    }

    private void OnEnable()
    {
        _hide.onFlyBegin += HandleFlyBegin;
    }

    private void OnDisable()
    {
        _hide.onFlyBegin -= HandleFlyBegin;
        UnbindArrowUI();
    }

    private void LateUpdate()
    {
        if (!_player.isLocalPlayer)
        {
            UnbindArrowUI();   // NPC・他人のプレイヤーは矢印を出さない
            return;
        }

        if (_arrowModel == null) BindArrowUI();

        // 飛んでいない間、狙っているのがグラップルの時だけ出す
        if (_hide.isHiding || _hide.currentCandidate is not CS_GrappleSAPoint grapple)
        {
            _arrowModel.Hide();
            return;
        }

        _arrowModel.Show(transform.position + Vector3.up * _startHeight, grapple.standPosition);
    }

    // 飛び始めたら、すぐ消す
    private void HandleFlyBegin(CS_SAPoint point)
    {
        _arrowModel?.Hide();
    }

    // 矢印UIのModelを作って公開する
    private void BindArrowUI()
    {
        _arrowModel = new CS_UIGrappleArrowModel();
        _arrowModel.Bind();
    }

    private void UnbindArrowUI()
    {
        if (_arrowModel == null) return;

        _arrowModel.Dispose();
        _arrowModel = null;
    }
}
