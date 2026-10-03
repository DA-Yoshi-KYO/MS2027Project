using System.Collections.Generic;
using UnityEngine;

/*
 * 悪人の見た目(アニメーション)を担当するクラス
 * ゲームロジック(CS_VillainCombat、CS_VillainKnockback)の状態を読み取り、Animatorへ反映するだけで、
 * ゲームロジック側はこのクラスの存在を知らない。見た目を差し替えても、外してもゲームは動く
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ■ ネットワークの考え方
 *   Animatorは全クライアントがローカルで動かす(NetworkAnimatorは使わない。プレイヤーと同じ)
 *   ・移動       : 座標の変化(NetworkTransformで同期済み)から自分で計算する
 *   ・溜め・攻撃 : 同期済みの攻撃の段階(CS_VillainCombat.attackPhase)から判定する
 *   ・くらい     : 同期済みのノックバック回数(CS_VillainKnockback.knockbackCount)が増えたら再生する
 *
 * ■ 差し替え
 *   ・Animatorは自動で子オブジェクトから探す(_animatorが空のとき)。子のModelを差し替えるだけでよい
 *   ・Controller側に用意するパラメータはCS_VillainAnimatorParamsを参照。無いパラメータは無視される
 *   ・ルートモーションは使わない(移動はNavMeshAgentが行うため、強制的にオフにする)
 *   ・仮の見た目は Tools/Villain/仮の見た目を組み込む で作る(CSED_VillainTempVisualBuilder)
 */
// ========================================

[RequireComponent(typeof(CS_VillainCombat))]
public class CS_VillainVisual : MonoBehaviour
{
    [SerializeField]
    [Tooltip("見た目のAnimator。空なら子オブジェクトから探す")]
    private Animator _animator;

    [SerializeField]
    [Tooltip("移動の速さの基準にするプレイヤーのステータス(DB_PlayerStats)。この速さで走るとSpeedが1になる")]
    private CSO_PlayerStats _playerBaseStats;

    [SerializeField, Min(0f)]
    [Tooltip("移動パラメータのなめらかさ(小さいほど素早く追従)")]
    private float _locomotionDamping = 0.08f;

    [SerializeField, Min(0f)]
    [Tooltip("これを超える座標の変化はテレポートとみなして無視する(m/秒)")]
    private float _maxTrackedSpeed = 40f;

    private CS_VillainCombat _combat;
    private CS_VillainKnockback _knockback;
    private readonly HashSet<int> _availableParams = new HashSet<int>();
    private Vector3 _lastPosition;
    private int _lastKnockbackCount;

    private float referenceSpeed => _playerBaseStats != null && _playerBaseStats.moveSpeed > 0f ? _playerBaseStats.moveSpeed : 1f;

    private void Awake()
    {
        _combat = GetComponent<CS_VillainCombat>();
        _knockback = GetComponent<CS_VillainKnockback>();

        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
        {
            _animator.applyRootMotion = false;
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                _availableParams.Add(parameter.nameHash);
            }
        }

        _lastPosition = transform.position;
    }

    private void Start()
    {
        // 途中参加などで、生成前のノックバックを再生しないようにする
        if (_knockback != null) _lastKnockbackCount = _knockback.knockbackCount;
    }

    private void LateUpdate()
    {
        if (_animator == null) return;

        UpdateLocomotion(Time.deltaTime);
        UpdateAttack();
        UpdateHit();
    }

    // 座標の変化から、体から見た移動方向と速さを求める(クライアントでも同じ計算で済む)
    private void UpdateLocomotion(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        Vector3 velocity = (transform.position - _lastPosition) / deltaTime;
        _lastPosition = transform.position;
        velocity.y = 0f;
        if (velocity.magnitude > _maxTrackedSpeed) velocity = Vector3.zero;

        Vector3 local = transform.InverseTransformDirection(velocity) / referenceSpeed;
        SetFloat(CS_VillainAnimatorParams.moveXHash, local.x, deltaTime);
        SetFloat(CS_VillainAnimatorParams.moveZHash, local.z, deltaTime);
        SetFloat(CS_VillainAnimatorParams.speedHash, new Vector2(local.x, local.z).magnitude, deltaTime);
    }

    // 逃走などで反撃が止まったら、攻撃の見た目もやめる
    private void UpdateAttack()
    {
        CSE_VillainAttackPhase phase = _combat.enabled ? _combat.attackPhase : CSE_VillainAttackPhase.None;
        SetBool(CS_VillainAnimatorParams.chargingHash, phase == CSE_VillainAttackPhase.Charge);
        SetBool(CS_VillainAnimatorParams.attackingHash, phase == CSE_VillainAttackPhase.Hit);
    }

    // ノックバック回数が増えたら、くらいモーションを再生する
    private void UpdateHit()
    {
        if (_knockback == null) return;

        int count = _knockback.knockbackCount;
        if (count == _lastKnockbackCount) return;

        _lastKnockbackCount = count;
        if (Has(CS_VillainAnimatorParams.hitHash)) _animator.SetTrigger(CS_VillainAnimatorParams.hitHash);
    }

    private void SetBool(int hash, bool value)
    {
        if (Has(hash)) _animator.SetBool(hash, value);
    }

    private void SetFloat(int hash, float value, float deltaTime)
    {
        if (Has(hash)) _animator.SetFloat(hash, value, _locomotionDamping, deltaTime);
    }

    private bool Has(int hash)
    {
        return _availableParams.Contains(hash);
    }
}
