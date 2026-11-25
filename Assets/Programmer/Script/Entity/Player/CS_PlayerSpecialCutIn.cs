using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

/*
 * 必殺技のカットイン(原神の元素爆発・崩壊スターレールの必殺技のような、カメラ・モーション・エフェクトの演出)
 * 必殺技を使った瞬間に、世界の時間をほぼ止め、カメラがキャラクターの正面へ回り込み、必殺技のモーションとエフェクトを見せる
 * 終わった瞬間に必殺技が発動する(攻撃する必殺技の当たり・効果だけの必殺技の解放)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様書「プレイヤー」必殺技: 「1秒以内とか超短くカットインを入れて(制限時間ゲーなので時間を取るのはストレス)」→ 既定0.9秒
 * ・始まる時: 必殺技を使った時(攻撃する必殺技: CS_PlayerSpecialAttack.onSpecialStarted / 効果だけの必殺技: onEffectOnlyUsed)
 *   自分のプレイヤー(人が操作している、isLocalPlayer)だけ。カットインの間、CS_PlayerSpecialAttackは必殺技の進行を止めて待つ
 *   (攻撃する必殺技は、カットインが終わってから当たる)
 * ・時間: ソロでは世界の時間を_worldTimeScale倍にする(CS_PlayerTimeScale。既定0.05でほぼ止まる)
 *   自分のAnimatorだけはTime.timeScaleの影響を受けない設定にして、必殺技のモーションを普通の速さで見せる(終わったら戻す)
 *   マルチでは世界の時間は変えない(他の人の世界まで止まるため)。カメラ・エフェクト・画面効果だけ出す
 * ・カメラ(CS_Player.UpdateCameraTransformから呼ばれるTryGetCameraPose)
 *   いつもの位置から、キャラクターの正面寄り(向いている向きから_yawOffset度ずれた、キャラクターの前)の近くへ、キャラクターの周りを弧を描いて回り込む
 *   寄っている間は_startDistanceから_endDistanceへ少しずつ近づく(ドリー)。最後の_blendOutRateの間に、いつもの位置へ戻る
 *   カットインの間は移動・視点の操作をしない(CS_Player.isPlayingCutIn)
 * ・エフェクト(CS_EffectManager。キャラクターの色で出す)
 *   始まり: 胸の高さに衝撃(PlayerAmbushBurst)。効果だけの必殺技は、発動のオーラ(PlayerSpecialStart)も付ける
 *   (攻撃する必殺技は、CS_PlayerAttackEffectが発動のオーラを出している)
 *   終わり(効果だけの必殺技): 解放(PlayerSpecialHit)とカメラの揺れ。攻撃する必殺技は、当たった時の演出(CS_PlayerAttackEffect・CS_PlayerCameraShake)に任せる
 * ・画面効果(CS_PlayerScreenEffect): カットインの間は_holdScreenEffect(既定は奇襲の溜めと同じモノクロ・ビネット)をかけ続け、終わりに_releaseScreenEffectを1回かける
 * ・以前のUIの帯のカットイン(CS_PlayerSpecialEffectCue)は外した(残り時間の表示は残している)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerSpecialCutIn : MonoBehaviour
{
    [Header("時間")]
    [SerializeField][Min(0.1f)] private float _duration = 0.9f;            // カットインの長さ(秒。Time.timeScaleの影響を受けない)
    [SerializeField][Range(0f, 1f)] private float _worldTimeScale = 0.05f;  // カットインの間の世界の時間の速さ(ソロのみ。0でぴったり止まる)

    [Header("カメラ")]
    [SerializeField][Range(0.05f, 0.5f)] private float _blendInRate = 0.2f;   // 長さのうち、いつもの位置から回り込むのにかける割合
    [SerializeField][Range(0.05f, 0.5f)] private float _blendOutRate = 0.2f;  // 長さのうち、いつもの位置へ戻るのにかける割合
    [SerializeField] private float _yawOffset = 25f;                         // カメラを置く向き(キャラクターの向きからの角度、度。0で真正面から、180で背中から)
    [SerializeField][Min(0.5f)] private float _startDistance = 2.8f;        // 回り込んだ直後の、キャラクターからカメラまでの距離(m)
    [SerializeField][Min(0.5f)] private float _endDistance = 2.1f;          // 最後の距離(m。だんだん寄る)
    [SerializeField] private float _cameraHeight = 1.2f;                    // カメラの高さ(足元から、m)
    [SerializeField] private float _lookHeight = 1.15f;                     // 見る所の高さ(足元から、m。胸のあたり)

    [Header("エフェクト・画面効果")]
    [SerializeField] private VolumeProfile _holdScreenEffect;               // カットインの間かけ続ける画面効果(空なら無し)
    [SerializeField] private VolumeProfile _releaseScreenEffect;            // 終わりに1回かける画面効果(空なら無し)
    [SerializeField][Min(0f)] private float _releaseShakeForce = 0.8f;      // 効果だけの必殺技を解放した時のカメラの揺れ(0で揺れない)

    private CS_Player _player;
    private CS_PlayerSpecialAttack _special;
    private CS_PlayerCharacter _character;
    private CS_PlayerScreenEffect _screenEffect;
    private CinemachineImpulseSource _impulseSource;
    private Animator[] _animators;
    private AnimatorUpdateMode[] _animatorModes;

    private bool _isPlaying;
    private bool _isEffectOnly;
    private float _elapsed;
    private Vector3 _anchor;      // 始まった時の足元の位置
    private float _facingYaw;     // 始まった時のキャラクターの向き(度)

    public bool isPlaying => _isPlaying;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _special = GetComponent<CS_PlayerSpecialAttack>();
        _character = GetComponent<CS_PlayerCharacter>();
        _screenEffect = GetComponent<CS_PlayerScreenEffect>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnEnable()
    {
        if (_special == null) return;

        _special.onSpecialStarted += HandleSpecialStarted;
        _special.onEffectOnlyUsed += HandleEffectOnlyUsed;
    }

    private void OnDisable()
    {
        if (_special != null)
        {
            _special.onSpecialStarted -= HandleSpecialStarted;
            _special.onEffectOnlyUsed -= HandleEffectOnlyUsed;
        }
        if (_isPlaying) Finish(false);
    }

    private void HandleSpecialStarted() => Play(false);
    private void HandleEffectOnlyUsed() => Play(true);

    private void Play(bool isEffectOnly)
    {
        if (!_player.isLocalPlayer || _isPlaying) return;

        _isPlaying = true;
        _isEffectOnly = isEffectOnly;
        _elapsed = 0f;
        _anchor = transform.position;
        _facingYaw = transform.eulerAngles.y;

        CS_PlayerTimeScale.Request(this, _worldTimeScale, _duration);
        SetAnimatorsUnscaled(true);
        if (_screenEffect != null && _holdScreenEffect != null) _screenEffect.SetHeld(_holdScreenEffect, true);

        Color color = GetThemeColor();
        Vector3 chest = _anchor + Vector3.up * _lookHeight;
        PlayEffect(CSE_EffectId.PlayerAmbushBurst, chest, transform.rotation, null, color);
        if (isEffectOnly) PlayEffect(CSE_EffectId.PlayerSpecialStart, _anchor, transform.rotation, transform, color);
    }

    private void Update()
    {
        if (!_isPlaying) return;

        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed >= _duration) Finish(true);
    }

    // 終える。released: 最後まで流れた(解放の演出を出す)
    private void Finish(bool released)
    {
        _isPlaying = false;
        CS_PlayerTimeScale.Cancel(this);
        SetAnimatorsUnscaled(false);
        if (_screenEffect != null && _holdScreenEffect != null) _screenEffect.SetHeld(_holdScreenEffect, false);
        if (!released) return;

        if (_screenEffect != null && _releaseScreenEffect != null) _screenEffect.Play(_releaseScreenEffect);
        if (!_isEffectOnly) return;

        // 効果だけの必殺技は、ここで解放の演出を出す(攻撃する必殺技は、当たった時の演出に任せる)
        PlayEffect(CSE_EffectId.PlayerSpecialHit, transform.position, transform.rotation, null, GetThemeColor());
        if (_impulseSource != null && _releaseShakeForce > 0f) _impulseSource.GenerateImpulseWithForce(_releaseShakeForce);
    }

    // カメラの位置と向き(カットイン中でなければfalse)。normalPosition・normalRotationは、いつものカメラの位置と向き
    // キャラクターの周りを弧を描いて回り込むよう、キャラクターから見たカメラの向きを球面で補間する
    public bool TryGetCameraPose(Vector3 normalPosition, Quaternion normalRotation, out Vector3 position, out Quaternion rotation)
    {
        position = normalPosition;
        rotation = normalRotation;
        if (!_isPlaying) return false;

        float t = Mathf.Clamp01(_elapsed / _duration);
        float weight = GetBlendWeight(t);

        Vector3 look = _anchor + Vector3.up * _lookHeight;
        Vector3 closeUpDirection = Quaternion.Euler(0f, _facingYaw + _yawOffset, 0f) * Vector3.forward;
        float distance = Mathf.Lerp(_startDistance, _endDistance, t);
        Vector3 closeUpOffset = closeUpDirection * distance + Vector3.up * (_cameraHeight - _lookHeight);

        Vector3 normalOffset = normalPosition - look;
        Vector3 offset = Vector3.Slerp(normalOffset, closeUpOffset, weight);
        position = look + offset;

        Quaternion closeUpRotation = Quaternion.LookRotation(look - (look + closeUpOffset));
        rotation = Quaternion.Slerp(normalRotation, closeUpRotation, weight);
        return true;
    }

    // 回り込む強さ(0: いつもの位置、1: 正面の寄り)。入りと戻りはなめらかに
    private float GetBlendWeight(float t)
    {
        if (t < _blendInRate) return Mathf.SmoothStep(0f, 1f, t / _blendInRate);
        if (t > 1f - _blendOutRate) return Mathf.SmoothStep(0f, 1f, (1f - t) / _blendOutRate);
        return 1f;
    }

    // 自分のAnimatorを、Time.timeScaleの影響を受けないようにする・戻す(世界が止まっている間もモーションを見せるため)
    private void SetAnimatorsUnscaled(bool unscaled)
    {
        if (unscaled)
        {
            _animators = GetComponentsInChildren<Animator>(true);
            _animatorModes = new AnimatorUpdateMode[_animators.Length];
            for (int i = 0; i < _animators.Length; i++)
            {
                _animatorModes[i] = _animators[i].updateMode;
                _animators[i].updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            return;
        }

        if (_animators == null) return;
        for (int i = 0; i < _animators.Length; i++)
        {
            if (_animators[i] != null) _animators[i].updateMode = _animatorModes[i];
        }
        _animators = null;
    }

    private Color GetThemeColor()
    {
        CSO_PlayableCharacter character = _character != null ? _character.currentCharacter : null;
        return character != null ? character.themeColor : Color.white;
    }

    private static void PlayEffect(CSE_EffectId id, Vector3 position, Quaternion rotation, Transform follow, Color color)
    {
        if (CS_EffectManager.instance != null) CS_EffectManager.instance.Play(id, position, rotation, follow, color);
    }
}
