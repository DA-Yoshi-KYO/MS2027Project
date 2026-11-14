using System;
using Unity.Netcode;
using UnityEngine;

/*
 * SA(ステルスアクション。ハイドポイントへ飛ぶ)と、ハイドポイントの隠れられる箱に入っている間の「隠れている」状態を扱うクラス
 * ペルソナ5のカバーがリファレンス。SAポイント(CS_SAPoint。ハイドポイント・信号機など)はフィールドに置く
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「プレイヤー」のSA(SAポイントの仕様について)と、Issue #381
 * ・SAの流れ(本人の手元で進める)
 *   1. 飛んでいない間、飛べるハイドポイント(currentCandidate)を探す
 *      条件: 足元がそのハイドポイントのSAできる範囲の箱(CS_SAPoint.IsInSAReachBox)の中にある・画面に映っている・
 *            カメラから見て、そのハイドポイントの隠れられる箱が_requiredVisibleRatio(既定30%)以上見えている・カメラの正面に一番近い
 *            体の中心がそのハイドポイントの隠れられる箱の中にある(今いるハイドポイント)時は、そこへは飛ばない
 * ・見えている割合
 *   隠れられる箱の中に、1辺_sightSampleCount個(既定3。3×3×3 = 27点)の点を並べ、カメラから各点へレイを飛ばす
 *   壁などに遮られずに届いた点の割合が_requiredVisibleRatio以上なら見えているとみなす(大きな物の一部に隠れていても、見えていれば飛べる)
 *   点は箱の端から少し内側(_sightSampleInset)に置く(箱の底が地面・屋根の面と重なっていても、面に当たって遮られないようにするため)
 *   届いたかの判定では、プレイヤー自身・キャラクター(IDamageable)は遮るものとして扱わない
 *   警察の視線と同じレイヤー(CS_PoliceLayers.obstacleLayers)を使う。警察の判定(CS_PoliceLineOfSight)とは別に持つ
 *   隠れられる箱が空のハイドポイントは、立つ位置の_pointHeight上の1点だけで調べる
 *   2. 飛べるハイドポイントがある時にダッシュボタン(RT/SHIFT)を押すと、ダッシュの代わりに飛ぶ(CS_PlayerがTryBeginHideを呼ぶ)
 *      無い時は今まで通りダッシュになる
 *   3. 飛ぶ: 体の底(足元)が立つ位置に来る所へ、高さも含めて_flySpeedでまっすぐ飛ぶ(SA専用の速度(CS_Player.SetSAVelocity)を毎物理フレーム渡す。Fly/Lungeの残り時間は使わない)。街灯の上・車の上などにも飛べる
 *      飛んでいる間は物にぶつからず(Rigidbody.detectCollisionsを切る)、重力も切る。_maxFlyTime秒以内に着けなかった時はやめる
 *      (飛べるのは、壁などに遮られず見えているハイドポイントだけなので、壁を抜けて飛ぶことはない)
 *   4. 着いた: 位置(高さ込み)を合わせ、飛ぶ前に向いていた向きに戻して、すぐ普通の状態に戻る(歩ける・ジャンプできる・重力あり)
 *      以前の「張り付いて動かない」は無くした(仕様「SAポイント移動後も通常の状態と変わらないように移動できる」)
 * ・隠れている判定(isHidden)
 *   プレイヤーの体の中心(当たり判定の中心)が、どれかのハイドポイントの隠れられる箱(CS_HidePoint)の中にある間は隠れている(信号機など箱を持たないSAポイントは隠れ扱いにならない)
 *   SAで飛んで来た時も、歩いて入った時も同じ。飛んでいる最中も、箱の外なら見つかる
 *   判定はサーバー(またはオフライン)で毎フレーム行い、NetworkVariableで全員に伝える(警察・悪人のAIはサーバーで動くため)
 *   警察(CS_PoliceVision)・悪人(CS_VillainCombat)は、IsConcealedFromで「隠れていて、_revealDistanceより離れているか」を問い合わせる
 *   _revealDistance(既定2m)まで近づかれると、隠れていても見つかる
 *   本人の手元では、サーバーから伝わる前でも箱に入ったかが分かるよう、isInHideBoxも公開する(奇襲の条件などに使う)
 * ・飛び方はSAポイントごとに変えられる(CS_SAPointのpullSpeed・releaseDistanceRate。グラップルなど)
 *   pullSpeedが0以下なら_flySpeedを使う。着いたら、releaseDistanceRateが0のSAポイント(信号機・ハイドポイント)は位置を合わせて速度を0にする(勢いは残さない)
 *   遠い的でも着けるよう、飛べる最長時間は max(_maxFlyTime, 距離 / 速さ × 1.5) とする
 *   releaseDistanceRateが0より大きいSAポイント(グラップル)は、飛び始めの距離のその割合まで引っ張られるか、着く位置に着いたら、早い方で自動で放す(ReleaseWithVelocity)
 *   放した時は、通常の状態に戻って自由落下する(位置の合わせ込みはしない)。放した瞬間の速度(向き・高さ込み)をそのまま残す
 *   終わる時はCS_Player.EndSAMotion(終わった時の速度)で、SA専用の速度をクリアすると同時にその速度を渡す(垂直はRigidbody、水平は空中の勢い)。着いた時(信号機・ハイドポイント)は速度0
 *   空中の勢いは、入力がある間は入力の速度へ近づき(_airControlAccel)、着地で0になる
 *   放した後は移動入力で曲げられる(飛んでいる間はSA専用の速度で動かす)
 *   放した直後は、_releaseGhostTime秒だけ当たり判定を切ったままにして足場の角に引っかからないようにする(BeginGhost)
 *   壁抜け・床抜けしないよう、上向きに進んでいる時だけ・着く位置を通り過ぎない範囲だけ行い、下向きになったらすぐ戻す
 *   飛び始めの瞬間に重力と当たり判定を切り、すぐ動かす(次のフレームを待たない)
 * ・飛び始め・飛び終わり(着いた時・途中でやめた時)をonFlyBegin・onFlyEndで知らせる(ロープの表示などが購読する。本人の手元でだけ起きる)
 * ・1つのハイドポイントに何人でも入れる(以前の予約は無くした)
 * ・SAで飛ぶのは人のプレイヤーだけ(NPCは使わない)。隠れている判定はNPCにも同じように効く
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(Rigidbody))]
public class CS_PlayerHide : NetworkBehaviour
{
    [Header("飛べるハイドポイントを探す")]
    [SerializeField] private float _pointHeight = 1f;                   // ハイドポイントの立つ位置から、画面に映っているか・カメラの正面に近いかを調べる点の高さ(m)
    [SerializeField][Range(0f, 1f)] private float _requiredVisibleRatio = 0.3f;   // 隠れられる箱がこの割合以上カメラから見えていれば飛べる
    [SerializeField][Range(2, 5)] private int _sightSampleCount = 3;              // 見えている割合を調べる点の、箱の1辺あたりの数(3なら27点)
    [SerializeField][Range(0f, 0.5f)] private float _sightSampleInset = 0.1f;     // 点を箱の端からどれだけ内側に置くか(箱の大きさに対する割合)

    [Header("飛ぶ")]
    [SerializeField][Min(0.1f)] private float _flySpeed = 25f;          // 飛ぶ速さ(m/秒)
    [SerializeField][Min(0f)] private float _arriveDistance = 0.3f;     // 着く位置との距離がこれ以内になったら着いたとみなす(m。高さ込み)
    [SerializeField][Min(0f)] private float _maxFlyTime = 1f;           // これ以上かかっても着かなければやめる(秒)
    [SerializeField][Min(0f)] private float _releaseGhostTime = 0.1f;   // 放した直後、この時間だけ当たり判定を切ったままにする(秒。足場の角に引っかからないように)

    [Header("見つかる距離")]
    [SerializeField][Min(0f)] private float _revealDistance = 2f;       // 隠れていても、警察・悪人とこの距離以内なら見つかる(m。高さ込み)

    // 隠れているか(書き込みはサーバー、またはオフラインのみ。全員で分かる)
    private readonly NetworkVariable<bool> _isHidden = new NetworkVariable<bool>();

    private const int _sightHitBufferSize = 16;   // 視線の判定で一度に調べる当たりの上限
    private readonly RaycastHit[] _sightHits = new RaycastHit[_sightHitBufferSize];

    private CS_Player _player;
    private Rigidbody _rigidbody;
    private Collider _collider;
    private CS_PlayerHealth _health;
    private CS_PlayerAmbush _ambush;    // 奇襲。奇襲の飛び込み中はSAしない(付いていなければ見ない)

    // 本人の手元の状態
    private bool _isFlying;
    private CS_SAPoint _point;            // 飛んでいる先のSAポイント
    private float _yawBeforeFly;            // 飛ぶ前に向いていた向き(度。着いたらこの向きに戻す)
    private CS_SAPoint _candidate;        // 今ダッシュボタンを押したら飛ぶSAポイント
    private float _elapsed;
    private float _flySpeedNow;             // 今飛んでいる速さ(m/秒。SAポイントごとに違う)
    private float _flyTimeLimit;            // 今の飛行で、着かなければやめる時間(秒)
    private Vector3 _flyVelocity;           // 最後に決めた飛行の速度(向き・高さ込み。放す時に残す。着く位置の手前で頭打ちにする前の、本来の速さ)
    private float _flyStartDistance;        // 飛び始めた時の、着く位置までの距離(m。放す位置を割合で決めるのに使う)
    private float _ghostRemaining;          // 放した直後に、当たり判定を切ったままにする残り時間(秒)
    private bool _isFloating;               // 重力を切っているか(元に戻すため)

    public event Action<CS_SAPoint> onFlyBegin;   // 飛び始めた時(飛ぶ先のSAポイント)
    public event Action<CS_SAPoint> onFlyEnd;     // 飛び終わった時(着いた時・途中でやめた時。飛んでいた先のSAポイント)

    public bool isHiding => _isFlying;                      // SAでハイドポイントへ飛んでいる最中か(本人の手元)
    public bool isHidden => _isHidden.Value;                // 隠れている状態か(全員で分かる。サーバーが決める)
    public bool isInHideBox => CS_HidePoint.IsInsideAny(bodyCenter);   // 体の中心が隠れられる箱の中にあるか(その場で調べる)
    public CS_SAPoint currentCandidate => _candidate;     // 今ダッシュボタンを押したら飛ぶSAポイント(本人の手元。アイコン表示用)

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
        EndGhost();
        SetHidden(false);
    }

    private void Update()
    {
        UpdateHidden();
        UpdateGhost();

        if (!CanUse())
        {
            _candidate = null;
            if (_isFlying) EndFly();
            return;
        }

        if (_isFlying)
        {
            _candidate = null;
            return;
        }

        _candidate = FindCandidate();
    }

    // 飛んでいる間は、物理の1フレームごとに、着く位置へ向かう一定の速度をCS_Playerへ渡す(Fly/Lungeの残り時間は使わない)
    private void FixedUpdate()
    {
        if (_isFlying) StepFly();
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
        _yawBeforeFly = transform.eulerAngles.y;
        _flySpeedNow = _point.pullSpeed > 0f ? _point.pullSpeed : _flySpeed;
        Vector3 toPoint = GetArrivePosition(_point) - _rigidbody.position;
        float distance = toPoint.magnitude;
        _flyTimeLimit = Mathf.Max(_maxFlyTime, distance / _flySpeedNow * 1.5f);
        _flyStartDistance = distance;
        _flyVelocity = distance > 0.0001f ? toPoint / distance * _flySpeedNow : Vector3.zero;
        _player.SetAimFacing(Quaternion.LookRotation(HorizontalDirection(_point.standPosition)).eulerAngles.y);

        // SA専用の速度で動かす。動き出しをなめらかにするため、飛び始めの瞬間に重力と当たり判定を切り、すぐ速度を渡す(次のフレームを待たない)
        _player.BeginSAMotion();
        SetFloating(true);
        _rigidbody.detectCollisions = false;
        _player.SetSAVelocity(_flyVelocity);

        onFlyBegin?.Invoke(_point);
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
    private CS_SAPoint FindCandidate()
    {
        Camera viewCamera = _player.playerCamera;
        if (viewCamera == null) return null;

        Vector3 cameraForward = viewCamera.transform.forward;
        Vector3 feet = GetFeetPosition();
        Vector3 sightPosition = viewCamera.transform.position;
        CS_SAPoint best = null;
        float bestAngle = float.MaxValue;
        foreach (CS_SAPoint point in CS_SAPoint.activePoints)
        {
            // SAできる範囲の箱はハイドポイントごと。足元が箱の中にある時だけ飛べる
            if (!point.IsInSAReachBox(feet)) continue;

            // 今いるハイドポイント(体の中心が隠れられる箱の中にある)へは飛ばない
            if (point.Contains(bodyCenter)) continue;

            Vector3 target = point.standPosition + Vector3.up * _pointHeight;
            if (!IsOnScreen(viewCamera, target)) continue;

            float angle = Vector3.Angle(cameraForward, target - viewCamera.transform.position);
            if (angle >= bestAngle) continue;

            // 壁などに隠れて、隠れられる箱が十分に見えていないハイドポイントには飛べない
            if (!IsVisibleEnough(sightPosition, point, target)) continue;

            best = point;
            bestAngle = angle;
        }
        return best;
    }

    // fromから見て、pointの隠れられる箱が_requiredVisibleRatio以上見えているか(箱が空ならfallbackの1点で調べる)
    private bool IsVisibleEnough(Vector3 from, CS_SAPoint point, Vector3 fallback)
    {
        if (!point.hasHideBox) return IsSightClear(from, fallback);

        int perSide = _sightSampleCount;
        int total = perSide * perSide * perSide;
        int required = Mathf.Max(1, Mathf.CeilToInt(total * _requiredVisibleRatio));
        float extent = 0.5f - _sightSampleInset;   // 箱の中心から点を置く端までの距離(箱の大きさに対する割合)

        int visible = 0;
        int checkedCount = 0;
        for (int x = 0; x < perSide; x++)
        {
            for (int y = 0; y < perSide; y++)
            {
                for (int z = 0; z < perSide; z++)
                {
                    Vector3 local = new Vector3(SampleOffset(x, perSide, extent), SampleOffset(y, perSide, extent), SampleOffset(z, perSide, extent));
                    if (IsSightClear(from, point.GetHideBoxPoint(local))) visible++;
                    checkedCount++;

                    // 足りた・残りを全部足しても足りない時は、そこで打ち切る
                    if (visible >= required) return true;
                    if (visible + (total - checkedCount) < required) return false;
                }
            }
        }
        return false;
    }

    // 箱の1辺にcount個の点を並べた時の、index番目の点の位置(箱の中心から-extent〜+extent)
    private static float SampleOffset(int index, int count, float extent)
    {
        return Mathf.Lerp(-extent, extent, index / (float)(count - 1));
    }

    // fromからtoまでの間に、壁など(CS_PoliceLayers.obstacleLayers)が無いか
    // プレイヤー自身・キャラクター(IDamageableを持つもの)は遮るものとして扱わない
    private bool IsSightClear(Vector3 from, Vector3 to)
    {
        Vector3 toTarget = to - from;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f) return true;

        int count = Physics.RaycastNonAlloc(from, toTarget / distance, _sightHits, distance, CS_PoliceLayers.obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = _sightHits[i].collider.transform;
            if (hit.IsChildOf(transform)) continue;
            if (hit.GetComponentInParent<IDamageable>() != null) continue;

            return false;
        }
        return true;
    }

    // 着く位置(体の底が立つ位置に来る所)へ直進し、着いたら普通の状態に戻る(物理の1フレームごと)
    private void StepFly()
    {
        _elapsed += Time.fixedDeltaTime;

        Vector3 toPoint = GetArrivePosition(_point) - _rigidbody.position;
        float distance = toPoint.magnitude;
        if (distance <= _arriveDistance)
        {
            // 放す設定のあるSAポイント(グラップル)は、着いた時も5割で放した時と同じように、速度を残して放す(位置は合わせない)
            if (_point.releaseDistanceRate > 0f) ReleaseWithVelocity(distance);
            else Arrive();
            return;
        }

        // 飛び始めの距離のreleaseDistanceRate(割合)まで引っ張られたら、その時の速度を残したまま放す(グラップル。1以上なら放さず着くまで)
        float releaseRate = _point.releaseDistanceRate;
        if (releaseRate > 0f && distance <= _flyStartDistance * (1f - releaseRate))
        {
            ReleaseWithVelocity(distance);
            return;
        }

        // 何かに引っかかって着かない時はやめる
        if (_elapsed >= _flyTimeLimit)
        {
            EndFly();
            return;
        }

        // 毎物理フレーム向きと速さを決め直す(少しずれても、着く位置へ向かう)。着く位置を通り越さないよう、速さは残りの距離で頭打ちにする
        float speed = Mathf.Min(_flySpeedNow, distance / Time.fixedDeltaTime);
        _flyVelocity = toPoint / distance * _flySpeedNow;   // 放す時に残す速度は、頭打ちにしない本来の速さ
        _player.SetSAVelocity(toPoint / distance * speed);
    }

    // 飛び始めの距離のreleaseDistanceRateまで引っ張られるか、着いた: ロープを放し、通常の状態に戻って重力で落ちる(位置は合わせない)
    // 放した瞬間の速度(向き・高さ込み)を、そのまま残す: CS_Player.EndSAMotionへ渡す(垂直はRigidbody、水平は空中の勢いになる)。放した後は移動入力で曲げられる
    // remainingDistanceは、放した時の、着く位置までの残りの距離(m)
    private void ReleaseWithVelocity(float remainingDistance)
    {
        Vector3 velocity = _flyVelocity;

        EndFly(velocity);   // 重力・当たり判定を戻し、速度を通常の状態に渡す(CS_Player.EndSAMotion)。ロープを消すonFlyEndもここで呼ばれる

        BeginGhost(velocity, remainingDistance);
    }

    // 放した直後だけ、当たり判定を切ったままにする(足場の角に引っかからないように)
    // 壁抜け・床抜けしないよう、次の時だけ行う: 上向きに進んでいる(床の下へ落ちない)・飛んでいた線の上(着く位置までの残りの距離を超えない)
    private void BeginGhost(Vector3 velocity, float remainingDistance)
    {
        if (_releaseGhostTime <= 0f || velocity.y < 0f || velocity.sqrMagnitude < 0.0001f) return;

        float untilTarget = remainingDistance / velocity.magnitude;   // 着く位置を通り過ぎるまでの時間
        _ghostRemaining = Mathf.Min(_releaseGhostTime, untilTarget);
        if (_ghostRemaining > 0f) _rigidbody.detectCollisions = false;
    }

    // 時間がきたか、下向きになったら、当たり判定を戻す
    private void UpdateGhost()
    {
        if (_ghostRemaining <= 0f) return;

        _ghostRemaining -= Time.deltaTime;
        if (_ghostRemaining <= 0f || _rigidbody.linearVelocity.y < 0f || _isFlying) EndGhost();
    }

    private void EndGhost()
    {
        _ghostRemaining = 0f;
        if (!_isFlying && _rigidbody != null) _rigidbody.detectCollisions = true;
    }

    // 着いた: 位置(高さ込み)を合わせ、速度を0にして、飛ぶ前に向いていた向きに戻して、普通の状態に戻る(勢いは残さない)
    private void Arrive()
    {
        _player.SnapPosition(GetArrivePosition(_point));
        EndFly();

        // 飛ぶ前の向きに戻す(ハイドポイントの向きには合わせない)。移動キーを押せば、いつも通り進む方向へ向く
        _player.SetAimFacing(_yawBeforeFly);
        _player.ClearAimFacing();
    }

    private void EndFly()
    {
        EndFly(Vector3.zero);
    }

    // 飛ぶのを終える。exitVelocityは、終わった時にCS_Playerへ渡す速度(0なら速度0)
    private void EndFly(Vector3 exitVelocity)
    {
        CS_SAPoint endedPoint = _point;
        bool wasFlying = _isFlying;

        _isFlying = false;
        _point = null;
        _elapsed = 0f;
        _player.ClearAimFacing();
        SetFloating(false);

        if (!wasFlying) return;

        _player.EndSAMotion(exitVelocity);   // SA専用の速度をクリアし、終わった時の速度を渡す(水平は空中の勢いになる)
        onFlyEnd?.Invoke(endedPoint);
    }

    // 体の底(足元)が立つ位置に来る時の、プレイヤーの位置(プレイヤーの位置は体の中心のため)
    private Vector3 GetArrivePosition(CS_SAPoint point)
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
