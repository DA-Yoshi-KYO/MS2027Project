using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの見た目(アニメーション)を担当するクラス
 * ゲームロジック(CS_Player、CS_PlayerAttackなど)から状態やイベントを受け取り、Animatorへ反映するだけで、
 * ゲームロジック側はこのクラスの存在を知らない。見た目を差し替えても、外してもゲームは動く
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ■ ネットワークの考え方
 *   Animatorは全クライアントが「自分のもの」をローカルで動かす(NetworkAnimatorは使わない)。
 *   ・移動、接地、死亡 : 全クライアントで、座標の変化や同期済みのHPから自分で計算する(通信なし)
 *   ・被弾           : 同期済みのHPが減ったのを各クライアントが検知して再生する(通信なし)
 *   ・ジャンプ、ダッシュ、攻撃、必殺技 : 操作している本人が再生し、同時にRPCで他クライアントへ通知する
 *   後から参加したクライアントには単発の動作は再生されないが、死亡は状態から判定するので反映される
 *   ・復活直後の無敵の点滅 : 同期済みの無敵終了時刻(CS_PlayerRespawn.isRespawnInvincible)から全クライアントで判定する
 *
 * ■ 点滅
 *   復活直後の無敵中は、子のRenderer(モデル)を_blinkInterval秒ごとに表示/非表示する
 *   Renderer.enabledは触らず、forceRenderingOffで隠す(他の処理が設定した表示状態を壊さないため)
 *
 * ■ 変身後の見た目
 *   変身が完了している間(CS_PlayerTransformation.isTransformed)は、_animatorのモデルを隠して
 *   _transformedAnimatorのモデルを表示し、以降のアニメーションは表示中の方のAnimatorへ送る
 *   変身状態は同期済みなので、全クライアント(途中参加も含む)で同じ見た目になる
 *   _transformedAnimatorが空なら、変身しても見た目は変わらない
 *
 * ■ 変身途中のエフェクト
 *   変身途中(CS_PlayerTransformation.isTransforming)の間だけ_transformingEffectを再生する(全クライアント)
 *   終わったら放出だけ止め、出ている粒は自然に消えるのを待つ。_transformingEffectが空なら何も出さない
 *
 * ■ 差し替え
 *   ・Animatorは自動で子オブジェクトから探す(_animatorが空のとき)。Modelの子を差し替えるだけでよい
 *   ・変身後の見た目はTransformedModel(_transformedAnimator)。通常の見た目と同じパラメータを持つControllerを使う
 *   ・Controller側に用意するパラメータはCS_PlayerAnimatorParamsを参照。無いパラメータは無視される
 *   ・ルートモーションは使わない(移動はRigidbodyが行うため、強制的にオフにする)
 *   ・詳しくは ClaudeUsers/プレイヤー見た目の差し替えガイド.md
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[DefaultExecutionOrder(2)] // CS_PlayerHealth.Start(オフライン時のHP初期化)の後に、基準となるHPを読むため
public class CS_PlayerVisual : NetworkBehaviour
{
    private enum ActionType : byte
    {
        Jump,
        Dash,
        Attack,
        Special,
    }

    [SerializeField] private Animator _animator;                // 通常の見た目のAnimator
    [SerializeField] private Animator _transformedAnimator;     // 変身後の見た目のAnimator(空なら変身しても見た目は変わらない)
    [SerializeField] private float _locomotionDamping = 0.08f;  // 移動パラメータのなめらかさ(小さいほど素早く追従)
    [SerializeField] private float _maxTrackedSpeed = 40f;      // これを超える座標の変化はテレポートとみなして無視する(m/秒)
    [SerializeField] private float _blinkInterval = 0.1f;       // 復活直後の無敵中に点滅する間隔(秒)
    [SerializeField] private ParticleSystem _transformingEffect;    // 変身途中に出すパーティクル(空なら出さない)

