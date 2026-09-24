using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの3段コンボ攻撃を行うクラス
 * 攻撃ボタンを続けて押すと、1段目 → 2段目 → 3段目と派生する
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・各段の数値や追加効果は CSO_AttackData(ScriptableObject) で決める
 *   Attack Steps に 1段目、2段目、3段目の順で割り当てる
 * ・派生できる受付時間は、各段の CSO_AttackData の Combo Window で変更できる
 * ・コンボの流れ
 *   待機 → 攻撃ボタン → [n段目] → 攻撃モーション終了 → 受付時間内にボタン → [n+1段目]
 *   ・攻撃モーション中に押したボタンは「先行入力」として覚え、モーション終了と同時に派生する
 *   ・受付時間を過ぎる、または最終段が終わると待機に戻る
 * ・攻撃判定の流れ
 *   1. 操作しているプレイヤー(Owner)が、判定のタイミングでHitRpcを呼ぶ
 *   2. サーバーが正面に球状の判定を出し、当たったIDamageableのTakeDamageを呼ぶ
 *   ※ オフライン(NetworkManagerが動いていない)のテストシーンでは、その場で判定する
 * ・入力(攻撃ボタン)はCS_Playerが持っているものを使う
 * ・フレンドリーファイアは常に有効(チーム判定なし)。IDamageableを実装していれば
 *   プレイヤーだろうと敵だろうと関係なく当たる
 * ・実際のダメージ = CSO_AttackData.CalculateDamage() × CS_PlayerStats.attackPower(倍率)
 * ・ヒットする度に、各段のGauge Gain分だけCS_PlayerSpecialGaugeが溜まる
 * ・必殺技(CS_PlayerSpecialAttack)を行っている間は、通常攻撃を行わない(isAttacking/isPerformingSpecialで排他制御)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerStats))]
[RequireComponent(typeof(CS_PlayerSpecialGauge))]
[RequireComponent(typeof(CS_PlayerSpecialAttack))]
public class CS_PlayerAttack : NetworkBehaviour
{
    [Header("コンボ")]
    [SerializeField] private CSO_AttackData[] _attackSteps;     // 1段目、2段目、3段目の順に割り当てる

    [Header("攻撃判定")]
    [SerializeField] private LayerMask _targetLayers;           // ダメージを与える対象のレイヤー

    private const int _noStep = -1;         // 待機中(攻撃していない)を表す値
    private const int _hitBufferSize = 16;  // 一度に判定できるコライダーの上限

    private CS_Player _player;
    private CS_PlayerStats _stats;
    private CS_PlayerSpecialGauge _gauge;
    private CS_PlayerSpecialAttack _specialAttack;
    private readonly Collider[] _hitBuffer = new Collider[_hitBufferSize];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

    private int _currentStep = _noStep;     // 現在の段(0始まり)
    private float _elapsed;                 // 現在の段が始まってからの経過時間
    private bool _hasHit;                   // 現在の段の攻撃判定を行ったか
    private bool _isInputBuffered;          // 攻撃モーション中に次の入力があったか(先行入力)

    public bool isAttacking => _currentStep != _noStep;   // コンボ中か(CS_PlayerSpecialAttackが参照)
    public int currentStep => _currentStep;               // 現在の段(0始まり、攻撃していなければ-1)
    public IReadOnlyList<CSO_AttackData> attackSteps => _attackSteps;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _stats = GetComponent<CS_PlayerStats>();
        _gauge = GetComponent<CS_PlayerSpecialGauge>();
        _specialAttack = GetComponent<CS_PlayerSpecialAttack>();

        if (HasValidSteps()) return;

