using UnityEngine;

/*
 * 通常攻撃・必殺技のエフェクトを出すクラス
 * CS_PlayerAttack / CS_PlayerSpecialAttackのイベントを受けて、CS_NetworkEffectPlayerで全員の画面に出すだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても攻撃は動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出すエフェクト(見た目はDB_EffectDataBaseで決める)
 *   通常攻撃の各段が始まった時 : PlayerAttack1〜5(プレイヤーに付いていく。6段目以降があれば5段目のものを使う)
 *   必殺技を発動した時         : PlayerSpecialStart(プレイヤーに付いていく)
 *   必殺技の判定が出た瞬間     : PlayerSpecialHit(その場に出す)
 * ・攻撃のイベントは操作しているクライアント(NPCはホスト)でしか起きないので、
 *   CS_NetworkEffectPlayerで他の人の画面にも送る
 * ・エフェクトの位置はプレイヤーの足元(Transformの位置)。高さなどはエフェクトのPrefab側で決める
 */
// ========================================

[RequireComponent(typeof(CS_PlayerAttack))]
[RequireComponent(typeof(CS_PlayerSpecialAttack))]
[RequireComponent(typeof(CS_NetworkEffectPlayer))]
public class CS_PlayerAttackEffect : MonoBehaviour
{
    // 通常攻撃の段ごとのエフェクト(段の番号 = 配列の番号)
    private static readonly CSE_EffectId[] _stepEffects =
    {
        CSE_EffectId.PlayerAttack1,
        CSE_EffectId.PlayerAttack2,
        CSE_EffectId.PlayerAttack3,
        CSE_EffectId.PlayerAttack4,
        CSE_EffectId.PlayerAttack5,
    };

    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _special;
    private CS_NetworkEffectPlayer _effectPlayer;

    private void Awake()
    {
        _attack = GetComponent<CS_PlayerAttack>();
        _special = GetComponent<CS_PlayerSpecialAttack>();
        _effectPlayer = GetComponent<CS_NetworkEffectPlayer>();
    }

    private void OnEnable()
    {
        _attack.onStepStarted += HandleStepStarted;
        _special.onSpecialStarted += HandleSpecialStarted;
        _special.onSpecialHitTiming += HandleSpecialHitTiming;
    }

    private void OnDisable()
    {
        _attack.onStepStarted -= HandleStepStarted;
        _special.onSpecialStarted -= HandleSpecialStarted;
        _special.onSpecialHitTiming -= HandleSpecialHitTiming;
    }

    private void HandleStepStarted(int step)
    {
        if (step < 0) return;

        CSE_EffectId id = _stepEffects[Mathf.Min(step, _stepEffects.Length - 1)];
        _effectPlayer.PlayForEveryoneAttached(id, Vector3.zero, Quaternion.identity);
    }

    private void HandleSpecialStarted()
    {
        _effectPlayer.PlayForEveryoneAttached(CSE_EffectId.PlayerSpecialStart, Vector3.zero, Quaternion.identity);
    }

    private void HandleSpecialHitTiming()
    {
        _effectPlayer.PlayForEveryone(CSE_EffectId.PlayerSpecialHit, transform.position, transform.rotation);
    }
}
