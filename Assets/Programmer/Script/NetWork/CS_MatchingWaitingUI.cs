/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-11 | 初回作成
 * ================================================ */

using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 待機シーン(WaitingforaMatch)のUIを担当するクラス
/// </summary>
// ========================================
/*
 * メモ
 * ・参加人数を表示する(ConnectedClientsIdsはクライアント側でも参照できる)
 * ・開始ボタンはホストだけに表示し、押すと入室受付を締め切ってゲームシーンへ移動する
 *   シーン移動はCS_SceneTransitioner経由(ホストのみNetworkSceneManagerで遷移)
 * ・「戻る」で通信を終了して部屋に入るシーンへ戻る
 *   ホストが抜けた・切断されたクライアントも同じく部屋に入るシーンへ戻る
 */
// ========================================
public class CS_MatchingWaitingUI : MonoBehaviour
{
    [SerializeField][Tooltip("ゲームシーンへの遷移")] private CS_SceneTransitioner _sceneTransitioner;
    [SerializeField][Tooltip("参加人数の表示")] private TMP_Text _playerCountText;
    [SerializeField][Tooltip("開始ボタン(ホストのみ表示)")] private Button _startButton;
    [SerializeField][Tooltip("戻るボタン")] private Button _backButton;
    [SerializeField][Tooltip("戻り先のシーン名")] private string _roomConnectionSceneName = "RoomConnection";

    private bool _isLeaving;

    void Start()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        // ホストだけ開始ボタンを出し、待機中は入室を受け付ける
        _startButton.gameObject.SetActive(networkManager.IsHost);
        if (networkManager.IsHost)
        {
            SetAcceptingConnections(true);
        }

        _startButton.onClick.AddListener(OnClickStart);
        _backButton.onClick.AddListener(Leave);
        networkManager.OnConnectionEvent += OnConnectionEvent;

        RefreshPlayerCount();
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
        }
    }

    private void OnClickStart()
    {
        _startButton.interactable = false;
        _backButton.interactable = false;

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

        _playerCountText.text = $"Players {networkManager.ConnectedClientsIds.Count} / {maxPlayers}";
    }

    private void SetAcceptingConnections(bool value)
    {
        CS_ConnectionGate gate = NetworkManager.Singleton.GetComponent<CS_ConnectionGate>();
        if (gate != null) gate.SetAcceptingConnections(value);
    }
}
