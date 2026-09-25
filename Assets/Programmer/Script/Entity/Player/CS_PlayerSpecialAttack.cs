using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * 必殺ゲージが満タンの時に使える必殺技を行うクラス
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・変身中(CS_PlayerTransformation.isTransformed)かつ必殺ゲージ(CS_PlayerSpecialGauge)が満タンでないと発動できない
 * ・通常攻撃(CS_PlayerAttack)のコンボ中は発動できず、必殺技を行っている間は通常攻撃もできない
 *   (お互いのisAttacking/isPerformingSpecialを見て排他制御している)
 * ・流れ
 *   満タン中にボタン → 発動 → Special Attack DataのhitDelay秒後に判定・ゲージ消費 → duration秒後に終了
 *   判定タイミングで改めて変身中かつゲージが満タンか確認してから消費するため、
 *   発動直後に状態が変化していた場合は不発(ダメージなし・ゲージ消費なし)になる
 * ・判定・ダメージの仕組みはCS_PlayerAttackと同じ(CS_AttackHitDetector、IDamageable、サーバー確定)
 * ・必殺技自体はゲージを増やさない想定(Special Attack DataのGauge Gainは0を推奨)
 * ・ダメージ倍率はCS_PlayerStats.specialAttackPowerを使う(通常攻撃のattackPowerとは別枠)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerStats))]
[RequireComponent(typeof(CS_PlayerSpecialGauge))]
[RequireComponent(typeof(CS_PlayerTransformation))]
public class CS_PlayerSpecialAttack : NetworkBehaviour
{
    [Header("必殺技")]
    [SerializeField] private CSO_AttackData _specialAttackData;

    [Header("攻撃判定")]
    [SerializeField] private LayerMask _targetLayers;           // ダメージを与える対象のレイヤー

    private const int _specialStepIndex = -1;   // AttackContext上で「必殺技」を表す値
    private const int _hitBufferSize = 16;

    private CS_Player _player;
    private CS_PlayerStats _stats;
    private CS_PlayerSpecialGauge _gauge;
    private CS_PlayerAttack _attack;
    private CS_PlayerTransformation _transformation;
    private readonly Collider[] _hitBuffer = new Collider[_hitBufferSize];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

    private bool _isPerforming;
    private float _elapsed;
    private bool _hasHit;

    public bool isPerformingSpecial => _isPerforming;   // 必殺技中か(CS_PlayerAttackが参照)

    // 操作しているクライアントでだけ発生する。見た目などが購読する
    public event System.Action onSpecialStarted;
    public CSO_AttackData specialAttackData => _specialAttackData;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _stats = GetComponent<CS_PlayerStats>();
        _gauge = GetComponent<CS_PlayerSpecialGauge>();
        _attack = GetComponent<CS_PlayerAttack>();
        _transformation = GetComponent<CS_PlayerTransformation>();

        if (_specialAttackData != null) return;

        Debug.LogError("CS_PlayerSpecialAttack: Special Attack Data が未設定です", this);
        enabled = false;
    }

    private void Update()
    {
        // 自分が操作していないプレイヤー、コンボ攻撃中は何もしない
        if (!_player.canAct || _attack.isAttacking) return;

        if (!_isPerforming && _transformation.isTransformed && _gauge.isFull && _player.specialAction.WasPressedThisFrame())
        {
            StartSpecial();
        }

        UpdateSpecial();
    }

    // 必殺技のモーションを開始する
    private void StartSpecial()
    {
        _isPerforming = true;
        _elapsed = 0f;
        _hasHit = false;
        onSpecialStarted?.Invoke();
    }

    // 経過時間を進め、判定の発生とモーション終了を管理する
    private void UpdateSpecial()
    {
        if (!_isPerforming) return;

        _elapsed += Time.deltaTime;

        if (!_hasHit && _elapsed >= _specialAttackData.hitDelay)
        {
            _hasHit = true;
            RequestHit();
        }

        if (_elapsed >= _specialAttackData.duration)
        {
            _isPerforming = false;
        }
    }

    // 必殺技の判定実行を依頼する(ゲージ消費の確定はサーバーで行う)
    private void RequestHit()
    {
        // オフライン(テストシーン)では、その場で判定する
        if (!IsSpawned)
        {
            ExecuteHit();
            return;
        }

        HitRpc();
    }

    // Ownerからサーバーへ、判定の実行を依頼する
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void HitRpc()
    {
        ExecuteHit();
    }

    // 変身中か、ゲージが満タンかを再確認しつつ消費し、判定を行う(サーバー、またはオフラインで実行される)
    private void ExecuteHit()
    {
        if (!_transformation.isTransformed) return;
        if (!_gauge.TryConsumeFull()) return;

        AttackContext context = new AttackContext(transform, _specialStepIndex);
        CS_AttackHitDetector.FindTargets(transform, _specialAttackData, _targetLayers, _hitBuffer, _hitTargets);

        foreach (IDamageable target in _hitTargets)
        {
            float damage = _specialAttackData.CalculateDamage(context, target) * _stats.specialAttackPower;
            target.TakeDamage(damage);
            _specialAttackData.OnHit(context, target);
        }
    }

    // 選択中に、判定範囲をシーンビューへ表示する(調整用)
    private void OnDrawGizmosSelected()
    {
        if (_specialAttackData == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position + transform.forward * _specialAttackData.hitRange, _specialAttackData.hitRadius);
    }
}
