using System.Collections.Generic;
using UnityEngine;

/*
 * ハイドポイント(物陰など、プレイヤーが張り付いて隠れられる場所)
 * フィールドに置くだけで使える。プレイヤー側(CS_PlayerHide)が、有効なハイドポイントの中から飛び移る先を探す
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「プレイヤー」のSA(移動先 ②隠密ポイント)と、方針ドキュメント「ハイドポイントの作り方の方針」
 *   SAの書き方に合わせ、物陰のオブジェクトに付けて、子に「立つ位置・向き」の空オブジェクト(_standPoint)を置く
 *   _standPointの位置へ張り付き、_standPointの正面(青い矢印)を向く。正面は隠れる物(壁など)の方へ向けておく
 *   _standPointが空なら、このオブジェクト自身の位置・向きを使う
 * ・_standPointは地面の高さに置く(今は水平方向にだけ飛ぶため。高所へ飛ぶSAは別に作る)
 * ・有効な間は一覧(activePoints)に登録する
 * ・使っているプレイヤー(1か所に1人まで)はサーバー(またはオフライン)だけが持つ(TryReserve / Release)
 * ・ネットワークの同期はしない(全員のフィールドに同じ位置で置かれている前提)
 *   プレイヤーからサーバーへは、立つ位置(FindAt)でどのハイドポイントかを伝える
 */
// ========================================

public class CS_HidePoint : MonoBehaviour
{
    [SerializeField] private Transform _standPoint;     // 張り付く位置と向き(空ならこのオブジェクト自身)

    private CS_PlayerHide _occupant;    // 今使っているプレイヤー(サーバー、またはオフラインのみ)

    // 有効なハイドポイントの一覧
    private static readonly List<CS_HidePoint> _activePoints = new List<CS_HidePoint>();
    public static IReadOnlyList<CS_HidePoint> activePoints => _activePoints;

    private Transform standTransform => _standPoint != null ? _standPoint : transform;
    public Vector3 standPosition => standTransform.position;
    // 張り付いている時に向く方向(水平)
    public Vector3 standForward => Vector3.ProjectOnPlane(standTransform.forward, Vector3.up).normalized;

    private void OnEnable()
    {
        if (!_activePoints.Contains(this)) _activePoints.Add(this);
    }

    private void OnDisable()
    {
        _activePoints.Remove(this);
        _occupant = null;
    }

    // Domain Reloadオフ対策(再生のたびに一覧を空にする)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activePoints.Clear();
    }

    // 立つ位置がpositionからtolerance以内のハイドポイント(無ければnull)。サーバーが、プレイヤーから伝えられた位置で探す
    public static CS_HidePoint FindAt(Vector3 position, float tolerance)
    {
        CS_HidePoint nearest = null;
        float nearestSqr = tolerance * tolerance;
        foreach (CS_HidePoint point in _activePoints)
        {
            float sqr = (point.standPosition - position).sqrMagnitude;
            if (sqr > nearestSqr) continue;

            nearest = point;
            nearestSqr = sqr;
        }
        return nearest;
    }

    // ---- 使っているプレイヤー(サーバー、またはオフラインのみ) ----

    // hideが使う予約をする。他のプレイヤーが使っていればfalse
    public bool TryReserve(CS_PlayerHide hide)
    {
        if (_occupant != null && _occupant != hide) return false;

        _occupant = hide;
        return true;
    }

    // hideの予約を外す(他のプレイヤーの予約は外さない)
    public void Release(CS_PlayerHide hide)
    {
        if (_occupant == hide) _occupant = null;
    }

    // シーン上で、立つ位置と向く方向が分かるようにする
    private void OnDrawGizmos()
    {
        Vector3 position = standPosition;
        Gizmos.color = new Color(0.3f, 0.9f, 1f);
        Gizmos.DrawWireSphere(position + Vector3.up * 0.9f, 0.4f);
        Gizmos.DrawLine(position, position + Vector3.up * 1.8f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(position + Vector3.up * 0.9f, position + Vector3.up * 0.9f + standForward);
    }
}
