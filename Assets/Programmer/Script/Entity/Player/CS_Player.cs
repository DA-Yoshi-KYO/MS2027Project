using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの操作(三人称視点)を行うクラス
 * キーボード+マウス、コントローラーの両方に対応
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・入力はIPlayerInputSource(入力の出どころ)から読む。各操作のコンポーネント(攻撃・必殺技・変身・アイテム)もinputを見る
 *   人が操作する場合: CS_PlayerInputActions(CS_CustomInputActionManagerが持つPlayerControls.inputactions由来の入力)
 *   NPCの場合      : 同じオブジェクトに付いているIPlayerInputSource(CS_NpcBrain)
 * ・入力、移動は「このマシンで動かすプレイヤー」だけが行う(_isControlled)
 *   オンライン: 自分がOwnerのプレイヤー。NPCはサーバーが動かす
 *   オフライン: NetworkManagerが動いていないテストシーンのプレイヤー(NPCも含む)
 * ・NPCかどうか(isNpc)はCS_NpcSpawnerがスポーン前にAssignAsNpc()で設定する(NetworkVariableなので全員へ同期される)
 *   NPCはカメラを使わず(無効化する)、カーソルも固定しない
 * ・他人のプレイヤーの位置は NetworkTransform(Authority Mode: Owner) が同期する
 * ・移動の流れ
 *   1. Update       : 入力を読み取り、視点(yaw/pitch)を更新する
 *   2. FixedUpdate  : カメラの向きへ回転し(移動入力が無くても回転する)、向き終わったら移動する
 *   3. LateUpdate   : カメラをプレイヤーの周りに配置する
 * ・カメラはプレイヤーの子だが、回転がプレイヤーに引っ張られないよう
 *   LateUpdateでワールド座標を直接指定している
 *   カメラにはCinemachineBrainが付いており、位置・向きはCinemachineCamera(_virtualCameraTransform)へ指定する
 *   (Brainが実際のカメラへ反映する。攻撃時の揺れはCS_PlayerCameraShakeがImpulseで加える)
 * ・死亡中(CS_PlayerHealth.isDead)は移動・攻撃を行わない(canActで判定)
 *   視点操作(カメラ)は死亡中も継続する
 *   死亡中は水平方向の速度を毎回0にし、死亡時の勢いで滑り続けないようにしている(落下はする)
 * ・移動速度はCS_PlayerStats.moveSpeedを使う(実際の変更はCS_PlayerStats側で行う)
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
    // 書き込みはサーバーのみ。CS_NpcSpawnerがスポーン前に設定する
    private readonly NetworkVariable<bool> _isNpc = new NetworkVariable<bool>();

    private Rigidbody _rigidbody;
    private CapsuleCollider _collider;
    private CS_PlayerHealth _health;
    private CS_PlayerStats _stats;

    private IPlayerInputSource _input;  // 入力の出どころ(このマシンで動かす間だけ持つ)

    private bool _isControlled;     // このプレイヤーをこのマシンで動かすか
    private Vector2 _moveInput;
    private bool _jumpRequested;    // Updateで押下を検知し、FixedUpdateで消費する
    private float _yaw;
    private float _pitch = 11f;

    private bool _isDashing;
    private float _dashElapsed;
    private float _dashCooldownRemaining;
    private Vector3 _dashDirection;
    private bool _isSprinting;      // ダッシュ後、ダッシュボタンを押し続けている間か

    public bool isControlled => _isControlled;          // このプレイヤーをこのマシンで動かしているか
    public bool canAct => _isControlled && !_health.isDead;   // 移動・攻撃してよいか(CS_PlayerAttackも参照)
    public IPlayerInputSource input => _input;          // 入力の出どころ(各操作のコンポーネントが使う。動かしていない間はnull)
    // 必殺技コード(RT+LT)のLT側が押されているか(CS_PlayerAttackが、RT+LT同時押し時に攻撃を誤発動させないため参照する)
    public bool isSpecialModifierHeld => _input != null && _input.specialModifierHeld;
    public float dashDuration => _dashDuration;         // ダッシュが続く時間(秒、見た目のモーション速度合わせに使う)
    public bool isGrounded => IsGrounded();             // 接地しているか(全クライアントで判定できる。見た目用にも使う)
    public bool isSprinting => _isSprinting;            // ダッシュ後、ボタンを押し続けて速くなっているか(見た目用)
    public int playerNumber => _playerNumber.Value;     // 何番目のプレイヤーか(0〜3。HP UIなど画面上の表示先を決めるのに使う)
    public bool isNpc => _isNpc.Value;                  // NPCか(表示名やリザルトでの判別用)
    public bool isLocalHuman => _isControlled && !_isNpc.Value;    // このマシンで人が操作しているプレイヤーか(ミニマップの中心などに使う)
    public float yaw => _yaw;                           // 視点の左右の向き(度)。移動入力はこの向き基準

    // 何番目のプレイヤーかを割り当てる(CS_PlayerSpawnerが、スポーンする前に呼ぶ)
    public void AssignPlayerNumber(int number)
    {
        if (IsSpawned && !IsServer) return;

        _playerNumber.Value = number;
    }

    // NPCとして扱う(CS_NpcSpawnerが、スポーンする前に呼ぶ)
    public void AssignAsNpc()
    {
        if (IsSpawned && !IsServer) return;

        _isNpc.Value = true;
    }

    // 操作しているクライアントでだけ発生する。見た目(CS_PlayerVisual)など、ゲームロジックの外から購読する
    public event Action onJumped;
    public event Action onDashStarted;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();
        _health = GetComponent<CS_PlayerHealth>();
        _stats = GetComponent<CS_PlayerStats>();

        // 壁などとの衝突で回転しないよう、物理による回転はすべて止める
        // (向きはRotateToCameraのMoveRotationでのみ変える)
        _rigidbody.freezeRotation = true;
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (_isControlled || IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        if (_isNpc.Value)
        {
            SetupAsNpc();
            return;
        }

        SetupAsLocalPlayer();
    }

    public override void OnNetworkSpawn()
    {
        // NPCはサーバーだけが動かす
        if (_isNpc.Value)
        {
            if (IsServer)
            {
                SetupAsNpc();
                return;
            }

            SetupAsRemotePlayer();
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
        ReleaseControl();
    }

    public override void OnDestroy()
    {
        ReleaseControl();
        base.OnDestroy();
    }

    private void Update()
    {
        if (!_isControlled) return;

        ReadInput();

        // ジャンプ・ダッシュは死亡中に予約されても復帰後に発動しないよう、ここでもcanActを見る
        if (canAct && _input.jumpPressed)
        {
            _jumpRequested = true;
        }

        if (canAct && !_isDashing && _dashCooldownRemaining <= 0f && _input.dashPressed)
        {
            StartDash();
        }

        // ダッシュ中でない間にダッシュボタンを押し続けていればスプリント(ZZZのような挙動。クールタイムとは無関係)
        _isSprinting = canAct && !_isDashing && _input.dashHeld;
    }

    private void FixedUpdate()
    {
        if (!_isControlled) return;

        // 死亡中は操作を受け付けず、死亡時の勢いで滑り続けないよう水平方向を止める(落下は残す)
        if (_health.isDead)
        {
            _isDashing = false;
            _jumpRequested = false;
            StopHorizontalVelocity();
            return;
        }

        if (_dashCooldownRemaining > 0f)
        {
            _dashCooldownRemaining -= Time.fixedDeltaTime;
        }

        // ダッシュ中も向きだけは更新する(移動方向自体はダッシュ開始時に固定した_dashDirectionを使うため影響しない)
        // これをしないと、ダッシュ中にカメラを動かした分だけ体の向きとカメラの向きがずれ、
        // ダッシュ終了直後にIsFacingCamera()がfalseになって、そのフレームだけ移動できず止まって見える
        RotateToCamera();

        if (_isDashing)
        {
            UpdateDash();
        }
        else
        {
            Move();
            ApplyJump();
        }
    }

    private void LateUpdate()
    {
        if (!isLocalHuman) return;

        UpdateCameraTransform();
    }

    // 自分のプレイヤーの初期化(入力の取得、カーソル固定)
    private void SetupAsLocalPlayer()
    {
        _isControlled = true;
        _input = new CS_PlayerInputActions(_mouseSensitivity, _stickSensitivity);

        // カメラ追従のガタつきを防ぐ
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // 今向いている方向を視点の初期値にする
        _yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // NPCの初期化(入力はNPCの頭脳から受け取る。カメラは使わない)
    private void SetupAsNpc()
    {
        _input = GetComponent<IPlayerInputSource>();
        if (_input == null)
        {
            Debug.LogError("CS_Player: NPCに入力の出どころ(IPlayerInputSource)が付いていません", this);
            return;
        }

        _isControlled = true;
        _yaw = transform.eulerAngles.y;
        DisableCameras();
    }

    // 動かしていたプレイヤーの後片付け(入力参照の解放、カーソル解除)
    private void ReleaseControl()
    {
        if (!_isControlled) return;

        bool wasLocalHuman = isLocalHuman;

        _isControlled = false;
        _input = null;
        _moveInput = Vector2.zero;
        _jumpRequested = false;
        _isDashing = false;
        _dashCooldownRemaining = 0f;
        _isSprinting = false;

        if (!wasLocalHuman) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 他人のプレイヤーの初期化(物理とカメラを切る)
    private void SetupAsRemotePlayer()
    {
        // 位置はNetworkTransformが更新するので、物理で動かさない
        _rigidbody.isKinematic = true;
        DisableCameras();
    }

    // カメラとAudioListenerが複数有効になるのを防ぐ(他人のプレイヤー、NPC)
    // (CinemachineCameraも消す。残すと、自分のBrainが他人のCinemachineCameraを選んでしまう)
    private void DisableCameras()
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

    // 入力を読み取り、移動入力と視点(yaw/pitch)を更新する
    private void ReadInput()
    {
        _moveInput = _input.move;

        Vector2 look = _input.look;
        _yaw += look.x;
        _pitch = Mathf.Clamp(_pitch - look.y, _minPitch, _maxPitch);
    }

    // カメラの向き(yaw)へ徐々に回転する。移動入力の有無に関わらず、カメラを振れば体も追従する
    private void RotateToCamera()
    {
        Quaternion target = Quaternion.Euler(0f, _yaw, 0f);
        Quaternion next = Quaternion.RotateTowards(
            _rigidbody.rotation, target, _rotationSpeed * Time.fixedDeltaTime);
        _rigidbody.MoveRotation(next);
    }

    // カメラの向き基準で移動する(向き終わるまでは移動しない)
    private void Move()
    {
        // 入力なし、または向き切っていないときは水平方向を止める(落下は残す)
        if (_moveInput == Vector2.zero || !IsFacingCamera())
        {
            StopHorizontalVelocity();
            return;
        }

        Quaternion cameraYaw = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 direction = cameraYaw * new Vector3(_moveInput.x, 0f, _moveInput.y);
        direction = Vector3.ClampMagnitude(direction, 1f);

        float speed = _isSprinting ? _stats.sprintSpeed : _stats.moveSpeed;
        Vector3 horizontal = direction * speed;
        _rigidbody.linearVelocity = new Vector3(horizontal.x, _rigidbody.linearVelocity.y, horizontal.z);
    }

    // 水平方向の速度だけを0にする(Y速度はそのまま残す)
    private void StopHorizontalVelocity()
    {
        _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
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
