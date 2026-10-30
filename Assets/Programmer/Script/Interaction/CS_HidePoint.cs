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
 * ・飛べる距離(_reachDistance、既定10m)はハイドポイントごとに決める。強調UIもこの距離に入った時だけ出る
 * ・隠れられる箱(_hideBoxSize。幅・高さ・奥行)
 *   立つ位置の足元を底にして、立つ位置の向きに合わせて回した箱。プレイヤーの体の中心が中に入っている間は隠れている扱い
 *   (SAを使わずに歩いて入った時も同じ)。既定は高さ3.3m(プレイヤーの高さ2m + ジャンプの高さ約1.3m)、幅・奥行1.5m
 *   シーンビューでは、選んでいない時も箱をギズモで描く
 * ・1つのハイドポイントに何人でも入れる(以前の「1か所に1人まで」の予約は無くした)
 * ・有効な間は一覧(activePoints)に登録する
 * ・ネットワークの同期はしない(全員のフィールドに同じ位置で置かれている前提)
 */
// ========================================

public class CS_HidePoint : MonoBehaviour
{
    [SerializeField] private Transform _standPoint;                                     // 着く位置と向き(空ならこのオブジェクト自身)
    [SerializeField][Min(0f)] private float _reachDistance = 10f;                      // 飛べる距離(m。高さ込み)。強調UIもこの距離で出る
    [SerializeField] private Vector3 _hideBoxSize = new Vector3(1.5f, 3.3f, 1.5f);      // 隠れられる箱の大きさ(x: 幅, y: 高さ, z: 奥行。m)

    // 有効なハイドポイントの一覧
    private static readonly List<CS_HidePoint> _activePoints = new List<CS_HidePoint>();
    public static IReadOnlyList<CS_HidePoint> activePoints => _activePoints;

    public Transform standTransform => _standPoint != null ? _standPoint : transform;     // 着く位置と向き(UIの目印の位置にも使う)
    public Vector3 standPosition => standTransform.position;
    // 着いた時に向く方向(水平)
    public Vector3 standForward => Vector3.ProjectOnPlane(standTransform.forward, Vector3.up).normalized;
    public float reachDistance => _reachDistance;

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
        Vector3 local = Quaternion.Inverse(GetBoxRotation()) * (position - standPosition);
        return Mathf.Abs(local.x) <= _hideBoxSize.x * 0.5f
            && local.y >= 0f && local.y <= _hideBoxSize.y
            && Mathf.Abs(local.z) <= _hideBoxSize.z * 0.5f;
    }

    // 箱の向き(立つ位置の向きの、水平の回転だけ)
    private Quaternion GetBoxRotation()
    {
        Vector3 forward = standForward;
        return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
    }

    private void OnValidate()
    {
        _hideBoxSize = Vector3.Max(_hideBoxSize, Vector3.zero);
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

        // 隠れられる箱(足元が底)
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(position, GetBoxRotation(), Vector3.one);
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, _hideBoxSize.y * 0.5f, 0f), _hideBoxSize);
        Gizmos.matrix = previous;
    }

    // 選んでいる時は、飛べる距離も描く
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(standPosition, _reachDistance);
    }
}
