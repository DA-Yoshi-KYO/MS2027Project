using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

/*
 * プレイヤーの操作(三人称視点)を行うクラス
 * キーボード+マウス、コントローラーの両方に対応
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・入力は CS_CustomInputActionManager(シングルトン)が保持する
 *   PlayerControls.inputactions由来のCustomInputActionを使う
 *   1台のPCで操作するプレイヤーは常に1体なので、複製はせず共有のまま参照する
 * ・入力、移動、カメラ操作は「自分が操作するプレイヤー」だけが行う(_isControlled)
 *   オンライン: 自分がOwnerのプレイヤー
 *   オフライン: NetworkManagerが動いていないテストシーンのプレイヤー
 * ・他人のプレイヤーの位置は NetworkTransform(Authority Mode: Owner) が同期する
 * ・移動の流れ
 *   1. Update       : 入力を読み取り、視点(yaw/pitch)を更新する
 *   2. FixedUpdate  : 体の向きを更新し(UpdateFacing)、押した方向へ移動する
 * ・体の向き(仕様書「カメラについて」は「カメラを左右に回すとキャラの向きも変わる」だったが、手触りの調整で変更)
 *   カメラを回しただけでは体は回らない。移動キーを押している間だけ、進む方向(カメラから見た入力の方向)へ_rotationSpeedで回る
 *   移動は押した方向へすぐ始まり、体の向きは回りながら追いつく(以前のように「向き終わるまで動かない」ことはない)
 *   例外: エイムアシスト中は攻撃する相手の方、ダッシュ中はダッシュの方向、NPCは止まっている間も指定された向きへ回る
 *   体の向きとカメラの向きがずれるため、警戒UI(CS_PoliceDetectionController)の方向の基準には、プレイヤー本体ではなく
 *   「位置はプレイヤー、向きはカメラの左右の向き」の基準(_viewDirectionAnchor)を渡す(画面の上 = カメラの正面になる)
 *   3. LateUpdate   : カメラをプレイヤーの周りに配置する
 * ・カメラはプレイヤーの子だが、回転がプレイヤーに引っ張られないよう
 *   LateUpdateでワールド座標を直接指定している
 *   カメラにはCinemachineBrainが付いており、位置・向きはCinemachineCamera(_virtualCameraTransform)へ指定する
 *   (Brainが実際のカメラへ反映する。攻撃時の揺れはCS_PlayerCameraShakeがImpulseで加える)
 * ・SAモーション(CS_PlayerHide): SAで飛んでいる間(isHiding)は、Fly/Lungeの残り時間とは別の、SA専用の速度(_saVelocity)をRigidbodyに入れて動く
 *   BeginSAMotion(始める)・SetSAVelocity(毎物理フレーム速度を決める)・EndSAMotion(終える。専用の速度をクリアして、終わった時の速度を渡す)
 *   終わった時の速度の水平は空中の勢い(_airMomentum)になる。勢いがある間は、水平の速度が入力の移動速度へ_airControlAccelで近づき、SAポイントごとの減速率(_airMomentumDrag。毎秒e^(-減速率)の割合)で弱まる
 *   勢いは着地・ダッシュ・踏み込み・倒れる・スタンで0になる。勢いが0の間・SAを使わない時の動きは今まで通り
 * ・死亡中(CS_PlayerHealth.isDead)は移動・攻撃を行わない(canActで判定)
 * ・スタート時のカメラの演出(CS_PlayerIntroCamera。付いていなければ演出しない): 自分のプレイヤーとして操作を始めた時に流す
 *   演出中は動けず(canAct)、視点も操作できない。カメラの位置は演出が決める(UpdateCameraTransform)
 *   マルチでは、全員の演出が終わってゲームが始まるまで動けない(CS_PlayerIntroCamera.isMatchHeld。視点は動かせる)
 *   視点操作(カメラ)は死亡中も継続する
 *   死亡中は水平方向の速度を毎回0にし、死亡時の勢いで滑り続けないようにしている(落下はする)
 * ・移動速度はCS_PlayerStats.currentMoveSpeed(暗躍ボーナス込み)を使う(実際の変更はCS_PlayerStats側で行う)
 * ・Colliderには摩擦0の物理マテリアル(PlayerFrictionless)を設定している
 *   (摩擦があると、空中で壁に向かって移動し続けた時に壁に張り付いて落ちなくなるため)
 * ・移動の手触り(原神のような操作感を目指して調整。各項目を0にすると、その項目は以前の動きに戻る)
 *   加速・減速: 入力の速さへ、地上は_groundAcceleration(速くなる時)・_groundDeceleration(止まる・遅くなる時)、空中は_airAccelerationで近づく
 *     (以前は押した瞬間に最高速・離した瞬間に完全停止。ごく短い加速・減速を入れて、細かい位置合わせをしやすくした。仕様の「慣性は少なめ」は守る値にしている)
 *     動く足場(CS_PlayerRideSAPoint)が足した速度は、加速の計算から除く(除かないと増え続ける)
 *   坂(_followSlope): 地上では地面の向き(_groundNormal)に沿って動き、入力が無い時はその場に止まる(下り坂で跳ねる・摩擦0で滑るのを防ぐ)
 *   段差を下りる(_stepDownHeight): 低い段差を下りた瞬間は、宙に浮かず下の地面へ吸い付く(1物理フレームで下りる)。高い段差は普通に落ちる
 *   段差を登る(_suppressStepBounce): ジャンプしていないのに付いた上向きの勢い(段差の角で押し上げられた分)を消す。走って段差を登った時に跳ねない
 *   ジャンプの猶予: 足場から出て_coyoteTime秒以内なら跳べる。着地前_jumpBufferTime秒以内に押したジャンプは、着地した時に跳ぶ
 *   カメラ: 追う高さだけ_cameraVerticalSmoothTime秒遅らせる(ジャンプ・段差でカクッと動かない)。SAで飛んでいる間・3m以上離れた時はすぐ合わせる
 *   接地の状態はFixedUpdateの最初に1回だけ調べる(UpdateGrounded。_isGroundedNow)
 * ・ジャンプ
 *   接地中にジャンプボタンを押すと、CS_PlayerStats.jumpPowerを初速として真上に飛ぶ
 *   入力はUpdateで拾って予約し、実際に飛ぶ処理はFixedUpdate側のMove()の後に行う
 *   (Move()は毎回、現在のY速度を保ったまま水平方向だけを書き換えるため、
 *    ジャンプの初速はMove()より後に適用しないと上書きされてしまう)
 * ・ダッシュ(ブリンク)
 *   クールタイム中でなければダッシュボタンで発動。移動入力があればその方向、
 *   無ければ現在向いている方向へ、CS_PlayerStats.dashSpeedで一定時間だけ直進する
 *   （「慣性は少なめできびきび動く」という要望に合わせ、通常のMove()は行わず
 *    速度を直接指定している）。攻撃・必殺技とは排他制御しておらず、いつでも出せる
 * ・スプリント(ZZZのような「ブリンク後、ボタンを押し続けている間だけ速くなる」挙動)
 *   ダッシュ(ブリンク)が終わった後、ダッシュボタンを押し続けている間はCS_PlayerStats.sprintSpeedで移動する
 *   (moveSpeedより速く、dashSpeedより遅い想定)。クールタイムとは無関係で、ボタンを離すと通常速度に戻る
 *   移動そのものの仕組みはMove()を共用し、参照する速度だけが変わる
 * ・playerNumber(0〜3)
 *   CS_PlayerSpawnerが、スポーンする直前にAssignPlayerNumber()で割り当てる(NetworkVariableなのでスポーン時に全員へ同期される)
 *   HP UI(CS_PlayerHpUI)など、「何番目のプレイヤーか」で表示先を決める仕組みが使う
 * ・NPC(4人に満たない枠を埋める、人の代わりのプレイヤー。仕様書「NPC(他プレイヤー)」)
 *   CS_PlayerSpawnerが、スポーンする直前にMarkAsNpc()で印を付ける(NetworkVariableなので全員へ同期される)
 *   NPCはサーバーが所有・操作する(_isControlledはサーバーでだけtrue)。入力は人のInputActionの代わりに
 *   CS_PlayerNpcInput(判断はCS_PlayerNpcBrain)から読む。カメラは使わない
 *   ※ NPCはサーバーが所有するため、ホストではIsOwnerがtrueになる
 *     「自分の画面のプレイヤーか」(HUD・ミニマップの中心など)は、IsOwnerではなくisLocalPlayerで判定すること
 * ・ボタン(攻撃・必殺技など)はCS_PlayerButtonで公開する。人かNPCかに関わらず、各部品は
 *   WasPressedThisFrame() / IsPressed() で読むだけでよい
 * ・落下の保険(Issue #290): 操作しているプレイヤーがy = _fallResetHeightより下に落ちたら、スポーンした位置へ戻す
 *   (床の読み込みより先に落ち始めた時などに、落ち続けないようにする。位置はNetworkTransformのTeleportで全員へ反映する)
 * ・ハイド(CS_PlayerHide。付いていなければ使わない)
 *   飛べるハイドポイントがある時のダッシュボタンは、ダッシュの代わりにハイドポイントへ飛ぶ(TryBeginHide)
 *   SAで飛んでいる間は、ダッシュ・スプリント・通常の移動をしない。ジャンプボタンで飛ぶのをやめてジャンプする
 *   着いた後は普通の状態に戻る(歩ける・ジャンプできる。Issue #381)
 *   飛ぶ時は高さも含めてまっすぐ進む(Fly)。張り付いている間は速度を0にする(重力はCS_PlayerHideが切る)
 */
