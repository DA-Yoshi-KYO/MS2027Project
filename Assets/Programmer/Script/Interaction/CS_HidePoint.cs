using UnityEngine;

/*
 * ハイドポイント(SAポイントの1つ。物陰・高所など、プレイヤーが飛んで行けて、隠れられる場所)
 * フィールドに置くだけで使える。SAの共通部分(立つ位置・SAできる範囲の箱・一覧)は基底クラス(CS_SAPoint)にある
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「プレイヤー」のSA(移動先 ①高所 ②隠密ポイント、SAポイントの仕様について)と、Issue #381
 *   立つ位置(_standPoint)・SAできる範囲の箱(_SAReachBox)の使い方は、基底クラスCS_SAPointのメモを見ること
 *   _standPointの正面(青い矢印)は、隠れる物(壁など)の方へ向けておく
 * ・隠れられる箱(子の空オブジェクト_hideBox)
 *   _hideBoxの位置(箱の中心)・回転・Scale(箱の大きさ)をそのまま箱にする。プレイヤーの体の中心が中に入っている間は隠れている扱い
 *   (SAを使わずに歩いて入った時も同じ)。_standPointとは別のオブジェクトなので、_standPointを動かした時はHideBoxも合わせて動かす
 *   プレハブの既定は位置 (0, 6.65, 0)・Scale (1.5, 3.3, 1.5)(StandPointの足元が底。高さ3.3m = プレイヤーの高さ2m + ジャンプの高さ約1.3m、幅・奥行1.5m)
 *   _hideBoxが空なら隠れられない。シーンビューでは、選んでいない時も箱をギズモで描く
 *   飛べるかを調べる時(CS_PlayerHide)は、この箱がカメラからどれだけ見えているかも使う(GetHideBoxPoint)
 * ・ネットワークの同期はしない(全員のフィールドに同じ位置で置かれている前提)
 */
// ========================================

public class CS_HidePoint : CS_SAPoint
{
    [SerializeField] private Transform _hideBox;                                        // 隠れられる箱(位置・回転・Scaleを使う。空なら隠れられない)

    public override bool hasHideBox => _hideBox != null;   // 隠れられる箱があるか

    // positionが、どれかのハイドポイントの隠れられる箱の中にあるか
    public static bool IsInsideAny(Vector3 position)
    {
        foreach (CS_SAPoint point in activePoints)
        {
            if (point.Contains(position)) return true;
        }
        return false;
    }

    // positionが、このハイドポイントの隠れられる箱の中にあるか
    public override bool Contains(Vector3 position)
    {
        return IsInsideBox(_hideBox, position);
    }

    // 隠れられる箱の中の点(localは箱の中心が(0, 0, 0)、端が±0.5)を、ワールドの位置にする。箱が空なら立つ位置を返す
    public override Vector3 GetHideBoxPoint(Vector3 local)
    {
        return _hideBox != null ? _hideBox.TransformPoint(local) : standPosition;
    }

    // シーン上で、隠れられる箱も分かるようにする
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        DrawBox(_hideBox, new Color(0.3f, 1f, 0.5f, 0.8f));
    }
}
