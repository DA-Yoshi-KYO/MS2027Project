/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System.Collections;
using UnityEngine;

/// <summary>
/// ランク画像の登場の演出(エリアごとのランク用)
///
/// 動き(同時に進む)
///   大きい状態(_startScale 倍)から、ふつうの大きさへ小さくなりながら、フェードインする
///   (数字の CS_UICountUpText と同じ動き。数えるところだけがない)
///
/// ・ランクの Image の GameObject にアタッチする(CanvasGroup は自動で追加される)
/// ・付けなくても動く。付けなかったランク画像は、これまでどおり、すぐ表示される
/// ・始めるのは CS_UIResultView(順番に Play する)
/// ・Play の前は、見えない状態で待つ。画像がセットされた後でも、Play の前なら見えない
/// ・大きさは「Pivot」を中心に変わる。その場で縮ませたい時は、Pivot を中央(0.5, 0.5)にする
/// ・時間は Time.timeScale の影響を受けない
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class CS_UIRankStamp : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("時間")]

    [Tooltip("登場にかける時間(秒)")]
    [Min(0.01f)]
    [SerializeField]
    private float _duration = 0.4f;

    [Header("大きさ")]

    [Tooltip("最初の大きさの倍率。1より大きいと、大きい状態から小さくなる")]
    [Min(1f)]
    [SerializeField]
    private float _startScale = 1.5f;

    [Header("フェードイン")]

    [Tooltip("全体の時間のうち、どこまでにフェードインを終えるか(0.6なら、60%の時点で完全に見える)")]
    [Range(0.05f, 1f)]
    [SerializeField]
    private float _fadeRate = 0.6f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CanvasGroup _group;
    private Vector3 _baseScale = Vector3.one;
    private bool _isInitialized;

    // Play が一度でも呼ばれたか(呼ばれた後は、見えている状態を保つ)
    private bool _hasPlayed;

    // 今動いているアニメーション(途中で止めるために覚えておく)
    private Coroutine _coroutine;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        _group = GetComponent<CanvasGroup>();
        _baseScale = transform.localScale;

        _group.interactable = false;
        _group.blocksRaycasts = false;
    }

    // =========================================================
    // 準備 / 再生
    // =========================================================

    /// <summary>
    /// 見えない状態(透明・大きい)で待たせる。まだ Play されていなければ、画像が変わる度に呼んでよい。
    /// すでに Play された後なら、何もしない(見えている状態を保つ)。
    /// </summary>
    public void PrepareHidden()
    {
        EnsureInitialized();

        if (_hasPlayed)
        {
            return;
        }

        StopAnimation();
        ApplyHidden();
    }

    /// <summary>
    /// 登場を始める。1回だけ有効。
    /// extraDelay は、動き出すまでの待ち時間(秒)。View が、順番に出すために使う。
    /// 非表示のオブジェクトでは動かせないので、その時は、すぐ表示する。
    /// </summary>
    /// <returns>始められたか(すでに Play 済みなら false)</returns>
    public bool Play(float extraDelay = 0f)
    {
        EnsureInitialized();

        if (_hasPlayed)
        {
            return false;
        }

        _hasPlayed = true;

        if (!isActiveAndEnabled)
        {
            ApplyFinal();
            return true;
        }

        StopAnimation();
        _coroutine = StartCoroutine(PlayRoutine(Mathf.Max(0f, extraDelay)));

        return true;
    }

    /// <summary>アニメーションを飛ばして、すぐ表示する(スキップ用)</summary>
    public void ShowImmediately()
    {
        EnsureInitialized();

        _hasPlayed = true;

        StopAnimation();
        ApplyFinal();
    }

    // =========================================================
    // アニメーション
    // =========================================================

    private IEnumerator PlayRoutine(float delay)
    {
        // 待ち時間の間は、見えない状態にしておく
        ApplyHidden();

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

            // 最初が速くて最後がゆっくり(EaseOutQuad)
            float progress =
                1f -
                (1f - rate) *
                (1f - rate);

            transform.localScale =
                _baseScale *
                Mathf.LerpUnclamped(
                    _startScale,
                    1f,
                    progress
                );

            _group.alpha =
                Mathf.Clamp01(
                    rate /
                    _fadeRate
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

    // =========================================================
    // 見た目の反映
    // =========================================================

    private void ApplyHidden()
    {
        transform.localScale =
            _baseScale *
            _startScale;

        _group.alpha = 0f;
    }

    private void ApplyFinal()
    {
        transform.localScale = _baseScale;
        _group.alpha = 1f;
    }

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnDisable()
    {
        // 途中で非表示になった時は、半透明のまま残らないように、見えている状態にしておく
        if (_coroutine != null)
        {
            _coroutine = null;
            ApplyFinal();
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

        _startScale =
            Mathf.Max(
                1f,
                _startScale
            );
    }
}
