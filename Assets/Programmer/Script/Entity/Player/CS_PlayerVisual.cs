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
    }

    [SerializeField] private Animator _animator;
    [SerializeField] private float _locomotionDamping = 0.08f;  // 移動パラメータのなめらかさ(小さいほど素早く追従)
    [SerializeField] private float _maxTrackedSpeed = 40f;      // これを超える座標の変化はテレポートとみなして無視する(m/秒)

    private CS_Player _player;
    private CS_PlayerStats _stats;
    private CS_PlayerHealth _health;
    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _special;

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
        if (_animator == null) return;

        UpdateLocomotion(Time.deltaTime);
        SetBool(CS_PlayerAnimatorParams.groundedHash, _player.isGrounded);
        SetBool(CS_PlayerAnimatorParams.deadHash, _health.isDead);
    }

    // Controllerが持っているパラメータを控え、無いパラメータへの操作を無視できるようにする
    private void CacheAnimatorParams()
    {
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
