using Unity.Netcode;
using UnityEngine;

/*
 * 悪人の位置と犯罪完遂度をミニマップ(CS_MiniMapController)へ登録するクラス
 * 登録・解除と完遂度を渡すことだけを行い、位置・向きの更新はCS_MiniMapController側が自動で行う
 * ミニマップ側はこのクラス・悪人の他のクラスの存在を知らない
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・使い方はClaudeDocs/ミニマップの使い方.md参照。悪人は Enemy として登録する
 *   出現時 : RegisterEnemy(id, transform)
 *   消える時(撃退・犯罪完遂の逃走でDespawn / Destroyされた時) : UnregisterEnemy(id)
 * ・悪人はサーバーが生成するが、全クライアントでOnNetworkSpawnが呼ばれるので、
 *   各クライアントがそれぞれ自分のミニマップに登録する(位置はNetworkTransformで同期済み)
 * ・idは種類をまたいで重複させない(プレイヤー・警察と混ざらないよう"Villain_"を付ける)
 * ・シーンにCS_MiniMapControllerが無ければ何もしない(ミニマップの無いテストシーンでも動く)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでは、Spawnされないので生成時に登録する
 * ・犯罪完遂度(CS_VillainCrime.crimeProgress、0〜1)を SetEnemyCrimeProgressRate で渡す
 *   完遂度が閾値(ミニマップ側の設定、初期値80%)以上になると、ミニマップの悪人アイコンが拡縮する
 *   crimeProgressはNetworkVariableで全クライアントに同期されている(グループが1%ごとに更新)ので、各クライアントが自分のミニマップへ渡す
 *   登録した直後に1回渡し(出現時から犯罪が進んでいる時のため)、その後は値が変わった時だけ渡す
 *   逃走中(犯罪完遂後のフェード中)も、消えるまでは完遂度(100%)を渡したままにする
 */
// ========================================

public class CS_VillainMiniMapIcon : NetworkBehaviour
{
    private CS_MiniMapController _miniMap;
    private CS_VillainCrime _crime;
    private string _miniMapId;   // 登録した時のid(未登録ならnull)
    private float _sentProgress; // 最後にミニマップへ渡した犯罪完遂度(0〜1)

    private void Awake()
    {
        _crime = GetComponent<CS_VillainCrime>();
    }

    // 犯罪完遂度が変わったら、ミニマップへ渡す
    private void Update()
    {
        if (_miniMapId == null || _crime == null) return;
        if (Mathf.Approximately(_crime.crimeProgress, _sentProgress)) return;

        SendCrimeProgress();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        Register($"Villain_{GetInstanceID()}");
    }

    public override void OnNetworkSpawn()
    {
        Register($"Villain_{NetworkObjectId}");
    }

    public override void OnNetworkDespawn()
    {
        Unregister();
    }

    // オフラインでDestroyされた時や、Despawnを通らずに消えた時も解除する
    public override void OnDestroy()
    {
        Unregister();
        base.OnDestroy();
    }

    private void Register(string id)
    {
        _miniMap = FindAnyObjectByType<CS_MiniMapController>();
        if (_miniMap == null) return;

        _miniMapId = id;
        _miniMap.RegisterEnemy(_miniMapId, transform);

        // 出現時から犯罪が進んでいる時のため、登録した直後にも渡す
        if (_crime != null) SendCrimeProgress();
    }

    private void SendCrimeProgress()
    {
        _sentProgress = _crime.crimeProgress;
        _miniMap.SetEnemyCrimeProgressRate(_miniMapId, _sentProgress);
    }

    // 解除しないと、アイコンが最後の位置に残り続ける
    private void Unregister()
    {
        if (_miniMap == null || _miniMapId == null) return;

        _miniMap.UnregisterEnemy(_miniMapId);
        _miniMapId = null;
    }
}
