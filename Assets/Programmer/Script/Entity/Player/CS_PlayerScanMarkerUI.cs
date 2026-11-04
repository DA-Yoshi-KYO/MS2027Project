using System.Collections.Generic;
using UnityEngine;

/*
 * スキャン(CS_PlayerScan)で見えている悪人を、スキャンの目印UI(CS_UIScanMarkerModel)へ渡すクラス
 * CS_PlayerScanに今見えている悪人を問い合わせ、UI側のModelに相手の一覧を渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・目印の見た目と、足元からの高さはUICanvas側(ScanMarker)のView・Presenterで決める
 * ・スキャンの目印は自分の画面にだけ出すため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   isLocalPlayerはスポーン後に決まるため、毎フレーム確かめて、自分のプレイヤーになった時にBindする
 * ・波の輪(ワールドに出すエフェクト)はUIではないので、CS_PlayerScanEffectが出す
 * ・CS_PlayerScanのロジックはこのクラスを知らない(外してもスキャンは動く)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerScan))]
[RequireComponent(typeof(CS_Player))]
public class CS_PlayerScanMarkerUI : MonoBehaviour
{
    private CS_Player _player;
    private CS_PlayerScan _scan;
    private CS_UIScanMarkerModel _markerModel;

    private readonly List<Transform> _targets = new List<Transform>();    // Modelへ渡す一覧(毎フレーム作り直さないように使い回す)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _scan = GetComponent<CS_PlayerScan>();
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

        _targets.Clear();
        foreach (CS_VillainHealth villain in _scan.revealedVillains)
        {
            if (villain != null) _targets.Add(villain.transform);
        }
        _markerModel.SetTargets(_targets);
    }

    // スキャンの目印のModelを作って公開する
    private void BindMarkerUI()
    {
        _markerModel = new CS_UIScanMarkerModel();
        _markerModel.Bind();
    }

    private void UnbindMarkerUI()
    {
        if (_markerModel == null) return;

        _markerModel.Dispose();
        _markerModel = null;
    }
}
