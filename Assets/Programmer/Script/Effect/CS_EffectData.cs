using System;
using UnityEngine;

/*
 * エフェクト1つ分の設定データ
 * CSO_EffectDataBase(DB_EffectDataBase)のリストに並べて使う
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・Prefabは ParticleSystem / VisualEffect(VFX Graph) / IEffectPlayable(仮エフェクト用のスクリプト) のどれでもよい
 *   子に入っているものもまとめて再生する(CS_PooledEffect)
 * ・Lifetime 秒たったら出すのをやめ(ParticleSystemはStop、VFX GraphはStop)、残っている粒が消えたらプールに戻す
 *   ループしないエフェクトは 0 にすると、粒が消えた時点で戻す
 * ・Scale を 1 以外にすると、ParticleSystemの Scaling Mode を Hierarchy に変えて大きさを反映する
 *   (Localのままだと親の大きさが効かないため。本番の素材は 1 のまま、Prefab側で大きさを決めるのがおすすめ)
 * ・Override Color をオンにすると、ParticleSystemの Start Color / VFX Graphの「Color」プロパティ / IEffectPlayableの色を Color で上書きする
 *   呼ぶ側が色を渡した時(4色のカラーバリエーション用)は、そちらが優先される
 */
// ========================================

[Serializable]
public class CS_EffectData
{
    [Header("エフェクトのID")]
    [SerializeField] private CSE_EffectId _id;

    [Header("出すPrefab")]
    [SerializeField] private GameObject _prefab;

    [Header("時間")]
    [Tooltip("何秒で出すのをやめるか(0: ループしないエフェクトなので、粒が消えたら終わる)")]
    [Min(0f)]
    [SerializeField] private float _lifetime = 1f;

    [Header("見た目の調整")]
    [Tooltip("出す時の大きさ(倍)")]
    [Min(0.01f)]
    [SerializeField] private float _scale = 1f;

    [Tooltip("オンにすると、色をColorで上書きする")]
    [SerializeField] private bool _overrideColor;

    [ColorUsage(true, true)]
    [SerializeField] private Color _color = Color.white;

    [Header("同時に出せる数")]
    [Tooltip("同じエフェクトを同時に出せる最大数。超えたら一番古いものを消して出し直す")]
    [Min(1)]
    [SerializeField] private int _maxConcurrent = 8;

    public CSE_EffectId id => _id;
    public GameObject prefab => _prefab;
    public float lifetime => _lifetime;
    public float scale => _scale;
    public bool overrideColor => _overrideColor;
    public Color color => _color;
    public int maxConcurrent => _maxConcurrent;
}
