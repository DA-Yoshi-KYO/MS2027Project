using System;
using System.Collections.Generic;
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
 *      ハイドポイントの隠れられる箱の中にいる時(CS_PlayerHide)は、③を見ない(正面からでも奇襲できる。ペルソナ5のカバーからの奇襲)
 *      必殺技の「正面からの奇襲」の効果が続いている間(CS_PlayerCharacter。ギロ)は、③に加えて①も見ない(気づかれていても奇襲できる。仕様書「必殺技とは」)
 * ・奇襲の流れ(攻撃している本人の手元で進める)
 *   1. 待機中に攻撃ボタン → 奇襲できる悪人(FindAmbushTarget)がいれば奇襲を始める(いなければCS_PlayerAttackが1段目を出す)
 *   2. 飛び込み: 悪人の今の位置を毎フレーム追いながら、_dashSpeedで_strikeDistanceの距離まで近づく(CS_Player.Lunge)
 *      高さが違う時(上から攻撃した時など)は、悪人の足元の高さまで斜めに飛び込む(縦の移動。CS_Player.Fly)。
 *      足元の高さの差が_verticalTolerance以内になるか、下へ飛び込んでいる途中で着地したら(飛び込み始めの_landCheckDelay秒は見ない)、高さは合ったとみなして水平だけ詰める
 *      _maxDashTime秒以内に着けなかった(何かに引っかかった)時は、その場から奇襲を当てる(中止しない。Issue #370)
 *      動けなくなった(死亡・スタン)・悪人が倒れた時は中止する
 *   3. 攻撃: 着いてから_strikeDelay秒後に、その悪人1体だけへ奇襲を当てる(範囲の当たり判定は使わないので空振りしない)
 *   4. _strikeDuration秒で奇襲が終わり、動けるようになる。この後に攻撃ボタンを押すと、通常攻撃の1段目から始まる
 *      当てた瞬間にヒットストップ(_hitStopTime秒)をかけるので、終わるのはその分だけ遅くなる
 * ・ヒットストップ: 当てた瞬間、自分を_hitStopTime秒、食らった悪人を_villainHitStopTime秒(既定0。止めない)止める
 * ・先行入力: 当てた後〜当て終わるまでに押した攻撃ボタンを_inputBufferTime秒まで覚えておき、当て終わったらすぐ次の奇襲を始める
 * ・連続奇襲のスロー(ソロ(通信していない時)だけ。_useChainTime)
 *   当てた瞬間に、ゲーム全体の時間(Time.timeScale)を_chainTimeScale倍(既定0.05倍)にする
 *   _slowWithoutChainがオン(既定)なら、次の対象がいない時(単体の奇襲・最後の1体)もスローにし、待機時間が過ぎたら解除する
 *   オフなら、次に奇襲できる悪人がいる時だけスローにし、最後の1体に当てた時に解除する
 *   _holdUntilReleaseがオンなら、スロー中に当てた悪人は吹き飛ばさず、その場で止めたままにする(CS_VillainKnockback.BlowAwayに無限の止める時間を渡す)
 *   オフ(既定)なら、普段どおりヒットストップの後に吹き飛び始める(スロー中なのでゆっくり動き、解除すると残りを一気に吹き飛ぶ)
 *   当て終わってから連続奇襲の受付時間(_chainWindowTime。実時間)が過ぎた時(_slowWithoutChainがオフなら、最後の1体に当てた時も)に、
 *   スローを解除し、止めていた悪人を一斉に吹き飛ばす(最後の1体はヒットストップの後に、他の悪人もそれに合わせる)
 *   解除する時は、_returnTime秒(実時間)かけて、_returnCurveの形でなめらかに等倍へ戻す(物理の更新間隔も一緒に戻す)
 *   戻っている途中に次の奇襲を当てたら、すぐスローに戻す。他の演出がゲームの速さを変えたら、戻すのをやめる
 *   スロー中(戻っている途中も)は、奇襲の段階(溜め・飛び込み・当て)を実時間で進め、飛び込みの速さも実時間に合わせる
 *   自分のモーション(Animator)は、_playerMotionRealTimeがオンなら実時間、オフ(既定)ならスローのまま動かす
 *   物理の更新間隔(Time.fixedDeltaTime)も同じ倍率で縮める
 *   戻す時: 物理の更新間隔は必ず戻す。ゲームの速さは、自分が変えた値のまま(1%以内)の時だけ戻す(開始演出などが止めた後なら触らない)
 *   見た目(モノクロ・視点の重さ・ズーム)はCS_PlayerAmbushEffect・CS_PlayerCameraFovが isChainTime を見て行う
 * ・連続数(chainCount): 連続奇襲で何体目に当てたか(1体目は1)。連続奇襲でない奇襲を始めると0に戻す。見た目(連続数に応じたカメラ揺れ)用
 *   自分: 当ての段階のまま動けず、Animatorも止まる(Animatorを止めるのはCS_PlayerAmbushEffectがhitStopTimeを見て行う)
 *   悪人: 吹き飛ぶ前に、その場で止まる(CS_VillainKnockback.BlowAway。サーバーで行うので全員の画面で止まる。巻き込んだ悪人も同じ)
 * ・連続奇襲: 奇襲を始めた攻撃ボタンを押し続けている間は、奇襲が終わるたびに、次に奇襲できる悪人へ続けて奇襲する
 *   奇襲できる悪人がいない間は待ち、ボタンを離す・必殺技の合図(LT)を押す・動けなくなると終わる(押し直すまで続けない)
 *   一度狙った悪人(倒した・中止した悪人)は_chainSkipTime秒のあいだ狙わない
 *   (通信では、倒れたことが手元に届くまで少しかかるため、倒したばかりの悪人へもう一度飛び込まないように)
 * ・奇襲になった時は、その悪人の残りHP以上のダメージを与えて一撃で倒す(仕様「戦闘をすぐに終わらせる」)
 * ・奇襲を食らった悪人(巻き込んだ悪人も)は、プレイヤーの方を一瞬で向き、背中側へ大きく吹き飛ぶ(CS_VillainKnockback.BlowAway。Issue #379)
 *   吹き飛ぶ距離・時間は悪人のプレハブ(CS_VillainKnockbackのBlow Away)で変える
 * ・巻き込み(Issue #337): 奇襲で倒した悪人の位置から半径_splashRadius以内にいる悪人も、
 *   密着している悪人だけを巻き込むよう、既定は0.7m(悪人のカプセルの半径0.5mと合わせ、中心どうしが約1.2m以内なら巻き込む。データ表は5m)
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
 * ・溜め(演出用): 押してから飛び込むまで、その場で悪人の方を向いて_windupTime秒だけ溜める(既定0秒 = 溜めなしで、押した瞬間に飛び込む。0より大きくすると溜めが入る)
 *   溜めた分、飛び込みは速い(_dashSpeed既定60m/秒)。溜めている間はisWindingUpがtrue
 *   飛び込みの1回の時間は、物理の1ステップ(Time.fixedDeltaTime)より少し長くする
 *   (CS_Player.Lunge/Flyは、残り時間が1ステップ以下だと1歩も進まない。速くすると最後の短い距離で止まり、_maxDashTimeまで待ってしまうため)
 *   連続奇襲の2体目以降は、_skipChainWindupがオンなら溜めを省略してすぐ飛び込む(オフなら溜めの時間に_chainWindupRate(既定0.5)を掛ける)
 *   連続奇襲になるのは、奇襲を始めたボタンを押し続けている時と、奇襲を当て終わってから_chainWindowTime秒以内に押し直した時
 *   (受付時間はTime.timeScaleの影響を受けない時間で数える。連続奇襲のスロー(CS_PlayerAmbushEffect)の間も長くならない)
 *   当て終わった時に、onAmbushFinishedを本人の手元で出す(連続奇襲のスローに使う)
 *   溜めの間もisAmbushingはtrue。死亡・スタン・悪人が倒れた時は、飛び込みと同じく中止する
 *   サーバーの予約・悪人を止める時間にも、溜めの分を足す(StartRpcで溜めの時間を送る。_windupTimeより長い値は受け付けない)
 *   見た目用に、溜めが始まった時にonAmbushWindupStarted(悪人, 溜めの秒数)を本人の手元で出す(溜めが0秒の時は出さない)
 *   onAmbushStartedは今まで通り「飛び込み開始」(溜めが終わった時)に出る。演出はCS_PlayerAmbushEffect
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
    [SerializeField][Min(0.1f)] private float _dashSpeed = 60f;          // 飛び込む速さ(m/秒)
    [SerializeField][Min(0f)] private float _maxDashTime = 1f;            // これ以上かかっても着かなければ奇襲をやめる(秒)
    [SerializeField][Min(0f)] private float _strikeDistance = 1.2f;       // 悪人との水平距離がこれ以内になったら着いたとみなす(m)
    [SerializeField][Min(0f)] private float _verticalTolerance = 0.3f;    // 悪人との足元の高さの差がこれ以内なら、高さは合っているとみなす(m)
    [SerializeField][Min(0f)] private float _strikeDelay = 0.1f;          // 着いてから奇襲が当たるまでの時間(秒)
    [SerializeField][Min(0f)] private float _strikeDuration = 0.4f;       // 着いてから奇襲が終わる(動けるようになる)までの時間(秒)
    [SerializeField][Min(0f)] private float _hitStopTime = 0.1f;          // 当てた瞬間に、自分を止める時間(秒。ヒットストップ。0で止めない)
    [SerializeField][Min(0f)] private float _villainHitStopTime = 0f;     // 当てた瞬間に、食らった悪人を止める時間(秒。吹き飛ぶ前に止まる・モーションを止める。0で止めない)
    [SerializeField][Min(0f)] private float _inputBufferTime = 0.25f;     // 当てた後に押した攻撃ボタンを覚えておく時間(秒。先行入力。0で覚えない)
    [SerializeField][Min(0f)] private float _freezeMargin = 0.5f;         // 悪人を止める時間・予約の期限に足す余裕(秒。通信の遅れの分)
    [SerializeField][Min(0f)] private float _strikeTolerance = 2f;        // 当てる時、着いたとみなす距離からさらにこれだけ離れていても許す(m)
    [SerializeField] private LayerMask _dashPassThroughLayers = 1 << 31;  // 飛び込み中にぶつからないレイヤー(悪人・プレイヤーのいるレイヤー)
    [SerializeField][Min(0f)] private float _pressGraceTime = 0.3f;       // 押した瞬間に奇襲できなくても、この秒数以内にロックオン表示していた悪人なら奇襲する
    [SerializeField][Min(0f)] private float _chainSkipTime = 1f;          // 連続奇襲で、一度狙った悪人をもう狙わない時間(秒。倒れたことが届くまでの間)

    [Header("溜め(押した瞬間の演出用の間)")]
    [SerializeField][Min(0f)] private float _windupTime = 0.5f;           // 押してから飛び込むまで、その場で溜める時間(秒。0で溜めなし)
    [SerializeField] private bool _skipChainWindup = true;                // 連続奇襲の2体目以降は溜めを省略して、すぐ飛び込む
    [SerializeField][Min(0f)] private float _chainWindupRate = 0.5f;      // 溜めを省略しない時、連続奇襲の2体目以降は、溜めの時間にこの倍率を掛ける
    [SerializeField][Min(0f)] private float _chainWindowTime = 1f;        // 奇襲を当て終わってから、この秒数以内に押し直しても連続奇襲とみなす

    [Header("連続奇襲のスロー(ソロだけ)")]
    [SerializeField] private bool _useChainTime = true;                    // 次の対象がいる時、最後の1体までスローにして、最後に一斉に吹き飛ばすか
    [SerializeField][Range(0.01f, 1f)] private float _chainTimeScale = 0.05f;   // スロー中のゲームの速さ(倍)
    [SerializeField][Min(0f)] private float _returnTime = 0.3f;            // スローから等倍へ戻るのにかける時間(秒。実時間。0ですぐ戻る)
    [SerializeField] private AnimationCurve _returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);   // 戻り方(横軸: 経過の割合0〜1 / 縦軸: 等倍へ戻った割合0〜1)
    [SerializeField] private bool _slowWithoutChain = true;               // 次の対象がいない時(単体の奇襲・最後の1体)もスローにするか(オフなら、最後の1体でスローを解除する)
    [SerializeField] private bool _playerMotionRealTime = false;         // スロー中も、自分のモーション(Animator)は実時間で動かすか(オフならモーションもスローになる)
    [SerializeField] private bool _holdUntilRelease = false;              // スロー中に当てた悪人を、解除まで止めたままにするか(オフならヒットストップの後、スローのまま吹き飛び始める)

    [Header("巻き込み")]
    [SerializeField][Min(0f)] private float _splashRadius = 0.7f;         // 奇襲で倒した悪人から、この半径以内の悪人も一撃で倒す(m。0で巻き込まない。密着している悪人だけ)
    [SerializeField] private bool _splashIncludesRaidBoss = false;        // レイドボスも巻き込むか

    private enum Phase
    {
        None,     // 奇襲していない
        Windup,   // 飛び込む前に、その場で溜めている
        Dash,     // 悪人へ飛び込んでいる
        Strike,   // 着いて、攻撃している
    }

    private const int _searchBufferSize = 32;   // 一度に調べるコライダーの上限
    private const float _minDashStepRate = 1.01f;   // 飛び込みの1回の時間の下限(物理の1ステップの何倍か)
    private const float _landCheckDelay = 0.1f;   // 下へ飛び込み始めてから、着地を見始めるまでの時間(秒)
    private readonly Collider[] _searchBuffer = new Collider[_searchBufferSize];
    private readonly RaycastHit[] _obstacleBuffer = new RaycastHit[_searchBufferSize];

    private CS_Player _player;
    private CS_PlayerScoring _scoring;       // 付いていればスコアを通してダメージを与える
    private CS_PlayerHide _hide;             // ハイド。隠れられる箱の中にいる時は「背後か上」を見ない(付いていなければ見る)
    private CS_PlayerCharacter _character;   // キャラクター。必殺技の「正面からの奇襲」の間は「気づかれていない」「背後か上」を見ない(付いていなければ見る)
    private Rigidbody _rigidbody;
    private Collider _collider;              // 自分の当たり判定(足元の高さを調べる)
    private LayerMask _defaultExcludeLayers;   // 飛び込む前のRigidbody.excludeLayers(飛び込み後に戻す)
    private bool _isPassingThrough;            // 飛び込み中で、悪人・プレイヤーとぶつからないようにしているか
    private Phase _phase = Phase.None;
    private CS_VillainHealth _target;        // 奇襲している悪人
    private Collider _targetCollider;        // その悪人の当たり判定(足元の高さを調べる。飛び込み中に毎フレーム探さないよう控える)
    private float _elapsed;                  // 今の段階(溜め・飛び込み・攻撃)が始まってからの時間
    private float _windupDuration;           // 今の奇襲の溜めの時間(秒。溜めなしなら0)
    private bool _hasStruck;                 // 奇襲を当てる処理を行ったか
    private CS_VillainHealth _reservedTarget;   // 奇襲すると決めた悪人(サーバー、またはオフラインのみ)
    private float _reservedUntil;               // 予約の期限(Time.time。サーバー、またはオフラインのみ)
    private CS_VillainHealth _lastFoundTarget;  // 最後にFindAmbushTargetで見つかった悪人(本人の手元。押した瞬間の猶予に使う)
    private float _lastFoundTime;               // それが見つかった時刻(Time.time)
    private bool _isChainHeld;                  // 奇襲を始めたボタンを押し続けているか(押し続けている間は、続けて奇襲する)
    private float _lastStrikeEndTime = float.NegativeInfinity;   // 最後に奇襲を当て終わった時刻(Time.unscaledTime)
    private float _bufferedPressTime = float.NegativeInfinity;   // 当てた後に攻撃ボタンが押された時刻(Time.unscaledTime。先行入力)
    private int _chainCount;                     // 連続奇襲で何体目に当てたか
    private bool _isChainTime;                   // 連続奇襲のスロー中か
    private float _chainTimeEnd = float.PositiveInfinity;   // スローを解除する時刻(Time.unscaledTime。奇襲中は無限)
    private float _savedTimeScale;               // スローの前のTime.timeScale
    private float _savedFixedDeltaTime;          // スローの前のTime.fixedDeltaTime
    private float _appliedTimeScale;             // 最後に自分が入れたTime.timeScale(他の演出が変えていないかを比べる)
    private bool _isReturning;                   // スローから等倍へ戻っている途中か
    private float _returnElapsed;                // 戻り始めてからの時間(秒。実時間)
    private float _returnFrom;                   // 戻り始めた時のTime.timeScale
    private bool _holdBlowAway;                  // 今当てる悪人を、吹き飛ばさずに止めておくか(当てる処理の間だけ)
    private bool _playAmbushSeOnFinish;          // 当て終わった時に奇襲のSEを鳴らすか(スローにならなかった奇襲)
    private readonly List<CS_VillainKnockback> _heldVillains = new List<CS_VillainKnockback>();   // スロー中に止めている悪人
    private readonly Dictionary<Animator, AnimatorUpdateMode> _animatorModes = new Dictionary<Animator, AnimatorUpdateMode>();   // スロー中に実時間で動かしている自分のAnimatorと元の設定
    private readonly Dictionary<CS_VillainHealth, float> _skipUntil = new Dictionary<CS_VillainHealth, float>();   // 一度狙った悪人と、また狙えるようになる時刻

    public float range => _range;                   // 奇襲できる距離(m)
    public float chainWindowTime => _chainWindowTime;   // 当て終わってから、連続奇襲とみなす時間(秒。Time.timeScaleの影響を受けない)
    public float hitStopTime => _hitStopTime;   // 自分のヒットストップの時間(秒。CS_PlayerAmbushEffectがAnimatorを止める時間に使う)
    public float villainHitStopTime => _villainHitStopTime;   // 食らった悪人のヒットストップの時間(秒。CS_PlayerAmbushEffectが悪人のAnimatorを止めるかに使う)
    public bool hasStruck => _hasStruck;          // 今の奇襲を当てたか(本人の手元。見た目用)
    // 連続奇襲が続いている間か(奇襲中、または当て終わってから受付時間(_chainWindowTime。実時間)の間)。コンボ数の表示用
    public bool isInChainWindow => isAmbushing || Time.unscaledTime <= _lastStrikeEndTime + _chainWindowTime;
    public bool isChainTime => _isChainTime;
    // 連続奇襲のスローの、待機時間の残り(0〜1)。奇襲中は1、当て終わると受付時間(_chainWindowTime。実時間)をかけて0へ減る。スロー中でなければ0
    public float chainTimeRemainingRate
    {
        get
        {
            if (!_isChainTime) return 0f;
            if (isAmbushing || float.IsPositiveInfinity(_chainTimeEnd)) return 1f;
            return _chainWindowTime > 0f ? Mathf.Clamp01((_chainTimeEnd - Time.unscaledTime) / _chainWindowTime) : 0f;
        }
    }      // 連続奇襲のスロー中か(本人の手元。見た目用)
    public int chainCount => _chainCount;         // 連続奇襲で何体目に当てたか(1体目は1。見た目用)
    public bool isAmbushing => _phase != Phase.None;   // 奇襲中か(CS_PlayerAttackが、通常攻撃・必殺技を止めるのに使う)
    public bool isWindingUp => _phase == Phase.Windup;   // 飛び込む前に溜めている間か(本人の手元。見た目用)
    public bool isLunging => _phase == Phase.Dash || (_phase == Phase.Strike && !_hasStruck);   // 飛び込み〜当たる瞬間か(本人の手元。見た目用)

    public event Action<CS_VillainHealth> onAmbush;     // 奇襲が決まった時(サーバー、またはオフラインのみ)
    public event Action<CS_VillainHealth> onAmbushSplash;      // 奇襲に巻き込んで悪人を倒した時(サーバー、またはオフラインのみ)
    public event Action<CS_VillainHealth, float> onAmbushWindupStarted;   // 溜めを始めた時(本人の手元のみ。溜めの秒数つき。溜めが0秒の時は出ない)。見た目用
    public event Action<CS_VillainHealth> onAmbushStarted;     // 飛び込みを始めた時(本人の手元のみ。溜めがあれば溜めの後)。見た目用
    public event Action<CS_VillainHealth> onAmbushHitTiming;   // 奇襲が当たる瞬間(本人の手元のみ)。見た目・カメラの揺れ用
    public event Action onAmbushFinished;   // 奇襲を当て終わった時(動けるようになった時。本人の手元のみ。中止した時は出ない)。連続奇襲のスロー用

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _scoring = GetComponent<CS_PlayerScoring>();
        _hide = GetComponent<CS_PlayerHide>();
        _character = GetComponent<CS_PlayerCharacter>();
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
    }

    private void OnDisable()
    {
        ReleaseChainTime(0f, true);
    }

    private void Update()
    {
        // 当て終わってから受付時間が過ぎたら、スローを解除して止めていた悪人を吹き飛ばす
        if (_isChainTime && _phase == Phase.None && Time.unscaledTime >= _chainTimeEnd) ReleaseChainTime(0f);
        UpdateReturn();

        if (_phase == Phase.None)
        {
            UpdateChain();
            return;
        }

        // 動けなくなった(死亡・スタン)・悪人が倒れた・消えた時はやめる
        if (!_player.canAct || !IsTargetAlive())
        {
            EndAmbush();
            return;
        }

        // 当てた後に押された攻撃ボタンを覚えておく(先行入力。当て終わったら次の奇襲を始める)
        if (_hasStruck && _inputBufferTime > 0f && _player.attackAction != null && _player.attackAction.WasPressedThisFrame())
        {
            _bufferedPressTime = Time.unscaledTime;
        }

        // スロー中は、奇襲の段階を実時間で進める
        _elapsed += isTimeSlowed ? Time.unscaledDeltaTime : Time.deltaTime;
        if (_phase == Phase.Windup) UpdateWindup();
        else if (_phase == Phase.Dash) UpdateDash();
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

        // 連続奇襲の2体目以降は、溜めを省略する(または短くする)
        // 押し続けている間は_isChainHeldがtrueのまま続く。当て終わってすぐ押し直した時も連続奇襲とみなす
        bool isChain = _isChainHeld || Time.unscaledTime <= _lastStrikeEndTime + _chainWindowTime;
        _windupDuration = !isChain ? _windupTime : _skipChainWindup ? 0f : _windupTime * _chainWindupRate;
        if (!isChain) _chainCount = 0;

        _target = villain;
        _targetCollider = villain.GetComponentInChildren<Collider>();
        _phase = _windupDuration > 0f ? Phase.Windup : Phase.Dash;
        _elapsed = 0f;
        _hasStruck = false;
        FaceTarget();
        if (_phase == Phase.Dash) SetPassThrough(true);

        // オフラインではその場で奇襲になるかが決まり、ならなければここで終わる(その時は通常攻撃の1段目を出す)
        RequestStart(villain, _windupDuration);
        if (!isAmbushing) return false;

        _isChainHeld = true;
        _chainTimeEnd = float.PositiveInfinity;   // 奇襲中はスローを続ける
        if (_phase == Phase.Windup) onAmbushWindupStarted?.Invoke(villain, _windupDuration);
        else onAmbushStarted?.Invoke(villain);
        return true;
    }

    // 溜めの間は、その場で悪人の方を向く。溜め終わったら飛び込む
    private void UpdateWindup()
    {
        FaceTarget();
        if (_elapsed < _windupDuration) return;

        _phase = Phase.Dash;
        _elapsed = 0f;
        SetPassThrough(true);
        onAmbushStarted?.Invoke(_target);
    }

    // 奇襲を始めた攻撃ボタンを押し続けている間は、次に奇襲できる悪人へ続けて奇襲する(いなければ、見つかるまで待つ)
    private void UpdateChain()
    {
        if (!_isChainHeld) return;

        // ボタンを離した・必殺技の合図(LT)を押した・動けなくなった時は、連続奇襲を終わる
        if (!_player.attackAction.IsPressed() || _player.isSpecialModifierHeld || !_player.canAct)
        {
            _isChainHeld = false;
            return;
        }

        // SAでハイドポイントへ飛んでいる間は待つ(CS_PlayerAttackと同じ)
        if (_player.isHiding) return;

        TryStartAmbush();
    }

    // 直前(_pressGraceTime秒以内)にロックオン表示していた悪人。倒れておらず、遠すぎず、間に壁が無ければ返す
    private CS_VillainHealth GetRecentTarget()
    {
        if (_lastFoundTarget == null || Time.time - _lastFoundTime > _pressGraceTime) return null;
        if (IsSkipped(_lastFoundTarget)) return null;
        if (_lastFoundTarget.isDefeated || _lastFoundTarget.currentHp <= 0f) return null;
        if (HorizontalDistance(_lastFoundTarget.transform.position) > _range) return null;
        if (!IsClearLine(transform.position, _lastFoundTarget.transform)) return null;

        return _lastFoundTarget;
    }

    // 飛び込みを始めてから奇襲が当たるまでの見込みの時間(秒)。見た目(CS_PlayerVisual)が、当たる瞬間にモーションの打撃を合わせるのに使う
    // 飛び込む距離(高さの差も含む) ÷ 飛び込む速さ + 着いてから当たるまでの時間(悪人は止めているので、始めた時の距離で見積もれる)
    public float EstimateTimeToHit(CS_VillainHealth villain)
    {
        if (villain == null) return _strikeDelay;

        float moveDistance = GetDashMove(villain).magnitude;
        return Mathf.Min(moveDistance / _dashSpeed, _maxDashTime) + _strikeDelay;
    }

    // 悪人の今の位置を追いながら、_strikeDistanceまで飛び込む。高さが違う時は悪人の足元の高さまで斜めに飛び込む
    private void UpdateDash()
    {
        FaceTarget();

        Vector3 move = GetDashMove(_target);
        if (move.sqrMagnitude <= 0.0001f)
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
        float moveDistance = move.magnitude;
        // 残りが短い時も1回は物理の更新で進めるよう、時間は物理の1ステップより少し長くする(短いと、踏み込みが1歩も進まず最後で止まる)
        // スロー中は実時間で同じ速さに見えるよう、ゲームの時間に直す(物理の更新間隔もスローに合わせて縮めてある)
        float timeRate = isTimeSlowed ? Time.timeScale / Mathf.Max(0.0001f, _savedTimeScale) : 1f;
        float duration = Mathf.Max(moveDistance / _dashSpeed * timeRate, Time.fixedDeltaTime * _minDashStepRate);
        if (Mathf.Approximately(move.y, 0f)) _player.Lunge(move, moveDistance, duration);
        else _player.Fly(move / moveDistance, moveDistance, duration);
    }

    // 着くまでに進む量(水平は_strikeDistanceまで詰める分、縦は悪人の足元の高さとの差)。着いていればゼロ
    // 高さの差が_verticalTolerance以内、または下へ飛び込んでいる途中で着地した時は、縦は動かない
    // 飛び込み始めは足場の上に立っているので、_landCheckDelay秒たつまでは着地を見ない
    private Vector3 GetDashMove(CS_VillainHealth villain)
    {
        float horizontalMove = Mathf.Max(0f, HorizontalDistance(villain.transform.position) - _strikeDistance);
        Vector3 move = HorizontalDirection(villain.transform.position) * horizontalMove;

        Collider villainCollider = villain == _target ? _targetCollider : villain.GetComponentInChildren<Collider>();
        float heightGap = GetFeetHeight(villain.transform, villainCollider) - GetFeetHeight(transform, _collider);
        bool isLevel = Mathf.Abs(heightGap) <= _verticalTolerance || (heightGap < 0f && _phase == Phase.Dash && _elapsed >= _landCheckDelay && _player.isGrounded);
        if (!isLevel) move.y = heightGap;
        return move;
    }

    // 足元の高さ(当たり判定の底。当たり判定が無ければ位置の高さ)
    private static float GetFeetHeight(Transform target, Collider collider)
    {
        return collider != null ? collider.bounds.min.y : target.position.y;
    }

    // 着いてから_strikeDelay秒後に当て、_strikeDuration秒(とヒットストップの分)で終わる
    private void UpdateStrike()
    {
        FaceTarget();

        if (!_hasStruck && _elapsed >= _strikeDelay)
        {
            _hasStruck = true;
            _chainCount++;

            // 連続奇襲のスローが使える時は、いったん吹き飛ばさずに止めておき、次の対象がいるかで決める
            bool canChainTime = CanUseChainTime();
            _holdBlowAway = canChainTime && _holdUntilRelease;
            RequestStrike(_target);
            _holdBlowAway = false;

            // 当てた悪人は、すぐ奇襲の対象から外す(倒れたことが届く前でも、ロックオン表示・次の対象に出ないように)
            _skipUntil[_target] = Time.time + _chainSkipTime;
            if (canChainTime)
            {
                // 次の対象がいる時(と、_slowWithoutChainがオンなら単体・最後の1体の時も): スローにする(待機時間が過ぎたら解除)
                // 次の対象がいなくて_slowWithoutChainがオフの時: スローを解除し、悪人のヒットストップの後に一斉に吹き飛ばす
                if (_slowWithoutChain || FindAmbushTarget() != null) EnterChainTime();
                else ReleaseChainTime(_villainHitStopTime);
            }

            onAmbushHitTiming?.Invoke(_target);

            // 当てた瞬間はヒット音だけ鳴らす(スロー中は、低く引き伸ばしたスロー用のヒット音)。奇襲のSEは、スローが終わる時(ReleaseChainTime)に鳴らす
            // スローにならなかった時(マルチ・スローを使わない設定)は、通常のヒット音を鳴らし、奇襲のSEは当て終わった時(EndAmbush)に鳴らす
            // 本人の手元でだけ起きるので全員へ
            CSE_SeId hitSe = _isChainTime ? CSE_SeId.PlayerSurpriseAttackSlowHit : CSE_SeId.PlayerAttackHit;
            CS_NetworkSoundPlayer.instance?.PlaySeForEveryone(hitSe, transform.position);
            _playAmbushSeOnFinish = !_isChainTime;
        }

        if (_elapsed >= Mathf.Max(_strikeDelay, _strikeDuration) + _hitStopTime) EndAmbush();
    }

    private void EndAmbush()
    {
        // 当てる前にやめた時は、止めていた悪人を動かす
        if (_phase != Phase.None && !_hasStruck) RequestRelease();

        // 狙った悪人(倒した・中止した)は、しばらく連続奇襲で狙わない
        if (_target != null) _skipUntil[_target] = Time.time + _chainSkipTime;

        // 当て終わった時刻(すぐ押し直した時に、連続奇襲として溜めを省略するのに使う)
        bool isFinished = _phase != Phase.None && _hasStruck;
        if (isFinished) _lastStrikeEndTime = Time.unscaledTime;

        // スロー中は、受付時間が過ぎたら解除する(次の奇襲を始めたら延びる)
        if (_isChainTime) _chainTimeEnd = Time.unscaledTime + _chainWindowTime;

        _phase = Phase.None;
        _target = null;
        _targetCollider = null;
        _elapsed = 0f;
        _hasStruck = false;
        _player.ClearAimFacing();
        SetPassThrough(false);

        if (isFinished) onAmbushFinished?.Invoke();

        // スローにならなかった奇襲は、当て終わった時に奇襲のSEを鳴らす
        if (isFinished && _playAmbushSeOnFinish) PlayAmbushSe();
        _playAmbushSeOnFinish = false;

        // 当てた後に攻撃ボタンが押されていたら、すぐ次の奇襲を始める(先行入力。連続奇襲になるので溜めは省略される)
        if (isFinished && Time.unscaledTime - _bufferedPressTime <= _inputBufferTime)
        {
            _bufferedPressTime = float.NegativeInfinity;
            TryStartAmbush();
        }
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
    private void RequestStart(CS_VillainHealth villain, float windupDuration)
    {
        Vector3 position = transform.position;

        // オフライン(テストシーン)では、その場で決める
        if (!IsSpawned)
        {
            if (!StartOnServer(villain, position, windupDuration)) EndAmbush();
            return;
        }

        StartRpc(new NetworkObjectReference(villain.NetworkObject), position, windupDuration);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void StartRpc(NetworkObjectReference villainReference, Vector3 position, float windupDuration)
    {
        CS_VillainHealth villain = GetVillain(villainReference);
        if (villain != null && StartOnServer(villain, position, windupDuration)) return;

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
    private bool StartOnServer(CS_VillainHealth villain, Vector3 position, float windupDuration)
    {
        Vector3 forward = transform.forward;
        CS_AttackHitDetector.ResolvePose(transform, ref position, ref forward);

        if (villain.isDefeated || villain.currentHp <= 0f) return false;
        if (HorizontalDistance(villain.transform.position, position) > _range + _strikeTolerance) return false;
        if (!IsClearLine(position, villain.transform)) return false;
        CS_VillainHealth ambushed = villain;

        ReleaseOnServer();   // 前の予約が残っていれば解く

        // 溜めの分も足す(送られてきた値は、サーバー側の溜めの時間より長くならないようにする)
        float windup = float.IsFinite(windupDuration) ? Mathf.Clamp(windupDuration, 0f, _windupTime) : _windupTime;
        float duration = windup + _maxDashTime + Mathf.Max(_strikeDelay, _strikeDuration) + _hitStopTime + _freezeMargin;
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
        BlowAway(villain, position);
        NotifyAmbush(villain);

        DefeatVillainsAround(center, villain, position);
    }

    // centerから半径_splashRadius以内にいる悪人(exclude以外)を、残りHP分のダメージで倒す(サーバー、またはオフラインで実行される)
    // 巻き込んだ悪人も、奇襲したプレイヤーの位置(attackerPosition)から吹き飛ばす
    private void DefeatVillainsAround(Vector3 center, CS_VillainHealth exclude, Vector3 attackerPosition)
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
            BlowAway(villain, attackerPosition);
            onAmbushSplash?.Invoke(villain);
        }
    }

    // 奇襲を食らった悪人を、プレイヤーの方へ一瞬で向かせて背中側へ大きく吹き飛ばす(サーバー、またはオフラインで実行される)
    // 吹き飛ぶ距離・時間は悪人側(CS_VillainKnockbackのBlow Away)で決める。倒した後に呼ぶ(倒れた悪人も吹き飛ぶ)
    // 吹き飛ぶ前に、ヒットストップの間その場で止まる。連続奇襲のスローが使える時は、止めたままにする(ReleaseChainTimeで吹き飛ばす)
    private void BlowAway(CS_VillainHealth villain, Vector3 attackerPosition)
    {
        if (!villain.TryGetComponent(out CS_VillainKnockback knockback)) return;

        knockback.BlowAway(attackerPosition, _holdBlowAway ? float.PositiveInfinity : _villainHitStopTime);
        if (_holdBlowAway && !_heldVillains.Contains(knockback)) _heldVillains.Add(knockback);
    }

    // ========================================
    // 連続奇襲のスロー(ソロだけ)
    // ========================================

    // スローが使えるか(ソロで通信していない時の、自分のプレイヤーだけ。マルチでTime.timeScaleを変えると全員が遅くなるため)
    private bool CanUseChainTime()
    {
        if (!_useChainTime || IsSpawned || !_player.isLocalPlayer || !CS_GameModeStore.isSolo) return false;
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening;
    }

    // スロー中か、スローから等倍へ戻っている途中か(奇襲の段階を実時間で進めるのに使う)
    private bool isTimeSlowed => _isChainTime || _isReturning;

    private void EnterChainTime()
    {
        if (_isChainTime) return;
        if (Time.timeScale <= 0f) return;   // 演出などで時間が止まっている

        // 等倍へ戻っている途中なら、スローの前の値はそのまま使う(途中の値を覚え直さない)
        if (!_isReturning)
        {
            _savedTimeScale = Time.timeScale;
            _savedFixedDeltaTime = Time.fixedDeltaTime;
        }
        _isReturning = false;
        Time.timeScale = _savedTimeScale * _chainTimeScale;
        Time.fixedDeltaTime = _savedFixedDeltaTime * _chainTimeScale;
        _appliedTimeScale = Time.timeScale;   // 実際に入った値(戻す時に、他の演出が変えていないかを比べる)

        // _playerMotionRealTimeがオンなら、自分のモーションは実時間で動かす(オフならモーションもスローになる)
        foreach (Animator animator in _playerMotionRealTime ? GetComponentsInChildren<Animator>(true) : new Animator[0])
        {
            if (_animatorModes.ContainsKey(animator)) continue;
            _animatorModes.Add(animator, animator.updateMode);
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
        _isChainTime = true;
    }

    // スローを解除し、止めていた悪人をdelay秒後に一斉に吹き飛ばす(スロー中でなくても、止めていた悪人は吹き飛ばす)
    // ゲームの速さは_returnTime秒かけて等倍へ戻す(immediateなら、すぐ戻す。無効にされた時など)
    private void ReleaseChainTime(float delay, bool immediate = false)
    {
        if (_isChainTime)
        {
            _isChainTime = false;
            if (!immediate) PlayAmbushSe();   // スローが終わる時に奇襲のSEを鳴らす(無効にされた時などは鳴らさない)

            if (!immediate && _returnTime > 0f && IsOwnTimeScale())
            {
                // なめらかに戻す(UpdateReturn)
                _isReturning = true;
                _returnElapsed = 0f;
                _returnFrom = Time.timeScale;
            }
            else
            {
                RestoreTimeScale();
            }

            foreach (KeyValuePair<Animator, AnimatorUpdateMode> pair in _animatorModes)
            {
                if (pair.Key != null) pair.Key.updateMode = pair.Value;
            }
            _animatorModes.Clear();
        }

        foreach (CS_VillainKnockback knockback in _heldVillains)
        {
            if (knockback != null) knockback.ReleaseHold(delay);
        }
        _heldVillains.Clear();
        _chainTimeEnd = float.PositiveInfinity;
        if (immediate && _isReturning) RestoreTimeScale();
    }

    // スローから等倍へ、_returnTime秒(実時間)かけて_returnCurveの形で戻す
    private void UpdateReturn()
    {
        if (!_isReturning) return;

        // 他の演出がゲームの速さを変えたら、戻すのをやめる(物理の更新間隔だけは戻す)
        if (!IsOwnTimeScale())
        {
            _isReturning = false;
            Time.fixedDeltaTime = _savedFixedDeltaTime;
            return;
        }

        _returnElapsed += Time.unscaledDeltaTime;
        float t = _returnTime > 0f ? Mathf.Clamp01(_returnElapsed / _returnTime) : 1f;
        if (t >= 1f)
        {
            RestoreTimeScale();
            return;
        }

        float k = _returnCurve != null && _returnCurve.length > 0 ? Mathf.Clamp01(_returnCurve.Evaluate(t)) : t;
        float scale = Mathf.Lerp(_returnFrom, _savedTimeScale, k);
        Time.timeScale = scale;
        Time.fixedDeltaTime = _savedFixedDeltaTime * (scale / Mathf.Max(0.0001f, _savedTimeScale));
        _appliedTimeScale = Time.timeScale;
    }

    // 奇襲のSEを鳴らす(本人の手元でだけ起きるので全員へ)
    private void PlayAmbushSe()
    {
        CS_NetworkSoundPlayer.instance?.PlaySeForEveryone(CSE_SeId.PlayerSurpriseAttack, transform.position);
    }

    // ゲームの速さが、最後に自分が入れた値のまま(1%以内)か(開始演出などが止めた(0にした)後ならfalse)
    // ※ Mathf.Approximatelyは小さい値だと厳しすぎて、少しのずれで戻らなくなるので使わない
    private bool IsOwnTimeScale()
    {
        return Mathf.Abs(Time.timeScale - _appliedTimeScale) <= _appliedTimeScale * 0.01f;
    }

    // ゲームの速さと物理の更新間隔を、スローの前に戻す
    // ゲームの速さは自分が入れた値のままの時だけ戻す。物理の更新間隔は、変えるのはここだけなので必ず戻す(戻らないと、地上の移動などがおかしくなる)
    private void RestoreTimeScale()
    {
        if (IsOwnTimeScale()) Time.timeScale = _savedTimeScale;
        Time.fixedDeltaTime = _savedFixedDeltaTime;
        _isReturning = false;
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
            if (IsSkipped(villain)) continue;
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

    // 少し前に狙った悪人か(期限が過ぎていたら忘れる)
    private bool IsSkipped(CS_VillainHealth villain)
    {
        if (!_skipUntil.TryGetValue(villain, out float until)) return false;
        if (Time.time < until) return true;

        _skipUntil.Remove(villain);
        return false;
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
        // 必殺技の「正面から奇襲」の間は、気づかれていても・正面からでも奇襲できる(仕様書「必殺技とは」ギロ: 見られていても関係なく奇襲ができる)
        bool isFrontalAmbush = IsFrontalAmbushActive();
        if (!isFrontalAmbush && IsNoticed(villain)) return false;
        if (!IsHidingAttacker() && !isFrontalAmbush && !IsAbove(toAttacker.y) && !IsBehind(villainTransform.forward, flat)) return false;
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

    // ハイドポイントの隠れられる箱の中にいるか(本人の手元はその場で調べ、サーバー・他の人は同期された状態でも分かる)
    private bool IsHidingAttacker()
    {
        return _hide != null && (_hide.isInHideBox || _hide.isHidden);
    }

    // 必殺技の「正面からの奇襲」の効果が続いているか(効果の残り時間は同期されているので、全員で分かる)
    private bool IsFrontalAmbushActive()
    {
        return _character != null && _character.IsEffectActive(CSE_PlayerSpecialEffect.FrontalAmbush);
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
