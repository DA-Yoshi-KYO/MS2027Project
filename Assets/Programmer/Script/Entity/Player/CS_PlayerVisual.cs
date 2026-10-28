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
 *     走っているか(Sprint)も、座標の変化の速さが通常の移動より速いかで決める
 *     接地(Grounded)は、ちょっとした段差を下りただけで空中のモーションにならないよう、
 *     落ちている(上がっていない)間は足元から_stepHeight以内に地面があれば接地とみなす(見た目だけ。ゲームの接地判定は変えない)
 *     (CS_Player.isSprinting / isDashingは操作している本人の手元にしか無いため)
 *   ・被弾           : 同期済みのHPが減ったのを各クライアントが検知して再生する(通信なし)
 *   ・ジャンプ、ダッシュ、攻撃、必殺技、奇襲 : 操作している本人が再生し、同時にRPCで他クライアントへ通知する
 *     奇襲は当たるまでの時間(CS_PlayerAmbush.EstimateTimeToHit、ミリ秒)を一緒に送り、各クライアントが、モーションの当たる瞬間
 *     (Ambush Impact Time)がちょうど当たる時に来る速さ(AmbushSpeed)で再生する。飛び込んでいる間に構えから振り抜きまでが進む
 *     通常攻撃の各段に使うモーションはAttack Motion For Step(ツールが、段ごとのタイミングに合うモーションを選んで書き込む)
 *   ・パリー(反撃) : CS_PlayerParry.onParriedが全クライアントで発生するので、各クライアントが再生する(通信なし)
 *   後から参加したクライアントには単発の動作は再生されないが、死亡は状態から判定するので反映される
 *   ・復活直後の無敵の点滅 : 同期済みの無敵終了時刻(CS_PlayerRespawn.isRespawnInvincible)から全クライアントで判定する
 *
 * ■ 点滅
 *   復活直後の無敵中は、子のRenderer(モデル)を_blinkInterval秒ごとに表示/非表示する
 *   Renderer.enabledは触らず、forceRenderingOffで隠す(他の処理が設定した表示状態を壊さないため)
 *
 * ■ 見た目は1つ
 *   常時変身(Issue #277)になったため、通常の見た目と変身後の見た目の切り替え、変身途中のエフェクトは無くした
 *   見た目は_animatorのモデル(Player.prefabのModel)だけ
 *
 * ■ 差し替え
 *   ・Animatorは自動で子オブジェクトから探す(_animatorが空のとき)。Modelの子を差し替えるだけでよい
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
        Parry,
        Ambush,
    }

    [SerializeField] private Animator _animator;                // 見た目のAnimator(空なら子オブジェクトから探す)
    [SerializeField] private float _locomotionDamping = 0.08f;  // 移動パラメータのなめらかさ(小さいほど素早く追従)
    [SerializeField] private float _maxTrackedSpeed = 40f;      // これを超える座標の変化はテレポートとみなして無視する(m/秒)
    [SerializeField] private float _blinkInterval = 0.1f;       // 復活直後の無敵中に点滅する間隔(秒)
    [SerializeField][Min(0f)] private float _stepHeight = 0.4f; // この高さまでの段差は、下りても空中のモーションにしない(m)
    [SerializeField] private LayerMask _stepGroundLayers = ~0;  // 段差の判定で地面とみなすレイヤー
    [SerializeField][Min(1)] private int _attackMotionCount = 3;    // Animatorにある通常攻撃のモーションの数。これを超えた段(5段コンボの4・5段目など)は先頭から使い回す
    [SerializeField] private int[] _attackMotionForStep;          // 通常攻撃の各段に使うモーションの番号(空なら段の番号をAttack Motion Countで割った余り)。ツールが書き込む
    [SerializeField][Min(0f)] private float _ambushImpactTime = 0.5f;  // 奇襲のモーションの頭から当たる瞬間までの秒数(等倍の時)。ツールが書き込む
    [SerializeField][Min(0.01f)] private float _minAmbushSpeed = 0.3f; // 奇襲のモーションの速さの下限・上限(倍)
    [SerializeField][Min(0.01f)] private float _maxAmbushSpeed = 6f;

    private CS_Player _player;
    private CS_PlayerStats _stats;
    private CS_PlayerHealth _health;
    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _special;
    private CS_PlayerRespawn _respawn;
    private Collider _collider;
    private CS_PlayerParry _parry;
    private CS_PlayerAmbush _ambush;
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
        _collider = GetComponent<Collider>();
        _parry = GetComponent<CS_PlayerParry>();
        _ambush = GetComponent<CS_PlayerAmbush>();
        _renderers = GetComponentsInChildren<Renderer>(true);

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
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
        if (_parry != null) _parry.onParried += HandleParried;
        if (_ambush != null) _ambush.onAmbushStarted += HandleAmbushStarted;
    }

    private void OnDisable()
    {
        _player.onJumped -= HandleJumped;
        _player.onDashStarted -= HandleDashStarted;
        _health.onHpChanged -= HandleHpChanged;

        if (_attack != null) _attack.onStepStarted -= HandleAttackStepStarted;
        if (_special != null) _special.onSpecialStarted -= HandleSpecialStarted;
        if (_parry != null) _parry.onParried -= HandleParried;
        if (_ambush != null) _ambush.onAmbushStarted -= HandleAmbushStarted;
    }

    private void LateUpdate()
    {
        UpdateBlink();

        if (_animator == null) return;

        float verticalSpeed = Time.deltaTime > 0f ? (transform.position.y - _lastPosition.y) / Time.deltaTime : 0f;
        UpdateLocomotion(Time.deltaTime);
        SetBool(CS_PlayerAnimatorParams.groundedHash, IsVisuallyGrounded(verticalSpeed));
        SetBool(CS_PlayerAnimatorParams.deadHash, _health.isDead);
        if (Has(CS_PlayerAnimatorParams.attackSpeedHash)) _animator.SetFloat(CS_PlayerAnimatorParams.attackSpeedHash, _stats.comboSpeedRate);
    }

    // 見た目の上での接地。本当に接地しているか、落ちている(上がっていない)間に足元から_stepHeight以内に地面があれば接地とみなす
    // (ジャンプで上がっている間は地面が近くても接地にしないので、ジャンプのモーションは今まで通り再生される)
    private bool IsVisuallyGrounded(float verticalSpeed)
    {
        if (_player.isGrounded) return true;
        if (_stepHeight <= 0f || verticalSpeed > 0.1f) return false;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float footOffset = _collider != null ? transform.position.y - _collider.bounds.min.y : 0f;
        return Physics.Raycast(origin, Vector3.down, 0.1f + footOffset + _stepHeight, _stepGroundLayers, QueryTriggerInteraction.Ignore);
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
        float reference = Mathf.Max(0.01f, _stats.currentMoveSpeed);
        Vector2 move = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / reference, 1f);

        SetFloat(CS_PlayerAnimatorParams.moveXHash, move.x, deltaTime);
        SetFloat(CS_PlayerAnimatorParams.moveZHash, move.y, deltaTime);
        SetFloat(CS_PlayerAnimatorParams.speedHash, move.magnitude, deltaTime);

        // 通常の移動の速さと、走る速さ(ダッシュボタンを押し続けている間)の中間より速ければ走っている
        float sprintThreshold = Mathf.Max(reference * 1.1f, (reference + _stats.currentSprintSpeed) * 0.5f);
        SetBool(CS_PlayerAnimatorParams.sprintHash, velocity.magnitude > sprintThreshold);
    }

    private void HandleJumped() => PerformOwnerAction(ActionType.Jump, 0);
    private void HandleDashStarted() => PerformOwnerAction(ActionType.Dash, 0);
    private void HandleAttackStepStarted(int step) => PerformOwnerAction(ActionType.Attack, step);
    private void HandleSpecialStarted() => PerformOwnerAction(ActionType.Special, 0);
    // 奇襲は、当たるまでの時間(ミリ秒)を一緒に送る(当たる瞬間に打撃が来るよう、各クライアントがモーションの速さを決める)
    private void HandleAmbushStarted(CS_VillainHealth villain) => PerformOwnerAction(ActionType.Ambush, Mathf.RoundToInt(_ambush.EstimateTimeToHit(villain) * 1000f));

    // パリー(反撃)は全クライアントで発生するので、通知せずにそれぞれで再生する
    private void HandleParried(GameObject target) => PlayAction(ActionType.Parry, 0);

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
                SetInteger(CS_PlayerAnimatorParams.comboStepHash, GetAttackMotion(parameter));
                SetTrigger(CS_PlayerAnimatorParams.attackHash);
                break;
            case ActionType.Special:
                SetTrigger(CS_PlayerAnimatorParams.specialHash);
                break;
            case ActionType.Parry:
                SetTrigger(CS_PlayerAnimatorParams.parryHash);
                break;
            case ActionType.Ambush:
                SetFloat(CS_PlayerAnimatorParams.ambushSpeedHash, GetAmbushSpeed(parameter));
                SetTrigger(CS_PlayerAnimatorParams.ambushHash);
                break;
        }
    }

    // 通常攻撃の段に使うモーションの番号
    private int GetAttackMotion(int step)
    {
        if (_attackMotionForStep != null && step >= 0 && step < _attackMotionForStep.Length) return _attackMotionForStep[step];

        return step % _attackMotionCount;
    }

    // 当たるまでの時間(ミリ秒)に、モーションの当たる瞬間が重なる速さ
    private float GetAmbushSpeed(int timeToHitMilliseconds)
    {
        float timeToHit = timeToHitMilliseconds / 1000f;
        if (timeToHit <= 0f || _ambushImpactTime <= 0f) return 1f;

        return Mathf.Clamp(_ambushImpactTime / timeToHit, _minAmbushSpeed, _maxAmbushSpeed);
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

    // なめらかにせず、すぐにその値にする
    private void SetFloat(int hash, float value)
    {
        if (Has(hash)) _animator.SetFloat(hash, value);
    }
}
