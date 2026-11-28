/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 数字の登場アニメーション(カウントアップ)
///
/// 動き(同時に進む)
///   1. 大きい状態(_startScale 倍)から、ふつうの大きさへ小さくなる
///   2. 透明から不透明へ、フェードインする
///   3. 0 から目標の数字まで、カウントアップする
///
/// ・数字を表示する TextMeshPro の GameObject にアタッチする(CanvasGroup は自動で追加される)
/// ・View から直接は使わず、CS_ResultNumberUtility を通して使う
///     SetValue : 目標の数字をセットする(見えない状態で待つ)
///     Play     : アニメーションを始める(いつ始めるかは Presenter / Controller / View が決める)
/// ・Play の前に SetValue が来ていれば、見えない状態で待つ。Play の後に SetValue が来たら、
///   アニメーションなしで、最後の状態をすぐ表示する
/// ・動き出すまでの待ち時間は「_startDelay(この部品の設定) + Play に渡された追加の待ち時間」。
///   View が、上から順に出すために、追加の待ち時間をずらして渡す
/// ・大きさは「文字の Pivot」を中心に変わる。その場で縮ませたい時は、Pivot を中央(0.5, 0.5)にする
/// ・時間は Time.timeScale の影響を受けない
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class CS_UICountUpText : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("時間")]

    [Tooltip("カウントアップにかける時間(秒)")]
    [Min(0.01f)]
    [SerializeField]
    private float _duration = 0.8f;

    [Tooltip("動き出すまでの待ち時間(秒)。View が順番に出す時の待ち時間に、これが足される。基本は0でよい")]
    [Min(0f)]
    [SerializeField]
    private float _startDelay = 0f;

    [Header("大きさ")]

    [Tooltip("最初の大きさの倍率。1より大きいと、大きい状態から小さくなる。画面からはみ出さない大きさにする")]
    [Min(1f)]
    [SerializeField]
    private float _startScale = 1.5f;

    [Header("フェードイン")]

    [Tooltip("全体の時間のうち、どこまでにフェードインを終えるか(0.6なら、60%の時点で完全に見える)")]
    [Range(0.05f, 1f)]
    [SerializeField]
    private float _fadeRate = 0.6f;

    [Header("緩急")]

    [Tooltip("進み具合のカーブ。横軸が時間(0〜1)、縦軸が進み具合(0〜1)。初期値は、最初が速くて最後がゆっくり")]
    [SerializeField]
    private AnimationCurve _curve =
        new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f),
            new Keyframe(1f, 1f, 0f, 0f)
        );

    // =========================================================
    // 内部フィールド
    // =========================================================

    private TMP_Text _text;
    private CanvasGroup _group;

    // 最初に置いてあった大きさ(1以外でも崩れないように、これに倍率を掛ける)
    private Vector3 _baseScale = Vector3.one;

    private bool _isInitialized;

    // 目標の数字と、その表示のしかた
    private float _targetValue;
    private CSE_CountUpFormat _format;
    private string _suffix = string.Empty;

    // 目標の数字がセットされているか
    private bool _hasValue;

    // Play が一度でも呼ばれたか(呼ばれた後は、アニメーションなしで最後の状態を出す)
    private bool _hasPlayed;

    // Play に渡された、追加の待ち時間(_startDelay に足される)
    private float _extraDelay;

    // 今動いているアニメーション(途中で止めるために覚えておく)
    private Coroutine _coroutine;

    // =========================================================
    // 公開
    // =========================================================

    /// <summary>アニメーション中か</summary>
    public bool isPlaying => _coroutine != null;

    /// <summary>
    /// 追加: Play してから、数え終わるまでの時間(秒)。_startDelay を含む。
    /// Play に渡す追加の待ち時間は含まない(呼ぶ側が足す)。リザルトの演出が終わる時刻の計算に使う。
    /// </summary>
    public float totalSeconds => _startDelay + _duration;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 部品の取得。何度呼んでも最初の1回しか動かない。
    /// Awake より先に SetValue が来ても動くように、公開メソッドの頭でも呼ぶ。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        _text = GetComponent<TMP_Text>();
        _group = GetComponent<CanvasGroup>();

        _baseScale = transform.localScale;

        // 数字は見せるだけなので、操作は受け付けない
        _group.interactable = false;
        _group.blocksRaycasts = false;
    }

    // =========================================================
    // 目標の数字のセット
    // =========================================================

    /// <summary>
    /// 目標の数字をセットする。
    /// ・まだ Play されていなければ、見えない状態(透明・大きい・0)で待つ
    /// ・すでに Play された後なら、アニメーションなしで、最後の状態をすぐ表示する
    /// </summary>
    public void SetValue(
        float value,
        CSE_CountUpFormat format,
        string suffix)
    {
        EnsureInitialized();

        _targetValue = value;
        _format = format;
        _suffix = suffix ?? string.Empty;
        _hasValue = true;

        StopAnimation();

        if (_hasPlayed)
        {
            ApplyFinal();
        }
        else
        {
            ApplyHidden();
        }
    }

    // =========================================================
    // 再生
    // =========================================================

    /// <summary>
    /// アニメーションを始める。1回だけ有効。
    /// extraDelay は、動き出すまでの追加の待ち時間(秒)。View が、上から順に出すために使う。
    /// 数字がセットされていない、またはすでに Play 済みなら、何もせず false を返す。
    /// 非表示のオブジェクトでは動かせないので、その時は最後の状態をすぐ表示する。
    /// </summary>
    /// <returns>始められたか(true なら、この数字は今回の順番に入った)</returns>
    public bool Play(float extraDelay = 0f)
    {
        EnsureInitialized();

        if (!_hasValue ||
            _hasPlayed)
        {
            return false;
        }

        _hasPlayed = true;
        _extraDelay = Mathf.Max(0f, extraDelay);

        if (!isActiveAndEnabled)
        {
            ApplyFinal();
            return true;
        }

        StopAnimation();
        _coroutine = StartCoroutine(PlayRoutine());

        return true;
    }

    /// <summary>アニメーションを飛ばして、最後の状態をすぐ表示する(スキップ用)</summary>
    public void ShowImmediately()
    {
        EnsureInitialized();

        if (!_hasValue)
        {
            return;
        }

        _hasPlayed = true;

        StopAnimation();
        ApplyFinal();
    }

    // =========================================================
    // アニメーション
    // =========================================================

    private IEnumerator PlayRoutine()
    {
        // 待ち時間の間は、見えない状態にしておく
        ApplyHidden();

        float delay = _startDelay + _extraDelay;

        if (delay > 0f)
        {
            // WaitForSecondsRealtime = timeScale=0 でも進む待ち時間
            yield return new WaitForSecondsRealtime(delay);
        }

        float elapsed = 0f;

        while (elapsed < _duration)
        {
            // unscaledDeltaTime = timeScale=0 でも進む時間
            elapsed += Time.unscaledDeltaTime;

            float rate =
                Mathf.Clamp01(
                    elapsed /
                    _duration
                );

            ApplyFrame(
                rate,
                Evaluate(rate)
            );

            yield return null;
        }

        ApplyFinal();

        _coroutine = null;
    }

    private void StopAnimation()
    {
        if (_coroutine == null)
        {
            return;
        }

        StopCoroutine(_coroutine);
        _coroutine = null;
    }

    /// <summary>
    /// カーブで、進み具合を変換する。カーブが未設定なら、そのまま(直線)。
    /// </summary>
    private float Evaluate(float rate)
    {
        if (_curve == null ||
            _curve.length == 0)
        {
            return rate;
        }

        return _curve.Evaluate(rate);
    }

    // =========================================================
    // 見た目の反映
    // =========================================================

    /// <summary>
    /// アニメーション中の1コマ。
    /// rate : 経過した時間の割合(0〜1)。フェードインに使う
    /// progress : カーブを通した進み具合。大きさと数字に使う
    /// </summary>
    private void ApplyFrame(
        float rate,
        float progress)
    {
        // 大きい状態 → ふつうの大きさ
        transform.localScale =
            _baseScale *
            Mathf.LerpUnclamped(
                _startScale,
                1f,
                progress
            );

        // 透明 → 不透明(_fadeRate の時点で完全に見える)
        _group.alpha =
            Mathf.Clamp01(
                rate /
                _fadeRate
            );

        // 0 → 目標の数字。途中は整数に切り捨てる(目標をはみ出さないようにそろえる)
        float shown =
            Mathf.Clamp(
                Mathf.Floor(_targetValue * progress),
                0f,
                Mathf.Max(0f, _targetValue)
            );

        _text.text =
            CS_ResultNumberUtility.Format(
                shown,
                _format,
                _suffix
            );
    }

    /// <summary>見えない状態(透明・大きい・0)にする</summary>
    private void ApplyHidden()
    {
        transform.localScale =
            _baseScale *
            _startScale;

        _group.alpha = 0f;

        _text.text =
            CS_ResultNumberUtility.Format(
                0f,
                _format,
                _suffix
            );
    }

    /// <summary>最後の状態(不透明・ふつうの大きさ・目標の数字)にする</summary>
    private void ApplyFinal()
    {
        transform.localScale = _baseScale;

        _group.alpha = 1f;

        _text.text =
            CS_ResultNumberUtility.Format(
                _targetValue,
                _format,
                _suffix
            );
    }

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnDisable()
    {
        // 途中で非表示になった時は、半透明のまま残らないように、最後の状態にしておく
        if (_coroutine != null)
        {
            _coroutine = null;

            if (_hasValue)
            {
                ApplyFinal();
            }
        }
    }

    // =========================================================
    // Inspector値の補正
    // =========================================================

    private void OnValidate()
    {
        _duration =
            Mathf.Max(
                0.01f,
                _duration
            );

        _startDelay =
            Mathf.Max(
                0f,
                _startDelay
            );

        _startScale =
            Mathf.Max(
                1f,
                _startScale
            );
    }
}
