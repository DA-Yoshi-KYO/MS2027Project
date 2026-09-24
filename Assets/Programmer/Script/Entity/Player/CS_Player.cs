using System;
using Unity.Netcode;
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
 *   2. FixedUpdate  : カメラの向きへ回転し、向き終わったら移動する
 *   3. LateUpdate   : カメラをプレイヤーの周りに配置する
 * ・カメラはプレイヤーの子だが、回転がプレイヤーに引っ張られないよう
 *   LateUpdateでワールド座標を直接指定している
 * ・死亡中(CS_PlayerHealth.isDead)は移動・攻撃を行わない(canActで判定)
 *   視点操作(カメラ)は死亡中も継続する
 * ・移動速度はCS_PlayerStats.moveSpeedを使う(実際の変更はCS_PlayerStats側で行う)
 * ・ジャンプ
 *   接地中にジャンプボタンを押すと、CS_PlayerStats.jumpPowerを初速として真上に飛ぶ
 *   入力はUpdateで拾って予約し、実際に飛ぶ処理はFixedUpdate側のMove()の後に行う
 *   (Move()は毎回、現在のY速度を保ったまま水平方向だけを書き換えるため、
 *    ジャンプの初速はMove()より後に適用しないと上書きされてしまう)
 * ・ダッシュ
 *   クールタイム中でなければダッシュボタンで発動。移動入力があればその方向、
 *   無ければ現在向いている方向へ、CS_PlayerStats.dashSpeedで一定時間だけ直進する
 *   （「慣性は少なめできびきび動く」という要望に合わせ、通常のMove()は行わず
 *    速度を直接指定している）。攻撃・必殺技とは排他制御しておらず、いつでも出せる
 */
// ========================================

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(CS_PlayerHealth))]
[RequireComponent(typeof(CS_PlayerStats))]
public class CS_Player : NetworkBehaviour
{
    [Header("移動")]
    [SerializeField] private float _rotationSpeed = 720f;   // カメラの向きへ回る速さ(度/秒)
    [SerializeField] private float _moveStartAngle = 30f;   // この角度以内まで向いたら移動を始める

    [Header("ジャンプ")]
    [SerializeField] private LayerMask _groundLayers = ~0;      // 地面と判定するレイヤー
    [SerializeField] private float _groundCheckDistance = 0.15f;  // 接地判定用の余白

    [Header("ダッシュ")]
    [SerializeField] private float _dashDuration = 0.2f;    // ダッシュが続く時間(秒)
    [SerializeField] private float _dashCooldown = 0.8f;    // 次に出せるようになるまでの時間(秒)

    [Header("カメラ")]
    [SerializeField] private Transform _cameraTransform;    // プレイヤーの子のカメラ
    [SerializeField] private float _cameraDistance = 3.5f;
    [SerializeField] private float _cameraPivotHeight = 1.3f;
    [SerializeField] private float _cameraFollowTime = 0.15f;         // 水平方向の追従の遅れ(秒)。0で完全追従
    [SerializeField] private float _cameraVerticalFollowTime = 0.3f;  // 上下方向の追従の遅れ(秒)。ジャンプで揺れすぎないよう長めにする
    [SerializeField] private float _minPitch = -30f;
    [SerializeField] private float _maxPitch = 60f;

    [Header("視点感度")]
    [SerializeField] private float _mouseSensitivity = 0.1f;    // マウス移動量(ピクセル)に対する回転量
    [SerializeField] private float _stickSensitivity = 180f;    // スティック全開時の回転速度(度/秒)

    private const float _cameraSnapDistance = 10f;  // これ以上離れたらテレポートとみなして遅らせない

    private Vector3 _cameraPivot;             // 遅れて追従しているカメラの注視点
    private Vector3 _cameraPivotVelocity;     // SmoothDamp用
    private bool _hasCameraPivot;

    private Rigidbody _rigidbody;
    private CapsuleCollider _collider;
    private CS_PlayerHealth _health;
    private CS_PlayerStats _stats;

    private InputAction _moveAction;
    private InputAction _mouseLookAction;
    private InputAction _stickLookAction;
    private InputAction _attackAction;
    private InputAction _jumpAction;
    private InputAction _specialAction;
    private InputAction _dashAction;    // ダッシュボタン
    private InputAction _useItemAction; // アイテム使用ボタン(CS_PlayerItemSlotが使う)