// ========================================

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(CS_PlayerHealth))]
[RequireComponent(typeof(CS_PlayerStats))]
[RequireComponent(typeof(CS_PlayerTransformation))]  // 警察に見つかるためのタグを付ける
public class CS_Player : NetworkBehaviour
{
    [Header("移動")]
    [SerializeField] private float _rotationSpeed = 720f;   // 進む方向へ体が回る速さ(度/秒)

    [Header("移動の手触り(0にすると、その項目は以前の動きに戻る)")]
    [SerializeField][Min(0f)] private float _groundAcceleration = 60f;   // 地上で、入力の速さへ近づく加速度(m/秒²)。0ならすぐ最高速
    [SerializeField][Min(0f)] private float _groundDeceleration = 80f;   // 地上で、止まる・遅くなる時の減速度(m/秒²)。0ならすぐ止まる
    [SerializeField][Min(0f)] private float _airAcceleration = 40f;      // 空中で、入力の速さへ近づく加速度(m/秒²)。0ならすぐ入力の速さ
    [SerializeField] private bool _followSlope = true;                   // 坂では地面に沿って動き、止まったらその場に止まる(下り坂で跳ねない・滑らない)
    [SerializeField][Min(0f)] private float _stepDownHeight = 0.4f;      // この高さまでの段差は、下りる時に宙に浮かず、下の地面へ足を吸い付ける(m)。0で吸い付けない
    [SerializeField] private bool _suppressStepBounce = true;            // ジャンプしていないのに付いた上向きの勢い(段差の角で押し上げられた分)を消す(走って段差を登った時に跳ねないように)
    [SerializeField][Min(0f)] private float _coyoteTime = 0.12f;         // 足場から出た直後でも、ジャンプできる猶予(秒)
    [SerializeField][Min(0f)] private float _jumpBufferTime = 0.12f;     // 着地の少し前に押したジャンプを覚えておく時間(秒)
    [SerializeField][Min(0f)] private float _cameraVerticalSmoothTime = 0.1f;   // カメラが上下にプレイヤーを追う遅れ(秒。ジャンプ・段差でカメラがカクッと動かないように)
    [SerializeField][Min(0f)] private float _cameraMaxVerticalLag = 0.5f;         // カメラが上下に遅れてよい最大の高さ(m。速く落ちる時も、これ以上は離れずに付いていく)

    [Header("空中の勢い(SAで放した後。減速率はSAポイントごと。CS_GrappleSAPointの_releaseDrag)")]
    [SerializeField][Min(0f)] private float _airControlAccel = 25f; // 勢いがある間、入力の移動速度へ近づく加速度(m/秒²)。大きいほど入力で曲げやすい

    [Header("ジャンプ")]
    [SerializeField] private LayerMask _groundLayers = ~0;      // 地面と判定するレイヤー
    [SerializeField] private float _groundCheckDistance = 0.15f;  // 接地判定用の余白

    [Header("落下(ふわっとしないよう、落ちている間だけ重力を強くする)")]
    [SerializeField][Min(1f)] private float _fallGravityMultiplier = 2f;   // 落下中の重力の倍率(1で通常の重力のまま)。上昇中・ジャンプの高さには影響しない
    [SerializeField][Min(0f)] private float _maxFallSpeed = 30f;           // 落下速度の上限(m/秒)。0なら上限なし

    [Header("ダッシュ")]
    [SerializeField] private float _dashDuration = 0.2f;    // ダッシュが続く時間(秒)
    [SerializeField] private float _dashCooldown = 0.8f;    // 次に出せるようになるまでの時間(秒)

    [Header("SE")]
    [SerializeField][Min(0.1f)] private float _footstepDistance = 4.4f;   // 足音を鳴らす、歩いた距離の間隔(m)
    [SerializeField][Min(0.1f)] private float _runFootstepDistance = 3.8f;    // 走っている時の、足音を鳴らす距離の間隔(m)
    [SerializeField][Min(1f)] private float _runFootstepSpeedRate = 1.1f;    // 歩く速さのこの倍率より速く動いていたら、走っているとみなす
    [SerializeField][Min(0f)] private float _landingMinAirTime = 0.25f;   // これ以上空中にいてから着地した時だけ、着地音を鳴らす(秒)
    [SerializeField] private bool _playSprintSe = false;   // スプリントに入った瞬間にダッシュのSE(PlayerDash)を鳴らすか(ブリンクの音と二重になるため既定オフ)

    [Header("落下の保険")]
    [SerializeField] private float _fallResetHeight = -30f; // これより下に落ちたら、スポーンした位置へ戻す(y座標)

    [Header("カメラ")]
    [SerializeField] private Transform _cameraTransform;    // プレイヤーの子のカメラ(CinemachineBrainが付いている)
    [SerializeField] private Transform _virtualCameraTransform;  // CinemachineBrainが見るCinemachineCamera(空なら_cameraTransformを直接動かす)
    [SerializeField] private float _cameraDistance = 3.5f;
    [SerializeField] private float _cameraPivotHeight = 1.3f;
    [SerializeField] private float _minPitch = -30f;
    [SerializeField] private float _maxPitch = 60f;

    [Header("視点感度")]
    [SerializeField] private float _mouseSensitivity = 0.1f;    // マウス移動量(ピクセル)に対する回転量
    [SerializeField] private float _stickSensitivity = 180f;    // スティック全開時の回転速度(度/秒)

    // 視点の重さ(演出用。連続奇襲のスローの間、CS_PlayerAmbushEffectがSetLookWeightで重くする)
    private float _lookScale = 1f;        // 視点の回転量の倍率(1で普段どおり)
    private float _lookSmoothTime;        // 視点の回転に付ける慣性の時間(秒。0で慣性なし)
    private Vector2 _smoothedLook;        // 慣性を付けた、1フレームの回転量

    // 視点を向ける先(演出用。連続奇襲のスローの間、CS_PlayerAmbushEffectがSetLookTargetで次の奇襲対象を指定する)
    private Transform _lookTarget;        // 向ける先(nullなら向けない)
    private float _lookTargetSpeed;       // 向ける速さ(1秒あたりに近づく割合の目安)
    private float _lookTargetHeight;      // 向ける先の足元からの高さ(m)
    private float _lookTargetPitchOffset; // 向ける先より、これだけ見下ろす(度。自分のプレイヤーが画面から外れないように)

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。CS_PlayerSpawnerがスポーン前に割り当てる
    private readonly NetworkVariable<int> _playerNumber = new NetworkVariable<int>();
    // 書き込みはサーバーのみ。CS_PlayerSpawnerがスポーン前に印を付ける
    private readonly NetworkVariable<bool> _isNpc = new NetworkVariable<bool>();

