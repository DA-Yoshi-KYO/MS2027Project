using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * マッチングの管理を行うクラス
 * 
 * 制作者：　秋野翔太
 */


// ========================================
/*
 * メモ
 * NetworkManagerからシングルトンを取得し
 *  var nm = NetworkManager.Singleton;
 *  接続時
 *  nm.OnClientConnectedCallback += 関数;
 *  切断時
 *  nm.OnClientDisconnectCallback += 関数;
 *  のようにコールバックを登録することで、クライアントの接続・切断を検知できる
 */
// ========================================


public class CS_Matching : MonoBehaviour
{
    private const int _maxPlayers = 4;
    public int MaxPlayers => _maxPlayers;

    [SerializeField] private string _matchingWaitingSceneName = "MatchingWaitingScene";

    private void Start()
    {
    }

    // ホストとして部屋を作成する
   public bool StartAsHost()
    {
        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utp.SetConnectionData("0.0.0.0", 7777, "0.0.0.0");
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        return NetworkManager.Singleton.StartHost();
    }

    // 参加者として接続する
    public bool StartAsClient(string ip)
    {
        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utp.SetConnectionData(ip, 7777);
        return NetworkManager.Singleton.StartClient();
    }

}