    private bool _isControlled;     // このプレイヤーを自分が操作するか
    private Vector2 _moveInput;
    private bool _jumpRequested;    // Updateで押下を検知し、FixedUpdateで消費する
    private float _yaw;
    private float _pitch = 11f;

    private bool _isDashing;
    private float _dashElapsed;
    private float _dashCooldownRemaining;
    private Vector3 _dashDirection;

    public bool isControlled => _isControlled;          // このプレイヤーを自分が操作しているか
    public bool canAct => _isControlled && !_health.isDead;   // 移動・攻撃してよいか(CS_PlayerAttackも参照)
    public InputAction attackAction => _attackAction;   // 攻撃ボタン(CS_PlayerAttackが使う)
    public InputAction specialAction => _specialAction; // 必殺技ボタン(CS_PlayerSpecialAttackが使う)
    public InputAction dashAction => _dashAction;       // ダッシュボタン
    public InputAction useItemAction => _useItemAction; // アイテム使用ボタン(CS_PlayerItemSlotが使う)
    public bool isGrounded => IsGrounded();             // 接地しているか(全クライアントで判定できる。見た目用にも使う)

    // 操作しているクライアントでだけ発生する。見た目(CS_PlayerVisual)など、ゲームロジックの外から購読する
    public event Action onJumped;
    public event Action onDashStarted;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();
        _health = GetComponent<CS_PlayerHealth>();
        _stats = GetComponent<CS_PlayerStats>();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (_isControlled || IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        SetupAsLocalPlayer();
    }

    public override void OnNetworkSpawn()
    {
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
        if (canAct && _jumpAction.WasPressedThisFrame())
        {
            _jumpRequested = true;
        }

        if (canAct && !_isDashing && _dashCooldownRemaining <= 0f && _dashAction.WasPressedThisFrame())
        {
            StartDash();
        }
    }

    private void FixedUpdate()
    {
        if (!canAct) return;

        if (_dashCooldownRemaining > 0f)
        {
            _dashCooldownRemaining -= Time.fixedDeltaTime;
        }

        if (_isDashing)
        {
            UpdateDash();
        }
        else
        {
            RotateToCamera();
            Move();
            ApplyJump();
        }
    }

    private void LateUpdate()
    {
        if (!_isControlled) return;

        UpdateCameraTransform();
    }

