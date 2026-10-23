using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの変身の状態を表すクラス
 * 仕様変更(仕様書「変身」、Issue #277)で、プレイヤーは最初から変身していて解除できなくなった
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・プレイヤーは常に変身完了(Transformed)の状態。変身ボタン・変身途中・解除・クールタイムは無くなった
 *   (以前の、変身中に一定間隔で警察へ信号を出す仕組みと、手配度(CS_PlayerWantedLevel)も、警戒状態への置き換えで無くなった)
 * ・タグは常に変身中のタグ(PlayerTransformation)にする。警察の視界(CS_PoliceVision)はこのタグのプレイヤーを見つける
 *   (Player.prefabのタグは通常のタグのままなので、起動時にここで切り替える)
 * ・攻撃・必殺技・パリーの反撃などは今も isTransformed を見ているが、常にtrueなので条件としては働かない
 *   (状態は変わらないため、NetworkVariableでの同期はしない)
 */
// ========================================

public class CS_PlayerTransformation : NetworkBehaviour
{
    private const string _transformedTag = "PlayerTransformation";

    public CSE_PlayerTransformState state => CSE_PlayerTransformState.Transformed;
    public bool isTransforming => false;
    public bool isTransformed => true;

    // どのクライアントでも、スポーンより前に変身中のタグにしておく
    private void Awake()
    {
        gameObject.tag = _transformedTag;
    }
}
