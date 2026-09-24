using Unity.Netcode;
using UnityEngine;

// NetworkManager‚Ì•¡»‚ğ–h‚®‚½‚ß‚ÌBootstrapƒNƒ‰ƒX

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