    // 自分のプレイヤーの初期化(入力の取得、カーソル固定)
    private void SetupAsLocalPlayer()
    {
        _isControlled = true;
        BindInputActions();

        // カメラ追従のガタつきを防ぐ
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // 今向いている方向を視点の初期値にする
        _yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        UnbindInputActions();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 他人のプレイヤーの初期化(物理とカメラを切る)
    private void SetupAsRemotePlayer()
    {
        // 位置はNetworkTransformが更新するので、物理で動かさない
        _rigidbody.isKinematic = true;

        // カメラとAudioListenerが複数有効になるのを防ぐ
        if (_cameraTransform != null)
        {
            _cameraTransform.gameObject.SetActive(false);
        }
    }

    // CS_CustomInputActionManagerが持つ各アクションの参照を取得する
    private void BindInputActions()
    {
        CustomInputAction.PlayerActions player = CS_CustomInputActionManager.instance.customInputAction.Player;

        _moveAction = player.Move;
        _mouseLookAction = player.Look;
        _stickLookAction = player.LookStick;
        _attackAction = player.Attack;
        _jumpAction = player.Jump;
        _specialAction = player.Special;
        _dashAction = player.Dash;
        _useItemAction = player.UseItem;
    }

    // アクションへの参照を外す(アクション自体は共有のシングルトンが持ち続ける)
    private void UnbindInputActions()
    {
        _moveAction = null;
        _mouseLookAction = null;
        _stickLookAction = null;
        _attackAction = null;
        _jumpAction = null;
        _specialAction = null;
        _dashAction = null;
        _useItemAction = null;
    }

    // 入力を読み取り、移動入力と視点(yaw/pitch)を更新する
    private void ReadInput()
    {
        _moveInput = _moveAction.ReadValue<Vector2>();

        // マウスは「1フレームの移動量」なのでdeltaTimeを掛けない
        // スティックは「傾き」なので速度として扱いdeltaTimeを掛ける
        Vector2 mouseLook = _mouseLookAction.ReadValue<Vector2>() * _mouseSensitivity;
        Vector2 stickLook = _stickLookAction.ReadValue<Vector2>() * _stickSensitivity * Time.deltaTime;
        Vector2 look = mouseLook + stickLook;

        _yaw += look.x;
        _pitch = Mathf.Clamp(_pitch - look.y, _minPitch, _maxPitch);
    }

    // 移動入力があるとき、カメラの向き(yaw)へ徐々に回転する
    private void RotateToCamera()
    {
        if (_moveInput == Vector2.zero) return;

        Quaternion target = Quaternion.Euler(0f, _yaw, 0f);
        Quaternion next = Quaternion.RotateTowards(
            _rigidbody.rotation, target, _rotationSpeed * Time.fixedDeltaTime);
        _rigidbody.MoveRotation(next);
    }

    // カメラの向き基準で移動する(向き終わるまでは移動しない)
    private void Move()
    {
        Vector3 velocity = _rigidbody.linearVelocity;

        // 入力なし、または向き切っていないときは水平方向を止める(落下は残す)
        if (_moveInput == Vector2.zero || !IsFacingCamera())
        {
            _rigidbody.linearVelocity = new Vector3(0f, velocity.y, 0f);
            return;
        }

        Quaternion cameraYaw = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 direction = cameraYaw * new Vector3(_moveInput.x, 0f, _moveInput.y);
        direction = Vector3.ClampMagnitude(direction, 1f);

        Vector3 horizontal = direction * _stats.moveSpeed;
        _rigidbody.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
    }

    // プレイヤーがカメラの向きに十分向いているか
    private bool IsFacingCamera()
    {
        float angle = Mathf.DeltaAngle(_rigidbody.rotation.eulerAngles.y, _yaw);
        return Mathf.Abs(angle) <= _moveStartAngle;
    }

    // ジャンプ予約を消費し、接地していれば真上に飛ばす
    private void ApplyJump()
    {
        if (!_jumpRequested) return;

        _jumpRequested = false;
        if (!IsGrounded()) return;

        Vector3 velocity = _rigidbody.linearVelocity;
        _rigidbody.linearVelocity = new Vector3(velocity.x, _stats.jumpPower, velocity.z);
        onJumped?.Invoke();
    }

    // ダッシュを開始する(方向をこの時点で決めて固定する)
    private void StartDash()
    {
        _isDashing = true;
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

    // カメラをプレイヤーの周りに配置する(プレイヤーの回転の影響を受けない)
    // 注視点は少し遅れて追従する。回転(視点操作)は遅らせず、操作に即座に反応する
    private void UpdateCameraTransform()
    {
        if (_cameraTransform == null) return;

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pivot = FollowPivot(transform.position + Vector3.up * _cameraPivotHeight);
        Vector3 position = pivot - rotation * Vector3.forward * _cameraDistance;

        _cameraTransform.SetPositionAndRotation(position, rotation);
    }

    // 注視点を目標へ向けてなめらかに近づける(水平と上下で遅れの長さを変える)
    private Vector3 FollowPivot(Vector3 target)
    {
        // 初回とテレポート(リスポーンなど)は遅らせず、その場に合わせる
        if (!_hasCameraPivot || (target - _cameraPivot).sqrMagnitude > _cameraSnapDistance * _cameraSnapDistance)
        {
            _cameraPivot = target;
            _cameraPivotVelocity = Vector3.zero;
            _hasCameraPivot = true;
            return _cameraPivot;
        }

        float deltaTime = Time.deltaTime;
        _cameraPivot.x = Mathf.SmoothDamp(_cameraPivot.x, target.x, ref _cameraPivotVelocity.x, _cameraFollowTime, Mathf.Infinity, deltaTime);
        _cameraPivot.z = Mathf.SmoothDamp(_cameraPivot.z, target.z, ref _cameraPivotVelocity.z, _cameraFollowTime, Mathf.Infinity, deltaTime);
        _cameraPivot.y = Mathf.SmoothDamp(_cameraPivot.y, target.y, ref _cameraPivotVelocity.y, _cameraVerticalFollowTime, Mathf.Infinity, deltaTime);
        return _cameraPivot;
    }
}