    private CS_Player _player;
    private CS_PlayerStats _stats;
    private CS_PlayerHealth _health;
    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _special;
    private CS_PlayerRespawn _respawn;
    private CS_PlayerTransformation _transformation;
    private Animator _normalAnimator;       // 通常の見た目のAnimator(_animatorは表示中の方を指す)
    private bool _isShowingTransformed;     // 変身後の見た目を表示しているか
    private bool _isPlayingTransformingEffect;  // 変身途中のエフェクトを再生しているか
    private Renderer[] _renderers;
    private bool _isHidden;     // 点滅で隠している最中か

    private readonly HashSet<int> _availableParams = new HashSet<int>();
    private Vector3 _lastPosition;
    private float _lastHp;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _stats = GetComponent<CS_PlayerStats>();
        _health = GetComponent<CS_PlayerHealth>();
        _attack = GetComponent<CS_PlayerAttack>();
        _special = GetComponent<CS_PlayerSpecialAttack>();
        _respawn = GetComponent<CS_PlayerRespawn>();
        _transformation = GetComponent<CS_PlayerTransformation>();
        _renderers = GetComponentsInChildren<Renderer>(true);

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        _normalAnimator = _animator;

        // 変身後の見た目は、変身するまで隠しておく
        if (_transformedAnimator != null)
        {
            _transformedAnimator.gameObject.SetActive(false);
        }

