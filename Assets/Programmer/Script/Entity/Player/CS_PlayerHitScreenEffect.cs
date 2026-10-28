using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/*
 * 攻撃を当てた瞬間に、自分の画面へ一瞬だけ画面効果(集中線)をかけるクラス
 * CS_PlayerAttack / CS_PlayerSpecialAttackのイベントを受けて、CS_PlayerScreenEffectへ効果を1回出すよう伝えるだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても攻撃は動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・当てたかどうかは、判定が出る瞬間に手元で同じ範囲(CS_AttackHitDetector)に相手がいるかで決める
 *   (実際のダメージ判定はサーバーで行うため、ここは見た目用の確認。空振りでは出ない。CS_PlayerCameraShakeと同じ方法)
 * ・かける効果は1つのVolume Profile(_hitEffect)。見た目も長さもこのProfileで調整する
 *   集中線: CSV_SpeedLines / 長さ: CSV_ScreenEffectTimingのPlay Duration・Play Weight Curve
 * ・自分の画面のプレイヤーかどうかの判定はCS_PlayerScreenEffect側で行う(NPC・他人の攻撃ではかからない)
 */
// ========================================

[RequireComponent(typeof(CS_PlayerAttack))]
[RequireComponent(typeof(CS_PlayerSpecialAttack))]
[RequireComponent(typeof(CS_PlayerScreenEffect))]
public class CS_PlayerHitScreenEffect : MonoBehaviour
{
    [Header("かける効果")]
    [SerializeField] private VolumeProfile _hitEffect;   // 攻撃を当てた瞬間にかける効果(集中線)

    private const int _hitBufferSize = 16;

    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _special;
    private CS_PlayerScreenEffect _screenEffect;
    private readonly Collider[] _hitBuffer = new Collider[_hitBufferSize];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

    private void Awake()
    {
        _attack = GetComponent<CS_PlayerAttack>();
        _special = GetComponent<CS_PlayerSpecialAttack>();
        _screenEffect = GetComponent<CS_PlayerScreenEffect>();
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

        PlayIfHit(steps[step], _attack.targetLayers);
    }

    private void HandleSpecialHitTiming()
    {
        PlayIfHit(_special.specialAttackData, _special.targetLayers);
    }

    // 判定の範囲に相手がいる(当たった)時だけかける
    private void PlayIfHit(CSO_AttackData data, LayerMask targetLayers)
    {
        if (data == null || _hitEffect == null) return;

        CS_AttackHitDetector.FindTargets(transform, data, targetLayers, _hitBuffer, _hitTargets);
        if (_hitTargets.Count == 0) return;

        _screenEffect.Play(_hitEffect);
    }
}
