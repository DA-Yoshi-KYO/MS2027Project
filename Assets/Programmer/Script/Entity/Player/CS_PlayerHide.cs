using Unity.Netcode;
using UnityEngine;

/*
 * SA(ステルスアクション。ハイドポイントへ飛ぶ)と、ハイドポイントの隠れられる箱に入っている間の「隠れている」状態を扱うクラス
 * ペルソナ5のカバーがリファレンス。ハイドポイント(CS_HidePoint)はフィールドに置く
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「プレイヤー」のSA(SAポイントの仕様について)と、Issue #381
 * ・SAの流れ(本人の手元で進める)
 *   1. 飛んでいない間、飛べるハイドポイント(currentCandidate)を探す
 *      条件: そのハイドポイントの飛べる距離(CS_HidePoint.reachDistance)以内(高さ込み)・画面に映っている・
 *            カメラの高さから見て壁などに遮られていない・カメラの正面に一番近い
 *   2. 飛べるハイドポイントがある時にダッシュボタン(RT/SHIFT)を押すと、ダッシュの代わりに飛ぶ(CS_PlayerがTryBeginHideを呼ぶ)
 *      無い時は今まで通りダッシュになる
 *   3. 飛ぶ: 体の底(足元)が立つ位置に来る所へ、高さも含めて_flySpeedでまっすぐ飛ぶ(CS_Player.Fly)。街灯の上・車の上などにも飛べる
 *      飛んでいる間は物にぶつからず(Rigidbody.detectCollisionsを切る)、重力も切る。_maxFlyTime秒以内に着けなかった時はやめる
 *      (飛べるのは、壁などに遮られず見えているハイドポイントだけなので、壁を抜けて飛ぶことはない)
 *   4. 着いた: 位置(高さ込み)を合わせ、ハイドポイントの正面を向いて、すぐ普通の状態に戻る(歩ける・ジャンプできる・重力あり)
 *      以前の「張り付いて動かない」は無くした(仕様「SAポイント移動後も通常の状態と変わらないように移動できる」)
 * ・隠れている判定(isHidden)
 *   プレイヤーの体の中心(当たり判定の中心)が、どれかのハイドポイントの隠れられる箱(CS_HidePoint)の中にある間は隠れている
 *   SAで飛んで来た時も、歩いて入った時も同じ。飛んでいる最中も、箱の外なら見つかる
 *   判定はサーバー(またはオフライン)で毎フレーム行い、NetworkVariableで全員に伝える(警察・悪人のAIはサーバーで動くため)
 *   警察(CS_PoliceVision)・悪人(CS_VillainCombat)は、IsConcealedFromで「隠れていて、_revealDistanceより離れているか」を問い合わせる
 *   _revealDistance(既定2m)まで近づかれると、隠れていても見つかる
 *   本人の手元では、サーバーから伝わる前でも箱に入ったかが分かるよう、isInHideBoxも公開する(奇襲の条件などに使う)
 * ・1つのハイドポイントに何人でも入れる(以前の予約は無くした)
 * ・SAで飛ぶのは人のプレイヤーだけ(NPCは使わない)。隠れている判定はNPCにも同じように効く
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(Rigidbody))]
public class CS_PlayerHide : NetworkBehaviour
{
    [Header("飛べるハイドポイントを探す")]
    [SerializeField] private float _sightHeight = 1.3f;                 // 遮るものを調べる視線の高さ(足元から。カメラの高さに合わせる)
    [SerializeField] private float _pointHeight = 1f;                   // ハイドポイントの立つ位置から、視線を通す先の高さ(m)

    [Header("飛ぶ")]
    [SerializeField][Min(0.1f)] private float _flySpeed = 25f;          // 飛ぶ速さ(m/秒)
    [SerializeField][Min(0f)] private float _arriveDistance = 0.3f;     // 着く位置との距離がこれ以内になったら着いたとみなす(m。高さ込み)
    [SerializeField][Min(0f)] private float _maxFlyTime = 1f;           // これ以上かかっても着かなければやめる(秒)

    [Header("見つかる距離")]
    [SerializeField][Min(0f)] private float _revealDistance = 2f;       // 隠れていても、警察・悪人とこの距離以内なら見つかる(m。高さ込み)

    // 隠れているか(書き込みはサーバー、またはオフラインのみ。全員で分かる)
    private readonly NetworkVariable<bool> _isHidden = new NetworkVariable<bool>();

    private CS_Player _player;
    private Rigidbody _rigidbody;
    private Collider _collider;
    private CS_PlayerHealth _health;
    private CS_PlayerAmbush _ambush;    // 奇襲。奇襲の飛び込み中はSAしない(付いていなければ見ない)

    // 本人の手元の状態
    private bool _isFlying;
    private CS_HidePoint _point;            // 飛んでいる先のハイドポイント
    private CS_HidePoint _candidate;        // 今ダッシュボタンを押したら飛ぶハイドポイント
    private float _elapsed;
    private bool _isFloating;               // 重力を切っているか(元に戻すため)

    public bool isHiding => _isFlying;                      // SAでハイドポイントへ飛んでいる最中か(本人の手元)
    public bool isHidden => _isHidden.Value;                // 隠れている状態か(全員で分かる。サーバーが決める)
    public bool isInHideBox => CS_HidePoint.IsInsideAny(bodyCenter);   // 体の中心が隠れられる箱の中にあるか(その場で調べる)
    public CS_HidePoint currentCandidate => _candidate;     // 今ダッシュボタンを押したら飛ぶハイドポイント(本人の手元。アイコン表示用)

    // 体の中心(当たり判定の中心)
    private Vector3 bodyCenter => _collider != null ? _collider.bounds.center : transform.position;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _health = GetComponent<CS_PlayerHealth>();
        _ambush = GetComponent<CS_PlayerAmbush>();
    }

    private void OnDisable()
    {
        if (_isFlying) EndFly();
        SetHidden(false);
    }

    private void Update()
    {
        UpdateHidden();

        if (!CanUse())
        {
            _candidate = null;
            if (_isFlying) EndFly();
            return;
        }

        if (_isFlying)
        {
            _candidate = null;
            UpdateFly();
            return;
        }

        _candidate = FindCandidate();
    }

    // ========================================
    // CS_Playerから呼ぶ
    // ========================================

    // 飛べるハイドポイントがあれば、そこへ飛び始める(始めたらtrue)。ダッシュボタンが押された時に呼ぶ
    public bool TryBeginHide()
    {
        if (_isFlying || !CanUse() || _candidate == null) return false;
        if (_ambush != null && _ambush.isAmbushing) return false;

        _point = _candidate;
        _candidate = null;
        _isFlying = true;
        _elapsed = 0f;
        _player.SetAimFacing(Quaternion.LookRotation(HorizontalDirection(_point.standPosition)).eulerAngles.y);
        return true;
    }

    // 飛ぶのをやめる(ジャンプボタンが押された時など)
    public void ExitHide()
    {
        if (_isFlying) EndFly();
    }

    // ========================================
    // 警察・悪人から呼ぶ(サーバー、またはオフライン)
    // ========================================

    // targetRootのプレイヤーが、observerPositionから見て隠れているか(近づかれていればfalse)
    public static bool IsConcealedFrom(Transform targetRoot, Vector3 observerPosition)
    {
        if (targetRoot == null || !targetRoot.TryGetComponent(out CS_PlayerHide hide) || !hide.isHidden) return false;

        return (targetRoot.position - observerPosition).sqrMagnitude > hide._revealDistance * hide._revealDistance;
    }

    // ========================================
    // 隠れている判定(サーバー、またはオフライン)
    // ========================================

    // 体の中心が隠れられる箱の中にある間は隠れている(倒れている間は隠れない)
    private void UpdateHidden()
    {
        if (IsSpawned && !IsServer) return;

        bool isDead = _health != null && _health.isDead;
        SetHidden(!isDead && isInHideBox);
    }

    private void SetHidden(bool hidden)
    {
        if (IsSpawned && !IsServer) return;
        if (_isHidden.Value != hidden) _isHidden.Value = hidden;
    }

    // ========================================
    // SA(本人の手元)
    // ========================================

    // SAを使えるか(自分が操作している人のプレイヤーで、動ける時だけ)
    private bool CanUse()
    {
        return _player.isLocalPlayer && _player.canAct;
    }

    // 飛べるハイドポイントのうち、カメラの正面に一番近いもの(無ければnull)
    private CS_HidePoint FindCandidate()
    {
        Camera viewCamera = _player.playerCamera;
        if (viewCamera == null) return null;

        Vector3 cameraForward = viewCamera.transform.forward;
        Vector3 feet = GetFeetPosition();
        Vector3 sightPosition = feet + Vector3.up * _sightHeight;
        CS_HidePoint best = null;
        float bestAngle = float.MaxValue;
        foreach (CS_HidePoint point in CS_HidePoint.activePoints)
        {
            // 飛べる距離はハイドポイントごと。足元同士の距離で比べる
            if (Vector3.Distance(feet, point.standPosition) > point.reachDistance) continue;

            Vector3 target = point.standPosition + Vector3.up * _pointHeight;
            if (!IsOnScreen(viewCamera, target)) continue;

            float angle = Vector3.Angle(cameraForward, target - viewCamera.transform.position);
            if (angle >= bestAngle) continue;

            // 壁などの向こうのハイドポイントには飛べない(キャラクターは遮るものとして扱わない)
            if (!CS_PoliceLineOfSight.IsClear(sightPosition, target, transform)) continue;

            best = point;
            bestAngle = angle;
        }
        return best;
    }

    // 着く位置(体の底が立つ位置に来る所)へ直進し、着いたら普通の状態に戻る
    private void UpdateFly()
    {
        _elapsed += Time.deltaTime;

        Vector3 toPoint = GetArrivePosition(_point) - transform.position;
        float distance = toPoint.magnitude;
        if (distance <= _arriveDistance)
        {
            Arrive();
            return;
        }

        // 何かに引っかかって着かない時はやめる
        if (_elapsed >= _maxFlyTime)
        {
            EndFly();
            return;
        }

        // 毎フレーム向きと距離を決め直す(少しずれても、着く位置へ向かう)
        SetFloating(true);
        _rigidbody.detectCollisions = false;
        _player.Fly(toPoint / distance, distance, distance / _flySpeed);
    }

    // 着いた: 位置(高さ込み)を合わせ、ハイドポイントの正面を向いて、普通の状態に戻る
    private void Arrive()
    {
        CS_HidePoint point = _point;
        _player.SnapPosition(GetArrivePosition(point));
        EndFly();

        // 正面(隠れる物の方)を向いておく。移動キーを押せば、いつも通り進む方向へ向く
        Vector3 forward = point.standForward;
        if (forward.sqrMagnitude > 0.0001f) _player.SetAimFacing(Quaternion.LookRotation(forward).eulerAngles.y);
        _player.ClearAimFacing();
    }

    private void EndFly()
    {
        _isFlying = false;
        _point = null;
        _elapsed = 0f;
        _player.ClearAimFacing();
        SetFloating(false);
    }

    // 体の底(足元)が立つ位置に来る時の、プレイヤーの位置(プレイヤーの位置は体の中心のため)
    private Vector3 GetArrivePosition(CS_HidePoint point)
    {
        return point.standPosition + (transform.position - GetFeetPosition());
    }

    // 足元の位置(当たり判定の底)
    private Vector3 GetFeetPosition()
    {
        if (_collider == null) return transform.position;

        Bounds bounds = _collider.bounds;
        return new Vector3(transform.position.x, bounds.min.y, transform.position.z);
    }

    private bool IsOnScreen(Camera viewCamera, Vector3 position)
    {
        Vector3 viewport = viewCamera.WorldToViewportPoint(position);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    // 飛んでいる間は重力を切る(floatingがfalseで元に戻し、当たり判定も戻す)
    private void SetFloating(bool floating)
    {
        if (!floating) _rigidbody.detectCollisions = true;
        if (_isFloating == floating) return;

        _isFloating = floating;
        _rigidbody.useGravity = !floating;
    }

    private Vector3 HorizontalDirection(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
    }
}