        CacheAnimatorParams();
        _lastPosition = transform.position;
    }

    private void Start()
    {
        _lastHp = _health.currentHp;
    }

    private void OnEnable()
    {
        _player.onJumped += HandleJumped;
        _player.onDashStarted += HandleDashStarted;
        _health.onHpChanged += HandleHpChanged;

        if (_attack != null) _attack.onStepStarted += HandleAttackStepStarted;
        if (_special != null) _special.onSpecialStarted += HandleSpecialStarted;
    }

    private void OnDisable()
    {
        _player.onJumped -= HandleJumped;
        _player.onDashStarted -= HandleDashStarted;
        _health.onHpChanged -= HandleHpChanged;

        if (_attack != null) _attack.onStepStarted -= HandleAttackStepStarted;
        if (_special != null) _special.onSpecialStarted -= HandleSpecialStarted;
    }

    private void LateUpdate()
    {
        UpdateModel();
        UpdateTransformingEffect();
        UpdateBlink();

        if (_animator == null) return;

        UpdateLocomotion(Time.deltaTime);
        SetBool(CS_PlayerAnimatorParams.groundedHash, _player.isGrounded);
        SetBool(CS_PlayerAnimatorParams.deadHash, _health.isDead);
    }

    // 変身が完了している間だけ変身後のモデルを表示し、アニメーションの送り先も切り替える
    private void UpdateModel()
    {
        if (_normalAnimator == null || _transformedAnimator == null) return;

        bool transformed = _transformation != null && _transformation.isTransformed;
        if (transformed == _isShowingTransformed) return;

        _isShowingTransformed = transformed;
        _normalAnimator.gameObject.SetActive(!transformed);
        _transformedAnimator.gameObject.SetActive(transformed);

        _animator = transformed ? _transformedAnimator : _normalAnimator;
        CacheAnimatorParams();
    }

    // 変身途中の間だけエフェクトを再生する
    private void UpdateTransformingEffect()
    {
        if (_transformingEffect == null) return;

        bool transforming = _transformation != null && _transformation.isTransforming;
        if (transforming == _isPlayingTransformingEffect) return;

        _isPlayingTransformingEffect = transforming;
        if (transforming)
        {
            _transformingEffect.Play(true);
            return;
        }

        _transformingEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // 復活直後の無敵中は点滅させ、無敵が終わったら表示に戻す
    private void UpdateBlink()
    {
        bool invincible = _respawn != null && _respawn.isRespawnInvincible;
        bool hidden = invincible && _blinkInterval > 0f && Mathf.FloorToInt(Time.time / _blinkInterval) % 2 == 1;
        if (hidden == _isHidden) return;

        _isHidden = hidden;
        foreach (Renderer target in _renderers)
        {
            if (target != null) target.forceRenderingOff = hidden;
        }
    }

    // Controllerが持っているパラメータを控え、無いパラメータへの操作を無視できるようにする
    private void CacheAnimatorParams()
    {
        _availableParams.Clear();
        if (_animator == null) return;

        // 移動はRigidbodyが行うので、モデル側のルートモーションは使わない
        _animator.applyRootMotion = false;

        if (_animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            _availableParams.Add(parameter.nameHash);
        }
    }

    // 座標の変化から、体から見た移動方向と速さを求めてAnimatorへ渡す(全クライアント共通の計算)
    private void UpdateLocomotion(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        Vector3 velocity = (transform.position - _lastPosition) / deltaTime;
        _lastPosition = transform.position;
        velocity.y = 0f;

        if (velocity.magnitude > _maxTrackedSpeed)
        {
            velocity = Vector3.zero;
        }

        Vector3 local = Quaternion.Inverse(transform.rotation) * velocity;
        float reference = Mathf.Max(0.01f, _stats.moveSpeed);
        Vector2 move = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / reference, 1f);

        SetFloat(CS_PlayerAnimatorParams.moveXHash, move.x, deltaTime);
        SetFloat(CS_PlayerAnimatorParams.moveZHash, move.y, deltaTime);
        SetFloat(CS_PlayerAnimatorParams.speedHash, move.magnitude, deltaTime);
    }

    private void HandleJumped() => PerformOwnerAction(ActionType.Jump, 0);
    private void HandleDashStarted() => PerformOwnerAction(ActionType.Dash, 0);
    private void HandleAttackStepStarted(int step) => PerformOwnerAction(ActionType.Attack, step);
    private void HandleSpecialStarted() => PerformOwnerAction(ActionType.Special, 0);

    // HPが減ったら被弾。全クライアントで同期済みのHPの変化から検知する
    private void HandleHpChanged(float current, float max)
    {
        bool damaged = current < _lastHp;
        _lastHp = current;

        if (damaged && !_health.isDead)
        {
            SetTrigger(CS_PlayerAnimatorParams.hitHash);
        }
    }

    // 操作している本人の動作。自分で再生し、オンラインなら他クライアントへも通知する
    private void PerformOwnerAction(ActionType action, int parameter)
    {
        PlayAction(action, parameter);

        if (IsSpawned && IsOwner)
        {
            PlayActionRpc((byte)action, parameter);
        }
    }

    // 操作している本人以外のクライアントで、同じ動作を再生する
    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Owner)]
    private void PlayActionRpc(byte action, int parameter)
    {
        PlayAction((ActionType)action, parameter);
    }

    private void PlayAction(ActionType action, int parameter)
    {
        switch (action)
        {
            case ActionType.Jump:
                SetTrigger(CS_PlayerAnimatorParams.jumpHash);
                break;
            case ActionType.Dash:
                SetTrigger(CS_PlayerAnimatorParams.dashHash);
                break;
            case ActionType.Attack:
                SetInteger(CS_PlayerAnimatorParams.comboStepHash, parameter);
                SetTrigger(CS_PlayerAnimatorParams.attackHash);
                break;
            case ActionType.Special:
                SetTrigger(CS_PlayerAnimatorParams.specialHash);
                break;
        }
    }

    private bool Has(int hash) => _animator != null && _availableParams.Contains(hash);

    private void SetTrigger(int hash)
    {
        if (Has(hash)) _animator.SetTrigger(hash);
    }

    private void SetBool(int hash, bool value)
    {
        if (Has(hash)) _animator.SetBool(hash, value);
    }

    private void SetInteger(int hash, int value)
    {
        if (Has(hash)) _animator.SetInteger(hash, value);
    }

    private void SetFloat(int hash, float value, float deltaTime)
    {
        if (Has(hash)) _animator.SetFloat(hash, value, _locomotionDamping, deltaTime);
    }
}