    private Rigidbody _rigidbody;
    private CapsuleCollider _collider;
    private CS_PlayerHealth _health;
    private CS_PlayerStats _stats;
    private CS_PlayerStun _stun;    // スタン(パリーされた時など)。付いていなければスタンしない
    private CS_PlayerHide _hide;    // SA(ハイドポイントへ飛ぶ)。付いていなければSAしない
    private CS_PlayerRideSAPoint _ride;   // 動く足場に乗る処理。付いていれば、足場が運んだ速度を加速の計算から除く
    private CS_PlayerIntroCamera _introCamera;    // スタート時のカメラの演出。付いていなければ演出しない
    private NetworkTransform _networkTransform;

    private Vector3 _spawnPosition;     // 操作を始めた時の位置と向き(落ちた時に戻す先)
    private Quaternion _spawnRotation;

    private InputAction _moveAction;
    private InputAction _mouseLookAction;
    private InputAction _stickLookAction;
    private InputAction _specialModifierAction;  // 必殺技のゲームパッド用コード(RT+LT)のLT側。攻撃(RT)との同時押しを判別するために使う
    private CS_PlayerButton _attackButton;
    private CS_PlayerButton _jumpButton;
    private CS_PlayerButton _specialButton;
    private CS_PlayerButton _dashButton;        // ダッシュボタン
    private CS_PlayerButton _useItemButton;     // アイテム使用ボタン(CS_PlayerItemSlotが使う)
    private CS_PlayerButton _scanButton;        // スキャンボタン(CS_PlayerScanが使う)

    private CS_PlayerNpcInput _npcInput;    // NPCの時の入力(人が操作する時はnull)

    private bool _isControlled;     // このプレイヤーを自分(このマシン)が操作するか。NPCはサーバーが操作する
    private Vector2 _moveInput;
    private bool _jumpRequested;    // Updateで押下を検知し、FixedUpdateで消費する
    private float _jumpRequestTime;         // ジャンプが押された時刻(_jumpBufferTimeの間だけ覚えておく)
    private float _lastGroundedTime = float.NegativeInfinity;   // 最後に接地していた時刻(_coyoteTimeの判定に使う)
    private bool _hasJumpedSinceGrounded;   // 接地してから、もうジャンプしたか(猶予の間に2回跳ばないように)
    private bool _isGroundedNow;            // この物理フレームで接地しているか(FixedUpdateの最初に1回だけ調べる)
    private bool _wasStanding;              // 前の物理フレームで地面に立っていたか(段差を下りた瞬間を見つけるため)
    private Vector3 _groundNormal = Vector3.up;   // 接地している地面の向き(坂に沿って動くのに使う)
    private float _cameraPivotY;            // カメラが追うプレイヤーの高さ(上下だけ遅れて追う)
    private float _cameraPivotYVelocity;
    private bool _hasCameraPivotY;
    private float _lastCameraTargetY;      // 前のフレームでカメラが追った高さ(ワープを見つけるため)
    private bool _isCarried;                // 運ばれている間か(CS_PlayerTowerCarry。移動・攻撃をせず、位置は運ぶ側が動かす)
    private float _yaw;
    private float _pitch = 11f;

    private bool _isDashing;
    private float _dashElapsed;
    private float _dashCooldownRemaining;
    private Vector3 _dashDirection;
    private bool _hasMovementSeBaseline;    // 足音・着地音の前の状態を覚えたか
    private Vector3 _lastSePosition;        // 足音の距離を数える、前のフレームの位置
    private bool _wasGroundedForSe;         // 前のフレームで接地していたか(着地音用)
    private float _airTime;                 // 空中にいる時間(着地音用)
    private float _footstepDistanceRemaining;   // 次の足音までの距離
    private float _footstepSpeed;               // 足音用の、なめらかにした動く速さ(m/秒。走っているかの判定)
    private bool _isSprinting;      // ダッシュ後、ダッシュボタンを押し続けている間か

    private Transform _viewDirectionAnchor;     // 位置はプレイヤー、向きはカメラの左右の向き。警戒UIの方向の基準(自分のプレイヤーのみ)

    private bool _hasAimFacing;     // エイムアシストで、攻撃する相手の方を向いている間か
    private float _aimFacingYaw;
    private Vector3 _lungeVelocity; // エイムアシストの踏み込みの速度
    private float _lungeRemaining;  // 踏み込みの残り時間(秒)
    private bool _isLungeVertical;  // 踏み込みが高さも含めて進むか(Fly)。falseなら水平だけ進む(Lunge)
    private Vector3 _saVelocity;    // SAで飛んでいる間の専用の速度(CS_PlayerHideが毎物理フレーム決める。Fly/Lungeの残り時間は使わない)
    private Vector3 _airMomentum;   // SAで放した後に残す水平の勢い(m/秒)。入力の速度へ近づく。着地・ダッシュ・踏み込み・倒れる・スタンで0になる(EndSAMotion)
    private float _airMomentumDrag;    // 空中の勢いの減速率(毎秒e^(-減速率)の割合で弱める)。SAで放す時に渡され、勢いが0になれば0に戻る(EndSAMotion)

    public bool isControlled => _isControlled;          // このプレイヤーを自分(このマシン)が操作しているか(NPCはサーバーでtrue)
    public bool canAct => _isControlled && !_isCarried && !_health.isDead && !isStunned && !isPlayingIntro && !CS_PlayerIntroCamera.isMatchHeld;   // 移動・攻撃してよいか(CS_PlayerAttackも参照)
    public bool isPlayingIntro => _introCamera != null && _introCamera.isPlaying;   // スタート時のカメラの演出中か(自分のプレイヤーのみ。この間は動けない)
    public bool isStunned => _stun != null && _stun.isStunned;   // スタン中か(パリーされた時など。死亡中と同じく行動できない)
    public bool isHiding => _hide != null && _hide.isHiding;     // SAでハイドポイントへ飛んでいる最中か(操作しているマシンでだけ正しい)
    public bool isNpc => _isNpc.Value;                  // NPCか(全クライアントで分かる)
    // 自分の画面のプレイヤー(このマシンで人が操作しているプレイヤー)か。HUD・ミニマップの中心などに使う
    // NPCはホストではIsOwnerがtrueになるため、IsOwnerではなくこちらを使う
    public bool isLocalPlayer => !_isNpc.Value && (IsSpawned ? IsOwner : !CS_PlayerNetwork.isOnline);
    public CS_PlayerNpcInput npcInput => _npcInput;     // NPCの入力(CS_PlayerNpcBrainが書き込む。NPCを操作するマシン以外ではnull)
    public CS_PlayerButton attackAction => _attackButton;   // 攻撃ボタン(CS_PlayerAttackが使う)
    public CS_PlayerButton specialAction => _specialButton; // 必殺技ボタン(CS_PlayerSpecialAttackが使う)
    // 必殺技コード(RT+LT)のLT側が押されているか(CS_PlayerAttackが、RT+LT同時押し時に攻撃を誤発動させないため参照する)
    public bool isSpecialModifierHeld => _specialModifierAction != null && _specialModifierAction.IsPressed();
    public CS_PlayerButton dashAction => _dashButton;       // ダッシュボタン
    public float dashDuration => _dashDuration;         // ダッシュが続く時間(秒、見た目のモーション速度合わせに使う)
    public CS_PlayerButton useItemAction => _useItemButton; // アイテム使用ボタン(CS_PlayerItemSlotが使う)
    public CS_PlayerButton scanAction => _scanButton;       // スキャンボタン(CS_PlayerScanが使う)
    public bool isGrounded => IsGrounded();             // 接地しているか(全クライアントで判定できる。見た目用にも使う)
    public bool isSprinting => _isSprinting;            // ダッシュ後、ボタンを押し続けて速くなっているか(見た目用)
    public bool isDashing => _isDashing;                // ダッシュ(ブリンク)で高速移動している最中か(見た目用。操作しているマシンでだけ正しい)
    public int playerNumber => _playerNumber.Value;     // 何番目のプレイヤーか(0〜3。HP UIなど画面上の表示先を決めるのに使う)
    public Camera playerCamera => _cameraTransform != null ? _cameraTransform.GetComponent<Camera>() : null;   // このプレイヤーのカメラ(他人のプレイヤーでは無効化されている)
    public float viewYaw => _yaw;                       // カメラの左右の向き(度)。エイムアシストが「狙っている方向」として使う
    public Vector3 moveDirection => GetMoveDirection();  // カメラから見た移動入力(スティック)の方向(水平、長さは入力の強さ。最大1)。エイムアシストが使う
    public Vector2 moveInput => _moveInput;              // 移動入力そのもの(x: 左右, y: 前後。-1〜1)。スタン中のコマンド入力(CS_PlayerStunEscape)が使う
    // 視点(カメラ)のTransform。揺れが乗る前のCinemachineCamera(無ければ実際のカメラ)。ミニマップの回転の基準に使う
    public Transform viewTransform => _virtualCameraTransform != null ? _virtualCameraTransform : _cameraTransform;

