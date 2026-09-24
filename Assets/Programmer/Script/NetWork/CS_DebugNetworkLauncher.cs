/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using Unity.Multiplayer.PlayMode;
#endif

/// <summary>
/// マッチングを経由せずに通信を開始し、ゲームシーンへ移動するデバッグ用クラス
/// </summary>
// ========================================
/*
 * メモ
 * ・マルチプレイ確認用の起動シーン(MultiplayerDebugBoot)に置いて使う
 *   Multiplayer Play Modeのメインエディタはホスト、仮想プレイヤーはクライアントとして開始する
 *   (仮想プレイヤーは Window > Multiplayer > Multiplayer Play Mode で有効にする)
 * ・ホストは通信開始後、NetworkSceneManagerでゲームシーンを読み込む
 *   クライアントは接続するとサーバーと同じシーンへ自動で同期される
 * ・ゲームシーンを直接再生せずに起動シーンを挟むのは、
 *   ゲームシーン内のスクリプトがAwake/Startで「オンラインかどうか」を判定するため
 *   (ゲームシーンを読み込む前に通信を開始しておく必要がある)
 * ・NetworkManagerがまだ無い場合はプレハブから生成する(DontDestroyOnLoadになる)
 */
// ========================================
public class CS_DebugNetworkLauncher : MonoBehaviour
{
    [SerializeField][Tooltip("NetworkManagerが無い場合に生成するプレハブ")] private NetworkManager _networkManagerPrefab;
    [SerializeField][Tooltip("通信開始後に読み込むゲームシーン")] private string _gameSceneName = "MainScene";
    [SerializeField][Tooltip("クライアントの接続先")] private string _address = "127.0.0.1";
    [SerializeField][Tooltip("ポート番号")] private ushort _port = 7777;

    void Start()
    {
        NetworkManager networkManager = GetOrCreateNetworkManager();
        if (networkManager == null) return;

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (IsHostPlayer())
        {
            StartHost(networkManager, transport);
        }
        else
        {
            transport.SetConnectionData(_address, _port);
            networkManager.StartClient();
        }
    }

    private void StartHost(NetworkManager networkManager, UnityTransport transport)
    {
        transport.SetConnectionData("0.0.0.0", _port, "0.0.0.0");
        if (!networkManager.StartHost())
        {
            Debug.LogError("CS_DebugNetworkLauncher: ホストの開始に失敗しました", this);
            return;
        }

        SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(_gameSceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"CS_DebugNetworkLauncher: {_gameSceneName} の読み込みに失敗しました({status})", this);
        }
    }

    private NetworkManager GetOrCreateNetworkManager()
    {
        if (NetworkManager.Singleton != null) return NetworkManager.Singleton;

        if (_networkManagerPrefab == null)
        {
            Debug.LogError("CS_DebugNetworkLauncher: Network Manager Prefab が未設定です", this);
            return null;
        }

        return Instantiate(_networkManagerPrefab);
    }

    // ホストとして開始するか(仮想プレイヤー以外はホスト)
    private static bool IsHostPlayer()
    {
#if UNITY_EDITOR
        return CurrentPlayer.IsMainEditor;
#else
        return true;
#endif
    }
}
