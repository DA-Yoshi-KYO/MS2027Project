using UnityEngine;

/*
 * 警察の攻撃判定が出た瞬間に、判定の位置へエフェクトを出すクラス
 * CS_PoliceAttackのイベントを受けて、CS_NetworkEffectPlayerで全員の画面に出すだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても攻撃は動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出すエフェクト: PoliceAttack(見た目はDB_EffectDataBaseで決める)
 *   攻撃の予告(赤い光。CS_PoliceAttackEffect)が消えるのと同じ瞬間に出る。当たったかどうかは関係ない
 * ・攻撃(チャージ・攻撃判定)はサーバー(またはオフライン)でしか動かないので、
 *   サーバーでイベントを受けて、CS_NetworkEffectPlayerで全員に送る
 * ・出す位置は攻撃判定(球)の中心、向きは攻撃の向き
 */
// ========================================

[RequireComponent(typeof(CS_PoliceAttack))]
[RequireComponent(typeof(CS_NetworkEffectPlayer))]
public class CS_PoliceAttackImpactEffect : MonoBehaviour
{
    private CS_PoliceAttack _attack;
    private CS_NetworkEffectPlayer _effectPlayer;

    private void Awake()
    {
        _attack = GetComponent<CS_PoliceAttack>();
        _effectPlayer = GetComponent<CS_NetworkEffectPlayer>();
    }

    private void OnEnable()
    {
        _attack.onAttackFired += HandleAttackFired;
    }

    private void OnDisable()
    {
        _attack.onAttackFired -= HandleAttackFired;
    }

    private void HandleAttackFired(Vector3 center, Vector3 direction)
    {
        Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : transform.rotation;
        _effectPlayer.PlayForEveryone(CSE_EffectId.PoliceAttack, center, rotation);
    }
}
