using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/*
 * ケアパッケージの中身「アイテムドロップ」
 * 開封した箱の周りに、有利になるアイテムを設置する(旧イベント「救援物資」の処理)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・設置するアイテム(どれを・いくつ)は supplies で設定する
 * ・置き方 : 箱があった位置を中心に、半径 placeRadius(m)の円周上に等間隔で並べる
 *   床から浮いたり埋まったりしないよう、置く位置をNavMeshの上に合わせてから heightOffset だけ上げる
 * ・全て拾われたら、イベントの終了時間を待たずに終わる
 * ・イベントが終わった時に残ったアイテムは、removeItemsOnEnd がオンなら消す(オフなら残す)
 * ・アイテムの生成はサーバー(またはオフライン)で行い、オンラインではSpawnして全員に見せる
 *   アイテムのプレハブはNetworkPrefabsList(DefaultNetworkPrefabs)に登録されていること
 */
// ========================================

[CreateAssetMenu(fileName = "DB_CarePackageItemDrop", menuName = "RandomEvent/CarePackage/Item Drop")]
public class CSO_CarePackageItemDrop : CSO_CarePackageContent
{
    // 設置するアイテム1種類分
    [Serializable]
    public class Supply
    {
        [SerializeField]
        [Tooltip("設置するアイテム(フィールドに置くアイテムのプレハブ)")]
        private CS_ItemBase _item;

        [SerializeField, Min(0)]
        [Tooltip("設置する数")]
        private int _count = 1;

        public CS_ItemBase item => _item;
        public int count => _count;
    }

    private const float _navMeshSampleRadius = 2f;   // 置く位置の近くでNavMeshを探す半径(m)

    [SerializeField]
    [Tooltip("設置するアイテムと数")]
    private Supply[] _supplies = new Supply[0];

    [SerializeField, Min(0f)]
    [Tooltip("アイテムを並べる円の半径(m)")]
    private float _placeRadius = 2f;

    [SerializeField, Min(0f)]
    [Tooltip("床(NavMesh)からアイテムを置く高さ(m)。プレイヤーが触れて拾える高さにする")]
    private float _heightOffset = 1f;

    [SerializeField]
    [Tooltip("イベントが終わった時に、拾われずに残ったアイテムを消す")]
    private bool _removeItemsOnEnd;

    public override void Open(CS_CarePackageOpening opening)
    {
        List<CS_ItemBase> placed = new List<CS_ItemBase>();
        opening.state = placed;

        List<CS_ItemBase> items = ExpandSupplies();
        for (int i = 0; i < items.Count; i++)
        {
            placed.Add(SpawnItem(items[i], GetPlacePosition(opening.position, i, items.Count)));
        }
    }

    // 全て拾われたら終わる(拾われたアイテムは消えてnull扱いになる)
    public override bool IsFinished(CS_CarePackageOpening opening)
    {
        if (!(opening.state is List<CS_ItemBase> placed)) return true;

        return !placed.Exists(item => item != null);
    }

    public override void OnEnd(CS_CarePackageOpening opening)
    {
        if (!_removeItemsOnEnd) return;
        if (!(opening.state is List<CS_ItemBase> placed)) return;

        foreach (CS_ItemBase item in placed)
        {
            if (item != null) RemoveItem(item);
        }
    }

    // 「アイテム × 数」を、置く順に1個ずつ並べたリストにする
    private List<CS_ItemBase> ExpandSupplies()
    {
        List<CS_ItemBase> items = new List<CS_ItemBase>();
        foreach (Supply supply in _supplies)
        {
            if (supply == null || supply.item == null) continue;

            for (int i = 0; i < supply.count; i++) items.Add(supply.item);
        }
        return items;
    }

    // 中心の周りの円周上に等間隔で並べ、NavMeshの上に合わせる
    private Vector3 GetPlacePosition(Vector3 center, int index, int count)
    {
        Vector3 position = center;
        if (count > 1)
        {
            float angle = 360f / count * index;
            position += Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _placeRadius;
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, _navMeshSampleRadius, NavMesh.AllAreas))
        {
            position = hit.position;
        }
        return position + Vector3.up * _heightOffset;
    }

    private static CS_ItemBase SpawnItem(CS_ItemBase prefab, Vector3 position)
    {
        CS_ItemBase instance = Instantiate(prefab, position, Quaternion.identity);
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            instance.NetworkObject.Spawn();
        }
        return instance;
    }

    private static void RemoveItem(CS_ItemBase item)
    {
        if (item.IsSpawned)
        {
            item.NetworkObject.Despawn();
            return;
        }
        Destroy(item.gameObject);
    }
}
