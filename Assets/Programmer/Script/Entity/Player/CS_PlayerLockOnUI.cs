using Unity.Netcode;
using UnityEngine;

/*
 * 奇襲できる悪人を、ロックオンUI(CS_UILockOnModel)でロックオン表示するクラス
 * CS_PlayerAmbushに今奇襲できる悪人を問い合わせ、UI側のModelに対象を渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・ロックオンUIは自分の画面にだけ出すため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   (UI側のPresenterがCS_UILockOnModel.localPlayerNumberと一致する番号だけを表示するため、
 *    Bindより先にSetLocalPlayerNumberで番号を登録する)
 * ・_searchInterval秒ごとにCS_PlayerAmbush.FindAmbushTarget()で、奇襲の条件(距離・気づかれていない・背後か上)が
 *   そろっている一番近い悪人を探し、その悪人をロックオンする。いなければロックオンを外す
 * ・マーカーの位置は悪人のTransformの位置 + UICanvas側Presenterの_worldOffset(既定は1m上)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く(自分のプレイヤー扱い、playerNumberは0)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerAmbush))]
public class CS_PlayerLockOnUI : NetworkBehaviour
{
    [SerializeField][Min(0f)] private float _searchInterval = 0.1f;   // 奇襲できる悪人を探し直す間隔(秒)

    private CS_Player _player;
    private CS_PlayerAmbush _ambush;
    private CS_UILockOnModel _lockOnModel;
    private float _searchTimer;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _ambush = GetComponent<CS_PlayerAmbush>();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        if (!_player.isLocalPlayer) return;   // NPCはロックオンUIに出さない

        BindLockOnUI();
    }

    public override void OnNetworkSpawn()
    {
        if (!_player.isLocalPlayer) return;   // NPCはホストでIsOwnerになるため、isLocalPlayerで判定する

        BindLockOnUI();
    }

    public override void OnNetworkDespawn()
    {
        UnbindLockOnUI();
    }

    public override void OnDestroy()
    {
        UnbindLockOnUI();
        base.OnDestroy();
    }

    private void Update()
    {
        if (_lockOnModel == null) return;

        _searchTimer -= Time.deltaTime;
        if (_searchTimer > 0f) return;
        _searchTimer = _searchInterval;

        UpdateTarget();
    }

    // ロックオンUIのModelを作り、自分のplayerNumberで公開する
    private void BindLockOnUI()
    {
        // 自分の番号を先に登録しておく(Bindの通知を受けた時点でPresenterが自分の分だと判断できるように)
        CS_UILockOnModel.SetLocalPlayerNumber(_player.playerNumber);

        _lockOnModel = new CS_UILockOnModel();
        _lockOnModel.Bind(_player.playerNumber);
        _searchTimer = 0f;
    }

    private void UnbindLockOnUI()
    {
        if (_lockOnModel == null) return;

        _lockOnModel.Dispose();
        _lockOnModel = null;
    }

    // 今奇襲できる悪人をロックオンする(いなければ外す)
    private void UpdateTarget()
    {
        CS_VillainHealth villain = _ambush.FindAmbushTarget();
        if (villain == null)
        {
            _lockOnModel.ClearTarget();
            return;
        }

        _lockOnModel.SetTarget(villain.transform);
    }
}
