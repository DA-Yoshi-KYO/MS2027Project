using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの5段コンボ攻撃を行うクラス(仕様書「攻撃」)
 * 攻撃ボタンを続けて押すと、1段目 → 2段目 → … → 5段目と派生する(段数はAttack Stepsの数で決まる)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・各段の数値や追加効果は CSO_AttackData(ScriptableObject) で決める
 *   Attack Steps に 1段目、2段目…の順で割り当てる(DB_PlayerAttack1〜5)
 * ・派生できる受付時間は、各段の CSO_AttackData の Combo Window で変更できる
 *   (データ表「次コンボのバッファ」1秒。モーションが終わってから1秒以内に押せば次の段へつながる)
 * ・コンボの流れ
 *   待機 → 攻撃ボタン → [n段目] → 攻撃モーション終了 → 受付時間内にボタン → [n+1段目]
 *   ・攻撃モーション中に押したボタンは「先行入力」として覚え、モーション終了と同時に派生する
 *   ・受付時間を過ぎる、または最終段が終わると待機に戻る
 * ・攻撃判定の流れ
 *   1. 操作しているプレイヤー(Owner)が、判定のタイミングでHitRpcを呼ぶ(手元の位置・向きも一緒に送る)
 *   2. サーバーが、送られてきた位置・向きの正面に球状の判定を出し、当たったIDamageableのTakeDamageを呼ぶ
 *      (手元の向きはエイムアシスト(CS_PlayerAimAssist)で補正済み。サーバー上の位置から大きく離れていれば使わない)
 *   ※ オフライン(NetworkManagerが動いていない)のテストシーンでは、その場で判定する
 * ・入力(攻撃ボタン)はCS_Playerが持っているものを使う
 *   攻撃ボタンはインタラクト(CS_PlayerInteractor)と共有。操作できるものの近く・操作中は攻撃を出さない
 * ・フレンドリーファイアは常に有効(チーム判定なし)。IDamageableを実装していれば
 *   プレイヤーだろうと敵だろうと関係なく当たる
 * ・実際のダメージ = CSO_AttackData.CalculateDamage() × CS_PlayerStats.currentAttackPower(倍率。暗躍ボーナス込み)
 * ・ヒットする度に、各段のGauge Gain分だけCS_PlayerSpecialGaugeが溜まる
 *   (仕様では通常攻撃のヒットでは溜まらないため、DB_PlayerAttack1〜5のGauge Gainは0にしている)
 * ・奇襲(CS_PlayerAmbush)はこの5段コンボとは別の攻撃
 *   待機中に攻撃ボタンを押した時、奇襲できる悪人がいれば1段目の代わりに奇襲を始める(いなければ1段目から)
 *   奇襲中は通常攻撃を出さない。奇襲が終わった後にボタンを押すと、1段目から始まる
 * ・SAでハイドポイントへ飛んでいる間(CS_PlayerHide.isHiding)は、通常攻撃を出さない
 *   着いた後は普通の状態に戻るので、いつも通り攻撃・奇襲できる(隠れられる箱の中なら、奇襲の「背後か上」は見ない)
 * ・ジャスト攻撃(手触りの調整。早押し・連打より、リズムよく押すほど強い)
 *   今の段の判定が出た後、モーションが終わる_justEarlyTime秒前から、終わった_justLateTime秒後までに押すと、次の段をすぐ出す(ジャスト)
 *   ジャストの段はダメージが_justDamageRate倍、当たると必殺ゲージが_justGaugeGain溜まる。この段で受付より前に押していた(早押し・連打)時はジャストにしない
 * ・ブリンク攻撃: 待機中に、ブリンク中か、ブリンクが終わって_blinkAttackWindow秒以内に攻撃すると、ブリンクを止めて1段目を強い一撃で出す
 *   ダメージが_blinkAttackDamageRate倍、当たると必殺ゲージが_blinkAttackGaugeGain溜まる(踏み込みはエイムアシストが行う)
 *   ジャスト・ブリンクのおまけ(StepBonus)は判定の依頼(HitRpc)と一緒にサーバーへ送り、サーバーがダメージに掛ける
 *   見た目・手触り(光る・ヒットストップ)はCS_PlayerHitFeelがonJustAttack・onBlinkAttack・currentStepBonusを見て出す
 * ・動けなくなった(死亡・スタン)時はコンボを中断する
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
    [SerializeField] private CSO_AttackData[] _attackSteps;     // 1段目、2段目…の順に割り当てる

    [Header("攻撃判定")]
    [SerializeField] private LayerMask _targetLayers;           // ダメージを与える対象のレイヤー

    [Header("ジャスト攻撃(モーションの終わり際に合わせて押すと、次の段がすぐ出て強くなる。早押し・連打ではならない)")]
    [SerializeField][Min(0f)] private float _justEarlyTime = 0.15f;      // モーションが終わる何秒前から受け付けるか
    [SerializeField][Min(0f)] private float _justLateTime = 0.2f;        // モーションが終わった何秒後まで受け付けるか
    [SerializeField][Min(1f)] private float _justDamageRate = 1.5f;      // ジャストで出した段のダメージの倍率
    [SerializeField][Min(0f)] private float _justGaugeGain = 2f;         // ジャストで出した段が当たった時に溜まる必殺ゲージ(1体につき)

    [Header("ブリンク攻撃(ブリンク中・直後に攻撃すると、踏み込んだ強い一撃になる)")]
    [SerializeField][Min(0f)] private float _blinkAttackWindow = 0.3f;   // ブリンクが終わってから何秒まで受け付けるか(ブリンク中も受け付ける。0で無し)
    [SerializeField][Min(1f)] private float _blinkAttackDamageRate = 2f; // ブリンク攻撃のダメージの倍率
    [SerializeField][Min(0f)] private float _blinkAttackGaugeGain = 3f;  // ブリンク攻撃が当たった時に溜まる必殺ゲージ(1体につき)

    private const int _noStep = -1;         // 待機中(攻撃していない)を表す値
    private const int _hitBufferSize = 16;  // 一度に判定できるコライダーの上限

    // 段に付いたおまけ(ジャスト・ブリンク攻撃)。判定の依頼と一緒にサーバーへ送る
    public enum StepBonus : byte
    {
        None = 0,
        Just = 1,    // ジャスト攻撃(モーションの終わり際に合わせて押した)
        Blink = 2,   // ブリンク攻撃(ブリンク中・直後に押した)
    }

    private CS_Player _player;
    private CS_PlayerStats _stats;
    private CS_PlayerSpecialGauge _gauge;
    private CS_PlayerSpecialAttack _specialAttack;
    private CS_PlayerScoring _scoring;
    private CS_PlayerInteractor _interactor;   // ケアパッケージなどの操作(攻撃と同じボタンを使う)。付いていなければnull
    private CS_PlayerAmbush _ambush;           // 奇襲(コンボとは別の攻撃)。付いていなければ奇襲しない
    private readonly Collider[] _hitBuffer = new Collider[_hitBufferSize];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

    private int _currentStep = _noStep;     // 現在の段(0始まり)
    private float _elapsed;                 // 現在の段が始まってからの経過時間
    private bool _hasHit;                   // 現在の段の攻撃判定を行ったか
    private bool _isInputBuffered;          // 攻撃モーション中に次の入力があったか(先行入力)
    private bool _isEarlyPressed;           // この段で、ジャストの受付より前に押したか(連打・早押しはジャストにしない)
    private StepBonus _stepBonus;           // 今の段に付いたおまけ
    private float _holdUntil;               // この時刻(Time.timeScaleの影響を受けない時間)まで、コンボの進行を止める(ヒットストップ)

    public bool isAttacking => _currentStep != _noStep || isAmbushing;   // コンボ中・奇襲中か(CS_PlayerSpecialAttackが参照)
    private bool isAmbushing => _ambush != null && _ambush.isAmbushing;

    // 操作しているクライアントでだけ発生する(引数は何段目か、0始まり)。見た目などが購読する
    public event System.Action<int> onStepStarted;
    // 操作しているクライアントでだけ発生する(引数は何段目か)。判定が出る瞬間に呼ばれる。カメラの揺れなどが購読する
    public event System.Action<int> onStepHitTiming;
    // ジャスト攻撃・ブリンク攻撃が出た時(操作しているクライアントでだけ発生する。引数は何段目か)。見た目・手触りの演出が購読する
    public event System.Action<int> onJustAttack;
    public event System.Action onBlinkAttack;
    public int currentStep => _currentStep;               // 現在の段(0始まり、攻撃していなければ-1)
    public StepBonus currentStepBonus => _stepBonus;     // 今の段に付いたおまけ(ジャスト・ブリンク攻撃)
    public IReadOnlyList<CSO_AttackData> attackSteps => _attackSteps;
    public LayerMask targetLayers => _targetLayers;       // ダメージを与える対象のレイヤー(カメラの揺れの当たり確認にも使う)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _stats = GetComponent<CS_PlayerStats>();
        _gauge = GetComponent<CS_PlayerSpecialGauge>();
        _specialAttack = GetComponent<CS_PlayerSpecialAttack>();
        _scoring = GetComponent<CS_PlayerScoring>();
        _interactor = GetComponent<CS_PlayerInteractor>();
        _ambush = GetComponent<CS_PlayerAmbush>();

        if (HasValidSteps()) return;

        Debug.LogError("CS_PlayerAttack: Attack Steps が未設定、または空の要素があります", this);
        enabled = false;
    }

    private void Update()
    {
        // 動けなくなった(死亡・スタン)ら、コンボを中断する(止まっていた判定が、動けるようになった後に出ないように)
        if (!_player.canAct)
        {
            if (isAttacking) ResetCombo();
            return;
        }

        // 必殺技中、奇襲中は何もしない
        if (_specialAttack.isPerformingSpecial || isAmbushing) return;

        // SAでハイドポイントへ飛んでいる間は、通常攻撃を出さない
        if (_player.isHiding) return;

        // ゲームパッドは攻撃(RT)と必殺技(RT+LT)が同じRTを共有するため、
        // LTを押しながらの場合は必殺技の合図とみなし、攻撃としては発動させない
        // 通常攻撃ボタンはインタラクトと共有のため、操作できるもの(ケアパッケージなど)の近く・操作中は攻撃しない
        if (_player.attackAction.WasPressedThisFrame() && !_player.isSpecialModifierHeld && !IsInteractPriority())
        {
            OnAttackPressed();
        }

        UpdateCombo();
    }

    // インタラクトを攻撃より優先する状況か(操作できるものが近くにある、または操作の途中)
    private bool IsInteractPriority()
    {
        if (_interactor == null) return false;

        return _interactor.isInteracting || _interactor.hasInteractTarget;
    }

    // 攻撃ボタンが押されたときの処理
    private void OnAttackPressed()
    {
        // 待機中 → 奇襲できる悪人がいれば奇襲。ブリンク中・直後ならブリンク攻撃。どちらでもなければ1段目から開始する
        if (_currentStep == _noStep)
        {
            if (_ambush != null && _ambush.TryStartAmbush()) return;

            if (_blinkAttackWindow > 0f && _player.timeSinceDashEnded <= _blinkAttackWindow)
            {
                _player.CancelDash();
                StartStep(0, StepBonus.Blink);
                onBlinkAttack?.Invoke();
                return;
            }

            StartStep(0);
            return;
        }

        // モーションの終わり際(ジャストの受付)に押した → 次の段をすぐ出す(早押し・連打した段はジャストにしない)
        if (IsInJustWindow())
        {
            StartStep(_currentStep + 1, StepBonus.Just);
            onJustAttack?.Invoke(_currentStep);
            return;
        }

        // 攻撃モーション中 → 先行入力として覚えておく
        if (!IsStepFinished())
        {
            _isInputBuffered = true;
            _isEarlyPressed = true;
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
        if (Time.unscaledTime < _holdUntil) return;   // ヒットストップの間は進めない(モーションと合わせる)
        _elapsed += Time.deltaTime * _stats.comboSpeedRate;   // 暗躍ボーナスでコンボのモーションが速くなる

        // 判定のタイミングになったら、一度だけ判定を依頼する
        if (!_hasHit && _elapsed >= step.hitDelay)
        {
            _hasHit = true;
            RequestHit(_currentStep, _stepBonus);
            onStepHitTiming?.Invoke(_currentStep);
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

    // 指定した段の攻撃を開始する(bonus: ジャスト・ブリンク攻撃のおまけ)
    private void StartStep(int stepIndex, StepBonus bonus = StepBonus.None)
    {
        _currentStep = stepIndex;
        _elapsed = 0f;
        _hasHit = false;
        _isInputBuffered = false;
        _isEarlyPressed = false;
        _stepBonus = bonus;
        onStepStarted?.Invoke(stepIndex);
    }

    // 次の段があれば派生する(派生できたらtrue)
    private bool TryStartNextStep()
    {
        if (!HasNextStep()) return false;

        StartStep(_currentStep + 1);
        return true;
    }

    // コンボの進行をseconds秒(Time.timeScaleの影響を受けない時間)止める(ヒットストップ。CS_PlayerHitFeelが使う)
    public void Hold(float seconds)
    {
        if (seconds <= 0f) return;
        _holdUntil = Mathf.Max(_holdUntil, Time.unscaledTime + seconds);
    }

    // コンボを終了して待機に戻る
    private void ResetCombo()
    {
        _currentStep = _noStep;
        _elapsed = 0f;
        _hasHit = false;
        _isInputBuffered = false;
        _isEarlyPressed = false;
        _stepBonus = StepBonus.None;
    }

    // ジャストの受付の間か(今の段の判定が出た後、モーションが終わる_justEarlyTime秒前から、終わった_justLateTime秒後まで。早押しした段は除く)
    private bool IsInJustWindow()
    {
        if (!_hasHit || _isEarlyPressed || !HasNextStep()) return false;

        float remaining = _attackSteps[_currentStep].duration - _elapsed;
        return remaining <= _justEarlyTime && remaining >= -_justLateTime;
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
    private void RequestHit(int stepIndex, StepBonus bonus)
    {
        // 判定は、攻撃した本人の手元の位置・向き(エイムアシストで補正済み)で行う
        // (サーバー上の位置・向きはNetworkTransformの同期で少し遅れるため、補正した向きがまだ届いていないことがある)
        Vector3 position = transform.position;
        Vector3 forward = transform.forward;

        // オフライン(テストシーン)では、その場で判定する
        if (!IsSpawned)
        {
            ExecuteHit(stepIndex, position, forward, bonus);
            return;
        }

        HitRpc(stepIndex, position, forward, (byte)bonus);
    }

    // Ownerからサーバーへ、判定の実行を依頼する
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void HitRpc(int stepIndex, Vector3 position, Vector3 forward, byte bonus)
    {
        ExecuteHit(stepIndex, position, forward, bonus <= (byte)StepBonus.Blink ? (StepBonus)bonus : StepBonus.None);
    }

    // 攻撃判定を行い、当たった相手のダメージ処理を呼ぶ(サーバー、またはオフラインで実行される)
    private void ExecuteHit(int stepIndex, Vector3 position, Vector3 forward, StepBonus bonus)
    {
        // 不正な段番号は無視する(クライアントからの値は信用しない)
        if (stepIndex < 0 || stepIndex >= _attackSteps.Length) return;

        CSO_AttackData step = _attackSteps[stepIndex];
        AttackContext context = new AttackContext(transform, stepIndex);

        // 送られてきた位置・向きが、サーバー上の位置から大きく離れていないか確かめてから使う
        CS_AttackHitDetector.ResolvePose(transform, ref position, ref forward);
        CS_AttackHitDetector.FindTargets(transform, position, forward, step, _targetLayers, _hitBuffer, _hitTargets);

        foreach (IDamageable target in _hitTargets)
        {
            float damage = step.CalculateDamage(context, target) * _stats.currentAttackPower * GetBonusDamageRate(bonus);

            DealDamage(target, damage);
            CS_AttackHitDetector.TryKnockback(target, position);   // 攻撃した位置から離れる方向へ下がる
            step.OnHit(context, target);
            _gauge.Fill(step.gaugeGain + GetBonusGaugeGain(bonus));
        }
    }

    private float GetBonusDamageRate(StepBonus bonus)
    {
        switch (bonus)
        {
            case StepBonus.Just: return _justDamageRate;
            case StepBonus.Blink: return _blinkAttackDamageRate;
            default: return 1f;
        }
    }

    private float GetBonusGaugeGain(StepBonus bonus)
    {
        switch (bonus)
        {
            case StepBonus.Just: return _justGaugeGain;
            case StepBonus.Blink: return _blinkAttackGaugeGain;
            default: return 0f;
        }
    }

    // ダメージを与える。スコア(CS_PlayerScoring)があればそれを通し、誰が倒したかをスコアに反映する
    private void DealDamage(IDamageable target, float damage)
    {
        if (_scoring != null)
        {
            _scoring.DealDamage(target, damage);
            return;
        }

        target.TakeDamage(damage, gameObject);
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
