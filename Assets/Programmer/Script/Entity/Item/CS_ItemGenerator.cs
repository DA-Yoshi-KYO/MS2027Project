/* ================================================
 * 
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * 2026-10-17 | 拾われたアイテムの再生成を追加
 * ================================================ */

using System.Collections.Generic;
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
 * ・再生成: フィールド上のアイテムが初期生成数より少ない間、再生成間隔ごとに1つずつ空いているポイントへ生成する
 *   再生成間隔が0なら再生成しない。生成するアイテムは初期生成の候補から選ぶ
 *   拾われたアイテムはDespawn(破棄)されるので、ポイントのitemがnullになった時点で空きとして扱う
 */
// ========================================
public class CS_ItemGenerator : MonoBehaviour
{
    [SerializeField][Tooltip("初期生成するアイテムの数")][Min(0)] private int _initGenerateNum = 0;      // 初期生成するアイテムの数
    [SerializeField][Tooltip("初期生成するアイテムの候補")] private CS_ItemBase[] _initGenerateItemKinds;    // 初期生成するアイテムの候補
    [SerializeField][Tooltip("再生成の間隔(秒)。0なら再生成しない")][Min(0f)] private float _respawnInterval = 30f;   // 再生成の間隔(秒)
    private CS_ItemGeneratePoint[] _itemGeneratePoints; // アイテム生成用のポイント
    private float _respawnTimer;                        // 再生成までの経過時間

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

        // アイテムを初期から生成する
        if (_initGenerateItemKinds.Length <= 0) 
            return; // 生成するアイテムの候補がない場合処理を飛ばす

        // 初期生成するアイテムの数がシーンに存在するアイテム召還用ポイントの数を上回っている時、
        // シーンに存在する分だけ生成する。
        int generateNum = Mathf.Min(_initGenerateNum, _itemGeneratePoints.Length);
        if (_initGenerateNum > _itemGeneratePoints.Length)
            Debug.LogWarning("初期生成する数がシーンに存在するアイテム召還用ポイントの数を上回っています。\n シーンに存在する分だけ生成します。");

        for (int i = 0; i < generateNum; ++i)
        {
            GenerateToRandomPoint(_initGenerateItemKinds[Random.Range(0, _initGenerateItemKinds.Length)]);
        }
    }

    void Update()
    {
        if (!HasAuthority) return;
        if (_respawnInterval <= 0f) return;
        if (_itemGeneratePoints == null || _initGenerateItemKinds == null || _initGenerateItemKinds.Length <= 0) return;

        // フィールド上のアイテムが初期生成数に足りている間はタイマーを進めない
        if (CountGeneratedItems() >= Mathf.Min(_initGenerateNum, _itemGeneratePoints.Length))
        {
            _respawnTimer = 0f;
            return;
        }

        _respawnTimer += Time.deltaTime;
        if (_respawnTimer < _respawnInterval) return;

        _respawnTimer = 0f;
        GenerateToRandomPoint(_initGenerateItemKinds[Random.Range(0, _initGenerateItemKinds.Length)]);
    }

    // フィールド上に残っている(拾われていない)アイテムの数を数える
    private int CountGeneratedItems()
    {
        int count = 0;
        foreach (var point in _itemGeneratePoints)
        {
            if (point.item != null) ++count;
        }
        return count;
    }

    /// <summary>
    /// シーンに存在する生成位置候補からランダムに選ばれた位置にアイテムを生成する
    /// </summary>
    /// <param name="item">生成するアイテム</param>
    /// <returns>生成に成功したかどうか</returns>
    public bool GenerateToRandomPoint(CS_ItemBase item)
    {
        if (_itemGeneratePoints == null || _itemGeneratePoints.Length <= 0)
        {
            Debug.LogError("ポイントが存在しません");
            return false;
        }

        // 既に生成されているアイテム生成位置をリストから除外する
        List<CS_ItemGeneratePoint> pointList = new List<CS_ItemGeneratePoint>(_itemGeneratePoints);
        pointList.RemoveAll(point => point.item != null);
        if (pointList.Count <= 0)
        {
            Debug.LogError("ポイントが全て生成済みです");
            return false;
        }

        // 除外した後の生成位置候補からランダムに決定し、アイテムを生成する
        var point = pointList[Random.Range(0, pointList.Count)];
        point.item = Generate(item, point.transform);

        return true;    //　生成に成功
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
