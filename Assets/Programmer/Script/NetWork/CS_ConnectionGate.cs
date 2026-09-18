using Unity.Netcode;
using UnityEngine;

// マッチ接続に関するゲートウェイクラス
// 制作者：　秋野翔太

public class CS_ConnectionGate : MonoBehaviour
{
    [SerializeField] private const int _maxPlayers = 4;
    [SerializeField] static bool AcceptingConnections = true;

    public static bool IsAcceptingConnections => AcceptingConnections;
   

    void Start()
    {
        GetComponent<NetworkManager>().ConnectionApprovalCallback = Approve;
    }

    void Approve(NetworkManager.ConnectionApprovalRequest req,NetworkManager.ConnectionApprovalResponse res)
    {
        var nm = NetworkManager.Singleton;
        if (!AcceptingConnections)
        {
            res.Approved = false;
            res.Reason = "ゲームが既に開始されています";
            return;
        }
        if (nm.ConnectedClientsIds.Count >= _maxPlayers)
        {
            res.Approved = false;
            res.Reason = "満員です";
            return;
        }
        res.Approved = true;
        res.CreatePlayerObject = false;
    }

    // 接続受付状態を設定するメソッド
    public void SetAcceptingConnections(bool value)
    {
        AcceptingConnections = value;
    }
}
