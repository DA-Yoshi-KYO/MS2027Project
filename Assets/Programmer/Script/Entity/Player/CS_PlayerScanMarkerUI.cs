using System.Collections.Generic;
using UnityEngine;

/*
 * スキャン(CS_PlayerScan)で見えている悪人グループ・警察・巡回ドローンを、スキャンの目印UI(CS_UIScanMarkerModel)へ渡すクラス
 * CS_PlayerScanに今見えている相手を問い合わせ、UI側のModelに表示内容(種類・位置・距離・犯罪完遂度)の一覧を渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・目印の見た目と、足元からの高さはUICanvas側(ScanMarker)のView・Presenterで決める
 * ・悪人はグループごとに1つ(位置は生きているメンバーの中心、完遂度はグループの犯罪の進行度)
 * ・距離は自分のプレイヤーから相手(悪人グループはメンバーの中心)まで
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

    private readonly List<CS_UIScanMarkerData> _targets = new List<CS_UIScanMarkerData>();    // Modelへ渡す一覧(毎フレーム作り直さないように使い回す)

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
        Vector3 origin = transform.position;

        foreach (CS_PlayerScan.RevealedVillainGroup group in _scan.revealedVillainGroups)
        {
            if (group.members.Count == 0) continue;

            Vector3 center = group.center;
            _targets.Add(new CS_UIScanMarkerData(CSE_UIScanTargetType.VillainGroup, center, Vector3.Distance(origin, center), group.crimeProgress));
        }

        foreach (CS_PoliceBrain police in _scan.revealedPolice)
        {
            if (police != null) _targets.Add(CreateData(CSE_UIScanTargetType.Police, police.transform, origin));
        }

        foreach (CS_SecurityDrone drone in _scan.revealedDrones)
        {
            if (drone != null) _targets.Add(CreateData(CSE_UIScanTargetType.Drone, drone.transform, origin));
        }

        _markerModel.SetTargets(_targets);
    }

    private static CS_UIScanMarkerData CreateData(CSE_UIScanTargetType type, Transform target, Vector3 origin)
    {
        Vector3 position = target.position;
        return new CS_UIScanMarkerData(type, position, Vector3.Distance(origin, position));
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