    // 何番目のプレイヤーかを割り当てる(CS_PlayerSpawnerが、スポーンする前に呼ぶ)
    public void AssignPlayerNumber(int number)
    {
        if (!this.IsServerOrOffline()) return;

        _playerNumber.Value = number;
    }

    // NPCとして印を付ける(CS_PlayerSpawnerが、スポーンする前に呼ぶ。オフラインではStartより前に呼ぶ)
    public void MarkAsNpc()
    {
        if (IsSpawned) return;

        _isNpc.Value = true;
    }

    // 自分の画面のプレイヤー(このマシンで人が操作しているプレイヤー)。いなければnull
    // 警察の警戒UI(CS_PoliceDetectionSource)が、「自分への信号か」を判定するのに使う
    public static CS_Player localPlayer { get; private set; }

    // 自分の画面のプレイヤーが決まった(出現した)時と、いなくなった(nullで渡される)時に発生する
    // 警察がプレイヤーより先に出現している場合に、警察側が表示を判定し直すために購読する
    public static event Action<CS_Player> onLocalPlayerSet;

    // 出現中のプレイヤー(人・NPC。全クライアント・オフラインで持つ)。NPCの判断などが、他のプレイヤーを探すのに使う
    // (以前は手配度(CS_PlayerWantedLevel)が持っていたが、手配度が無くなったためこちらへ移した)
    private static readonly List<CS_Player> _activePlayers = new List<CS_Player>();
    public static IReadOnlyList<CS_Player> activePlayers => _activePlayers;

    // 操作しているクライアントでだけ発生する。見た目(CS_PlayerVisual)など、ゲームロジックの外から購読する
    public event Action onJumped;
    public event Action onDashStarted;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();
        _health = GetComponent<CS_PlayerHealth>();
        _stats = GetComponent<CS_PlayerStats>();
        _stun = GetComponent<CS_PlayerStun>();
        _hide = GetComponent<CS_PlayerHide>();
        _ride = GetComponent<CS_PlayerRideSAPoint>();
        _introCamera = GetComponent<CS_PlayerIntroCamera>();
        _networkTransform = GetComponent<NetworkTransform>();

