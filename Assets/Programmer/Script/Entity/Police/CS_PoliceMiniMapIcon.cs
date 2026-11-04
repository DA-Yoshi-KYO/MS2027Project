/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-05 | 初回作成
 * ================================================ */

using Unity.Netcode;

/// <summary>
/// 警察をミニマップ(CS_MiniMapController)に警察アイコンとして登録するクラス
/// ・出現時に登録し、消える時に登録を解除する(位置の更新はCS_MiniMapControllerが行う)
/// ・ネットワーク接続中は、各クライアントがそれぞれ自分のミニマップに登録する
/// ・シーンにCS_MiniMapControllerが無ければ何もしない(ミニマップの無いテストシーンでも動く)
/// </summary>
public class CS_PoliceMiniMapIcon : NetworkBehaviour
{
    // 登録先のミニマップ
    private CS_MiniMapController _miniMap = null;

    // ミニマップに登録した時のid(登録していなければnull)
    private string _miniMapId = null;

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        Register($"Police_{GetInstanceID()}");
    }

    public override void OnNetworkSpawn()
    {
        Register($"Police_{NetworkObjectId}");
    }

    public override void OnNetworkDespawn()
    {
        Unregister();
    }

    public override void OnDestroy()
    {
        Unregister();
        base.OnDestroy();
    }

    /// <summary>
    /// ミニマップに警察アイコンとして登録するメソッド
    /// </summary>
    /// <param name="id">アイコンのid(他の種類と重ならないよう「Police_」を付ける)</param>
    private void Register(string id)
    {
        _miniMap = FindAnyObjectByType<CS_MiniMapController>();
        if (_miniMap == null) return;

        _miniMapId = id;
        _miniMap.RegisterPolice(_miniMapId, transform);
    }

    /// <summary>
    /// ミニマップの登録を解除するメソッド(解除しないとアイコンが最後の位置に残り続ける)
    /// </summary>
    private void Unregister()
    {
        if (_miniMap == null || _miniMapId == null) return;

        _miniMap.UnregisterPolice(_miniMapId);
        _miniMapId = null;
    }
}
