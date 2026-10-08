using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/*
 * ランダムイベント「救援物資」
 * 発生地点の周りに、有利になるアイテムを設置する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・設置するアイテム(どれを・いくつ)は supplies で設定する(プランナー確認中のため、アセットで調整する)
 * ・置き方 : 発生地点の範囲(CS_RandomEventPoint.radius × placeRadiusRate)の円周上に、等間隔で並べる
 *   床から浮いたり埋まったりしないよう、置く位置をNavMeshの上に合わせてから heightOffset だけ上げる
 *   (近くにNavMeshが無い位置は、そのままの高さに置く)
 * ・全て拾われたら、終了時間(Duration)を待たずに終わる
 * ・終わった時に拾われずに残ったアイテムは、removeItemsOnEnd がオンなら消す(オフなら残す)
 * ・アイテムの生成はサーバー(またはオフライン)で行い、オンラインではSpawnして全員に見せる
 *   アイテムのプレハブはNetworkPrefabsList(DefaultNetworkPrefabs)に登録されていること
 *   (CS_ItemGenerator.GenerateはTransformの子に生成するため使わず、同じ手順でその場に生成している)
 */
// ========================================

[CreateAssetMenu(fileName = "DB_RandomEventSupplyDrop", menuName = "RandomEvent/Supply Drop")]
public class CSO_RandomEventSupplyDrop : CSO_RandomEvent
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

    [Header("救援物資")]
    [SerializeField]
    [Tooltip("設置するアイテムと数")]
    private Supply[] _supplies = new Supply[0];

    [SerializeField, Range(0f, 1f)]
    [Tooltip("アイテムを並べる円の半径(発生地点の範囲に対する割合)")]
    private float _placeRadiusRate = 0.5f;

    [SerializeField, Min(0f)]
    [Tooltip("床(NavMesh)からアイテムを置く高さ(m)。プレイヤーが触れて拾える高さにする")]
    private float _heightOffset = 1f;

    [SerializeField]
    [Tooltip("終わった時に、拾われずに残ったアイテムを消す")]
    private bool _removeItemsOnEnd;

    public override void OnStart(CS_RandomEventContext context)
    {
        List<CS_ItemBase> placed = new List<CS_ItemBase>();
        context.state = placed;

        List<CS_ItemBase> items = ExpandSupplies();
        float radius = context.point.radius * _placeRadiusRate;
        for (int i = 0; i < items.Count; i++)
        {
            Vector3 position = GetPlacePosition(context.point.transform.position, radius, i, items.Count);
            placed.Add(SpawnItem(items[i], position));
        }
    }

    // 全て拾われたら終わる(拾われたアイテムは消えてnull扱いになる)
    public override bool IsFinished(CS_RandomEventContext context)
    {
        if (!(context.state is List<CS_ItemBase> placed)) return true;

        return !placed.Exists(item => item != null);
    }

    public override void OnEnd(CS_RandomEventContext context)
    {
        if (!_removeItemsOnEnd) return;
        if (!(context.state is List<CS_ItemBase> placed)) return;

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

    // 発生地点を中心とした円周上に等間隔で並べ、NavMeshの上に合わせる
    private Vector3 GetPlacePosition(Vector3 center, float radius, int index, int count)
    {
        Vector3 position = center;
        if (count > 1)
        {
            float angle = 360f / count * index;
            position += Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
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
