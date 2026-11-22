using UnityEngine;

/*
 * 動くSAポイント(CS_PathFollowerで動く物)に乗っている間、プレイヤーを一緒に運ぶクラス(プレイヤーに付ける)
 * 信号機の上などに着いたあと、信号機が動き出しても、乗ったまま運ばれる
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・乗っている判定: 接地していて、SAで飛んでいない間に、足元の真下(_rayLength以内)にある物が、動くSAポイント(CS_PathFollowerの動かす物)の一部なら乗っている
 *   この判定は全クライアントで行う(他の人の見た目の補正にも使うため。通信は増やさない)
 * ・運び方(本人の手元だけ): 乗っている間、毎物理フレーム、CS_Playerが決めた速度に、足場の水平の速度を足す(歩く・ジャンプは今まで通り)
 *   位置を直接動かさないのは、Rigidbodyの補間が切れてカクカクし、歩きのアニメーションが揺れるため。ジャンプ・落下・SAで離れたら、そこで運ぶのをやめる
 *   位置は今まで通りNetworkTransformが他の人へ伝えるので、他の人の画面にも運ばれて見える
 * ・見た目の補正: 乗っている足場の速度(CS_PathFollower.worldVelocity)をplatformVelocityで公開する(乗っていなければ0)
 *   CS_PlayerVisualは、座標の変化から歩きの速さを求める時に、この速度を引く(運ばれただけで歩きのモーションにならないように)
 *   足場の位置は全員が同じ時刻から計算するので、他の人の画面でも同じ速度になる
 * ・対象は動くSAポイント(CS_PathFollower)だけ。床や他の物の上は運ばない。向き(回転)には追従しない(CS_PathFollowerは位置だけ動かすため)
 * ・SAポイント(CS_SAPoint)は変えていない
 * ・SATestのPlayerにだけ付けている。Player.prefabには付いていない(CS_PlayerGrappleRopeと同じ扱い)
 */
// ========================================

[DefaultExecutionOrder(100)] // CS_PlayerのFixedUpdate(速度の決定)より後に、足場の速度を足すため
[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(Rigidbody))]
public class CS_PlayerRideSAPoint : MonoBehaviour
{
    [SerializeField][Min(0.05f)] private float _rayLength = 0.4f;     // 足元から、乗っている物を探す距離(m)

    private const int _hitBufferSize = 8;
    private readonly RaycastHit[] _hits = new RaycastHit[_hitBufferSize];

    private CS_Player _player;
    private CS_PlayerHide _hide;
    private Rigidbody _rigidbody;
    private Collider _collider;

    private Vector3 _platformVelocity;      // 乗っている足場の速度(m/秒。ワールド)

    // 乗っている足場の速度(乗っていなければ0)。見た目(CS_PlayerVisual)が、運ばれた分を歩きに数えないために使う
    public Vector3 platformVelocity => _platformVelocity;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _hide = GetComponent<CS_PlayerHide>();
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
    }

    private void OnDisable()
    {
        _platformVelocity = Vector3.zero;
    }

    // 足場の速度を毎フレーム更新する(全クライアント)
    private void Update()
    {
        CS_PathFollower follower = FindFollower();
        _platformVelocity = follower != null ? follower.worldVelocity : Vector3.zero;
    }

    // 運ぶ(本人の手元だけ)。CS_PlayerのFixedUpdateが速度を決めた後に、足場の水平の速度を足す
    // 位置を直接動かすと、Rigidbodyの補間(Interpolate)が切れて物理フレームごとにカクカクし、歩きのアニメーションが揺れる。速度なら補間のまま、なめらかに運べる
    // 垂直の速度は足さない(CS_Playerは毎フレーム垂直の速度を読み直すため、足すと増え続ける。足場の上下の動きは、足場との接触で運ばれる)
    private void FixedUpdate()
    {
        if (!_player.isLocalPlayer || !_player.canAct) return;
        if (FindFollower() == null) return;

        Vector3 velocity = _rigidbody.linearVelocity;
        _rigidbody.linearVelocity = new Vector3(velocity.x + _platformVelocity.x, velocity.y, velocity.z + _platformVelocity.z);
    }

    // 足元の真下の、乗っている動くSAポイントのCS_PathFollower(無ければnull)
    private CS_PathFollower FindFollower()
    {
        if (_hide != null && _hide.isHiding) return null;
        if (!_player.isGrounded || _collider == null) return null;

        Bounds bounds = _collider.bounds;
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.1f, bounds.center.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, _rayLength + 0.1f, ~0, QueryTriggerInteraction.Ignore);

        // 一番近い、自分以外の当たりを調べる
        float nearest = float.MaxValue;
        Collider ground = null;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _hits[i].collider;
            if (hit.transform.IsChildOf(transform)) continue;
            if (_hits[i].distance >= nearest) continue;

            nearest = _hits[i].distance;
            ground = hit;
        }
        if (ground == null) return null;

        // 動くSAポイント(CS_PathFollowerの動かす物)の一部か
        CS_PathFollower follower = ground.GetComponentInParent<CS_PathFollower>();
        if (follower == null || follower.saPoint == null) return null;

        return ground.transform.IsChildOf(follower.saPoint.transform) ? follower : null;
    }
}
