using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/*
 * 攻撃を当てた時・攻撃を当てられた時に、自分のカメラを揺らすクラス
 * CS_PlayerAttack / CS_PlayerSpecialAttack / CS_PlayerHealthのイベントを受けて、CinemachineのImpulseを発生させるだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても攻撃は動く)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様(プレイヤー「攻撃時の演出について」): 敵に攻撃を当てられた or 当てた時に、カメラを揺らす
 * ・当てた時: 判定が出る瞬間に、手元で同じ範囲(CS_AttackHitDetector)に相手がいるか確かめ、いた時だけ揺らす
 *   (実際のダメージ判定はサーバーで行うため、ここは見た目用の確認。空振りでは揺れない)
 *   強さは攻撃ごとにCSO_AttackDataの Camera Shake Force で決める(0なら揺れない)
 * ・当てられた時: 自分のHPが減った時に _damagedShakeForce の強さで揺らす(0なら揺れない)
 * ・揺れ方(長さ・形)はこのオブジェクトのCinemachineImpulseSourceのImpulse Definitionで決める
 *   カメラ側はCinemachineCameraに付いているCinemachineImpulseListenerが受け取る(Gainで全体の強さを一括調整できる)
 * ・揺れるのは自分のカメラだけ。攻撃のイベントは操作しているクライアントでしか発生せず、
 *   被弾は自分が操作しているプレイヤー(CS_Player.isControlled)の時だけ揺らす
 *   (他人のプレイヤーのCinemachineCameraは無効化されている)
 * ・見た目の演出用なので通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerHealth))]
[RequireComponent(typeof(CS_PlayerAttack))]
[RequireComponent(typeof(CS_PlayerSpecialAttack))]
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CS_PlayerCameraShake : MonoBehaviour
{
    [Header("被弾")]
    [SerializeField] private float _damagedShakeForce = 0.3f;   // 攻撃を当てられた時の揺れの強さ(0で揺れない。仮の値)

    private const int _hitBufferSize = 16;

    private CS_Player _player;
    private CS_PlayerHealth _health;
    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _special;
    private CinemachineImpulseSource _impulseSource;
    private readonly Collider[] _hitBuffer = new Collider[_hitBufferSize];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

    // 前回のHP(減ったかどうかの判定に使う)
    // スポーン時の同期などで変更通知が来ないこともあるため、毎フレーム現在値で取り直す
    private float _lastHp;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _health = GetComponent<CS_PlayerHealth>();
        _attack = GetComponent<CS_PlayerAttack>();
        _special = GetComponent<CS_PlayerSpecialAttack>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnEnable()
    {
        _lastHp = _health.currentHp;

        _attack.onStepHitTiming += HandleStepHitTiming;
        _special.onSpecialHitTiming += HandleSpecialHitTiming;
        _health.onHpChanged += HandleHpChanged;
    }

    private void OnDisable()
    {
        _attack.onStepHitTiming -= HandleStepHitTiming;
        _special.onSpecialHitTiming -= HandleSpecialHitTiming;
        _health.onHpChanged -= HandleHpChanged;
    }

    private void LateUpdate()
    {
        _lastHp = _health.currentHp;
    }

    private void HandleStepHitTiming(int step)
    {
        var steps = _attack.attackSteps;
        if (step < 0 || step >= steps.Count) return;

        ShakeIfHit(steps[step], _attack.targetLayers);
    }

    private void HandleSpecialHitTiming()
    {
        ShakeIfHit(_special.specialAttackData, _special.targetLayers);
    }

    // 自分のHPが減った(攻撃を当てられた)時に揺らす。回復・復活では揺らさない
    private void HandleHpChanged(float current, float max)
    {
        bool isDamaged = current < _lastHp;
        _lastHp = current;

        if (!isDamaged || !_player.isControlled) return;

        Shake(_damagedShakeForce);
    }

    // 判定の範囲に相手がいる(当たった)時だけ揺らす
    private void ShakeIfHit(CSO_AttackData data, LayerMask targetLayers)
    {
        if (data == null || data.cameraShakeForce <= 0f) return;

        CS_AttackHitDetector.FindTargets(transform, data, targetLayers, _hitBuffer, _hitTargets);
        if (_hitTargets.Count == 0) return;

        Shake(data.cameraShakeForce);
    }

    private void Shake(float force)
    {
        if (force <= 0f) return;

        _impulseSource.GenerateImpulseWithForce(force);
    }
}
