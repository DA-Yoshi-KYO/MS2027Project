using Unity.Cinemachine;
using UnityEngine;

/*
 * 攻撃の判定が出る瞬間に、自分のカメラを揺らすクラス
 * CS_PlayerAttack / CS_PlayerSpecialAttackのイベントを受けて、CinemachineのImpulseを発生させるだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても攻撃は動く)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・揺れの強さは攻撃ごとにCSO_AttackDataの Camera Shake Force で決める(0なら揺れない)
 *   通常攻撃の各段、必殺技それぞれ別の値にできる
 * ・揺れ方(長さ・形)はこのオブジェクトのCinemachineImpulseSourceのImpulse Definitionで決める
 *   カメラ側はCinemachineCameraに付いているCinemachineImpulseListenerが受け取る(Gainで全体の強さを一括調整できる)
 * ・揺れるのは自分のカメラだけ。イベントは操作しているクライアントでしか発生せず、
 *   他人のプレイヤーのCinemachineCameraは無効化されているため
 * ・見た目の演出用なので通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_PlayerAttack))]
[RequireComponent(typeof(CS_PlayerSpecialAttack))]
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CS_PlayerCameraShake : MonoBehaviour
{
    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _special;
    private CinemachineImpulseSource _impulseSource;

    private void Awake()
    {
        _attack = GetComponent<CS_PlayerAttack>();
        _special = GetComponent<CS_PlayerSpecialAttack>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnEnable()
    {
        _attack.onStepHitTiming += HandleStepHitTiming;
        _special.onSpecialHitTiming += HandleSpecialHitTiming;
    }

    private void OnDisable()
    {
        _attack.onStepHitTiming -= HandleStepHitTiming;
        _special.onSpecialHitTiming -= HandleSpecialHitTiming;
    }

    private void HandleStepHitTiming(int step)
    {
        var steps = _attack.attackSteps;
        if (step < 0 || step >= steps.Count) return;

        Shake(steps[step]);
    }

    private void HandleSpecialHitTiming()
    {
        Shake(_special.specialAttackData);
    }

    private void Shake(CSO_AttackData data)
    {
        if (data == null || data.cameraShakeForce <= 0f) return;

        _impulseSource.GenerateImpulseWithForce(data.cameraShakeForce);
    }
}
