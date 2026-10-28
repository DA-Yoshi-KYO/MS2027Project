/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-11 | 初回作成
 * 2026-10-19 | ソロで遊ぶボタンを追加
 * 2026-10-28 | UIをMVPに分け、CS_RoomConnectionUIから名前を変えた(表示はCS_UIRoomConnectionModel/Presenter/View)
 * ================================================ */

using R3;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 部屋に入るシーン(RoomConnection)の処理を担当するクラス
/// 画面の表示はMVP(CS_UIRoomConnectionModel/Presenter/View)に任せ、このクラスはModelに状態を書くだけ
/// </summary>
// ========================================
/*
 * メモ
 * ・Modelをnewして持ち、Viewと同じGameObjectのPresenterに渡す(CS_DefeatVillainCountControllerと同じ形)
 *   ボタンが押されたことはPresenterから受け取る
 * ・「部屋を作る」でホストとして開始し、待機シーンへ移動する(CS_Matching.StartAsHost)
 * ・「参加する」で入力欄の部屋番号(今はホストのIPアドレス)へ接続する
 *   接続に成功すると、サーバーと同じ待機シーンへ自動で移動する
 * ・接続に失敗したときはOnClientDisconnectCallbackが呼ばれるので、ボタンを押せる状態に戻す
 * ・「ソロ」で通信を開始せずにゲームシーンへ移動する(遊び方はCS_GameModeStoreで持ち越す)
 *   通信していない時の処理(NPCを出さない、悪人を1人分で数えるなど)がそのまま使われる
 */
// ========================================
public class CS_RoomConnectionController : MonoBehaviour
{
    [SerializeField][Tooltip("ホスト・参加の処理")] private CS_Matching _matching;
    [SerializeField][Tooltip("画面のView(同じGameObjectのPresenterを使う)")] private CS_UIRoomConnectionView _view;
    [SerializeField][Tooltip("入力が空のときの接続先")] private string _defaultAddress = "127.0.0.1";
    [SerializeField][Tooltip("ソロの時に移動するゲームシーン名")] private string _soloSceneName = "MainScene";

    private CS_UIRoomConnectionModel _model;
    private DisposableBag _disposables;

    void Awake()
    {
        _model = new CS_UIRoomConnectionModel();
    }

    void Start()
    {
        CS_UIRoomConnectionPresenter presenter = _view.GetComponent<CS_UIRoomConnectionPresenter>();
        presenter.BindModel(_model);
        presenter.onHostRequested.Subscribe(_ => OnHostRequested()).AddTo(ref _disposables);
        presenter.onJoinRequested.Subscribe(OnJoinRequested).AddTo(ref _disposables);
        presenter.onSoloRequested.Subscribe(_ => OnSoloRequested()).AddTo(ref _disposables);

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
        _disposables.Dispose();
        _model?.Dispose();
    }

    private void OnHostRequested()
    {
        CS_GameModeStore.SetMode(CSE_GameMode.Multi);
        _model.SetInteractable(false);
        if (!_matching.StartAsHost())
        {
            _model.SetStatus("Failed to create a room");
            _model.SetInteractable(true);
        }
    }

    private void OnJoinRequested(string roomInput)
    {
        string address = string.IsNullOrWhiteSpace(roomInput) ? _defaultAddress : roomInput;

        CS_GameModeStore.SetMode(CSE_GameMode.Multi);
        _model.SetInteractable(false);
        if (!_matching.StartAsClient(address))
        {
            _model.SetStatus("Failed to start connecting");
            _model.SetInteractable(true);
            return;
        }
        _model.SetStatus($"Connecting to {address} ...");
    }

    // 通信を開始せずにゲームシーンへ移動する
    private void OnSoloRequested()
    {
        CS_GameModeStore.SetMode(CSE_GameMode.Solo);
        _model.SetInteractable(false);
        SceneManager.LoadScene(_soloSceneName);
    }

    // 接続失敗・満員などで切断されたとき(クライアント側は自分の切断時だけ呼ばれる)
    private void OnClientDisconnect(ulong clientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager.IsServer) return;

        // 拒否理由は日本語のため、画面には出さずログに残す(TMPのフォントが日本語非対応)
        if (!string.IsNullOrEmpty(networkManager.DisconnectReason))
        {
            Debug.Log($"CS_RoomConnectionController: 切断理由 {networkManager.DisconnectReason}", this);
        }
        _model.SetStatus("Could not join the room");
        _model.SetInteractable(true);
    }
}
