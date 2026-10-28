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
 *   通常攻撃の各段が始まった時 : PlayerAttack1〜5(こぶしの軌跡。プレイヤーに付いていく。6段目以降があれば5段目のものを使う)
 *     どちらの手で殴るかは、DB_EffectDataBaseで段ごとに右手用・左手用のPrefabを選んで決める
 *   通常攻撃の判定が出た瞬間   : PlayerAttackImpact(最後の段だけ PlayerAttackFinishImpact)。空振りでも出す
 *     位置は判定の中心(プレイヤーの正面 Hit Range の所)の、Impact Height の高さ
 *   必殺技を発動した時         : PlayerSpecialStart(プレイヤーに付いていく)
 *   必殺技の判定が出た瞬間     : PlayerSpecialHit(その場に出す)
 * ・攻撃のイベントは操作しているクライアント(NPCはホスト)でしか起きないので、
 *   CS_NetworkEffectPlayerで他の人の画面にも送る
 * ・殴った所の衝撃以外は、プレイヤーの足元(Transformの位置)に出す。高さなどはエフェクトのPrefab側で決める
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

    [SerializeField] private float _impactHeight = 1.1f;   // 殴った所の衝撃を出す高さ(足元から。m)

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
        _attack.onStepHitTiming += HandleStepHitTiming;
        _special.onSpecialStarted += HandleSpecialStarted;
        _special.onSpecialHitTiming += HandleSpecialHitTiming;
    }

    private void OnDisable()
    {
        _attack.onStepStarted -= HandleStepStarted;
        _attack.onStepHitTiming -= HandleStepHitTiming;
        _special.onSpecialStarted -= HandleSpecialStarted;
        _special.onSpecialHitTiming -= HandleSpecialHitTiming;
    }

    private void HandleStepStarted(int step)
    {
        if (step < 0) return;

        CSE_EffectId id = _stepEffects[Mathf.Min(step, _stepEffects.Length - 1)];
        _effectPlayer.PlayForEveryoneAttached(id, Vector3.zero, Quaternion.identity);
    }

    // 判定の中心に、殴った所の衝撃を出す(最後の段は強いもの)
    private void HandleStepHitTiming(int step)
    {
        var steps = _attack.attackSteps;
        if (step < 0 || step >= steps.Count || steps[step] == null) return;

        bool isLastStep = step == steps.Count - 1;
        CSE_EffectId id = isLastStep ? CSE_EffectId.PlayerAttackFinishImpact : CSE_EffectId.PlayerAttackImpact;
        Vector3 position = transform.position + Vector3.up * _impactHeight + transform.forward * steps[step].hitRange;
        _effectPlayer.PlayForEveryone(id, position, transform.rotation);
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
