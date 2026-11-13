using System.Collections.Generic;
using UnityEngine;

/*
 * ハイドポイント(SAポイント。物陰・高所など、プレイヤーが飛んで行けて、隠れられる場所)
 * フィールドに置くだけで使える。プレイヤー側(CS_PlayerHide)が、有効なハイドポイントの中から飛ぶ先を探す
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「プレイヤー」のSA(移動先 ①高所 ②隠密ポイント、SAポイントの仕様について)と、Issue #381
 *   SAの書き方に合わせ、物陰のオブジェクトに付けて、子に「立つ位置・向き」の空オブジェクト(_standPoint)を置く
 *   _standPointの位置へ飛び、_standPointの正面(青い矢印)を向く。正面は隠れる物(壁など)の方へ向けておく
 *   _standPointが空なら、このオブジェクト自身の位置・向きを使う
 * ・_standPointは、着いた時の足元の位置に置く(地面・街灯の上・車の上など。高さも含めてそこへ飛ぶ)
 * ・SAReachBox(SAできる範囲の箱。子の空オブジェクト_SAReachBox)はハイドポイントごとに決める(Issue #414)
 *   プレイヤーの足元がこの箱の中にある時だけSAできる。強調UIもこの箱に入った時だけ出る
 *   _SAReachBoxの位置(箱の中心)・回転・Scale(箱の大きさ。x: 幅, y: 高さ, z: 奥行。m)をそのまま箱にする
 *   Unityの移動・回転・拡大ツールで直せる。プレハブの既定はScale (20, 10, 20)
 *   _standPointとは別に動かせる(_standPointを回しても箱は回らない。子なので、HidePointごと回した時は一緒に回る)
 *   _SAReachBoxが空ならSAできない
 *   シーンビューでは、このハイドポイントか子(SAReachBox・StandPoint・HideBox)を選んでいる時に箱をギズモで描く(隠れられる箱とは色を分ける)
 * ・隠れられる箱(子の空オブジェクト_hideBox)
 *   _hideBoxの位置(箱の中心)・回転・Scale(箱の大きさ)をそのまま箱にする。プレイヤーの体の中心が中に入っている間は隠れている扱い
 *   (SAを使わずに歩いて入った時も同じ)。_standPointとは別のオブジェクトなので、_standPointを動かした時はHideBoxも合わせて動かす
 *   プレハブの既定は位置 (0, 6.65, 0)・Scale (1.5, 3.3, 1.5)(StandPointの足元が底。高さ3.3m = プレイヤーの高さ2m + ジャンプの高さ約1.3m、幅・奥行1.5m)
 *   _hideBoxが空なら隠れられない。シーンビューでは、選んでいない時も箱をギズモで描く
 *   飛べるかを調べる時(CS_PlayerHide)は、この箱がカメラからどれだけ見えているかも使う(GetHideBoxPoint)
 * ・1つのハイドポイントに何人でも入れる(以前の「1か所に1人まで」の予約は無くした)
 * ・有効な間は一覧(activePoints)に登録する
 * ・ネットワークの同期はしない(全員のフィールドに同じ位置で置かれている前提)
 */
// ========================================

public class CS_HidePoint : MonoBehaviour
{
    [SerializeField] private Transform _standPoint;                                     // 着く位置と向き(空ならこのオブジェクト自身)
    [SerializeField] private Transform _SAReachBox;                                     // SAできる範囲の箱(位置・回転・Scaleを使う。空ならSAできない)。強調UIもこの箱で出る
    [SerializeField] private Transform _hideBox;                                        // 隠れられる箱(位置・回転・Scaleを使う。空なら隠れられない)

    // 有効なハイドポイントの一覧
    private static readonly List<CS_HidePoint> _activePoints = new List<CS_HidePoint>();
    public static IReadOnlyList<CS_HidePoint> activePoints => _activePoints;

    public Transform standTransform => _standPoint != null ? _standPoint : transform;     // 着く位置と向き(UIの目印の位置にも使う)
    public Vector3 standPosition => standTransform.position;
    // 着いた時に向く方向(水平)
    public Vector3 standForward => Vector3.ProjectOnPlane(standTransform.forward, Vector3.up).normalized;

    private void OnEnable()
    {
        if (!_activePoints.Contains(this)) _activePoints.Add(this);
    }

    private void OnDisable()
    {
        _activePoints.Remove(this);
    }

    // Domain Reloadオフ対策(再生のたびに一覧を空にする)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activePoints.Clear();
    }

    // positionが、どれかのハイドポイントの隠れられる箱の中にあるか
    public static bool IsInsideAny(Vector3 position)
    {
        foreach (CS_HidePoint point in _activePoints)
        {
            if (point.Contains(position)) return true;
        }
        return false;
    }

    // positionが、このハイドポイントの隠れられる箱の中にあるか
    public bool Contains(Vector3 position)
    {
        return IsInsideBox(_hideBox, position);
    }

    public bool hasHideBox => _hideBox != null;   // 隠れられる箱があるか

    // 隠れられる箱の中の点(localは箱の中心が(0, 0, 0)、端が±0.5)を、ワールドの位置にする。箱が空なら立つ位置を返す
    public Vector3 GetHideBoxPoint(Vector3 local)
    {
        return _hideBox != null ? _hideBox.TransformPoint(local) : standPosition;
    }

    // positionが、SAできる範囲の箱(SAReachBox)の中にあるか
    public bool IsInSAReachBox(Vector3 position)
    {
        return IsInsideBox(_SAReachBox, position);
    }

    // positionが、boxの位置・回転・Scaleで決まる箱の中にあるか(boxが空ならfalse)
    private static bool IsInsideBox(Transform box, Vector3 position)
    {
        if (box == null) return false;

        // 箱のローカル座標では、箱は中心(0, 0, 0)・大きさ(1, 1, 1)になる
        Vector3 local = box.InverseTransformPoint(position);
        return Mathf.Abs(local.x) <= 0.5f && Mathf.Abs(local.y) <= 0.5f && Mathf.Abs(local.z) <= 0.5f;
    }

    // シーン上で、立つ位置・向く方向・隠れられる箱が分かるようにする
    private void OnDrawGizmos()
    {
        Vector3 position = standPosition;
        Gizmos.color = new Color(0.3f, 0.9f, 1f);
        Gizmos.DrawWireSphere(position + Vector3.up * 0.9f, 0.4f);
        Gizmos.DrawLine(position, position + Vector3.up * 1.8f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(position + Vector3.up * 0.9f, position + Vector3.up * 0.9f + standForward);

        // 隠れられる箱
        DrawBox(_hideBox, new Color(0.3f, 1f, 0.5f, 0.8f));

        // このハイドポイントか、その子(SAReachBox・StandPoint・HideBox)を選んでいる時は、SAできる範囲の箱(SAReachBox)も描く
        if (IsSelectedInEditor()) DrawBox(_SAReachBox, new Color(1f, 0.8f, 0.2f, 0.5f));
    }

    // boxの位置・回転・Scaleで決まる箱を描く(boxが空なら描かない)
    private static void DrawBox(Transform box, Color color)
    {
        if (box == null) return;

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = box.localToWorldMatrix;
        Gizmos.color = color;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = previous;
    }

    // エディタで、このハイドポイントか、その子のどれかを選んでいるか
    private bool IsSelectedInEditor()
    {
#if UNITY_EDITOR
        foreach (Transform selected in UnityEditor.Selection.transforms)
        {
            if (selected.IsChildOf(transform)) return true;
        }
#endif
        return false;
    }
}
