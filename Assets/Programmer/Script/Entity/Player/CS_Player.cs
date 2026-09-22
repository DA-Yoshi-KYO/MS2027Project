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
 * ・入力は InputActionAsset(PlayerControls) を使う
 *   複数のプレイヤーで状態を共有しないよう、Instantiateで複製して使っている
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
 */
// ========================================

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CS_PlayerHealth))]
public class CS_Player : NetworkBehaviour
{
    [Header("移動")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 720f;   // カメラの向きへ回る速さ(度/秒)
    [SerializeField] private float _moveStartAngle = 30f;   // この角度以内まで向いたら移動を始める

    [Header("カメラ")]
    [SerializeField] private Transform _cameraTransform;    // プレイヤーの子のカメラ
    [SerializeField] private float _cameraDistance = 3.5f;
    [SerializeField] private float _cameraPivotHeight = 1.3f;
    [SerializeField] private float _minPitch = -30f;
    [SerializeField] private float _maxPitch = 60f;

    [Header("視点感度")]
    [SerializeField] private float _mouseSensitivity = 0.1f;    // マウス移動量(ピクセル)に対する回転量
    [SerializeField] private float _stickSensitivity = 180f;    // スティック全開時の回転速度(度/秒)

    [Header("入力")]
    [SerializeField] private InputActionAsset _inputActions;    // PlayerControls を割り当てる

    private const string _actionMapName = "Player";
    private const string _moveActionName = "Move";
    private const string _mouseLookActionName = "Look";
    private const string _stickLookActionName = "LookStick";
    private const string _attackActionName = "Attack";

    private Rigidbody _rigidbody;
    private CS_PlayerHealth _health;

    private InputActionAsset _runtimeActions;   // プレイヤーごとに複製した入力アセット
    private InputAction _moveAction;
    private InputAction _mouseLookAction;
    private InputAction _stickLookAction;
    private InputAction _attackAction;

    private bool _isControlled;     // このプレイヤーを自分が操作するか
    private Vector2 _moveInput;
    private float _yaw;
    private float _pitch = 11f;

    public bool isControlled => _isControlled;          // このプレイヤーを自分が操作しているか
    public bool canAct => _isControlled && !_health.isDead;   // 移動・攻撃してよいか(CS_PlayerAttackも参照)
    public InputAction attackAction => _attackAction;   // 攻撃ボタン(CS_PlayerAttackが使う)

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _health = GetComponent<CS_PlayerHealth>();
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
    }

    private void FixedUpdate()
    {
        if (!canAct) return;

        RotateToCamera();
        Move();
    }

    private void LateUpdate()
    {
        if (!_isControlled) return;

        UpdateCameraTransform();
    }

    // 自分のプレイヤーの初期化(入力の有効化、カーソル固定)
    private void SetupAsLocalPlayer()
    {
        if (_inputActions == null)
        {
            Debug.LogError("CS_Player: Input Actions が未設定です(PlayerControls を割り当ててください)", this);
            return;
        }

        _isControlled = true;
        CreateInputActions();

        // カメラ追従のガタつきを防ぐ
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // 今向いている方向を視点の初期値にする
        _yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 自分のプレイヤーの後片付け(入力の破棄、カーソル解除)
    private void ReleaseLocalPlayer()
    {
        if (!_isControlled) return;

        _isControlled = false;
        _moveInput = Vector2.zero;
        DisposeInputActions();

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

    // 入力アセットを複製し、各アクションを取得して有効化する
    private void CreateInputActions()
    {
        _runtimeActions = Instantiate(_inputActions);
        InputActionMap actionMap = _runtimeActions.FindActionMap(_actionMapName, true);

        _moveAction = actionMap.FindAction(_moveActionName, true);
        _mouseLookAction = actionMap.FindAction(_mouseLookActionName, true);
        _stickLookAction = actionMap.FindAction(_stickLookActionName, true);
        _attackAction = actionMap.FindAction(_attackActionName, true);

        actionMap.Enable();
    }

    // 複製した入力アセットを破棄する
    private void DisposeInputActions()
    {
        if (_runtimeActions == null) return;

        _runtimeActions.Disable();
        Destroy(_runtimeActions);

        _runtimeActions = null;
        _moveAction = null;
        _mouseLookAction = null;
        _stickLookAction = null;
        _attackAction = null;
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

        Vector3 horizontal = direction * _moveSpeed;
        _rigidbody.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
    }

    // プレイヤーがカメラの向きに十分向いているか
    private bool IsFacingCamera()
    {
        float angle = Mathf.DeltaAngle(_rigidbody.rotation.eulerAngles.y, _yaw);
        return Mathf.Abs(angle) <= _moveStartAngle;
    }

    // カメラをプレイヤーの周りに配置する(プレイヤーの回転の影響を受けない)
    private void UpdateCameraTransform()
    {
        if (_cameraTransform == null) return;

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pivot = transform.position + Vector3.up * _cameraPivotHeight;
        Vector3 position = pivot - rotation * Vector3.forward * _cameraDistance;

        _cameraTransform.SetPositionAndRotation(position, rotation);
    }
}
