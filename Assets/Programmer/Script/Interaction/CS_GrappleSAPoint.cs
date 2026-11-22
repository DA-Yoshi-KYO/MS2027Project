using UnityEngine;

/*
 * グラップルSAポイント(SAポイントの1つ。ロープを飛ばして的へ引っ張られる鉤縄)
 * 隠れられる箱は持たない(飛ぶだけ。隠れている扱いにはならない)
 * フィールドに置くだけで使える。立つ位置(_standPoint)を的の少し手前に、SAできる範囲の箱(_SAReachBox)を的の周りに広めに置く
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・仕様書「プレイヤー」の移動(ダッシュ以外の移動が欲しい。例: グラップル)。作業リストではSAで対応する扱い。動きの参考は鳴潮の鉤縄
 *   立つ位置・SAできる範囲の箱の使い方は、基底クラスCS_SAPointのメモを見ること
 * ・操作はSAと同じ(飛べる時にダッシュボタン)。放すボタンは無い。スイング・ぶら下がりも無い
 * ・動き
 *   1. ロープが飛び、立つ位置(_standPoint)へ一定の速さ(_pullSpeed)で引っ張られる
 *   2. 飛び始めの距離の_releaseDistanceRate(既定0.5 = 5割)まで引っ張られるか、着く位置に着いたら、早い方で自動でロープを放す
 *      (着いた時も、5割で放した時と同じ挙動。位置の合わせ込みはしない)
 *      放した後は飛んでいない。通常の状態に戻って、自由落下する(移動入力は通常どおり足される)
 *      放した瞬間の速度(向き・高さ込み)をそのまま残す。水平の勢いは、_releaseDragの割合で弱まりながら続く(0なら着地まで。CS_Player.EndSAMotion)。入力がある間は入力の速度へ近づくので、空中で曲げられる
 *   ジャンプボタンを押すと、途中で飛ぶのをやめられる(SA共通。やめたらその場で落ちる)
 * ・飛ばすだけで、着地はさせない(足場は要らない)。ポイントごとに値を変えられる(早く放したい的は割合を小さく、など)
 * ・的(_ropeEndPoint)には当たり判定を付けない(ロープの先を示すだけ)。飛んでいる間はプレイヤー側も当たり判定を切っている
 *   放した直後の短い間(CS_PlayerHideの_releaseGhostTime)も切ったままにして、近くの物の角に引っかからないようにしている
 * ・ロープの表示はCS_PlayerGrappleRope(プレイヤー側)が行う。ロープの先は_ropeEndPoint(空ならこのオブジェクトの位置)
 */
// ========================================

public class CS_GrappleSAPoint : CS_SAPoint
{
    [Header("グラップル")]
    [SerializeField][Min(0.1f)] private float _pullSpeed = 35f;                 // 引っ張られる速さ(m/秒)
    [SerializeField][Range(0.05f, 1f)] private float _releaseDistanceRate = 0.5f;  // 飛び始めの距離のこの割合まで引っ張られるか、着いたら放す(早い方)
    [SerializeField][Min(0f)] private float _releaseDrag = 0.5f;                // 放した後の水平の勢いの減速率(毎秒e^(-減速率)の割合。0なら減速せず着地まで続く。1なら約1.5秒で歩く速さ)
    [SerializeField] private Transform _ropeEndPoint;                           // ロープの先(的の位置。空ならこのオブジェクト自身)

    public override float pullSpeed => _pullSpeed;
    public override float releaseDistanceRate => _releaseDistanceRate;
    public override float releaseDrag => _releaseDrag;

    public Vector3 ropeEndPosition => _ropeEndPoint != null ? _ropeEndPoint.position : transform.position;
}
