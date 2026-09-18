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
 *  ※　ホストとクライアントではコールバックの呼ばれるタイミングが異なるので注意
 *  ホスト側では、誰か一人でも接続・切断があった場合に呼ばれる
 *  クライアント側では、自分が接続・切断した場合に呼ばれる
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
        var nm = NetworkManager.Singleton;
        nm.GetComponent<UnityTransport>().SetConnectionData("0.0.0.0", 7777, "0.0.0.0");

        if (!nm.StartHost()) return false;

        var status = nm.SceneManager.LoadScene(_matchingWaitingSceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            nm.Shutdown(); // シーン遷移に失敗したら部屋ごと畳む
            return false;
        }
        return true;
    }

    /*
     * 参加者として部屋に接続する
     * 戻り値は接続失敗ではなく、接続処理が開始できたかどうかを返す
     */
    public bool StartAsClient(string ip)
    {
        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utp.SetConnectionData(ip, 7777);
        return NetworkManager.Singleton.StartClient();
    }

}
