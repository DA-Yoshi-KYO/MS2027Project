using Unity.Netcode;
using UnityEngine;

// NetworkManagerの複製を防ぐためのBootstrapクラス

public class CS_NetworkManagerBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkManager _networkManagerPrefab;

    private void Awake()
    {
        if(NetworkManager.Singleton == null)
        {
            Instantiate(_networkManagerPrefab);
        }
    }
}
