/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「閉じた状態から、上から下へ開く」演出(BackImage 用)
///
/// 仕組み
///   RectMask2D の Padding(Bottom)を使って、見える範囲を、上から下へ広げていく。
///   画像は拡大も移動もしないので、中身(文字やアイコン)は動かず、そのまま現れる。
///
/// ・「ボードの本体」をまとめた GameObject(Body)にアタッチする(RectMask2D は自動で追加される)
///     Body の子 : BackImage、クリアタイム、エリアごとの行、各回数の欄 など
///     Body の外 : BackHeadImage、総合ランク(これらは、画面の外から動いてくるため)
/// ・始める前は、全体が隠れている(閉じている)
/// ・始めるのは CS_UIResultView(Play に待ち時間を渡して、順番に始める)
/// ・開き終わったら、RectMask2D を無効にする(_disableMaskAfterOpen)。
///   大きく見せる数字などが、本体の枠で切れないようにするため
/// ・時間は Time.timeScale の影響を受けない
/// </summary>
[RequireComponent(typeof(RectMask2D))]
public sealed class CS_UIRevealMask : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("時間")]

    [Tooltip("開くのにかける時間(秒)")]
    [Min(0.01f)]
    [SerializeField]
    private float _duration = 0.5f;

    [Header("緩急")]

    [Tooltip("進み具合のカーブ。横軸が時間(0〜1)、縦軸が進み具合(0〜1)。初期値は、最初が速くて最後がゆっくり")]
    [SerializeField]
    private AnimationCurve _curve =
        new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f),
            new Keyframe(1f, 1f, 0f, 0f)
        );

    [Header("開き終わった後")]

    [Tooltip("ON: 開き終わったら RectMask2D を無効にする(はみ出して見せる演出が切れないように)")]
    [SerializeField]
    private bool _disableMaskAfterOpen = true;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private RectMask2D _mask;
    private RectTransform _rect;

    private bool _isInitialized;

    // Play が一度でも呼ばれたか(呼ばれた後は、開いた状態を保つ)
    private bool _hasPlayed;

    // 今動いているアニメーション(途中で止めるために覚えておく)
    private Coroutine _coroutine;

    // =========================================================
    // 公開
    // =========================================================

    /// <summary>開くのにかかる時間(秒)。順番に出す時の、次の開始時刻の計算に使う</summary>
    public float duration => _duration;

    // =========================================================
    // 初期化
    // =========================================================

    private void Start()
    {
        // 最初のフレームの描画より前に、閉じておく
        EnsureInitialized();

        if (!_hasPlayed)
        {
            ApplyClosed();
        }
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        _mask = GetComponent<RectMask2D>();
        _rect = GetComponent<RectTransform>();
    }

    // =========================================================
    // 公開(準備 / 再生)
    // =========================================================

    /// <summary>閉じた状態で待たせる。まだ Play されていなければ、何度呼んでもよい</summary>
    public void PrepareHidden()
    {
        EnsureInitialized();

        if (_hasPlayed)
        {
            return;
        }

        StopAnimation();
        ApplyClosed();
    }

    /// <summary>
    /// 開く動きを始める。1回だけ有効。
    /// extraDelay は、動き出すまでの待ち時間(秒)。View が、順番に出すために使う。
    /// 非表示のオブジェクトでは動かせないので、その時は、すぐ開いた状態にする。
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
            ApplyOpened();
            return true;
        }

        StopAnimation();
        _coroutine = StartCoroutine(PlayRoutine(Mathf.Max(0f, extraDelay)));

        return true;
    }

    /// <summary>アニメーションを飛ばして、すぐ開いた状態にする(スキップ用)</summary>
    public void ShowImmediately()
    {
        EnsureInitialized();

        _hasPlayed = true;

        StopAnimation();
        ApplyOpened();
    }

    // =========================================================
    // アニメーション
    // =========================================================

    private IEnumerator PlayRoutine(float delay)
    {
        // 待ち時間の間は、閉じた状態にしておく
        ApplyClosed();

        if (delay > 0f)
        {
            // WaitForSecondsRealtime = timeScale=0 でも進む待ち時間
            yield return new WaitForSecondsRealtime(delay);
        }

        // 開く量の基準になる高さ。配置が確定してから読む
        Canvas.ForceUpdateCanvases();

        float height = GetHeight();

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

            // 下側の隠す量を、高さから 0 へ減らす = 上から下へ開く
            SetHiddenHeight(
                height *
                (1f - Evaluate(rate))
            );

            yield return null;
        }

        ApplyOpened();

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

    /// <summary>閉じた状態(全体が隠れている)にする</summary>
    private void ApplyClosed()
    {
        _mask.enabled = true;

        Canvas.ForceUpdateCanvases();

        SetHiddenHeight(GetHeight());
    }

    /// <summary>開いた状態(全体が見えている)にする</summary>
    private void ApplyOpened()
    {
        SetHiddenHeight(0f);

        if (_disableMaskAfterOpen)
        {
            _mask.enabled = false;
        }
    }

    /// <summary>
    /// 下から hiddenHeight の分だけを隠す。
    /// RectMask2D の Padding は (x=左, y=下, z=右, w=上)。下(y)だけを動かす。
    /// </summary>
    private void SetHiddenHeight(float hiddenHeight)
    {
        _mask.padding =
            new Vector4(
                0f,
                Mathf.Max(0f, hiddenHeight),
                0f,
                0f
            );
    }

    private float GetHeight()
    {
        return Mathf.Max(0f, _rect.rect.height);
    }

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnDisable()
    {
        // 途中で非表示になった時は、半分隠れたままにならないように、開いた状態にしておく
        if (_coroutine != null)
        {
            _coroutine = null;
            ApplyOpened();
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
    }
}
