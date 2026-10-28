using System.Collections.Generic;
using UnityEngine;

/*
 * エフェクトをIDで出すマネージャー(SEのCS_SoundManagerにあたる)
 * DB_EffectDataBaseからPrefabを引き、プールして使い回す
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・Prefabは Assets/Programmer/Prefab/Effect/EffectManager.prefab。MainSceneに置いてある
 *   シーンをまたいでも残る(DontDestroyOnLoad)。2つ目が来たら後から来た方を消す
 * ・出し方
 *   CS_EffectManager.instance.Play(ID, 位置, 向き)               : その場に出す
 *   CS_EffectManager.instance.Play(ID, 位置, 向き, 付いていく先)   : 付いていく先の子にして出す(一緒に動く)
 *   CS_EffectManager.instance.Play(ID, 位置, 向き, 付いていく先, 色) : 色を変えて出す(4色のカラーバリエーション用。付いていく先はnullでもよい)
 * ・ここで出したエフェクトは自分の画面にだけ出る(通信しない)
 *   全員の画面に出したい時は CS_NetworkEffectPlayer を使う
 *   (HPなど、全員に同期されている値の変化を見て出す時は、各クライアントでこちらを直接呼べばよい)
 * ・付いていく先が消えると、エフェクトも一緒に消える(プールからも外れ、次に必要になった時に作り直す)
 * ・プール
 *   使っていないエフェクトは、非表示の「Pool」オブジェクトの子に置いておく
 *   同じIDのエフェクトがMax Concurrentより多くなる時は、一番古いものを消して出し直す
 */
// ========================================

public class CS_EffectManager : MonoBehaviour
{
    private static CS_EffectManager _instance;
    public static CS_EffectManager instance => _instance;

    [Header("データベース")]
    [SerializeField] private CSO_EffectDataBase _database;

    private readonly Dictionary<CSE_EffectId, Queue<CS_PooledEffect>> _freeEffects = new();
    private readonly Dictionary<CSE_EffectId, List<CS_PooledEffect>> _activeEffects = new();
    private Transform _poolRoot;   // 使っていないエフェクトを置く場所(非表示)

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 非表示の親の下で作ると、Play On Awakeで勝手に再生されない
        GameObject poolObject = new GameObject("Pool");
        poolObject.transform.SetParent(transform, false);
        poolObject.SetActive(false);
        _poolRoot = poolObject.transform;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // その場にエフェクトを出す
    public void Play(CSE_EffectId id, Vector3 position, Quaternion rotation)
    {
        Play(id, position, rotation, null, null);
    }

    // followの子にしてエフェクトを出す(followがnullならその場に出す)
    public void Play(CSE_EffectId id, Vector3 position, Quaternion rotation, Transform follow)
    {
        Play(id, position, rotation, follow, null);
    }

    // 色を変えてエフェクトを出す(colorがnullなら、DBのOverride Colorか、Prefabの元の色)
    public void Play(CSE_EffectId id, Vector3 position, Quaternion rotation, Transform follow, Color? color)
    {
        if (!TryGetData(id, out CS_EffectData data)) return;

        CS_PooledEffect effect = Rent(data);
        Transform effectTransform = effect.transform;

        // 付いていく先が無い時はマネージャーの子にする(シーンをまたいでも消えないように)
        effectTransform.SetParent(follow != null ? follow : transform, false);
        effectTransform.SetPositionAndRotation(position, rotation);
        effectTransform.localScale = Vector3.one * data.scale;

        Color? playColor = color;
        if (!playColor.HasValue && data.overrideColor) playColor = data.color;

        effect.Play(playColor);
    }

    private bool TryGetData(CSE_EffectId id, out CS_EffectData data)
    {
        data = null;
        if (id == CSE_EffectId.None) return false;

        if (_database == null)
        {
            Debug.LogError("[CS_EffectManager] エフェクトデータベースが未設定です。", this);
            return false;
        }

        if (!_database.TryGetData(id, out data))
        {
            Debug.LogWarning($"[CS_EffectManager] エフェクトが未登録です: {id}", this);
            return false;
        }

        if (data.prefab != null) return true;

        Debug.LogWarning($"[CS_EffectManager] Prefabが未設定です: {id}", this);
        return false;
    }

    // プールから1つ取り出す(足りなければ作る。多すぎれば一番古いものを使い回す)
    private CS_PooledEffect Rent(CS_EffectData data)
    {
        List<CS_PooledEffect> actives = GetActiveList(data.id);
        actives.RemoveAll(effect => effect == null);   // 付いていく先と一緒に消えたもの

        if (actives.Count >= data.maxConcurrent) FinishOldest(actives);

        Queue<CS_PooledEffect> frees = GetFreeQueue(data.id);
        CS_PooledEffect pooled = null;
        while (pooled == null && frees.Count > 0)
        {
            pooled = frees.Dequeue();
        }

        if (pooled == null) pooled = Create(data);

        actives.Add(pooled);
        return pooled;
    }

    private CS_PooledEffect Create(CS_EffectData data)
    {
        GameObject instance = Instantiate(data.prefab, _poolRoot);
        instance.name = data.prefab.name;

        CS_PooledEffect pooled = instance.AddComponent<CS_PooledEffect>();
        pooled.Initialize(data, Release);
        return pooled;
    }

    // 再生が終わったエフェクトをプールに戻す(CS_PooledEffectから呼ばれる)
    private void Release(CS_PooledEffect effect)
    {
        GetActiveList(effect.data.id).Remove(effect);

        effect.transform.SetParent(_poolRoot, false);
        GetFreeQueue(effect.data.id).Enqueue(effect);
    }

    private static void FinishOldest(List<CS_PooledEffect> actives)
    {
        CS_PooledEffect oldest = actives[0];
        foreach (CS_PooledEffect effect in actives)
        {
            if (effect.playStartTime < oldest.playStartTime) oldest = effect;
        }

        oldest.ForceFinish();
    }

    private List<CS_PooledEffect> GetActiveList(CSE_EffectId id)
    {
        if (!_activeEffects.TryGetValue(id, out List<CS_PooledEffect> list))
        {
            list = new List<CS_PooledEffect>();
            _activeEffects.Add(id, list);
        }
        return list;
    }

    private Queue<CS_PooledEffect> GetFreeQueue(CSE_EffectId id)
    {
        if (!_freeEffects.TryGetValue(id, out Queue<CS_PooledEffect> queue))
        {
            queue = new Queue<CS_PooledEffect>();
            _freeEffects.Add(id, queue);
        }
        return queue;
    }
}
