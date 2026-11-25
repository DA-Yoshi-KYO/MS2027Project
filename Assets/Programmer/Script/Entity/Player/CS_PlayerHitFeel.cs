using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

/*
 * 攻撃の手応え(ヒットストップ・ジャスト攻撃とブリンク攻撃の光・悪人グループを殲滅した時のスロー)を出すクラス
 * 見た目・手触りだけの演出で、ダメージなどのゲームのロジックは変えない(外しても攻撃は動く)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・自分のプレイヤー(isLocalPlayer)だけで動く(攻撃のイベントは操作している本人の手元でしか起きないため)
 * ・ヒットストップ: 通常攻撃・必殺技の判定が出た瞬間に、手元で当たる相手がいれば(当たりの確定はサーバーだが、見た目は手元で判断する)、
 *   自分と当たった相手のAnimatorを一瞬止める(奇襲の演出CS_PlayerAmbushEffectと同じやり方。マルチでも他の人の世界は止めない)
 *   止める時間: 通常 _hitStopTime、最後の段 _finisherHitStopTime、必殺技 _specialHitStopTime。ジャスト・ブリンク攻撃は _bonusHitStopTime を足す
 * ・ジャストで押した瞬間(CS_PlayerAttack.onJustAttack)は、当たらなくても頭の上に「JUST!」(CS_FloatingText)を出し、効果音(_justSe)を鳴らす
 *   効果音は専用の音がまだ無いので、パリー成功の音を仮に使っている(サウンド担当が音を用意したら_justSeを差し替える)
 * ・ブリンク攻撃を出した瞬間(CS_PlayerAttack.onBlinkAttack)も同じく、頭の上に「BLINK!」を出し、効果音(_blinkSe。仮に奇襲の音)を鳴らす
 * ・ジャスト攻撃・ブリンク攻撃(CS_PlayerAttack.currentStepBonus)が当たった時は、当たった所に光(PlayerParry)をキャラクターの色で出し、
 *   画面効果(_bonusScreenEffect)とカメラの揺れ(_bonusShakeForce)を足す
 * ・殲滅スロー: 悪人グループの最後の1人を自分が倒した時(CS_PlayerScoring.onGroupDefeatedLocal)、
 *   ソロでは世界を_finishSlowScale倍の速さに_finishSlowTime秒だけする(CS_PlayerTimeScale)。マルチでは時間は変えず、画面効果と揺れだけ
 * ・時間はTime.timeScaleの影響を受けない時間で数える(スロー中も、止める時間が延びないように)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerAttack))]
public class CS_PlayerHitFeel : MonoBehaviour
{
    [Header("ヒットストップ")]
    [SerializeField][Min(0f)] private float _hitStopTime = 0.05f;           // 通常攻撃が当たった時に止める時間(秒。0で止めない)
    [SerializeField][Min(0f)] private float _finisherHitStopTime = 0.1f;    // 最後の段が当たった時(秒)
    [SerializeField][Min(0f)] private float _specialHitStopTime = 0.15f;    // 必殺技が当たった時(秒)
    [SerializeField][Min(0f)] private float _bonusHitStopTime = 0.04f;      // ジャスト・ブリンク攻撃の時に足す時間(秒)
    [SerializeField][Min(0f)] private float _hitStopAnimatorSpeed = 0.02f;  // 止めている間のAnimatorの速さ(倍。0でぴったり止まる)

    [Header("ジャスト攻撃・ブリンク攻撃")]
    [SerializeField] private VolumeProfile _bonusScreenEffect;              // 当たった時にかける画面効果(空なら無し)
    [SerializeField][Min(0f)] private float _bonusShakeForce = 0.5f;       // 当たった時のカメラの揺れ(0で揺れない)
    [SerializeField] private float _hitPointHeight = 1f;                    // 光を出す高さ(足元から、m)

    [Header("ジャスト攻撃の文字・効果音(ジャストで押した瞬間に出す)")]
    [SerializeField] private CS_FloatingText.Settings _justText = new CS_FloatingText.Settings();   // 頭の上に出す文字(「JUST!」)
    [SerializeField] private float _justTextHeight = 1.9f;                  // 文字を出す高さ(足元から、m。ブリンク攻撃の文字も同じ高さ)
    [SerializeField] private CSE_SeId _justSe = CSE_SeId.PlayerParrySuccess; // 効果音(専用の音ができたら差し替える。Noneで鳴らさない)

    [Header("ブリンク攻撃の文字・効果音(ブリンク攻撃を出した瞬間に出す)")]
    [SerializeField] private CS_FloatingText.Settings _blinkText = new CS_FloatingText.Settings();   // 頭の上に出す文字(「BLINK!」)
    [SerializeField] private CSE_SeId _blinkSe = CSE_SeId.PlayerSurpriseAttack; // 効果音(専用の音ができたら差し替える。Noneで鳴らさない)

    [Header("殲滅スロー(悪人グループの最後の1人を倒した時)")]
    [SerializeField][Range(0.05f, 1f)] private float _finishSlowScale = 0.25f;   // 世界の速さ(ソロのみ。1で変えない)
    [SerializeField][Min(0f)] private float _finishSlowTime = 0.45f;            // スローにする時間(秒。0でしない)
    [SerializeField] private VolumeProfile _finishScreenEffect;                 // かける画面効果(空なら無し)
    [SerializeField][Min(0f)] private float _finishShakeForce = 1f;             // カメラの揺れ(0で揺れない)

    private const int _hitBufferSize = 16;

    private CS_Player _player;
    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _special;
    private CS_PlayerScoring _scoring;
    private CS_PlayerCharacter _character;
    private CS_PlayerScreenEffect _screenEffect;
    private CinemachineImpulseSource _impulseSource;

    private readonly Collider[] _hitBuffer = new Collider[_hitBufferSize];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private readonly Dictionary<Animator, float> _frozenAnimators = new Dictionary<Animator, float>();
    private float _unfreezeTime;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _attack = GetComponent<CS_PlayerAttack>();
        _special = GetComponent<CS_PlayerSpecialAttack>();
        _scoring = GetComponent<CS_PlayerScoring>();
        _character = GetComponent<CS_PlayerCharacter>();
        _screenEffect = GetComponent<CS_PlayerScreenEffect>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnEnable()
    {
        _attack.onStepHitTiming += HandleStepHitTiming;
        _attack.onJustAttack += HandleJustAttack;
        _attack.onBlinkAttack += HandleBlinkAttack;
        if (_special != null) _special.onSpecialHitTiming += HandleSpecialHitTiming;
        if (_scoring != null) _scoring.onGroupDefeatedLocal += HandleGroupDefeated;
    }

    private void OnDisable()
    {
        _attack.onStepHitTiming -= HandleStepHitTiming;
        _attack.onJustAttack -= HandleJustAttack;
        _attack.onBlinkAttack -= HandleBlinkAttack;
        if (_special != null) _special.onSpecialHitTiming -= HandleSpecialHitTiming;
        if (_scoring != null) _scoring.onGroupDefeatedLocal -= HandleGroupDefeated;
        RestoreAnimators();
    }

    private void Update()
    {
        if (_frozenAnimators.Count > 0 && Time.unscaledTime >= _unfreezeTime) RestoreAnimators();
    }

    // ========================================
    // 通常攻撃・必殺技が当たった時
    // ========================================

    private void HandleStepHitTiming(int step)
    {
        if (!_player.isLocalPlayer) return;

        IReadOnlyList<CSO_AttackData> steps = _attack.attackSteps;
        if (step < 0 || step >= steps.Count || !FindLocalTargets(steps[step], _attack.targetLayers)) return;

        bool isFinisher = step == steps.Count - 1;
        bool hasBonus = _attack.currentStepBonus != CS_PlayerAttack.StepBonus.None;
        float stopTime = (isFinisher ? _finisherHitStopTime : _hitStopTime) + (hasBonus ? _bonusHitStopTime : 0f);
        FreezeHitAnimators(stopTime);
        _attack.Hold(stopTime);   // 止めている間は、コンボの進行も止める(モーションとずれないように)

        if (hasBonus) PlayBonusHit(steps[step]);
    }

    private void HandleSpecialHitTiming()
    {
        if (!_player.isLocalPlayer || _special == null) return;
        if (!FindLocalTargets(_special.specialAttackData, _special.targetLayers)) return;

        FreezeHitAnimators(_specialHitStopTime);
    }

    // 手元で、判定の範囲に当たる相手がいるか(いれば_hitTargetsに入る)
    private bool FindLocalTargets(CSO_AttackData data, LayerMask targetLayers)
    {
        if (data == null) return false;

        CS_AttackHitDetector.FindTargets(transform, data, targetLayers, _hitBuffer, _hitTargets);
        return _hitTargets.Count > 0;
    }

    // ジャスト・ブリンク攻撃が当たった時: 当たった所の光・画面効果・カメラの揺れ
    private void PlayBonusHit(CSO_AttackData data)
    {
        Vector3 hitPoint = transform.position + transform.forward * data.hitRange + Vector3.up * _hitPointHeight;
        if (CS_EffectManager.instance != null) CS_EffectManager.instance.Play(CSE_EffectId.PlayerParry, hitPoint, transform.rotation, null, GetThemeColor());
        if (_screenEffect != null && _bonusScreenEffect != null) _screenEffect.Play(_bonusScreenEffect);
        Shake(_bonusShakeForce);
    }

    // ジャストで押した瞬間: 頭の上に「JUST!」を出し、効果音を鳴らす(当たらなくても出す。押したタイミングのご褒美のため)
    private void HandleJustAttack(int step)
    {
        if (!_player.isLocalPlayer) return;

        PlayCallout(_justText, _justSe);
    }

    // ブリンク攻撃を出した瞬間: 頭の上に「BLINK!」を出し、効果音を鳴らす(当たらなくても出す)
    private void HandleBlinkAttack()
    {
        if (!_player.isLocalPlayer) return;

        PlayCallout(_blinkText, _blinkSe);
    }

    // 頭の上に文字を出し、効果音を鳴らす
    private void PlayCallout(CS_FloatingText.Settings text, CSE_SeId se)
    {
        CS_FloatingText.Spawn(transform.position + Vector3.up * _justTextHeight, text);
        if (se != CSE_SeId.None && CS_SoundManager.instance != null) CS_SoundManager.instance.PlaySe(se, transform.position);
    }

    // ========================================
    // 殲滅スロー
    // ========================================

    private void HandleGroupDefeated()
    {
        if (!_player.isLocalPlayer) return;

        if (_finishSlowTime > 0f && _finishSlowScale < 1f) CS_PlayerTimeScale.Request(this, _finishSlowScale, _finishSlowTime);
        if (_screenEffect != null && _finishScreenEffect != null) _screenEffect.Play(_finishScreenEffect);
        Shake(_finishShakeForce);
    }

    // ========================================
    // 共通
    // ========================================

    // 自分と、手元で当たった相手のAnimatorを、duration秒だけ止める(見た目だけ)
    private void FreezeHitAnimators(float duration)
    {
        if (duration <= 0f) return;

        if (_frozenAnimators.Count == 0) _unfreezeTime = 0f;   // 前に止めた分が終わっていれば、数え直す
        Freeze(gameObject);
        foreach (IDamageable target in _hitTargets)
        {
            if (target is Component component && component != null) Freeze(component.gameObject);
        }
        _unfreezeTime = Mathf.Max(_unfreezeTime, Time.unscaledTime + duration);
    }

    private void Freeze(GameObject root)
    {
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            // すでに止めているものは、元の速さを上書きしない
            if (!_frozenAnimators.ContainsKey(animator)) _frozenAnimators.Add(animator, animator.speed);
            animator.speed = _hitStopAnimatorSpeed;
        }
    }

    private void RestoreAnimators()
    {
        foreach (KeyValuePair<Animator, float> pair in _frozenAnimators)
        {
            if (pair.Key != null) pair.Key.speed = pair.Value;
        }
        _frozenAnimators.Clear();
    }

    private void Shake(float force)
    {
        if (_impulseSource != null && force > 0f) _impulseSource.GenerateImpulseWithForce(force);
    }

    private Color GetThemeColor()
    {
        CSO_PlayableCharacter character = _character != null ? _character.currentCharacter : null;
        return character != null ? character.themeColor : Color.white;
    }
}
