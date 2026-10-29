using System;
using Unity.Netcode;
using UnityEngine;

/*
 * 奇襲(気づかれていない悪人の背後・上から攻撃すると、一撃で倒せる)を行うクラス
 * 通常攻撃(5段コンボ、CS_PlayerAttack)とは別の攻撃。待機中に攻撃ボタンを押した時、CS_PlayerAttackがTryStartAmbush()を呼ぶ
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「奇襲」(Issue #275)。次の3つがそろった時に攻撃ボタンを押すと、奇襲になる
 *   ① 悪人がこちらに気づいていない: 臨戦態勢(CS_VillainCombat.isEngaged)でない、または自分以外を狙っている
 *   ② 一定距離まで近づいた: 悪人との水平距離が_range(既定3m)以内
 *   ③ 背後か上を取っている
 *      背後: 悪人の正面から見て、自分のいる方向が_backAngle度(既定120度)以上うしろ(真後ろを中心に左右60度ずつ)
 *      上  : 自分が悪人より_aboveHeight(既定1.5m)以上高い位置にいる
 *      ハイドポイントに張り付いて隠れている時(CS_PlayerHide)は、③を見ない(正面からでも奇襲できる。ペルソナ5のカバーからの奇襲)
 * ・奇襲の流れ(攻撃している本人の手元で進める)
 *   1. 待機中に攻撃ボタン → 奇襲できる悪人(FindAmbushTarget)がいれば奇襲を始める(いなければCS_PlayerAttackが1段目を出す)
 *   2. 飛び込み: 悪人の今の位置を毎フレーム追いながら、_dashSpeedで_strikeDistanceの距離まで近づく(CS_Player.Lunge)
 *      _maxDashTime秒以内に着けなかった(何かに引っかかった)時は、その場から奇襲を当てる(中止しない。Issue #370)
 *      動けなくなった(死亡・スタン)・悪人が倒れた時は中止する
 *   3. 攻撃: 着いてから_strikeDelay秒後に、その悪人1体だけへ奇襲を当てる(範囲の当たり判定は使わないので空振りしない)
 *   4. _strikeDuration秒で奇襲が終わり、動けるようになる。この後に攻撃ボタンを押すと、通常攻撃の1段目から始まる
 * ・奇襲になった時は、その悪人の残りHP以上のダメージを与えて一撃で倒す(仕様「戦闘をすぐに終わらせる」)
 * ・巻き込み(Issue #337。データ表「奇襲攻撃時の巻き込み範囲」5m): 奇襲で倒した悪人の位置から半径_splashRadius以内にいる悪人も、
 *   残りHPに関係なく一撃で倒す(仕様「敵の体力を問わず一定範囲にいる敵を一撃で倒す」)
 *   巻き込むのは悪人だけ(他のプレイヤー・警察・防犯装置は巻き込まない)。壁越しでも巻き込む
 *   レイドボスは_splashIncludesRaidBossがオフ(既定)なら巻き込まない(一撃で倒せてしまうため。狙った1体がボスの場合は今まで通り倒す)
 *   巻き込んだ悪人もスコアを通して倒すので、撃破スコア・暗躍ボーナスの連続撃破数・必殺ゲージに1体ずつ数えられる
 *   onAmbushは狙った悪人の分だけ発生する(巻き込んだ悪人ではonAmbushSplashが発生する。サーバー、またはオフラインのみ)
 * ・奇襲を押して飛び込み始めたら、必ず奇襲を成功させる(Issue #370「押せて移動している時点で成功にしたい」)
 *   以前は、押してからサーバーに届くまで・飛び込んでいる間に悪人に気づかれる、引っかかって着くのが遅れる、
 *   サーバー上の位置がずれている、などで中止・空振りになることがあった。今は次のようにしている
 *   ・押した瞬間に奇襲できる悪人がいなくても、直前(_pressGraceTime秒以内)にロックオン表示していた悪人がいれば奇襲する
 *     (表示の更新は一定間隔なので、表示が出ているのに押した瞬間だけ条件が外れて通常攻撃になる、ということをなくす)
 *   ・サーバーでは「気づかれていない」「背後か上」をもう一度見ない(押した本人の手元で判定済み)。悪人が倒れていないか・遠すぎないか・
 *     間に壁が無いかだけを見る
 *   ・_maxDashTime秒以内に着けなくても中止せず、その場から当てる。当てる時の距離の確かめも、奇襲できる距離(_range)まで許す
 * ・奇襲を始めたら、サーバー(またはオフライン)でその悪人を予約して、その場に止める(CS_VillainCombat.Freeze)。
 *   予約できなければ(悪人が倒れていた・遠すぎる・壁越し)、手元へ中止を返す
 *   止める時間・予約の期限は、飛び込みの最大時間 + 攻撃の時間 + _freezeMargin。奇襲が中止された時はすぐに解く
 *   当てる時は「予約した悪人か」と「離れすぎていないか(_range + _strikeTolerance以内)」だけを見て倒す
 *   (飛び込んでいる間に向きが変わったり、気づかれたりしても外れることがない)
 *   飛び込んだ直後はサーバー上の位置が追いついていないので、送られてきた位置のずれは_range分多めに許す
 * ・奇襲が決まった時はonAmbush(倒した悪人)で知らせる(サーバー、またはオフラインのみ)
 *   見た目用に、onAmbushStarted(飛び込み開始)・onAmbushHitTiming(当たる瞬間)を本人の手元で出す
 *   飛び込み〜当たる瞬間の間はisLungingがtrueになる。風切りの画面効果(CS_PlayerDashScreenEffect)・画角(CS_PlayerCameraFov)が使う
 * ・壁越しの悪人は奇襲できない: プレイヤーの体の中心から悪人の体の中心までを、半径_clearRadiusの球でなぞり、_obstacleLayersのもの(壁など)があれば奇襲にならない
 *   体の幅で調べるので、線は通っても体が壁の角に引っかかる、ということがない
 *   キャラクター(IDamageableを持つもの)は遮るものとして扱わない。飛び込みもこの線の上を進むので、壁をすり抜けて奇襲することもない
 * ・飛び込んでいる間は、_dashPassThroughLayers(悪人・プレイヤー)とぶつからない(Rigidbody.excludeLayers)
 *   間にいる他の悪人・仲間に引っかかって止まり、奇襲が中止されることがないようにするため。着いたら(攻撃に移ったら)元に戻す
 * ・今奇襲できる悪人はFindAmbushTarget()で探せる(全クライアントで呼べる)。ロックオンUI(CS_PlayerLockOnUI)が使う
 * ・_rangeを広げると、遠くから奇襲できるようになる(既定はPlayer.prefabで18m)
 *   悪人に気づかれているかは、CS_VillainCombat.IsTargeting(同期された標的)で判定するので、クライアントでも分かる
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerAmbush : NetworkBehaviour
{
    [Header("奇襲の条件")]
    [SerializeField][Min(0f)] private float _range = 3f;                   // 悪人との水平距離がこれ以内(m)
    [SerializeField][Range(0f, 180f)] private float _backAngle = 120f;     // 悪人の正面から見て、これ以上うしろなら背後(度)
    [SerializeField][Min(0f)] private float _aboveHeight = 1.5f;           // 悪人よりこれ以上高ければ上を取っている(m)

    [Header("奇襲できる悪人を探す(ロックオン表示用)")]
    [SerializeField] private LayerMask _villainLayers = ~0;               // 悪人を探すレイヤー
    [SerializeField] private LayerMask _obstacleLayers = ~0;              // 悪人との間にあると奇襲できなくなるもの(壁など)のレイヤー
    [SerializeField][Min(0f)] private float _clearRadius = 0.4f;          // 壁が無いかを調べる太さ(m。体の半径より少し小さく)

    [Header("奇襲攻撃")]
    [SerializeField][Min(0.1f)] private float _dashSpeed = 30f;           // 飛び込む速さ(m/秒)
    [SerializeField][Min(0f)] private float _maxDashTime = 1f;            // これ以上かかっても着かなければ奇襲をやめる(秒)
    [SerializeField][Min(0f)] private float _strikeDistance = 1.2f;       // 悪人との水平距離がこれ以内になったら着いたとみなす(m)
    [SerializeField][Min(0f)] private float _strikeDelay = 0.1f;          // 着いてから奇襲が当たるまでの時間(秒)
    [SerializeField][Min(0f)] private float _strikeDuration = 0.4f;       // 着いてから奇襲が終わる(動けるようになる)までの時間(秒)
    [SerializeField][Min(0f)] private float _freezeMargin = 0.5f;         // 悪人を止める時間・予約の期限に足す余裕(秒。通信の遅れの分)
    [SerializeField][Min(0f)] private float _strikeTolerance = 2f;        // 当てる時、着いたとみなす距離からさらにこれだけ離れていても許す(m)
    [SerializeField] private LayerMask _dashPassThroughLayers = 1 << 31;  // 飛び込み中にぶつからないレイヤー(悪人・プレイヤーのいるレイヤー)
    [SerializeField][Min(0f)] private float _pressGraceTime = 0.3f;       // 押した瞬間に奇襲できなくても、この秒数以内にロックオン表示していた悪人なら奇襲する

    [Header("巻き込み")]
    [SerializeField][Min(0f)] private float _splashRadius = 5f;           // 奇襲で倒した悪人から、この半径以内の悪人も一撃で倒す(m。0で巻き込まない)
    [SerializeField] private bool _splashIncludesRaidBoss = false;        // レイドボスも巻き込むか

    private enum Phase
    {
        None,     // 奇襲していない
        Dash,     // 悪人へ飛び込んでいる
        Strike,   // 着いて、攻撃している
    }

    private const int _searchBufferSize = 32;   // 一度に調べるコライダーの上限
    private readonly Collider[] _searchBuffer = new Collider[_searchBufferSize];
    private readonly RaycastHit[] _obstacleBuffer = new RaycastHit[_searchBufferSize];

    private CS_Player _player;
    private CS_PlayerScoring _scoring;       // 付いていればスコアを通してダメージを与える
    private CS_PlayerHide _hide;             // ハイド。隠れている時は「背後か上」を見ない(付いていなければ見る)
    private Rigidbody _rigidbody;
    private LayerMask _defaultExcludeLayers;   // 飛び込む前のRigidbody.excludeLayers(飛び込み後に戻す)
    private bool _isPassingThrough;            // 飛び込み中で、悪人・プレイヤーとぶつからないようにしているか
    private Phase _phase = Phase.None;
    private CS_VillainHealth _target;        // 奇襲している悪人
    private float _elapsed;                  // 今の段階(飛び込み・攻撃)が始まってからの時間
    private bool _hasStruck;                 // 奇襲を当てる処理を行ったか
    private CS_VillainHealth _reservedTarget;   // 奇襲すると決めた悪人(サーバー、またはオフラインのみ)
    private float _reservedUntil;               // 予約の期限(Time.time。サーバー、またはオフラインのみ)
    private CS_VillainHealth _lastFoundTarget;  // 最後にFindAmbushTargetで見つかった悪人(本人の手元。押した瞬間の猶予に使う)
    private float _lastFoundTime;               // それが見つかった時刻(Time.time)

    public float range => _range;                   // 奇襲できる距離(m)
    public bool isAmbushing => _phase != Phase.None;   // 奇襲中か(CS_PlayerAttackが、通常攻撃・必殺技を止めるのに使う)
    public CS_VillainHealth currentTarget => isAmbushing ? _target : null;   // 奇襲している悪人(本人の手元のみ。見た目のIKが使う)
    public bool isLunging => _phase == Phase.Dash || (_phase == Phase.Strike && !_hasStruck);   // 飛び込み〜当たる瞬間か(本人の手元。見た目用)

    public event Action<CS_VillainHealth> onAmbush;     // 奇襲が決まった時(サーバー、またはオフラインのみ)
    public event Action<CS_VillainHealth> onAmbushSplash;      // 奇襲に巻き込んで悪人を倒した時(サーバー、またはオフラインのみ)
    public event Action<CS_VillainHealth> onAmbushStarted;     // 飛び込みを始めた時(本人の手元のみ)。見た目用
    public event Action<CS_VillainHealth> onAmbushHitTiming;   // 奇襲が当たる瞬間(本人の手元のみ)。見た目・カメラの揺れ用

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _scoring = GetComponent<CS_PlayerScoring>();
        _hide = GetComponent<CS_PlayerHide>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (_phase == Phase.None) return;

        // 動けなくなった(死亡・スタン)・悪人が倒れた・消えた時はやめる
        if (!_player.canAct || !IsTargetAlive())
        {
            EndAmbush();
            return;
        }

        _elapsed += Time.deltaTime;
        if (_phase == Phase.Dash) UpdateDash();
        else UpdateStrike();
    }

    // ========================================
    // 奇襲攻撃(攻撃している本人の手元で進める)
    // ========================================

    // 奇襲できる悪人がいれば奇襲を始める(始めたらtrue)。CS_PlayerAttackが、待機中に攻撃ボタンが押された時に呼ぶ
    public bool TryStartAmbush()
    {
        if (isAmbushing) return true;

        CS_VillainHealth villain = FindAmbushTarget() ?? GetRecentTarget();
        if (villain == null) return false;

        _target = villain;
        _phase = Phase.Dash;
        _elapsed = 0f;
        _hasStruck = false;
        FaceTarget();
        SetPassThrough(true);

        // オフラインではその場で奇襲になるかが決まり、ならなければここで終わる(その時は通常攻撃の1段目を出す)
        RequestStart(villain);
        if (!isAmbushing) return false;

        onAmbushStarted?.Invoke(villain);
        return true;
    }

    // 直前(_pressGraceTime秒以内)にロックオン表示していた悪人。倒れておらず、遠すぎず、間に壁が無ければ返す
    private CS_VillainHealth GetRecentTarget()
    {
        if (_lastFoundTarget == null || Time.time - _lastFoundTime > _pressGraceTime) return null;
        if (_lastFoundTarget.isDefeated || _lastFoundTarget.currentHp <= 0f) return null;
        if (HorizontalDistance(_lastFoundTarget.transform.position) > _range) return null;
        if (!IsClearLine(transform.position, _lastFoundTarget.transform)) return null;

        return _lastFoundTarget;
    }

    // 飛び込みを始めてから奇襲が当たるまでの見込みの時間(秒)。見た目(CS_PlayerVisual)が、当たる瞬間にモーションの打撃を合わせるのに使う
    // 飛び込む距離 ÷ 飛び込む速さ + 着いてから当たるまでの時間(悪人は止めているので、始めた時の距離で見積もれる)
    public float EstimateTimeToHit(CS_VillainHealth villain)
    {
        if (villain == null) return _strikeDelay;

        float moveDistance = Mathf.Max(0f, HorizontalDistance(villain.transform.position) - _strikeDistance);
        return Mathf.Min(moveDistance / _dashSpeed, _maxDashTime) + _strikeDelay;
    }

    // 悪人の今の位置を追いながら、_strikeDistanceまで飛び込む
    private void UpdateDash()
    {
        FaceTarget();

        float distance = HorizontalDistance(_target.transform.position);
        if (distance <= _strikeDistance)
        {
            _phase = Phase.Strike;
            _elapsed = 0f;
            SetPassThrough(false);
            return;
        }

        // 何かに引っかかって着かない時も、やめずにその場から当てる(押して飛び込み始めたら必ず奇襲にする。Issue #370)
        if (_elapsed >= _maxDashTime)
        {
            _phase = Phase.Strike;
            _elapsed = 0f;
            SetPassThrough(false);
            return;
        }

        // 毎フレーム向きと距離を決め直す(悪人が動いても追いかける)
        float moveDistance = distance - _strikeDistance;
        _player.Lunge(HorizontalDirection(_target.transform.position), moveDistance, moveDistance / _dashSpeed);
    }

    // 着いてから_strikeDelay秒後に当て、_strikeDuration秒で終わる
    private void UpdateStrike()
    {
        FaceTarget();

        if (!_hasStruck && _elapsed >= _strikeDelay)
        {
            _hasStruck = true;
            RequestStrike(_target);
            onAmbushHitTiming?.Invoke(_target);
        }

        if (_elapsed >= Mathf.Max(_strikeDelay, _strikeDuration)) EndAmbush();
    }

    private void EndAmbush()
    {
        // 当てる前にやめた時は、止めていた悪人を動かす
        if (_phase != Phase.None && !_hasStruck) RequestRelease();

        _phase = Phase.None;
        _target = null;
        _elapsed = 0f;
        _hasStruck = false;
        _player.ClearAimFacing();
        SetPassThrough(false);
    }

    // 飛び込み中だけ、悪人・プレイヤーとぶつからないようにする(間にいるキャラクターに引っかかって止まらないように)
    private void SetPassThrough(bool passThrough)
    {
        if (_rigidbody == null || _isPassingThrough == passThrough) return;

        _isPassingThrough = passThrough;
        if (passThrough)
        {
            _defaultExcludeLayers = _rigidbody.excludeLayers;
            _rigidbody.excludeLayers = _defaultExcludeLayers | _dashPassThroughLayers;
        }
        else
        {
            _rigidbody.excludeLayers = _defaultExcludeLayers;
        }
    }

    // 奇襲を始めたことをサーバーへ伝える(サーバーで奇襲になるかを決め、悪人を止める)
    private void RequestStart(CS_VillainHealth villain)
    {
        Vector3 position = transform.position;

        // オフライン(テストシーン)では、その場で決める
        if (!IsSpawned)
        {
            if (!StartOnServer(villain, position)) EndAmbush();
            return;
        }

        StartRpc(new NetworkObjectReference(villain.NetworkObject), position);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void StartRpc(NetworkObjectReference villainReference, Vector3 position)
    {
        CS_VillainHealth villain = GetVillain(villainReference);
        if (villain != null && StartOnServer(villain, position)) return;

        CancelRpc();   // 奇襲にならなかったので、手元の飛び込みをやめさせる
    }

    // サーバーから、奇襲にならなかったことを本人へ伝える
    [Rpc(SendTo.Owner)]
    private void CancelRpc()
    {
        if (isAmbushing) EndAmbush();
    }

    // 悪人を予約してその場に止める(サーバー、またはオフラインで実行される)
    // 「気づかれていない」「背後か上」は押した本人の手元で判定済みなので、ここでは見ない(サーバーに届くまでの間に
    // 気づかれて中止になるのを防ぐ。Issue #370)。悪人が倒れていないか・遠すぎないか・間に壁が無いかだけを見る
    private bool StartOnServer(CS_VillainHealth villain, Vector3 position)
    {
        Vector3 forward = transform.forward;
        CS_AttackHitDetector.ResolvePose(transform, ref position, ref forward);

        if (villain.isDefeated || villain.currentHp <= 0f) return false;
        if (HorizontalDistance(villain.transform.position, position) > _range + _strikeTolerance) return false;
        if (!IsClearLine(position, villain.transform)) return false;
        CS_VillainHealth ambushed = villain;

        ReleaseOnServer();   // 前の予約が残っていれば解く

        float duration = _maxDashTime + Mathf.Max(_strikeDelay, _strikeDuration) + _freezeMargin;
        _reservedTarget = ambushed;
        _reservedUntil = Time.time + duration;
        if (ambushed.TryGetComponent(out CS_VillainCombat combat)) combat.Freeze(duration);
        return true;
    }

    // 奇襲をやめたことをサーバーへ伝える(止めていた悪人を動かす)
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

    // 予約を消し、止めていた悪人を動かす(サーバー、またはオフラインで実行される)
    private void ReleaseOnServer()
    {
        if (_reservedTarget != null && _reservedTarget.TryGetComponent(out CS_VillainCombat combat)) combat.Unfreeze();
        _reservedTarget = null;
    }

    private static CS_VillainHealth GetVillain(NetworkObjectReference villainReference)
    {
        if (!villainReference.TryGet(out NetworkObject villainObject)) return null;
        return villainObject.TryGetComponent(out CS_VillainHealth villain) ? villain : null;
    }

    // 奇襲を当てる処理を依頼する(ダメージはサーバーで確定させる)
    private void RequestStrike(CS_VillainHealth villain)
    {
        Vector3 position = transform.position;

        // オフライン(テストシーン)では、その場で判定する
        if (!IsSpawned)
        {
            ExecuteStrike(villain, position);
            return;
        }

        StrikeRpc(new NetworkObjectReference(villain.NetworkObject), position);
    }

    // Ownerからサーバーへ、奇襲の実行を依頼する
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void StrikeRpc(NetworkObjectReference villainReference, Vector3 position)
    {
        CS_VillainHealth villain = GetVillain(villainReference);
        if (villain != null) ExecuteStrike(villain, position);
    }

    // 予約した悪人に、残りHP分のダメージを与えて倒す(サーバー、またはオフラインで実行される)
    // 奇襲になるかは始めた時に決めているので、ここでは予約した悪人か・離れすぎていないかだけを見る
    // 引っかかって着かないまま当てることもあるので、離れすぎの判定は奇襲できる距離(_range)まで許す(Issue #370)
    private void ExecuteStrike(CS_VillainHealth villain, Vector3 position)
    {
        bool isReserved = villain == _reservedTarget && Time.time <= _reservedUntil;
        ReleaseOnServer();
        if (!isReserved || villain.isDefeated) return;

        // 送られてきた位置が、サーバー上の位置から大きく離れていないか確かめてから使う
        // 飛び込んだ直後はサーバー上の位置が追いついていないので、奇襲できる距離の分だけ多めに許す
        Vector3 forward = transform.forward;
        CS_AttackHitDetector.ResolvePose(transform, ref position, ref forward, _range);
        if (HorizontalDistance(villain.transform.position, position) > _range + _strikeTolerance) return;

        Vector3 center = villain.transform.position;
        DealDamage(villain, villain.currentHp);
        CS_AttackHitDetector.TryKnockback(villain, position);
        NotifyAmbush(villain);

        DefeatVillainsAround(center, villain);
    }

    // centerから半径_splashRadius以内にいる悪人(exclude以外)を、残りHP分のダメージで倒す(サーバー、またはオフラインで実行される)
    private void DefeatVillainsAround(Vector3 center, CS_VillainHealth exclude)
    {
        if (_splashRadius <= 0f) return;

        int count = Physics.OverlapSphereNonAlloc(center, _splashRadius, _searchBuffer, _villainLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            CS_VillainHealth villain = _searchBuffer[i].GetComponentInParent<CS_VillainHealth>();
            if (villain == null || villain == exclude) continue;
            if (villain.isDefeated || villain.currentHp <= 0f) continue;   // 倒れている・同じ悪人の別のコライダー
            if (!_splashIncludesRaidBoss && villain.TryGetComponent(out CS_VillainRaidBoss _)) continue;

            DealDamage(villain, villain.currentHp);
            CS_AttackHitDetector.TryKnockback(villain, center);
            onAmbushSplash?.Invoke(villain);
        }
    }

    // ダメージを与える。スコア(CS_PlayerScoring)があればそれを通し、誰が倒したかをスコアに反映する
    private void DealDamage(IDamageable target, float damage)
    {
        if (_scoring != null)
        {
            _scoring.DealDamage(target, damage);
            return;
        }

        target.TakeDamage(damage, gameObject);
    }

    private bool IsTargetAlive()
    {
        return _target != null && !_target.isDefeated && _target.currentHp > 0f;
    }

    private void FaceTarget()
    {
        Vector3 direction = HorizontalDirection(_target.transform.position);
        if (direction == Vector3.zero) return;

        _player.SetAimFacing(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
    }

    private Vector3 HorizontalDirection(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private float HorizontalDistance(Vector3 targetPosition)
    {
        return HorizontalDistance(targetPosition, transform.position);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.y = 0f;
        return offset.magnitude;
    }

    // ========================================
    // 奇襲の条件
    // ========================================

    // この相手に、attackerPositionから攻撃した時に奇襲になるか(サーバー、またはオフラインで呼ぶ)
    public bool CanAmbush(IDamageable target, Vector3 attackerPosition, out CS_VillainHealth villain)
    {
        villain = null;

        CS_VillainHealth candidate = target as CS_VillainHealth;
        if (candidate == null || candidate.isDefeated) return false;
        if (!MeetsAmbushCondition(candidate, attackerPosition)) return false;

        villain = candidate;
        return true;
    }

    // 今の位置から奇襲できる悪人のうち、一番近い悪人を探す(いなければnull。全クライアントで呼べる)
    public CS_VillainHealth FindAmbushTarget()
    {
        Vector3 position = transform.position;
        int count = Physics.OverlapSphereNonAlloc(position, _range, _searchBuffer, _villainLayers, QueryTriggerInteraction.Ignore);

        CS_VillainHealth nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            CS_VillainHealth villain = _searchBuffer[i].GetComponentInParent<CS_VillainHealth>();
            if (villain == null || villain == nearest) continue;

            // isDefeatedはサーバーのみなので、クライアントでも分かるHPでも見る
            if (villain.isDefeated || villain.currentHp <= 0f) continue;
            if (!MeetsAmbushCondition(villain, position)) continue;

            float sqr = (villain.transform.position - position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            nearest = villain;
            nearestSqr = sqr;
        }

        if (nearest != null)
        {
            _lastFoundTarget = nearest;
            _lastFoundTime = Time.time;
        }
        return nearest;
    }

    // 奇襲が決まったことを知らせる(一撃で倒すダメージを与えた後に呼ぶ)
    private void NotifyAmbush(CS_VillainHealth villain)
    {
        onAmbush?.Invoke(villain);
    }

    // ②距離 ①気づかれていない ③背後か上 ④間に壁が無い、がそろっているか
    private bool MeetsAmbushCondition(CS_VillainHealth villain, Vector3 attackerPosition)
    {
        Transform villainTransform = villain.transform;
        Vector3 toAttacker = attackerPosition - villainTransform.position;
        Vector3 flat = new Vector3(toAttacker.x, 0f, toAttacker.z);

        if (flat.magnitude > _range) return false;
        if (IsNoticed(villain)) return false;
        if (!IsHidingAttacker() && !IsAbove(toAttacker.y) && !IsBehind(villainTransform.forward, flat)) return false;
        return IsClearLine(attackerPosition, villainTransform);
    }

    // fromから悪人の体の中心までの間に、壁など(_obstacleLayers)が無いか(体の幅の分、半径_clearRadiusの球でなぞって調べる)
    // 悪人自身・プレイヤー・他の悪人などのキャラクター(IDamageableを持つもの)は遮るものとして扱わない
    private bool IsClearLine(Vector3 from, Transform villain)
    {
        Vector3 toVillain = villain.position - from;
        float distance = toVillain.magnitude;
        if (distance <= 0.0001f) return true;

        int count = Physics.SphereCastNonAlloc(from, _clearRadius, toVillain / distance, _obstacleBuffer, distance, _obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = _obstacleBuffer[i].collider;
            if (hit.transform.IsChildOf(villain) || hit.transform.IsChildOf(transform)) continue;
            if (hit.GetComponentInParent<IDamageable>() != null) continue;

            return false;
        }
        return true;
    }

    // 悪人がこちらに気づいているか(臨戦態勢で、自分を狙っている)
    private bool IsNoticed(CS_VillainHealth villain)
    {
        CS_VillainCombat combat = villain.GetComponent<CS_VillainCombat>();
        if (combat == null) return false;

        return combat.IsTargeting(transform.root);
    }

    // ハイドポイントに張り付いて隠れているか(本人の手元は張り付いた時点で、サーバー・他の人は同期された状態で分かる)
    private bool IsHidingAttacker()
    {
        return _hide != null && (_hide.isAttached || _hide.isHidden);
    }

    private bool IsAbove(float heightDifference)
    {
        return heightDifference >= _aboveHeight;
    }

    // 悪人の正面から見て、自分が背後の範囲にいるか
    private bool IsBehind(Vector3 villainForward, Vector3 flatToAttacker)
    {
        villainForward.y = 0f;
        if (villainForward.sqrMagnitude < 0.0001f || flatToAttacker.sqrMagnitude < 0.0001f) return true;   // 重なっている時は背後扱い

        return Vector3.Angle(villainForward, flatToAttacker) >= _backAngle;
    }

    // 選択中に、奇襲できる範囲をシーンビューへ表示する(調整用)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, _range);
    }
}
