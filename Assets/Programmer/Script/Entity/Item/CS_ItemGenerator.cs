/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * 2026-10-17 | 拾われたアイテムの再生成を追加
 * 2026-10-30 | ポイントごとに一定時間で入れ替える・拾われてから一定時間後に再生成するように変更
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// アイテムをフィールド上に生成するクラス
/// </summary>
// ========================================
/*
 * メモ
 * ・生成の権威はサーバーのみが持つ
 *   オンライン: サーバー/ホスト以外は何もしない(サーバーが生成したNetworkObjectが同期されてくるのを待つだけでよい)
 *   オフライン(NetworkManagerが動いていないテストシーン): その場で生成する
 * ・生成するアイテムのプレハブはNetworkPrefabsList(DefaultNetworkPrefabs)に登録しておくこと
 * ・初期生成はStartで行う(NetworkSceneManagerでのシーン読み込み中のAwakeでSpawnすると、
 *   シーン配置オブジェクトとして二重に処理されエラーになるため)
 * ・アイテムは、シーンにあるすべてのポイント(タグ ItemGeneratePoint)に1つずつ生成する
 * ・ポイントごとに、一定時間(_changeInterval秒)で次のことが起きる(時間はポイントごとに数える)
 *   ・アイテムがそのまま残っていれば、消して、新しいアイテムに入れ替える
 *   ・アイテムが拾われていれば、拾われた時から数え直し、一定時間後に、新しいアイテムを生成する
 *   新しいアイテムは候補からランダムに選ぶ(前と同じ種類になってもよい)
 *   拾われたアイテムはDespawn(破棄)されるので、ポイントのitemがnullになった時点で拾われたとみなす
 */
// ========================================
public class CS_ItemGenerator : MonoBehaviour
{
    [SerializeField][Tooltip("生成するアイテムの候補(ポイントごとに、この中からランダムに選ぶ。同じ種類が続いてもよい)")] private CS_ItemBase[] _initGenerateItemKinds;    // 生成するアイテムの候補
    [SerializeField][Tooltip("アイテムが入れ替わる・拾われた後に再生成されるまでの時間(秒)。ポイントごとに数える")][Min(1f)] private float _changeInterval = 60f;   // 入れ替え・再生成までの時間(秒)
    private CS_ItemGeneratePoint[] _itemGeneratePoints; // アイテム生成用のポイント

    // このマシンが生成の権威を持つか(オフライン、またはサーバー/ホスト)
    private static bool HasAuthority =>
        NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

    void Start()
    {
        // クライアントはサーバーが生成したものが同期されてくるのを待つだけでよい
        if (!HasAuthority) return;

        // シーンに存在するアイテム召還用のポイントを取得する
        var generatePointObjects = GameObject.FindGameObjectsWithTag("ItemGeneratePoint");
        if (generatePointObjects.Length <= 0)
            return;   // シーンにポイントが存在しない場合処理を飛ばす

        _itemGeneratePoints = new CS_ItemGeneratePoint[generatePointObjects.Length];
        for (int i = 0; i < generatePointObjects.Length; ++i)
        {
            _itemGeneratePoints[i] = generatePointObjects[i].GetComponent<CS_ItemGeneratePoint>();
        }

        // すべてのポイントにアイテムを生成する
        if (!HasItemKinds())
            return; // 生成するアイテムの候補がない場合処理を飛ばす

        foreach (var point in _itemGeneratePoints)
        {
            if (point != null) GenerateAt(point);
        }
    }

    void Update()
    {
        if (!HasAuthority) return;
        if (_itemGeneratePoints == null || !HasItemKinds()) return;

        foreach (var point in _itemGeneratePoints)
        {
            if (point != null) UpdatePoint(point);
        }
    }

    // 生成するアイテムの候補があるか
    private bool HasItemKinds()
    {
        return _initGenerateItemKinds != null && _initGenerateItemKinds.Length > 0;
    }

    // ポイント1つ分の時間を進め、時間が来たらアイテムを入れ替える(拾われていれば新しく生成する)
    private void UpdatePoint(CS_ItemGeneratePoint point)
    {
        bool hasItem = point.item != null;

        // 拾われた(アイテムがなくなった)瞬間から、数え直す
        if (point.hadItem && !hasItem) point.timer = 0f;
        point.hadItem = hasItem;

        point.timer += Time.deltaTime;
        if (point.timer < _changeInterval) return;

        // 時間が来た。残っているアイテムは消して、新しいアイテムを生成する
        if (hasItem) RemoveItem(point.item);
        GenerateAt(point);
    }

    // ポイントに、候補からランダムに選んだアイテムを生成し、数える時間をリセットする
    private void GenerateAt(CS_ItemGeneratePoint point)
    {
        CS_ItemBase kind = _initGenerateItemKinds[Random.Range(0, _initGenerateItemKinds.Length)];
        point.item = Generate(kind, point.transform);
        point.hadItem = true;
        point.timer = 0f;
    }

    // フィールドに残っているアイテムを取り除く(オンラインならネットワーク越しにDespawn、オフラインならDestroy)
    private static void RemoveItem(CS_ItemBase item)
    {
        if (item.IsSpawned)
        {
            item.NetworkObject.Despawn();
            return;
        }

        Destroy(item.gameObject);
    }

    /// <summary>
    /// 指定の位置にアイテムを生成する
    /// </summary>
    /// <param name="item">生成するアイテム</param>
    /// <param name="spawnPoint">生成する位置</param>
    /// <returns>生成したアイテム</returns>
    public CS_ItemBase Generate(CS_ItemBase item, Transform spawnPoint)
    {
        CS_ItemBase instance = Instantiate(item, spawnPoint);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            instance.NetworkObject.Spawn();
        }

        return instance;
    }
}
