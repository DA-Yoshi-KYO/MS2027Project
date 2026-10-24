using Unity.Netcode;
using UnityEngine;

/*
 * ハイド(ハイドポイントへ飛んで張り付き、警察・悪人から見つからなくなる)を行うクラス
 * ペルソナ5のカバーがリファレンス。ハイドポイント(CS_HidePoint)はフィールドに置く
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・仕様は方針ドキュメント「ハイドポイントの作り方の方針」(仕様書「プレイヤー」のSA ②隠密ポイント)
 *   今は段階1(飛んで張り付き、抜けられる。張り付いている間は見つからない)まで
 *   段階2(張り付いたまま次のハイドポイントへ飛び移る)・段階3(ハイド中から奇襲)はまだ
 * ・流れ(本人の手元で進める)
 *   1. ハイドしていない間、飛べるハイドポイント(currentCandidate)を探す
 *      条件: _searchRange以内(水平)・画面に映っている・カメラの高さから見て壁などに遮られていない・カメラの正面に一番近い
 *   2. 飛べるハイドポイントがある時にダッシュボタン(RT/SHIFT)を押すと、ダッシュの代わりに飛ぶ(CS_PlayerがTryBeginHideを呼ぶ)
 *      無い時は今まで通りダッシュになる
 *   3. 飛ぶ: ハイドポイントの立つ位置へ_flySpeedで直進する(CS_Player.Lunge)。着いたら位置を合わせ、ハイドポイントの正面を向く
 *      _maxFlyTime秒以内に着けなかった(何かに引っかかった)時はやめる
 *   4. 張り付く: その場から動かない。カメラは自由に動かせる。通常攻撃は出さない(CS_PlayerAttackがisHidingを見る)
 *   5. 抜ける: スティックでハイドポイントの正面(隠れている物)以外の方向へ倒す・ジャンプ(CS_PlayerがExitHideを呼ぶ)
 *      動けなくなった(倒れた・スタン)時も抜ける
 * ・見つからない判定はサーバーで行う(警察・悪人のAIはサーバーで動くため)
 *   張り付いたことをサーバーへ伝え、サーバーがisHidden(NetworkVariable)をtrueにする。飛んでいる間はまだ見つかる
 *   警察(CS_PoliceVision)・悪人(CS_VillainCombat)は、IsConcealedFromで「ハイド中で、_revealDistanceより離れているか」を問い合わせる
 *   _revealDistance(既定2m)まで近づかれると、ハイド中でも見つかる
 * ・1か所のハイドポイントに入れるのは1人だけ。飛び始めた時にサーバーで予約し、他の人が使っていれば手元の飛びをやめさせる
 * ・NPCは使わない(人のプレイヤーだけ)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerHide : NetworkBehaviour
{
    [Header("飛べるハイドポイントを探す")]
    [SerializeField][Min(0f)] private float _searchRange = 10f;         // ハイドポイントとの水平距離がこれ以内(m)
    [SerializeField] private float _sightHeight = 1.3f;                 // 遮るものを調べる視線の高さ(足元から。カメラの高さに合わせる)
    [SerializeField] private float _pointHeight = 1f;                   // ハイドポイントの立つ位置から、視線を通す先の高さ(m)

    [Header("飛ぶ")]
    [SerializeField][Min(0.1f)] private float _flySpeed = 25f;          // 飛ぶ速さ(m/秒)
    [SerializeField][Min(0f)] private float _arriveDistance = 0.3f;     // 立つ位置との水平距離がこれ以内になったら着いたとみなす(m)
    [SerializeField][Min(0f)] private float _maxFlyTime = 1f;           // これ以上かかっても着かなければやめる(秒)

    [Header("抜ける")]
    [SerializeField][Range(0f, 1f)] private float _exitInputThreshold = 0.5f;  // スティックをこれ以上倒したら抜ける

    [Header("見つかる距離")]
    [SerializeField][Min(0f)] private float _revealDistance = 2f;       // ハイド中でも、警察・悪人とこの水平距離以内なら見つかる(m)

    private const float _findTolerance = 0.5f;  // サーバーが、伝えられた位置からハイドポイントを探す時に許すずれ(m)

    private enum Phase
    {
        None,     // ハイドしていない
        Fly,      // ハイドポイントへ飛んでいる
        Hidden,   // 張り付いている
    }

    // ハイド中か(書き込みはサーバー、またはオフラインのみ。全員で分かる)
    private readonly NetworkVariable<bool> _isHidden = new NetworkVariable<bool>();

    private CS_Player _player;
    private CS_PlayerAmbush _ambush;    // 奇襲。奇襲の飛び込み中はハイドしない(付いていなければ見ない)

    // 本人の手元の状態
    private Phase _phase = Phase.None;
    private CS_HidePoint _point;            // 飛んでいる先・張り付いているハイドポイント
    private CS_HidePoint _candidate;        // 今ダッシュボタンを押したら飛ぶハイドポイント
    private float _elapsed;

    // サーバー(またはオフライン)で予約しているハイドポイント
    private CS_HidePoint _reservedPoint;

    public bool isHiding => _phase != Phase.None;           // 飛んでいる・張り付いている最中か(本人の手元)
    public bool isHidden => _isHidden.Value;                // 見つからない状態か(全員で分かる。サーバーが決める)
    public CS_HidePoint currentCandidate => _candidate;     // 今ダッシュボタンを押したら飛ぶハイドポイント(本人の手元。アイコン表示用)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _ambush = GetComponent<CS_PlayerAmbush>();
    }

    private void OnDisable()
    {
        if (_phase != Phase.None) EndHide();
        ReleaseOnServer();
    }

    public override void OnNetworkDespawn()
    {
        ReleaseOnServer();
    }

    private void Update()
    {
        if (!CanUse())
        {
            _candidate = null;
            if (_phase != Phase.None) EndHide();
            return;
        }

        switch (_phase)
        {
            case Phase.None:
                _candidate = FindCandidate();
                break;
            case Phase.Fly:
                _candidate = null;
                UpdateFly();
                break;
            case Phase.Hidden:
                _candidate = null;
                if (WantsToExit()) EndHide();
                break;
        }
    }

    // ========================================
    // CS_Playerから呼ぶ
    // ========================================

    // 飛べるハイドポイントがあれば、そこへ飛び始める(始めたらtrue)。ダッシュボタンが押された時に呼ぶ
    public bool TryBeginHide()
    {
        if (_phase != Phase.None || !CanUse() || _candidate == null) return false;
        if (_ambush != null && _ambush.isAmbushing) return false;

        _point = _candidate;
        _candidate = null;
        _phase = Phase.Fly;
        _elapsed = 0f;
        _player.SetAimFacing(Quaternion.LookRotation(HorizontalDirection(_point.standPosition)).eulerAngles.y);

        RequestReserve(_point);
        return _phase != Phase.None;   // オフラインでは、その場で予約できなければここで終わる
    }

    // ハイドをやめる(ジャンプボタンが押された時など)
    public void ExitHide()
    {
        if (_phase != Phase.None) EndHide();
    }

    // ========================================
    // 警察・悪人から呼ぶ(サーバー、またはオフライン)
    // ========================================

    // targetRootのプレイヤーが、observerPositionから見てハイドで隠れているか(近づかれていればfalse)
    public static bool IsConcealedFrom(Transform targetRoot, Vector3 observerPosition)
    {
        if (targetRoot == null || !targetRoot.TryGetComponent(out CS_PlayerHide hide) || !hide.isHidden) return false;

        Vector3 offset = targetRoot.position - observerPosition;
        offset.y = 0f;
        return offset.sqrMagnitude > hide._revealDistance * hide._revealDistance;
    }

    // ========================================
    // 本人の手元
    // ========================================

    // ハイドを使えるか(自分が操作している人のプレイヤーで、動ける時だけ)
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
        Vector3 sightPosition = transform.position + Vector3.up * _sightHeight;
        CS_HidePoint best = null;
        float bestAngle = float.MaxValue;
        foreach (CS_HidePoint point in CS_HidePoint.activePoints)
        {
            Vector3 target = point.standPosition + Vector3.up * _pointHeight;
            if (HorizontalDistance(point.standPosition) > _searchRange) continue;
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

    // ハイドポイントへ直進し、着いたら張り付く
    private void UpdateFly()
    {
        _elapsed += Time.deltaTime;

        float distance = HorizontalDistance(_point.standPosition);
        if (distance <= _arriveDistance)
        {
            Arrive();
            return;
        }

        // 何かに引っかかって着かない時はやめる
        if (_elapsed >= _maxFlyTime)
        {
            EndHide();
            return;
        }

        // 毎フレーム向きと距離を決め直す(引っかかって少しずれても、立つ位置へ向かう)
        _player.Lunge(HorizontalDirection(_point.standPosition), distance, distance / _flySpeed);
    }

    // 着いた: 位置を立つ位置に合わせ、ハイドポイントの正面を向いて張り付く
    private void Arrive()
    {
        _phase = Phase.Hidden;
        _player.SnapHorizontalPosition(_point.standPosition);

        Vector3 forward = _point.standForward;
        if (forward.sqrMagnitude > 0.0001f) _player.SetAimFacing(Quaternion.LookRotation(forward).eulerAngles.y);

        RequestHide();
    }

    // スティックを、ハイドポイントの正面(隠れている物の方)以外へ倒したか
    private bool WantsToExit()
    {
        Vector3 move = _player.moveDirection;
        if (move.magnitude < _exitInputThreshold) return false;

        return Vector3.Dot(move.normalized, _point.standForward) < 0.5f;
    }

    private void EndHide()
    {
        bool wasHiding = _phase != Phase.None;
        _phase = Phase.None;
        _point = null;
        _elapsed = 0f;
        _player.ClearAimFacing();

        if (wasHiding) RequestRelease();
    }

    private bool IsOnScreen(Camera viewCamera, Vector3 position)
    {
        Vector3 viewport = viewCamera.WorldToViewportPoint(position);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    private float HorizontalDistance(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private Vector3 HorizontalDirection(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
    }

    // ========================================
    // サーバーへの依頼
    // ========================================

    // 飛び始めたことを伝え、ハイドポイントを予約する(他の人が使っていれば、手元の飛びをやめさせる)
    private void RequestReserve(CS_HidePoint point)
    {
        // オフライン(テストシーン)では、その場で決める
        if (!IsSpawned)
        {
            if (!ReserveOnServer(point.standPosition)) EndHide();
            return;
        }

        ReserveRpc(point.standPosition);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReserveRpc(Vector3 standPosition)
    {
        if (ReserveOnServer(standPosition)) return;

        CancelRpc();   // 予約できなかったので、手元の飛びをやめさせる
    }

    // サーバーから、予約できなかったことを本人へ伝える
    [Rpc(SendTo.Owner)]
    private void CancelRpc()
    {
        if (_phase != Phase.None) EndHide();
    }

    // 張り付いたことを伝える(ここから見つからなくなる)
    private void RequestHide()
    {
        if (!IsSpawned)
        {
            HideOnServer();
            return;
        }

        HideRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void HideRpc()
    {
        HideOnServer();
    }

    // ハイドをやめたことを伝える(予約を外し、見つかるようにする)
    private void RequestRelease()
    {
        if (!IsSpawned)
        {
            ReleaseOnServer();
            return;
        }

        ReleaseRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReleaseRpc()
    {
        ReleaseOnServer();
    }

    // ---- サーバー(またはオフライン)で実行される ----

    private bool ReserveOnServer(Vector3 standPosition)
    {
        CS_HidePoint point = CS_HidePoint.FindAt(standPosition, _findTolerance);
        if (point == null || !point.TryReserve(this)) return false;

        if (_reservedPoint != null && _reservedPoint != point) _reservedPoint.Release(this);
        _reservedPoint = point;
        SetHidden(false);   // 飛んでいる間は、まだ見つかる
        return true;
    }

    private void HideOnServer()
    {
        // 予約できなかった(他の人が使っていた)時は、隠れない
        if (_reservedPoint == null) return;

        SetHidden(true);
    }

    private void ReleaseOnServer()
    {
        if (_reservedPoint != null) _reservedPoint.Release(this);
        _reservedPoint = null;
        SetHidden(false);
    }

    private void SetHidden(bool hidden)
    {
        if (IsSpawned && !IsServer) return;
        if (_isHidden.Value != hidden) _isHidden.Value = hidden;
    }
}