        Debug.LogError("CS_PlayerAttack: Attack Steps が未設定、または空の要素があります", this);
        enabled = false;
    }

    private void Update()
    {
        // 自分が操作していないプレイヤー、必殺技中は何もしない
        if (!_player.canAct || _specialAttack.isPerformingSpecial) return;

        if (_player.attackAction.WasPressedThisFrame())
        {
            OnAttackPressed();
        }

        UpdateCombo();
    }

    // 攻撃ボタンが押されたときの処理
    private void OnAttackPressed()
    {
        // 待機中 → 1段目から開始する
        if (_currentStep == _noStep)
        {
            StartStep(0);
            return;
        }

        // 攻撃モーション中 → 先行入力として覚えておく
        if (!IsStepFinished())
        {
            _isInputBuffered = true;
            return;
        }

        // モーション終了後の受付時間内 → 次の段へ派生する
        TryStartNextStep();
    }

    // 経過時間を進め、判定の発生とコンボの終了を管理する
    private void UpdateCombo()
    {
        if (_currentStep == _noStep) return;

        CSO_AttackData step = _attackSteps[_currentStep];
        _elapsed += Time.deltaTime;

        // 判定のタイミングになったら、一度だけ判定を依頼する
        if (!_hasHit && _elapsed >= step.hitDelay)
        {
            _hasHit = true;
            RequestHit(_currentStep);
        }

        // 攻撃モーション中は、これ以上何もしない
        if (!IsStepFinished()) return;

        // 先行入力があれば、すぐに次の段へ派生する
        if (_isInputBuffered && TryStartNextStep()) return;

        // 最終段が終わった、または受付時間を過ぎたらコンボ終了
        if (!HasNextStep() || _elapsed >= step.duration + step.comboWindow)
        {
            ResetCombo();
        }
    }

    // 指定した段の攻撃を開始する
    private void StartStep(int stepIndex)
    {
        _currentStep = stepIndex;
        _elapsed = 0f;
        _hasHit = false;
        _isInputBuffered = false;
    }

    // 次の段があれば派生する(派生できたらtrue)
    private bool TryStartNextStep()
    {
        if (!HasNextStep()) return false;

        StartStep(_currentStep + 1);
        return true;
    }

    // コンボを終了して待機に戻る
    private void ResetCombo()
    {
        _currentStep = _noStep;
        _elapsed = 0f;
        _hasHit = false;
        _isInputBuffered = false;
    }

    private bool HasNextStep()
    {
        return _currentStep + 1 < _attackSteps.Length;
    }

    // 現在の段の攻撃モーションが終わったか
    private bool IsStepFinished()
    {
        return _elapsed >= _attackSteps[_currentStep].duration;
    }

    private bool HasValidSteps()
    {
        if (_attackSteps == null || _attackSteps.Length == 0) return false;

        foreach (CSO_AttackData step in _attackSteps)
        {
            if (step == null) return false;
        }

        return true;
    }

    // 攻撃判定を依頼する(ダメージはサーバーで確定させる)
    private void RequestHit(int stepIndex)
    {
        // オフライン(テストシーン)では、その場で判定する
        if (!IsSpawned)
        {
            ExecuteHit(stepIndex);
            return;
        }

        HitRpc(stepIndex);
    }

    // Ownerからサーバーへ、判定の実行を依頼する
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void HitRpc(int stepIndex)
    {
        ExecuteHit(stepIndex);
    }

    // 攻撃判定を行い、当たった相手のダメージ処理を呼ぶ(サーバー、またはオフラインで実行される)
    private void ExecuteHit(int stepIndex)
    {
        // 不正な段番号は無視する(クライアントからの値は信用しない)
        if (stepIndex < 0 || stepIndex >= _attackSteps.Length) return;

        CSO_AttackData step = _attackSteps[stepIndex];
        AttackContext context = new AttackContext(transform, stepIndex);

        CS_AttackHitDetector.FindTargets(transform, step, _targetLayers, _hitBuffer, _hitTargets);

        foreach (IDamageable target in _hitTargets)
        {
            float damage = step.CalculateDamage(context, target) * _stats.attackPower;
            target.TakeDamage(damage);
            step.OnHit(context, target);
            _gauge.Fill(step.gaugeGain);
        }
    }

    // 選択中に、各段の判定範囲をシーンビューへ表示する(調整用)
    private void OnDrawGizmosSelected()
    {
        if (_attackSteps == null) return;

        Gizmos.color = Color.red;
        foreach (CSO_AttackData step in _attackSteps)
        {
            if (step == null) continue;

            Gizmos.DrawWireSphere(transform.position + transform.forward * step.hitRange, step.hitRadius);
        }
    }
}
