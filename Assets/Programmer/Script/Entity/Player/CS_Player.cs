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
 * ・死亡中(CS_PlayerHealth.isDead)は移動・攻撃を行わない(canActで判定)
 *   視点操作(カメラ)は死亡中も継続する
 *   死亡中は水平方向の速度を毎回0にし、死亡時の勢いで滑り続けないようにしている(落下はする)
 * ・移動速度はCS_PlayerStats.currentMoveSpeed(暗躍ボーナス込み)を使う(実際の変更はCS_PlayerStats側で行う)
 * ・Colliderには摩擦0の物理マテリアル(PlayerFrictionless)を設定している
 *   (摩擦があると、空中で壁に向かって移動し続けた時に壁に張り付いて落ちなくなるため)
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
 *   張り付いている間のダッシュボタンは、次のハイドポイントへ飛び移る(無ければ何もしない)
 *   ハイド中(飛んでいる・張り付いている)は、ダッシュ・スプリント・通常の移動をしない。ジャンプボタンでハイドを抜けてジャンプする
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

    [Header("ジャンプ")]
    [SerializeField] private LayerMask _groundLayers = ~0;      // 地面と判定するレイヤー
    [SerializeField] private float _groundCheckDistance = 0.15f;  // 接地判定用の余白

    [Header("ダッシュ")]
    [SerializeField] private float _dashDuration = 0.2f;    // ダッシュが続く時間(秒)
    [SerializeField] private float _dashCooldown = 0.8f;    // 次に出せるようになるまでの時間(秒)

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

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。CS_PlayerSpawnerがスポーン前に割り当てる
    private readonly NetworkVariable<int> _playerNumber = new NetworkVariable<int>();
    // 書き込みはサーバーのみ。CS_PlayerSpawnerがスポーン前に印を付ける
    private readonly NetworkVariable<bool> _isNpc = new NetworkVariable<bool>();

    private Rigidbody _rigidbody;
    private CapsuleCollider _collider;
    private CS_PlayerHealth _health;
    private CS_PlayerStats _stats;
    private CS_PlayerStun _stun;    // スタン(パリーされた時など)。付いていなければスタンしない
    private CS_PlayerHide _hide;    // ハイド(ハイドポイントに張り付いて隠れる)。付いていなければハイドしない
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
    private float _yaw;
    private float _pitch = 11f;

    private bool _isDashing;
    private float _dashElapsed;
    private float _dashCooldownRemaining;
    private Vector3 _dashDirection;
    private bool _isSprinting;      // ダッシュ後、ダッシュボタンを押し続けている間か

    private Transform _viewDirectionAnchor;     // 位置はプレイヤー、向きはカメラの左右の向き。警戒UIの方向の基準(自分のプレイヤーのみ)

    private bool _hasAimFacing;     // エイムアシストで、攻撃する相手の方を向いている間か
    private float _aimFacingYaw;
    private Vector3 _lungeVelocity; // エイムアシストの踏み込みの速度
    private float _lungeRemaining;  // 踏み込みの残り時間(秒)
    private bool _isLungeVertical;  // 踏み込みが高さも含めて進むか(Fly)。falseなら水平だけ進む(Lunge)

    public bool isControlled => _isControlled;          // このプレイヤーを自分(このマシン)が操作しているか(NPCはサーバーでtrue)
    public bool canAct => _isControlled && !_health.isDead && !isStunned;   // 移動・攻撃してよいか(CS_PlayerAttackも参照)
    public bool isStunned => _stun != null && _stun.isStunned;   // スタン中か(パリーされた時など。死亡中と同じく行動できない)
    public bool isHiding => _hide != null && _hide.isHiding;     // ハイドポイントへ飛んでいる・張り付いている最中か(操作しているマシンでだけ正しい)
    public bool isNpc => _isNpc.Value;                  // NPCか(全クライアントで分かる)
    // 自分の画面のプレイヤー(このマシンで人が操作しているプレイヤー)か。HUD・ミニマップの中心などに使う
    // NPCはホストではIsOwnerがtrueになるため、IsOwnerではなくこちらを使う
    public bool isLocalPlayer => !_isNpc.Value && (IsSpawned ? IsOwner : !IsOnline());
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
        if (IsSpawned && !IsServer) return;

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
        if (IsOnline()) return;

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
            // ハイド中はハイドを抜けてからジャンプする
            if (isHiding) _hide.ExitHide();
            _jumpRequested = true;
        }

        // 飛べるハイドポイントがあれば、ダッシュの代わりにそこへ飛ぶ(クールタイム中でも飛べる。張り付いている間は次のハイドポイントへ)
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
        _isSprinting = canAct && !isHiding && !_isDashing && _dashButton.IsPressed();
    }

    private void FixedUpdate()
    {
        if (!_isControlled) return;

        // 床の下へ落ちてしまったら、スポーンした位置へ戻す
        if (ReturnToSpawnIfFallen()) return;

        // 死亡中・スタン中は操作を受け付けず、その時の勢いで滑り続けないよう水平方向を止める(落下は残す)
        if (_health.isDead || isStunned)
        {
            _isDashing = false;
            _jumpRequested = false;
            _lungeRemaining = 0f;
            StopHorizontalVelocity();
            return;
        }

        if (_dashCooldownRemaining > 0f)
        {
            _dashCooldownRemaining -= Time.fixedDeltaTime;
        }

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
            // ハイドポイントに張り付いている間は動かない(街灯の上などから落ちないよう、縦も止める)
            _rigidbody.linearVelocity = Vector3.zero;
        }
        else
        {
            Move();
            ApplyJump();
        }
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

    public void ClearAimFacing()
    {
        _hasAimFacing = false;
    }

    // 指定した方向へ短く踏み込む(攻撃が届く距離まで詰める)。ダッシュを始めると打ち切られる
    public void Lunge(Vector3 direction, float distance, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f || duration <= 0f) return;

        _lungeVelocity = direction.normalized * (distance / duration);
        _lungeRemaining = duration;
        _isLungeVertical = false;
    }

    // 指定した方向へ、高さも含めてまっすぐ進む(ハイドポイントへ飛ぶ時に使う)。ダッシュを始めると打ち切られる
    public void Fly(Vector3 direction, float distance, float duration)
    {
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f || duration <= 0f) return;

        _lungeVelocity = direction.normalized * (distance / duration);
        _lungeRemaining = duration;
        _isLungeVertical = true;
    }

    // 位置を指定した位置へ合わせ、踏み込みと速度を止める(ハイドポイントに張り付く時に使う)
    public void SnapPosition(Vector3 position)
    {
        _lungeRemaining = 0f;
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
        if (!_isControlled || _npcInput != null) return;   // NPCはカメラを使わない

        UpdateCameraTransform();
        UpdateViewDirectionAnchor();
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

    // 自分のプレイヤーの初期化(入力の取得、カーソル固定)
    private void SetupAsLocalPlayer()
    {
        _isControlled = true;
        RememberSpawnPose();
        BindInputActions();

        // カメラ追従のガタつきを防ぐ
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // 今向いている方向を視点の初期値にする
        _yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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

    // 自分のプレイヤーの後片付け(入力参照の解放、カーソル解除)
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

        // NPCはカーソルを触っていない
        if (_npcInput != null)
        {
            _npcInput = null;
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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

        _moveInput = _moveAction.ReadValue<Vector2>();

        // マウスは「1フレームの移動量」なのでdeltaTimeを掛けない
        // スティックは「傾き」なので速度として扱いdeltaTimeを掛ける
        Vector2 mouseLook = _mouseLookAction.ReadValue<Vector2>() * _mouseSensitivity;
        Vector2 stickLook = _stickLookAction.ReadValue<Vector2>() * _stickSensitivity * Time.deltaTime;
        Vector2 look = mouseLook + stickLook;

        _yaw += look.x;
        _pitch = Mathf.Clamp(_pitch - look.y, _minPitch, _maxPitch);
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
        // 入力なしのときは水平方向を止める(落下は残す)
        if (_moveInput == Vector2.zero)
        {
            StopHorizontalVelocity();
            return;
        }

        Vector3 direction = GetMoveDirection();

        float speed = _isSprinting ? _stats.currentSprintSpeed : _stats.currentMoveSpeed;
        Vector3 horizontal = direction * speed;
        _rigidbody.linearVelocity = new Vector3(horizontal.x, _rigidbody.linearVelocity.y, horizontal.z);
    }

    // 水平方向の速度だけを0にする(Y速度はそのまま残す)
    private void StopHorizontalVelocity()
    {
        _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
    }

    // ジャンプ予約を消費し、接地していれば真上に飛ばす
    private void ApplyJump()
    {
        if (!_jumpRequested) return;

        _jumpRequested = false;
        if (!IsGrounded()) return;

        Vector3 velocity = _rigidbody.linearVelocity;
        _rigidbody.linearVelocity = new Vector3(velocity.x, _stats.currentJumpPower, velocity.z);
        onJumped?.Invoke();
    }

    // ダッシュを開始する(方向をこの時点で決めて固定する)
    private void StartDash()
    {
        _isDashing = true;
        _lungeRemaining = 0f;   // ダッシュ(回避)を優先し、エイムアシストの踏み込みは打ち切る
        _dashElapsed = 0f;
        _dashCooldownRemaining = _dashCooldown;
        _dashDirection = CalculateDashDirection();
        onDashStarted?.Invoke();
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
        Vector3 origin = _collider.bounds.center;
        float rayLength = _collider.bounds.extents.y + _groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, rayLength, _groundLayers, QueryTriggerInteraction.Ignore);
    }

    // NetworkManagerが動いているか(オフラインのテストシーンではfalse)
    private static bool IsOnline()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    // カメラをプレイヤーの周りに配置する(プレイヤーの回転の影響を受けない)
    // CinemachineCameraがあれば、そちらを動かす(Brainが実際のカメラへ反映し、揺れなどの演出も加える)
    private void UpdateCameraTransform()
    {
        Transform target = _virtualCameraTransform != null ? _virtualCameraTransform : _cameraTransform;
        if (target == null) return;

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pivot = transform.position + Vector3.up * _cameraPivotHeight;
        Vector3 position = pivot - rotation * Vector3.forward * _cameraDistance;

        target.SetPositionAndRotation(position, rotation);
    }
}
