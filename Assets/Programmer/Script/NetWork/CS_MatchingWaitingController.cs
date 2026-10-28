/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-11 | 初回作成
 * 2026-10-28 | UIをMVPに分け、CS_MatchingWaitingUIから名前を変えた(表示はCS_UIMatchingWaitingModel/Presenter/View)
 * ================================================ */

using R3;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 待機シーン(WaitingforaMatch)の処理を担当するクラス
/// 画面の表示はMVP(CS_UIMatchingWaitingModel/Presenter/View)に任せ、このクラスはModelに状態を書くだけ
/// </summary>
// ========================================
/*
 * メモ
 * ・Modelをnewして持ち、Viewと同じGameObjectのPresenterに渡す(CS_DefeatVillainCountControllerと同じ形)
 *   ボタンが押されたことはPresenterから受け取る
 * ・参加人数を表示する(ConnectedClientsIdsはクライアント側でも参照できる)
 * ・開始ボタンはホストだけに表示し、押すと入室受付を締め切ってゲームシーンへ移動する
 *   シーン移動はCS_SceneTransitioner経由(ホストのみNetworkSceneManagerで遷移)
 * ・「戻る」で通信を終了して部屋に入るシーンへ戻る
 *   ホストが抜けた・切断されたクライアントも同じく部屋に入るシーンへ戻る
 */
// ========================================
public class CS_MatchingWaitingController : MonoBehaviour
{
    [SerializeField][Tooltip("ゲームシーンへの遷移")] private CS_SceneTransitioner _sceneTransitioner;
    [SerializeField][Tooltip("画面のView(同じGameObjectのPresenterを使う)")] private CS_UIMatchingWaitingView _view;
    [SerializeField][Tooltip("戻り先のシーン名")] private string _roomConnectionSceneName = "RoomConnection";

    private CS_UIMatchingWaitingModel _model;
    private DisposableBag _disposables;
    private bool _isLeaving;

    void Awake()
    {
        _model = new CS_UIMatchingWaitingModel();
    }

    void Start()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        CS_UIMatchingWaitingPresenter presenter = _view.GetComponent<CS_UIMatchingWaitingPresenter>();
        presenter.BindModel(_model);
        presenter.onStartRequested.Subscribe(_ => OnStartRequested()).AddTo(ref _disposables);
        presenter.onBackRequested.Subscribe(_ => Leave()).AddTo(ref _disposables);

        // ホストだけ開始ボタンを出し、待機中は入室を受け付ける
        _model.SetStartVisible(networkManager.IsHost);
        if (networkManager.IsHost)
        {
            SetAcceptingConnections(true);
        }

        networkManager.OnConnectionEvent += OnConnectionEvent;

        RefreshPlayerCount();
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
        }
        _disposables.Dispose();
        _model?.Dispose();
    }

    private void OnStartRequested()
    {
        _model.SetInteractable(false);

        // 開始後は途中参加させない
        SetAcceptingConnections(false);
        _sceneTransitioner.StartTransition();
    }

    private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData data)
    {
        // クライアント側で自分が切断された(ホストが抜けた等)ときは部屋に入るシーンへ戻る
        // (他のプレイヤーの切断はPeerDisconnectedなので、クライアント側のClientDisconnectedは自分の切断)
        if (!networkManager.IsServer && data.EventType == ConnectionEvent.ClientDisconnected)
        {
            Leave();
            return;
        }
        RefreshPlayerCount();
    }

    // 通信を終了して部屋に入るシーンへ戻る
    private void Leave()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(_roomConnectionSceneName);
    }

    private void RefreshPlayerCount()
    {
        if (_isLeaving) return;

        NetworkManager networkManager = NetworkManager.Singleton;
        int maxPlayers = 4;
        CS_ConnectionGate gate = networkManager.GetComponent<CS_ConnectionGate>();
        if (gate != null) maxPlayers = gate.MaxPlayers;

        _model.SetPlayerCount(networkManager.ConnectedClientsIds.Count, maxPlayers);
    }

    private void SetAcceptingConnections(bool value)
    {
        CS_ConnectionGate gate = NetworkManager.Singleton.GetComponent<CS_ConnectionGate>();
        if (gate != null) gate.SetAcceptingConnections(value);
    }
}
