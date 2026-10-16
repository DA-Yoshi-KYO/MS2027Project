using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/*
 * ランダムイベント「ケアパッケージ」(旧: 救援物資・支援物資を統合)
 * 発生地点に箱が出現し、プレイヤーが長押しで開封すると、中身がランダムに1つ起きる
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・箱(CS_CarePackage)は発生地点の中心に置く(NavMeshの上に合わせる)
 * ・開封にかかる時間は openTime(初期値10秒)。開封の仕方・中断の条件はCS_CarePackage参照
 * ・中身は contents から重み(weight)で抽選する。重みが大きいほど出やすい(全て同じなら等確率)
 *   中身の種類を増やす時は、CSO_CarePackageContentを継承して作り、contentsに入れる
 *     アイテムドロップ(CSO_CarePackageItemDrop) : 箱の周りにアイテムを置く
 *     ステータスアップ(CSO_CarePackageStatBoost) : 開けた人のステータスを上げる
 * ・終わり方
 *   開封前 : 終了時間(Duration)が来たら、箱を消して終わる
 *   開封後 : 中身の処理が終わったら(CSO_CarePackageContent.IsFinished)、または終了時間で終わる
 * ・箱のプレハブはNetworkPrefabsList(DefaultNetworkPrefabs)に登録しておくこと
 */
// ========================================

[CreateAssetMenu(fileName = "DB_RandomEventCarePackage", menuName = "RandomEvent/Care Package")]
public class CSO_RandomEventCarePackage : CSO_RandomEvent
{
    // 中身1種類分の候補
    [Serializable]
    public class ContentEntry
    {
        [SerializeField]
        [Tooltip("中身")]
        private CSO_CarePackageContent _content;

        [SerializeField, Min(0f)]
        [Tooltip("出やすさ(重み)。0なら出ない")]
        private float _weight = 1f;

        public CSO_CarePackageContent content => _content;
        public float weight => _weight;
    }

    // 開催1回分の状態
    private class State
    {
        public CS_CarePackage box;
        public Vector3 position;
        public bool isOpened;
        public CSO_CarePackageContent content;
        public CS_CarePackageOpening opening;
    }

    private const float _navMeshSampleRadius = 2f;   // 箱を置く位置の近くでNavMeshを探す半径(m)

    [Header("ケアパッケージ")]
    [SerializeField]
    [Tooltip("箱のプレハブ(CarePackage)")]
    private CS_CarePackage _carePackagePrefab;

    [SerializeField, Min(0f)]
    [Tooltip("開封にかかる時間(秒)。この間ボタンを押し続ける必要がある")]
    private float _openTime = 10f;

    [SerializeField, Min(0f)]
    [Tooltip("床(NavMesh)から箱を置く高さ(m)。箱の中心の高さにする")]
    private float _boxHeight = 0.5f;

    [SerializeField]
    [Tooltip("中身の候補と出やすさ")]
    private ContentEntry[] _contents = new ContentEntry[0];

    public override void OnStart(CS_RandomEventContext context)
    {
        State state = new State();
        context.state = state;

        if (_carePackagePrefab == null)
        {
            Debug.LogWarning($"CSO_RandomEventCarePackage: {name} の箱のプレハブが未設定です", this);
            return;
        }

        state.position = GetGroundPosition(context.point.transform.position);
        state.box = Instantiate(_carePackagePrefab, state.position + Vector3.up * _boxHeight, Quaternion.identity);
        state.box.Setup(_openTime, opener => HandleOpened(context, state, opener));
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) state.box.NetworkObject.Spawn();
    }

    // 箱が開封されたら、中身を抽選して開ける
    private void HandleOpened(CS_RandomEventContext context, State state, GameObject opener)
    {
        state.isOpened = true;
        state.content = PickContent();
        if (state.content == null) return;

        state.opening = new CS_CarePackageOpening(context, opener, state.position);
        state.content.Open(state.opening);
    }

    public override bool IsFinished(CS_RandomEventContext context)
    {
        if (!(context.state is State state)) return true;

        // 開封前に箱が無くなった(プレハブ未設定など)
        if (!state.isOpened) return state.box == null;

        return state.content == null || state.content.IsFinished(state.opening);
    }

    public override void OnEnd(CS_RandomEventContext context)
    {
        if (!(context.state is State state)) return;

        if (!state.isOpened)
        {
            RemoveBox(state.box);
            return;
        }

        if (state.content != null) state.content.OnEnd(state.opening);
    }

    // 重み付きで中身を1つ選ぶ(候補が無ければnull)
    private CSO_CarePackageContent PickContent()
    {
        float total = 0f;
        foreach (ContentEntry entry in _contents)
        {
            if (entry != null && entry.content != null) total += entry.weight;
        }
        if (total <= 0f) return null;

        float pick = UnityEngine.Random.Range(0f, total);
        foreach (ContentEntry entry in _contents)
        {
            if (entry == null || entry.content == null || entry.weight <= 0f) continue;

            pick -= entry.weight;
            if (pick <= 0f) return entry.content;
        }
        return null;
    }

    private static Vector3 GetGroundPosition(Vector3 position)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, _navMeshSampleRadius, NavMesh.AllAreas) ? hit.position : position;
    }

    private static void RemoveBox(CS_CarePackage box)
    {
        if (box == null) return;

        if (box.IsSpawned)
        {
            box.NetworkObject.Despawn();
            return;
        }
        Destroy(box.gameObject);
    }
}
