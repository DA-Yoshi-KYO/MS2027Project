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
using UnityEngine.UI;

/// <summary>
/// 部屋に入るシーン(RoomConnection)のUIを担当するクラス
/// </summary>
// ========================================
/*
 * メモ
 * ・「部屋を作る」でホストとして開始し、待機シーンへ移動する(CS_Matching.StartAsHost)
 * ・「参加する」で入力欄の部屋番号(今はホストのIPアドレス)へ接続する
 *   接続に成功すると、サーバーと同じ待機シーンへ自動で移動する
 * ・接続に失敗したときはOnClientDisconnectCallbackが呼ばれるので、ボタンを押せる状態に戻す
 */
// ========================================
public class CS_RoomConnectionUI : MonoBehaviour
{
    [SerializeField][Tooltip("ホスト・参加の処理")] private CS_Matching _matching;
    [SerializeField][Tooltip("部屋番号(ホストのIPアドレス)の入力欄")] private TMP_InputField _roomInput;
    [SerializeField][Tooltip("部屋を作るボタン")] private Button _hostButton;
    [SerializeField][Tooltip("参加するボタン")] private Button _joinButton;
    [SerializeField][Tooltip("状態表示")] private TMP_Text _statusText;
    [SerializeField][Tooltip("入力が空のときの接続先")] private string _defaultAddress = "127.0.0.1";

    void Start()
    {
        _hostButton.onClick.AddListener(OnClickHost);
        _joinButton.onClick.AddListener(OnClickJoin);
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        SetStatus("");
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
    }

    private void OnClickHost()
    {
        SetInteractable(false);
        if (!_matching.StartAsHost())
        {
            SetStatus("Failed to create a room");
            SetInteractable(true);
        }
    }

    private void OnClickJoin()
    {
        string address = string.IsNullOrWhiteSpace(_roomInput.text) ? _defaultAddress : _roomInput.text.Trim();

        SetInteractable(false);
        if (!_matching.StartAsClient(address))
        {
            SetStatus("Failed to start connecting");
            SetInteractable(true);
            return;
        }
        SetStatus($"Connecting to {address} ...");
    }

    // 接続失敗・満員などで切断されたとき(クライアント側は自分の切断時だけ呼ばれる)
    private void OnClientDisconnect(ulong clientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager.IsServer) return;

        // 拒否理由は日本語のため、画面には出さずログに残す(TMPのフォントが日本語非対応)
        if (!string.IsNullOrEmpty(networkManager.DisconnectReason))
        {
            Debug.Log($"CS_RoomConnectionUI: 切断理由 {networkManager.DisconnectReason}", this);
        }
        SetStatus("Could not join the room");
        SetInteractable(true);
    }

    private void SetInteractable(bool value)
    {
        _hostButton.interactable = value;
        _joinButton.interactable = value;
        _roomInput.interactable = value;
    }

    private void SetStatus(string message)
    {
        if (_statusText != null) _statusText.text = message;
    }
}
