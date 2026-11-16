using UnityEngine;

/*
 * 今狙っているグラップルのSAポイントの上に出す、飛んでいく方向の矢印(Issue #505)を、矢印UI(CS_UIGrappleArrowModel)へ渡すクラス
 * CS_PlayerHideに狙っているSAポイントを問い合わせ、UI側のModelに矢印の位置・向きを渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出す条件: 狙っているSAポイント(CS_PlayerHide.currentCandidate)がグラップル(CS_GrappleSAPoint)で、飛んでいない時
 *   狙いを外した時・飛び始めた時(onFlyBegin)・飛んでいる最中は出さない(矢印UI側が短いフェードで消す)
 * ・位置: 的(CS_GrappleSAPoint.ropeEndPosition)の_heightOffset上
 * ・向き: プレイヤーの位置から、立つ位置(CS_SAPoint.standPosition)へ向かう向き(高さも含む)
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
    [Header("矢印の位置")]
    [SerializeField] private float _heightOffset = 1.5f;     // 的から矢印までの高さ(m)

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

        Vector3 direction = grapple.standPosition - transform.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            _arrowModel.Hide();
            return;
        }

        _arrowModel.Show(grapple.ropeEndPosition + Vector3.up * _heightOffset, direction);
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
