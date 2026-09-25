/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警察の視界・攻撃で使うレイヤーをまとめたクラス
/// キャラクター(プレイヤー・悪人・警察)はEntityレイヤーに属する前提で、
/// ・標的を探す・攻撃を当てる相手 → Entityレイヤーだけ
/// ・視線や攻撃を遮るもの       → Entity以外(壁・床などのステージ)
/// とする。警察自身もEntityなので、仲間の体が視線や攻撃を遮ることはない
/// </summary>
public static class CS_PoliceLayers
{
    // キャラクターが属するレイヤーの名前(Project Settings > Tags and Layers で追加したもの)
    private const string _entityLayerName = "Entity";

    // キャラクター(プレイヤー・悪人・警察)が属するレイヤー
    private static readonly LayerMask _entityLayers;

    // 視線・攻撃を遮るレイヤー(Entity以外。Ignore Raycastは含めない)
    private static readonly LayerMask _obstacleLayers;

    // キャラクター(プレイヤー・悪人・警察)が属するレイヤー
    public static LayerMask entityLayers => _entityLayers;

    // 視線・攻撃を遮るレイヤー
    public static LayerMask obstacleLayers => _obstacleLayers;

    /// <summary>
    /// レイヤー名からレイヤーを決める静的コンストラクタ(最初に使われた時に1回だけ実行される)
    /// </summary>
    static CS_PoliceLayers()
    {
        int entityLayer = LayerMask.NameToLayer(_entityLayerName);
        if (entityLayer < 0)
        {
            // レイヤーが無い環境でも動くよう、全レイヤーを対象・遮るものにする(レイヤー追加前と同じ動き)
            Debug.LogError($"CS_PoliceLayers: \"{_entityLayerName}\" レイヤーがありません。Project Settings > Tags and Layers を確認してください");
            _entityLayers = Physics.DefaultRaycastLayers;
            _obstacleLayers = Physics.DefaultRaycastLayers;
            return;
        }

        _entityLayers = 1 << entityLayer;
        _obstacleLayers = Physics.DefaultRaycastLayers & ~_entityLayers;
    }
}
