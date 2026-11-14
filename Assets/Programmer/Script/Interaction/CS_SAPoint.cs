using System.Collections.Generic;
using UnityEngine;

/*
 * SAポイント(SAで飛んで行ける場所)の基底クラス。ハイドポイント(CS_HidePoint)・信号機(CS_TrafficLightSAPoint)などが継承する
 * フィールドに置くだけで使える。プレイヤー側(CS_PlayerHide)が、有効なSAポイントの中から飛ぶ先を探す
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「プレイヤー」のSA(移動先 ①高所 ②隠密ポイント、SAポイントの仕様について)と、Issue #381
 *   SAの書き方に合わせ、物のオブジェクトに付けて、子に「立つ位置・向き」の空オブジェクト(_standPoint)を置く
 *   _standPointの位置へ飛ぶ。_standPointは、着いた時の足元の位置に置く(地面・街灯の上・車の上など。高さも含めてそこへ飛ぶ)
 *   _standPointが空なら、このオブジェクト自身の位置・向きを使う
 * ・SAReachBox(SAできる範囲の箱。子の空オブジェクト_SAReachBox)はSAポイントごとに決める(Issue #414)
 *   プレイヤーの足元がこの箱の中にある時だけSAできる。強調UIもこの箱に入った時だけ出る
 *   _SAReachBoxの位置(箱の中心)・回転・Scale(箱の大きさ。x: 幅, y: 高さ, z: 奥行。m)をそのまま箱にする
 *   _SAReachBoxが空ならSAできない
 *   シーンビューでは、このSAポイントか子(SAReachBox・StandPointなど)を選んでいる時に箱をギズモで描く
 * ・種類ごとの違いは、このクラスを継承して作る(例: 隠れられる箱を持つCS_HidePoint、飛ぶだけのCS_TrafficLightSAPoint)
 *   隠れられる箱を持つ種類は、Contains・hasHideBox・GetHideBoxPointを上書きする(持たない種類は何もしなくてよい)
 * ・有効な間は一覧(activePoints)に登録する。1つのSAポイントに何人でも入れる
 * ・ネットワークの同期はしない(全員のフィールドに同じ位置で置かれている前提)
 */
// ========================================

public abstract class CS_SAPoint : MonoBehaviour
{
    [SerializeField] private Transform _standPoint;                                     // 着く位置と向き(空ならこのオブジェクト自身)
    [SerializeField] private Transform _SAReachBox;                                     // SAできる範囲の箱(位置・回転・Scaleを使う。空ならSAできない)。強調UIもこの箱で出る

    // 有効なSAポイントの一覧
    private static readonly List<CS_SAPoint> _activePoints = new List<CS_SAPoint>();
    public static IReadOnlyList<CS_SAPoint> activePoints => _activePoints;

    public Transform standTransform => _standPoint != null ? _standPoint : transform;     // 着く位置と向き(UIの目印の位置にも使う)
    public Vector3 standPosition => standTransform.position;
    // 着いた時に向く方向(水平)
    public Vector3 standForward => Vector3.ProjectOnPlane(standTransform.forward, Vector3.up).normalized;

    // ========================================
    // 飛び方(変えたい種類だけ上書きする。既定は今までのSAと同じ動き)
    // ========================================

    public virtual float pullSpeed => 0f;       // 飛ぶ速さ(m/秒)。0以下ならCS_PlayerHideの_flySpeedを使う
    public virtual float releaseDistanceRate => 0f; // 飛び始めの距離のこの割合(0〜1)まで引っ張られるか、着いたら、早い方で自動で放して自由落下する(グラップル)。0以下なら放さず、着いたら位置を合わせて速度0(信号機・ハイドポイント)

    // ========================================
    // 隠れられる箱(持つ種類だけ上書きする)
    // ========================================

    public virtual bool hasHideBox => false;   // 隠れられる箱があるか

    // positionが、このSAポイントの隠れられる箱の中にあるか
    public virtual bool Contains(Vector3 position)
    {
        return false;
    }

    // 隠れられる箱の中の点(localは箱の中心が(0, 0, 0)、端が±0.5)を、ワールドの位置にする。箱が無ければ立つ位置を返す
    public virtual Vector3 GetHideBoxPoint(Vector3 local)
    {
        return standPosition;
    }

    // ========================================
    // 共通
    // ========================================

    protected virtual void OnEnable()
    {
        if (!_activePoints.Contains(this)) _activePoints.Add(this);
    }

    protected virtual void OnDisable()
    {
        _activePoints.Remove(this);
    }

    // Domain Reloadオフ対策(再生のたびに一覧を空にする)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activePoints.Clear();
    }

    // positionが、SAできる範囲の箱(SAReachBox)の中にあるか
    public bool IsInSAReachBox(Vector3 position)
    {
        return IsInsideBox(_SAReachBox, position);
    }

    // positionが、boxの位置・回転・Scaleで決まる箱の中にあるか(boxが空ならfalse)
    protected static bool IsInsideBox(Transform box, Vector3 position)
    {
        if (box == null) return false;

        // 箱のローカル座標では、箱は中心(0, 0, 0)・大きさ(1, 1, 1)になる
        Vector3 local = box.InverseTransformPoint(position);
        return Mathf.Abs(local.x) <= 0.5f && Mathf.Abs(local.y) <= 0.5f && Mathf.Abs(local.z) <= 0.5f;
    }

    // シーン上で、立つ位置・向く方向・SAできる範囲の箱が分かるようにする
    protected virtual void OnDrawGizmos()
    {
        Vector3 position = standPosition;
        Gizmos.color = new Color(0.3f, 0.9f, 1f);
        Gizmos.DrawWireSphere(position + Vector3.up * 0.9f, 0.4f);
        Gizmos.DrawLine(position, position + Vector3.up * 1.8f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(position + Vector3.up * 0.9f, position + Vector3.up * 0.9f + standForward);

        // このSAポイントか、その子(SAReachBox・StandPointなど)を選んでいる時は、SAできる範囲の箱(SAReachBox)も描く
        if (IsSelectedInEditor()) DrawBox(_SAReachBox, new Color(1f, 0.8f, 0.2f, 0.5f));
    }

    // boxの位置・回転・Scaleで決まる箱を描く(boxが空なら描かない)
    protected static void DrawBox(Transform box, Color color)
    {
        if (box == null) return;

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = box.localToWorldMatrix;
        Gizmos.color = color;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = previous;
    }

    // エディタで、このSAポイントか、その子のどれかを選んでいるか
    protected bool IsSelectedInEditor()
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