        // 壁などとの衝突で回転しないよう、物理による回転はすべて止める
        // (向きはUpdateFacingのMoveRotationでのみ変える)
        _rigidbody.freezeRotation = true;
    }

    private void OnEnable()
    {
        if (!_activePlayers.Contains(this)) _activePlayers.Add(this);
    }

    private void OnDisable()
    {
        _activePlayers.Remove(this);
    }

    // Domain Reloadオフ対策(再生のたびに一覧を空にする)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activePlayers.Clear();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (_isControlled || IsSpawned) return;
        if (CS_PlayerNetwork.isOnline) return;

        if (_isNpc.Value)
        {
            SetupAsNpc();
            return;
        }

        SetupAsLocalPlayer();
    }

    public override void OnNetworkSpawn()
    {
        // NPCはサーバーが操作する。それ以外のクライアントでは他人のプレイヤーと同じ扱い
        if (_isNpc.Value)
        {
            if (IsServer)
            {
                SetupAsNpc();
            }
            else
            {
                SetupAsRemotePlayer();
            }
            return;
        }

        // 他人のプレイヤーは入力もカメラも不要
        if (!IsOwner)
        {
            SetupAsRemotePlayer();
            return;
        }

        SetupAsLocalPlayer();
    }

    public override void OnNetworkDespawn()
    {
        ReleaseLocalPlayer();
    }

    public override void OnDestroy()
    {
        ReleaseLocalPlayer();
        base.OnDestroy();
    }

    private void Update()
    {
        if (!_isControlled) return;

        ReadInput();

        // ジャンプ・ダッシュは死亡中に予約されても復帰後に発動しないよう、ここでもcanActを見る
        if (canAct && _jumpButton.WasPressedThisFrame())
        {
            // SAで飛んでいる途中なら、飛ぶのをやめてからジャンプする
            if (isHiding) _hide.ExitHide();
            _jumpRequested = true;
            _jumpRequestTime = Time.time;
        }

        // 飛べるハイドポイントがあれば、ダッシュの代わりにそこへ飛ぶ(クールタイム中でも飛べる)
        if (canAct && _dashButton.WasPressedThisFrame())
        {
            if (_hide != null && _hide.TryBeginHide())
            {
                _isDashing = false;
            }
            else if (!isHiding && !_isDashing && _dashCooldownRemaining <= 0f)
            {
                StartDash();
            }
        }

        // ダッシュ中でない間にダッシュボタンを押し続けていればスプリント(ZZZのような挙動。クールタイムとは無関係)
        bool wasSprinting = _isSprinting;
        _isSprinting = canAct && !isHiding && !_isDashing && _dashButton.IsPressed();

        // スプリントに入った瞬間(ブリンクのあとも押し続けて、速く走り始めた時)
        if (_playSprintSe && _isSprinting && !wasSprinting)
        {
            CS_NetworkSoundPlayer.instance?.PlaySeForEveryone(CSE_SeId.PlayerDash, transform.position);
        }
    }

    private void FixedUpdate()
    {
        if (!_isControlled) return;
        if (_isCarried) return;   // 運ばれている間は、位置を運ぶ側(CS_PlayerTowerCarry)が動かす

        // 床の下へ落ちてしまったら、スポーンした位置へ戻す
        if (ReturnToSpawnIfFallen()) return;

        // 死亡中・スタン中は操作を受け付けず、その時の勢いで滑り続けないよう水平方向を止める(落下は残す)
        if (_health.isDead || isStunned)
        {
            _isDashing = false;
            _jumpRequested = false;
            _lungeRemaining = 0f;
            _airMomentum = Vector3.zero;
            StopHorizontalVelocity();
            return;
        }

        if (_dashCooldownRemaining > 0f)
        {
            _dashCooldownRemaining -= Time.fixedDeltaTime;
        }

        UpdateGrounded();

        // 体の向きを更新する(移動している方向へ向く。カメラを回しただけでは回らない)
        UpdateFacing();

        if (_isDashing)
        {
            UpdateDash();
        }
        else if (_lungeRemaining > 0f)
        {
            UpdateLunge();
        }
        else if (isHiding)
        {
            // SAで飛んでいる間は、CS_PlayerHideが決めたSA専用の速度(_saVelocity)で動く(Move()の速度上書きは通らない)
            _rigidbody.linearVelocity = _saVelocity;
        }
        else
        {
            Move();
            ApplyJump();
            ApplyFallGravity();
        }
    }

    // 空中で落ちている間だけ、重力を_fallGravityMultiplier倍にする(落下がふわっとしないように)
    // 通常の重力(Physics.gravity)に足りない分を足す。落下速度は_maxFallSpeedまで
    private void ApplyFallGravity()
    {
        if (_isGroundedNow || !_rigidbody.useGravity) return;

        Vector3 velocity = _rigidbody.linearVelocity;
        if (velocity.y >= 0f) return;

        float extraGravity = Mathf.Abs(Physics.gravity.y) * (_fallGravityMultiplier - 1f);
        float newY = velocity.y - extraGravity * Time.fixedDeltaTime;
        if (_maxFallSpeed > 0f) newY = Mathf.Max(newY, -_maxFallSpeed);

        _rigidbody.linearVelocity = new Vector3(velocity.x, newY, velocity.z);
    }

    // ========================================
    // エイムアシスト(CS_PlayerAimAssist)から使う
    // ========================================

    // 体の向きをカメラではなく指定した向き(攻撃する相手の方)にする。その場ですぐに向きを変える
    // ClearAimFacing()を呼ぶまで続き、その後は移動キーを押した時に進む方向へ回る
    public void SetAimFacing(float yaw)
    {
        _hasAimFacing = true;
        _aimFacingYaw = yaw;

        // 判定はこのフレーム中に出ることがあるため、物理の更新を待たずに向きを変えておく
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        _rigidbody.rotation = rotation;
        transform.rotation = rotation;
    }

    // 視点の操作を重くする(演出用)。scale: 回転量の倍率 / smoothTime: 回転に付ける慣性の時間(秒)
    public void SetLookWeight(float scale, float smoothTime)
    {
        _lookScale = Mathf.Max(0f, scale);
        _lookSmoothTime = Mathf.Max(0f, smoothTime);
    }

    // 視点の操作の重さを普段どおりに戻す
    public void ClearLookWeight()
    {
        _lookScale = 1f;
        _lookSmoothTime = 0f;
        _smoothedLook = Vector2.zero;
    }

    // 視点をtargetの方へなめらかに向ける(演出用。nullで向けない)。視点の操作も効いたまま
    // speed: 向ける速さ / height: 向ける先の足元からの高さ(m) / pitchOffset: 向ける先より見下ろす角度(度)
    public void SetLookTarget(Transform target, float speed, float height, float pitchOffset)
    {
        _lookTarget = target;
        _lookTargetSpeed = Mathf.Max(0f, speed);
        _lookTargetHeight = height;
        _lookTargetPitchOffset = pitchOffset;
    }

    public void ClearLookTarget()
    {
        _lookTarget = null;
    }

    public void ClearAimFacing()
    {
        _hasAimFacing = false;
    }

    // 運ばれている状態にする・戻す(CS_PlayerTowerCarryが使う)。運ばれている間は移動・攻撃・ジャンプ・ダッシュをしない
    // 戻した時は、ダッシュ・踏み込み・空中の勢い・ジャンプの予約を消し、速度0から動き出す
    public void SetCarried(bool carried)
    {
        _isCarried = carried;
        _isDashing = false;
        _isSprinting = false;
        _lungeRemaining = 0f;
        _airMomentum = Vector3.zero;
        _jumpRequested = false;
        _hasCameraPivotY = false;   // カメラは今の位置からすぐ追う
    }

    // 指定した方向へ短く踏み込む(攻撃が届く距離まで詰める)。ダッシュを始めると打ち切られる
    public void Lunge(Vector3 direction, float distance, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f || duration <= 0f) return;

        _lungeVelocity = direction.normalized * (distance / duration);
        _lungeRemaining = duration;
        _isLungeVertical = false;
        _airMomentum = Vector3.zero;
    }

    // 指定した方向へ、高さも含めてまっすぐ進む(ハイドポイントへ飛ぶ時に使う)。ダッシュを始めると打ち切られる
    public void Fly(Vector3 direction, float distance, float duration)
    {
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f || duration <= 0f) return;

        _lungeVelocity = direction.normalized * (distance / duration);
        _lungeRemaining = duration;
        _isLungeVertical = true;
        _airMomentum = Vector3.zero;
    }

    // ========================================
    // SAモーション(CS_PlayerHideから使う)
    // SAで飛んでいる間は、Fly/Lungeの残り時間とは別の、SA専用の速度(_saVelocity)で動く
    // ========================================

    // SAの飛行を始める。専用の速度を0にし、踏み込み(Fly/Lunge)・空中の勢いを消す。この後はisHidingの間、_saVelocityで動く
    public void BeginSAMotion()
    {
        _saVelocity = Vector3.zero;
        _lungeRemaining = 0f;
        _airMomentum = Vector3.zero;
    }

    // SAで飛んでいる間の速度(向き・高さ込み)を決める。毎物理フレーム呼ぶ
    public void SetSAVelocity(Vector3 velocity)
    {
        _saVelocity = velocity;
    }

    // SAの飛行を終える。専用の速度をクリアすると同時に、終わった時の速度を通常の状態に渡す(0なら速度0)
    // 垂直はRigidbodyにそのまま渡して重力に任せ、水平は空中の勢いとして残す(入力の速度へ近づき、着地で0になる)
    // 呼ぶ前に、CS_PlayerHide側でisHidingをfalseにしておくこと(通常の動きに戻るため)
    public void EndSAMotion(Vector3 exitVelocity, float drag = 0f)
    {
        _saVelocity = Vector3.zero;
        _lungeRemaining = 0f;   // 踏み込みの残り時間を取り残さない(残ると、その間速度が上書きされ、切れた時に速度が0になる)
        _rigidbody.linearVelocity = exitVelocity;
        _airMomentum = new Vector3(exitVelocity.x, 0f, exitVelocity.z);
        _airMomentumDrag = Mathf.Max(0f, drag);
        if (exitVelocity.y > 0f) _hasJumpedSinceGrounded = true;   // 上向きに放された時は跳んだ扱いにする(段差で跳ねないための、上向きの勢いを消す処理に消されないように)
    }

    // 位置を指定した位置へ合わせ、踏み込みと速度を止める(SAでハイドポイントに着いた時に使う)
    public void SnapPosition(Vector3 position)
    {
        _lungeRemaining = 0f;
        _airMomentum = Vector3.zero;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.position = position;
        transform.position = position;
    }

    // 踏み込み中は、決めた速さで直進する(垂直速度はそのまま)
    private void UpdateLunge()
    {
        _lungeRemaining -= Time.fixedDeltaTime;
        float verticalVelocity = _isLungeVertical ? _lungeVelocity.y : _rigidbody.linearVelocity.y;
        _rigidbody.linearVelocity = new Vector3(_lungeVelocity.x, verticalVelocity, _lungeVelocity.z);

        if (_lungeRemaining <= 0f)
        {
            if (_isLungeVertical) _rigidbody.linearVelocity = Vector3.zero;
            else StopHorizontalVelocity();
        }
    }

    private void LateUpdate()
    {
        UpdateMovementSe();

        if (!_isControlled || _npcInput != null) return;   // NPCはカメラを使わない

        UpdateCameraTransform();
        UpdateViewDirectionAnchor();
    }

    // 足音と着地音を、見えている動きから各クライアントが自分のPCで鳴らす(全員のプレイヤー分。通信しない)
    private void UpdateMovementSe()
    {
        Vector3 position = transform.position;
        bool grounded = IsGrounded();

        if (!_hasMovementSeBaseline)
        {
            _hasMovementSeBaseline = true;
            _lastSePosition = position;
            _wasGroundedForSe = grounded;
            return;
        }

        // 位置が大きく飛んだ時(スポーン・SAの移動・リスポーンなど)は距離に数えない
        Vector3 delta = position - _lastSePosition;
        delta.y = 0f;
        _lastSePosition = position;
        bool isTeleported = delta.magnitude > 3f;

        // 走っているか(速さで判定する。isSprintingは操作している本人の手元にしか無いため、他の人のプレイヤーでも同じになるよう速さを使う)
        if (!isTeleported && Time.deltaTime > 0f) _footstepSpeed = Mathf.Lerp(_footstepSpeed, delta.magnitude / Time.deltaTime, 0.2f);
        bool isRunning = _stats != null && _footstepSpeed > _stats.currentMoveSpeed * _runFootstepSpeedRate;
        float footstepDistance = isRunning ? _runFootstepDistance : _footstepDistance;

        if (grounded)
        {
            if (!_wasGroundedForSe)
            {
                if (_airTime >= _landingMinAirTime) CS_SoundManager.instance?.PlaySe(CSE_SeId.PlayerLanding, position);
                _footstepDistanceRemaining = footstepDistance;
            }
            _airTime = 0f;

            if (!isTeleported)
            {
                _footstepDistanceRemaining = Mathf.Min(_footstepDistanceRemaining, footstepDistance) - delta.magnitude;   // 走り出したら、すぐ走りの間隔にする
                if (_footstepDistanceRemaining <= 0f)
                {
                    CS_SoundManager.instance?.PlaySe(CSE_SeId.PlayerFootstep, position);
                    _footstepDistanceRemaining = footstepDistance;
                }
            }
        }
        else
        {
            _airTime += Time.deltaTime;
        }

        _wasGroundedForSe = grounded;
    }

    // 警戒UIの方向の基準を、プレイヤーの位置とカメラの左右の向きに合わせる
    private void UpdateViewDirectionAnchor()
    {
        if (_viewDirectionAnchor == null) return;

        _viewDirectionAnchor.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, _yaw, 0f));
    }

    // y = _fallResetHeightより下に落ちていたら、スポーンした位置へ戻す。戻したらtrue
    private bool ReturnToSpawnIfFallen()
    {
        if (_rigidbody.position.y >= _fallResetHeight) return false;

        _isDashing = false;
        _jumpRequested = false;
        _lungeRemaining = 0f;
        _rigidbody.linearVelocity = Vector3.zero;

        // NetworkTransformのTeleportで、他の人の画面でも補間せずにその場へ移す(操作している側が位置の権威を持つ)
        if (IsSpawned && _networkTransform != null)
        {
            _networkTransform.Teleport(_spawnPosition, _spawnRotation, transform.localScale);
        }
        else
        {
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        }

        _rigidbody.position = _spawnPosition;
        _rigidbody.rotation = _spawnRotation;
        return true;
    }

    // 落ちた時に戻す先として、今の位置と向きを覚えておく(操作を始める時に呼ぶ)
    private void RememberSpawnPose()
    {
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
    }

    // NPCの初期化(サーバー、またはオフライン)。入力はCS_PlayerNpcInputから読み、カメラは使わない
    private void SetupAsNpc()
    {
        _isControlled = true;
        RememberSpawnPose();

        _npcInput = new CS_PlayerNpcInput();
        _npcInput.yaw = transform.eulerAngles.y;
        _yaw = _npcInput.yaw;

        _attackButton = _npcInput.attack;
        _jumpButton = _npcInput.jump;
        _specialButton = _npcInput.special;
        _dashButton = _npcInput.dash;
        _useItemButton = _npcInput.useItem;
        _scanButton = _npcInput.scan;

        // 人のプレイヤーのカメラと取り合わないよう、カメラは消しておく(物理はこのマシンで動かす)
        DisableCamera();
    }

    // 自分のプレイヤーの初期化(入力の取得)
    // ※ カーソルはCS_ExhibitionRuntimeがonLocalPlayerSetを受けて固定する
    private void SetupAsLocalPlayer()
    {
        _isControlled = true;
        RememberSpawnPose();
        BindInputActions();

        // カメラ追従のガタつきを防ぐ
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // 今向いている方向を視点の初期値にする
        _yaw = transform.eulerAngles.y;

        // スタート時のカメラの演出(プレイヤーの周りを下から上へ螺旋状に回る)
        if (_introCamera != null) _introCamera.Play();

        RegisterAsLocalPlayer();
    }

    // 自分の画面のプレイヤーとして公開し、警戒UIの方向の基準に登録する
    // 体はカメラと別の方向を向くため、カメラの左右の向きを持つ基準(_viewDirectionAnchor)を作って渡す
    // (プレイヤーの子にすると体の回転に引っ張られるので、親は持たせずLateUpdateで追従させる)
    private void RegisterAsLocalPlayer()
    {
        localPlayer = this;

        if (_viewDirectionAnchor == null)
        {
            _viewDirectionAnchor = new GameObject($"{name}_ViewDirectionAnchor").transform;
        }
        UpdateViewDirectionAnchor();

        CS_PoliceDetectionController detection = FindAnyObjectByType<CS_PoliceDetectionController>();
        if (detection != null) detection.SetPlayerTransform(_viewDirectionAnchor);

        onLocalPlayerSet?.Invoke(this);
    }

    // 自分のプレイヤーの後片付け(入力参照の解放)
    // ※ カーソルはCS_ExhibitionRuntimeがonLocalPlayerSetを受けて元に戻す
    // ※ CS_CustomInputActionManagerは共有のシングルトンなので、Disable/Disposeはしない
    private void ReleaseLocalPlayer()
    {
        if (!_isControlled) return;

        _isControlled = false;
        _moveInput = Vector2.zero;
        _jumpRequested = false;
        _isDashing = false;
        _dashCooldownRemaining = 0f;
        _isSprinting = false;
        _lungeRemaining = 0f;
        _hasAimFacing = false;
        UnbindInputActions();

        if (localPlayer == this)
        {
            localPlayer = null;
            onLocalPlayerSet?.Invoke(null);
        }

        if (_viewDirectionAnchor != null)
        {
            Destroy(_viewDirectionAnchor.gameObject);
            _viewDirectionAnchor = null;
        }

        _npcInput = null;
    }

    // 他人のプレイヤーの初期化(物理とカメラを切る)
    private void SetupAsRemotePlayer()
    {
        // 位置はNetworkTransformが更新するので、物理で動かさない
        _rigidbody.isKinematic = true;

        DisableCamera();
    }

    // カメラとAudioListenerが複数有効になるのを防ぐ
    // (CinemachineCameraも消す。残すと、自分のBrainが他人のCinemachineCameraを選んでしまう)
    private void DisableCamera()
    {
        if (_cameraTransform != null)
        {
            _cameraTransform.gameObject.SetActive(false);
        }

        if (_virtualCameraTransform != null)
        {
            _virtualCameraTransform.gameObject.SetActive(false);
        }
    }

    // CS_CustomInputActionManagerが持つ各アクションの参照を取得する
    private void BindInputActions()
    {
        CustomInputAction.PlayerActions player = CS_CustomInputActionManager.instance.customInputAction.Player;

        _moveAction = player.Move;
        _mouseLookAction = player.Look;
        _stickLookAction = player.LookStick;
        _specialModifierAction = player.SpecialModifier;
        _attackButton = new CS_PlayerButton(player.Attack);
        _jumpButton = new CS_PlayerButton(player.Jump);
        _specialButton = new CS_PlayerButton(player.Special);
        _dashButton = new CS_PlayerButton(player.Dash);
        _useItemButton = new CS_PlayerButton(player.UseItem);
        _scanButton = new CS_PlayerButton(player.Scan);
    }

    // アクションへの参照を外す(アクション自体は共有のシングルトンが持ち続ける)
    private void UnbindInputActions()
    {
        _moveAction = null;
        _mouseLookAction = null;
        _stickLookAction = null;
        _specialModifierAction = null;
        _attackButton = null;
        _jumpButton = null;
        _specialButton = null;
        _dashButton = null;
        _useItemButton = null;
        _scanButton = null;
    }

    // 入力を読み取り、移動入力と視点(yaw/pitch)を更新する
    private void ReadInput()
    {
        // NPCは判断(CS_PlayerNpcBrain)が書き込んだ値をそのまま使う
        if (_npcInput != null)
        {
            _moveInput = Vector2.ClampMagnitude(_npcInput.move, 1f);
            _yaw = _npcInput.yaw;
            return;
        }

        // スタート時のカメラの演出中は、移動も視点の操作も受け付けない(演出の終わりの位置がずれないように)
        if (isPlayingIntro)
        {
            _moveInput = Vector2.zero;
            return;
        }

        _moveInput = _moveAction.ReadValue<Vector2>();

        // マウスは「1フレームの移動量」なのでdeltaTimeを掛けない
        // スティックは「傾き」なので速度として扱いdeltaTimeを掛ける
        // スティックはスロー(Time.timeScale)の影響を受けない時間で数える(スロー中もマウスと同じく普段の速さで回せる。重さはSetLookWeightで決める)
        // 時間が止まっている時(Time.timeScale = 0)は回さない
        float stickDeltaTime = Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
        Vector2 mouseLook = _mouseLookAction.ReadValue<Vector2>() * _mouseSensitivity;
        Vector2 stickLook = _stickLookAction.ReadValue<Vector2>() * _stickSensitivity * stickDeltaTime;
        Vector2 look = (mouseLook + stickLook) * _lookScale;
        if (_lookSmoothTime > 0f)
        {
            // 回転に慣性を付ける(動き出し・止まりが遅れる)
            float rate = 1f - Mathf.Exp(-Time.unscaledDeltaTime / _lookSmoothTime);
            _smoothedLook = Vector2.Lerp(_smoothedLook, look, rate);
            look = _smoothedLook;
        }

        // マルチで全員のスタート時の演出が終わるまでは、動けない(視点は動かせる)
        if (CS_PlayerIntroCamera.isMatchHeld) _moveInput = Vector2.zero;

        _yaw += look.x;
        _pitch = Mathf.Clamp(_pitch - look.y, _minPitch, _maxPitch);
        UpdateLookTarget();
    }

    // 向ける先(SetLookTarget)があれば、視点をそちらへなめらかに向ける(Time.timeScaleの影響を受けない時間で動かす)
    private void UpdateLookTarget()
    {
        if (_lookTarget == null) return;

        Vector3 pivot = transform.position + Vector3.up * _cameraPivotHeight;
        Vector3 direction = _lookTarget.position + Vector3.up * _lookTargetHeight - pivot;
        float horizontal = new Vector2(direction.x, direction.z).magnitude;
        if (horizontal < 0.01f) return;

        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float targetPitch = -Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg + _lookTargetPitchOffset;
        float rate = 1f - Mathf.Exp(-_lookTargetSpeed * Time.unscaledDeltaTime);
        _yaw = Mathf.LerpAngle(_yaw, targetYaw, rate);
        _pitch = Mathf.Clamp(Mathf.Lerp(_pitch, targetPitch, rate), _minPitch, _maxPitch);
    }

    // 体の向きを更新する
    // ・エイムアシスト中(SetAimFacing): 攻撃する相手の方を向いたまま
    // ・ダッシュ中: ダッシュの方向へ
    // ・移動入力がある間: 進む方向(カメラから見た入力の方向)へ徐々に回る
    // ・移動入力が無い間: 人はそのまま(カメラを回しても体は回らない)。NPCは指定された向き(yaw)へ回る
    //   (NPCは止まって相手の方を向く動きに、向きの指定を使っているため)
    private void UpdateFacing()
    {
        if (_hasAimFacing)
        {
            _rigidbody.MoveRotation(Quaternion.Euler(0f, _aimFacingYaw, 0f));
            return;
        }

        Vector3 direction;
        if (_isDashing)
        {
            direction = _dashDirection;
        }
        else if (_moveInput != Vector2.zero)
        {
            direction = GetMoveDirection();
        }
        else if (_npcInput != null)
        {
            direction = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
        }
        else
        {
            return;
        }

        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z));
        Quaternion next = Quaternion.RotateTowards(
            _rigidbody.rotation, target, _rotationSpeed * Time.fixedDeltaTime);
        _rigidbody.MoveRotation(next);
    }

    // カメラから見た移動入力の方向(水平、長さは入力の強さ。最大1)
    private Vector3 GetMoveDirection()
    {
        Quaternion cameraYaw = Quaternion.Euler(0f, _yaw, 0f);
        return Vector3.ClampMagnitude(cameraYaw * new Vector3(_moveInput.x, 0f, _moveInput.y), 1f);
    }

    // カメラの向き基準で移動する。押した方向へすぐ動き出し、体の向きは回りながら追いつく(UpdateFacing)
    private void Move()
    {
        // SAで放した後の勢い(_airMomentum)がある間は、入力の移動に足す。勢いが0の時は、下の今まで通りの動き
        if (_airMomentum != Vector3.zero)
        {
            MoveWithAirMomentum();
            return;
        }
        _airMomentumDrag = 0f;   // 勢いが無い間は、減速率も0に戻す

        // 入力の速さ(スティックを少し倒せばゆっくり、入力なしなら0)へ、加速度・減速度で近づける
        float speed = _isSprinting ? _stats.currentSprintSpeed : _stats.currentMoveSpeed;
        Vector3 target = _moveInput == Vector2.zero ? Vector3.zero : GetMoveDirection() * speed;
        Vector3 velocity = _rigidbody.linearVelocity;
        Vector3 horizontal = ApproachHorizontal(GetOwnHorizontalVelocity(velocity), target);

        // 坂では地面に沿って動く(上り・下りで地面から離れない)。止まっている時はその場に止める(摩擦0なので、放っておくと滑る)
        if (_followSlope && IsStandingOnGround(velocity.y))
        {
            _rigidbody.linearVelocity = horizontal.sqrMagnitude < 0.0001f
                ? Vector3.zero
                : Vector3.ProjectOnPlane(horizontal, _groundNormal).normalized * horizontal.magnitude;
            return;
        }

        _rigidbody.linearVelocity = new Vector3(horizontal.x, SuppressStepBounce(GetStepDownVelocity(velocity.y)), horizontal.z);
    }

    // ジャンプしていないのに上へ動いている時(段差の角に当たって押し上げられた時)は、上向きの勢いを消す
    // 段差の上へ登る分は当たり判定が押し上げてくれるので、残った勢いだけを消す(消さないと、登った勢いのまま段差の上へ飛び出して跳ねる)
    private float SuppressStepBounce(float verticalVelocity)
    {
        if (!_suppressStepBounce || verticalVelocity <= 0f) return verticalVelocity;
        if (_hasJumpedSinceGrounded || _jumpRequested) return verticalVelocity;   // ジャンプの上昇は消さない

        return 0f;
    }

    // 低い段差を下りた瞬間(前の物理フレームでは立っていて、今は浮いている)は、下の地面まで1物理フレームで下りる速度にする
    // (宙に浮いて落ちる間がなくなり、空中のモーションにもならない)。下の地面が_stepDownHeightより遠ければ、普通に落ちる
    private float GetStepDownVelocity(float verticalVelocity)
    {
        if (_stepDownHeight <= 0f || _isGroundedNow || !_wasStanding || verticalVelocity > 0.1f || _jumpRequested) return verticalVelocity;

        Vector3 origin = _collider.bounds.center;
        float footDistance = _collider.bounds.extents.y;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, footDistance + _stepDownHeight, _groundLayers, QueryTriggerInteraction.Ignore)) return verticalVelocity;

        float gap = hit.distance - footDistance;
        return Mathf.Min(verticalVelocity, -gap / Time.fixedDeltaTime);
    }

    // 自分で動いている分の水平の速度)(動く足場に乗っている時は、前の物理フレームで足場が足した速度を除く。除かないと加速の計算で増え続ける)
    private Vector3 GetOwnHorizontalVelocity(Vector3 velocity)
    {
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        if (_ride == null || !_ride.isActiveAndEnabled || !isLocalPlayer) return horizontal;   // 足場の速度を足すのは自分のプレイヤーだけ

        Vector3 carried = _ride.platformVelocity;
        return horizontal - new Vector3(carried.x, 0f, carried.z);
    }

    // 今の水平の速度を、目標の速度へ加速度・減速度で近づける(0の項目は、すぐ目標の速度にする)
    // 地上: 速くなる時は_groundAcceleration、止まる・遅くなる時は_groundDeceleration。空中: _airAcceleration
    private Vector3 ApproachHorizontal(Vector3 current, Vector3 target)
    {
        float rate;
        if (!_isGroundedNow) rate = _airAcceleration;
        else rate = target.sqrMagnitude < current.sqrMagnitude ? _groundDeceleration : _groundAcceleration;

        return rate > 0f ? Vector3.MoveTowards(current, target, rate * Time.fixedDeltaTime) : target;
    }

    // 地面に立っているか(接地していて、跳び上がっている途中でない)
    private bool IsStandingOnGround(float verticalVelocity)
    {
        return _isGroundedNow && verticalVelocity <= 0.1f && !_jumpRequested;
    }

    // SAで放した後の勢いで動く。入力がある間は、水平の速度が入力の移動速度へ_airControlAccelで近づく(曲げられる)
    // 入力の有無にかかわらず、SAポイントごとの減速率(_airMomentumDrag。毎秒e^(-減速率)の割合)で弱まる(0なら勢いはそのまま続く)。着地(接地かつ垂直速度が0以下)で勢いを0にして、通常の動きに戻る
    private void MoveWithAirMomentum()
    {
        float verticalVelocity = _rigidbody.linearVelocity.y;
        if (verticalVelocity <= 0f && IsGrounded())
        {
            _airMomentum = Vector3.zero;
            return;   // 次の物理フレームから、今まで通りの動きに戻る(この1フレームの水平速度は変えない)
        }

        float deltaTime = Time.fixedDeltaTime;
        if (_moveInput != Vector2.zero)
        {
            float speed = _isSprinting ? _stats.currentSprintSpeed : _stats.currentMoveSpeed;
            Vector3 inputVelocity = GetMoveDirection() * speed;
            _airMomentum = Vector3.MoveTowards(_airMomentum, inputVelocity, _airControlAccel * deltaTime);
        }

        if (_airMomentumDrag > 0f) _airMomentum *= Mathf.Exp(-_airMomentumDrag * deltaTime);
        if (_airMomentum.sqrMagnitude < 0.0001f) _airMomentum = Vector3.zero;   // ほぼ止まったら、勢いを0にして通常の動きに戻る

        _rigidbody.linearVelocity = new Vector3(_airMomentum.x, verticalVelocity, _airMomentum.z);
    }

    // 水平方向の速度だけを0にする(Y速度はそのまま残す)
    private void StopHorizontalVelocity()
    {
        _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
    }

    // ジャンプ予約を消費し、跳べれば真上に飛ばす
    // 跳べる: 接地している、または足場から出て_coyoteTime秒以内(まだ跳んでいない)
    // 跳べない時は、押してから_jumpBufferTime秒の間だけ予約を残す(着地の少し前に押しても跳べるように)
    private void ApplyJump()
    {
        if (!_jumpRequested) return;

        bool canJump = _isGroundedNow || (!_hasJumpedSinceGrounded && Time.time - _lastGroundedTime <= _coyoteTime);
        if (!canJump)
        {
            if (Time.time - _jumpRequestTime > _jumpBufferTime) _jumpRequested = false;
            return;
        }

        _jumpRequested = false;
        _hasJumpedSinceGrounded = true;
        _lastGroundedTime = float.NegativeInfinity;

        Vector3 velocity = _rigidbody.linearVelocity;
        _rigidbody.linearVelocity = new Vector3(velocity.x, _stats.currentJumpPower, velocity.z);
        onJumped?.Invoke();
        CS_NetworkSoundPlayer.instance?.PlaySeForEveryone(CSE_SeId.PlayerJump, transform.position);
    }

    // この物理フレームの接地の状態を調べる(FixedUpdateの最初に1回)
    private void UpdateGrounded()
    {
        _wasStanding = _isGroundedNow && _rigidbody.linearVelocity.y <= 0.1f;
        _isGroundedNow = TryGetGround(out RaycastHit hit);
        _groundNormal = _isGroundedNow ? hit.normal : Vector3.up;
        if (!_isGroundedNow || _rigidbody.linearVelocity.y > 0.1f) return;

        // 着地している(跳び上がっている途中でない)間は、猶予の基準の時刻を更新し、また跳べるようにする
        _lastGroundedTime = Time.time;
        _hasJumpedSinceGrounded = false;
    }

    // ダッシュを開始する(方向をこの時点で決めて固定する)
    private void StartDash()
    {
        _isDashing = true;
        _airMomentum = Vector3.zero;
        _lungeRemaining = 0f;   // ダッシュ(回避)を優先し、エイムアシストの踏み込みは打ち切る
        _dashElapsed = 0f;
        _dashCooldownRemaining = _dashCooldown;
        _dashDirection = CalculateDashDirection();
        onDashStarted?.Invoke();
        CS_NetworkSoundPlayer.instance?.PlaySeForEveryone(CSE_SeId.PlayerBlink, transform.position);   // Shiftを押した瞬間のブリンク回避
    }

    // 移動入力があればその方向、無ければ現在向いている方向をダッシュ方向にする
    private Vector3 CalculateDashDirection()
    {
        if (_moveInput != Vector2.zero)
        {
            Quaternion cameraYaw = Quaternion.Euler(0f, _yaw, 0f);
            return (cameraYaw * new Vector3(_moveInput.x, 0f, _moveInput.y)).normalized;
        }

        return _rigidbody.rotation * Vector3.forward;
    }

    // ダッシュ方向へ一定時間だけ直進する(垂直速度はそのまま)
    private void UpdateDash()
    {
        _dashElapsed += Time.fixedDeltaTime;

        Vector3 velocity = _rigidbody.linearVelocity;
        Vector3 horizontal = _dashDirection * _stats.dashSpeed;
        _rigidbody.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);

        if (_dashElapsed >= _dashDuration)
        {
            _isDashing = false;
        }
    }

    // カプセルの底から下方向にレイを飛ばして接地しているか調べる
    private bool IsGrounded()
    {
        return TryGetGround(out _);
    }

    private bool TryGetGround(out RaycastHit hit)
    {
        Vector3 origin = _collider.bounds.center;
        float rayLength = _collider.bounds.extents.y + _groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, out hit, rayLength, _groundLayers, QueryTriggerInteraction.Ignore);
    }

    // カメラをプレイヤーの周りに配置する(プレイヤーの回転の影響を受けない)
    // CinemachineCameraがあれば、そちらを動かす(Brainが実際のカメラへ反映し、揺れなどの演出も加える)
    private void UpdateCameraTransform()
    {
        Transform target = _virtualCameraTransform != null ? _virtualCameraTransform : _cameraTransform;
        if (target == null) return;

        // スタート時のカメラの演出中は、演出の位置に置く(終わるといつもの位置にぴったり重なる)
        if (_introCamera != null && _introCamera.TryGetPose(_yaw, _pitch, _cameraDistance, _cameraPivotHeight, out Vector3 introPosition, out Quaternion introRotation))
        {
            target.SetPositionAndRotation(introPosition, introRotation);
            return;
        }

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pivot = transform.position + Vector3.up * _cameraPivotHeight;
        pivot.y = SmoothCameraPivotY(pivot.y);
        Vector3 position = pivot - rotation * Vector3.forward * _cameraDistance;

        target.SetPositionAndRotation(position, rotation);
    }

    // カメラが追う高さを、上下だけ少し遅らせる(左右・前後はぴったり追う)
    // 遅れは_cameraMaxVerticalLag(m)までにする(高い所から速く落ちる時も、離れすぎずになめらかに付いていく)
    // 1フレームで大きく動いた時(復活・落下の戻し・ワープなど)と、SAで飛んでいる間は、遅れずにすぐ合わせる
    // (以前は「3m以上離れたらすぐ合わせる」だったため、タワーから落ちる速さだと、遅れて離れる・一気に追いつくを繰り返してガタガタしていた)
    private float SmoothCameraPivotY(float targetY)
    {
        const float teleportDistance = 5f;   // 1フレームでこれ以上動いたら、ワープとみなしてすぐ合わせる
        bool isFlying = isHiding || (_lungeRemaining > 0f && _isLungeVertical);   // SAで飛んでいる間は、遅らせない(高さが大きく変わるため)
        bool isTeleported = _hasCameraPivotY && Mathf.Abs(targetY - _lastCameraTargetY) > teleportDistance;
        _lastCameraTargetY = targetY;
        if (_cameraVerticalSmoothTime <= 0f || isFlying || !_hasCameraPivotY || isTeleported)
        {
            _hasCameraPivotY = true;
            _cameraPivotY = targetY;
            _cameraPivotYVelocity = 0f;
            return targetY;
        }

        _cameraPivotY = Mathf.SmoothDamp(_cameraPivotY, targetY, ref _cameraPivotYVelocity, _cameraVerticalSmoothTime);
        _cameraPivotY = Mathf.Clamp(_cameraPivotY, targetY - _cameraMaxVerticalLag, targetY + _cameraMaxVerticalLag);   // 離れすぎない
        return _cameraPivotY;
    }
}
