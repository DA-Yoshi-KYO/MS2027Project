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
/// 画面の外から、置いてある位置まで、スライドして入ってくる演出
///
/// ・動かしたい UI(Image など)の GameObject にアタッチする
/// ・「置いてある位置」は、シーン(エディタ)で置いた位置。そこが到着の位置になる
/// ・「出発の位置」は、Canvas(画面全体)と自分の大きさから、自動で計算する
///     CSE_SlideDirection の向きの、画面の外(_margin ぶん余計に離す)
/// ・始める前は、出発の位置(画面の外)で待つ(見えない)
/// ・始めるのは CS_UIResultView(Play に待ち時間を渡して、順番に始める)
/// ・使う場所の例
///     BackHeadImage / 総合ランク : FromTop(画面の上の外から、下へ)
///     称号                        : FromLeft(画面の左の外から、右へ)
/// ・Layout Group の子には置かない(位置を毎回上書きされて、動かなくなる)
/// ・時間は Time.timeScale の影響を受けない
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class CS_UISlideIn : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("向き")]

    [Tooltip("どの方向の画面の外から入ってくるか")]
    [SerializeField]
    private CSE_SlideDirection _direction = CSE_SlideDirection.FromTop;

    [Header("時間")]

    [Tooltip("スライドにかける時間(秒)")]
    [Min(0.01f)]
    [SerializeField]
    private float _duration = 0.5f;

    [Header("出発の位置")]

    [Tooltip("画面の外から、さらにこの距離だけ離す(ピクセル)。画面の端に、少し見えてしまう時に増やす")]
    [Min(0f)]
    [SerializeField]
    private float _margin = 20f;

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

    // 四隅の座標を取り出すための、使い回しの配列(毎回 new しないため)
    private static readonly Vector3[] _corners = new Vector3[4];

    private RectTransform _rect;

    // 到着の位置(シーンに置いてある位置)と、出発の位置(画面の外)
    private Vector2 _targetPosition;
    private Vector2 _startPosition;

    // 位置を計算済みか
    private bool _isPrepared;

    // Play が一度でも呼ばれたか(呼ばれた後は、到着の位置を保つ)
    private bool _hasPlayed;

    // 今動いているアニメーション(途中で止めるために覚えておく)
    private Coroutine _coroutine;

    // =========================================================
    // 公開
    // =========================================================

    /// <summary>スライドにかかる時間(秒)。順番に出す時の、次の開始時刻の計算に使う</summary>
    public float duration => _duration;

    // =========================================================
    // 初期化
    // =========================================================

    private void Start()
    {
        // 最初のフレームの描画より前に、画面の外へ置いておく
        EnsurePrepared();
    }

    /// <summary>
    /// 到着と出発の位置を計算して、まだ始まっていなければ、出発の位置(画面の外)に置く。
    /// 何度呼んでも、最初の1回しか計算しない。
    /// Start より先に Play が来ても動くように、公開メソッドの頭でも呼ぶ。
    /// </summary>
    private void EnsurePrepared()
    {
        if (_isPrepared)
        {
            return;
        }

        _isPrepared = true;

        _rect = GetComponent<RectTransform>();

        // 置いてある位置を、到着の位置として覚える
        _targetPosition = _rect.anchoredPosition;

        _startPosition =
            _targetPosition +
            CalculateStartOffset();

        if (!_hasPlayed)
        {
            ApplyHidden();
        }
    }

    // =========================================================
    // 公開(準備 / 再生)
    // =========================================================

    /// <summary>
    /// 出発の位置(画面の外)に置いて待たせる。まだ Play されていなければ、何度呼んでもよい。
    /// すでに Play された後なら、何もしない。
    /// </summary>
    public void PrepareHidden()
    {
        EnsurePrepared();

        if (_hasPlayed)
        {
            return;
        }

        StopAnimation();
        ApplyHidden();
    }

    /// <summary>
    /// スライドを始める。1回だけ有効。
    /// extraDelay は、動き出すまでの待ち時間(秒)。View が、順番に出すために使う。
    /// 非表示のオブジェクトでは動かせないので、その時は、到着の位置にすぐ置く。
    /// </summary>
    /// <returns>始められたか(すでに Play 済みなら false)</returns>
    public bool Play(float extraDelay = 0f)
    {
        EnsurePrepared();

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

    /// <summary>アニメーションを飛ばして、到着の位置にすぐ置く(スキップ用)</summary>
    public void ShowImmediately()
    {
        EnsurePrepared();

        _hasPlayed = true;

        StopAnimation();
        ApplyFinal();
    }

    // =========================================================
    // アニメーション
    // =========================================================

    private IEnumerator PlayRoutine(float delay)
    {
        // 待ち時間の間は、画面の外で待つ
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

            _rect.anchoredPosition =
                Vector2.LerpUnclamped(
                    _startPosition,
                    _targetPosition,
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

    /// <summary>出発の位置(画面の外)に置く</summary>
    private void ApplyHidden()
    {
        _rect.anchoredPosition = _startPosition;
    }

    /// <summary>到着の位置(置いてある位置)に置く</summary>
    private void ApplyFinal()
    {
        _rect.anchoredPosition = _targetPosition;
    }

    // =========================================================
    // 出発の位置の計算
    // =========================================================

    /// <summary>
    /// 到着の位置から見た、出発の位置までのずれ(親の座標系)を求める。
    /// 自分が、指定の向きの、画面の外へ完全に出る距離だけ動かす(+ _margin)。
    /// </summary>
    private Vector2 CalculateStartOffset()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning(
                $"{nameof(CS_UISlideIn)}: Canvas の下に置いてください。動かす位置を計算できません。",
                this
            );

            return Vector2.zero;
        }

        RectTransform screenRect =
            canvas.rootCanvas.GetComponent<RectTransform>();

        RectTransform space =
            _rect.parent as RectTransform;

        if (screenRect == null ||
            space == null)
        {
            return Vector2.zero;
        }

        // 位置を測る前に、Canvas の配置と拡縮(CanvasScaler)を確定させる
        Canvas.ForceUpdateCanvases();

        Measure(screenRect, space, out Vector2 screenMin, out Vector2 screenMax);
        Measure(_rect, space, out Vector2 min, out Vector2 max);

        switch (_direction)
        {
            case CSE_SlideDirection.FromTop:
                // 自分の下端が、画面の上端より上になる所まで、上へ
                return new Vector2(
                    0f,
                    Mathf.Max(0f, (screenMax.y + _margin) - min.y)
                );

            case CSE_SlideDirection.FromBottom:
                // 自分の上端が、画面の下端より下になる所まで、下へ
                return new Vector2(
                    0f,
                    Mathf.Min(0f, (screenMin.y - _margin) - max.y)
                );

            case CSE_SlideDirection.FromLeft:
                // 自分の右端が、画面の左端より左になる所まで、左へ
                return new Vector2(
                    Mathf.Min(0f, (screenMin.x - _margin) - max.x),
                    0f
                );

            case CSE_SlideDirection.FromRight:
                // 自分の左端が、画面の右端より右になる所まで、右へ
                return new Vector2(
                    Mathf.Max(0f, (screenMax.x + _margin) - min.x),
                    0f
                );
        }

        return Vector2.zero;
    }

    /// <summary>
    /// target の四隅を、space の座標系で測り、最小と最大を返す。
    /// 画面と自分を、同じ座標系にそろえて比べるために使う。
    /// </summary>
    private static void Measure(
        RectTransform target,
        RectTransform space,
        out Vector2 min,
        out Vector2 max)
    {
        target.GetWorldCorners(_corners);

        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < _corners.Length; i++)
        {
            Vector3 local = space.InverseTransformPoint(_corners[i]);

            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
    }

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnDisable()
    {
        // 途中で非表示になった時は、画面の外に残らないように、到着の位置にしておく
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

        _margin =
            Mathf.Max(
                0f,
                _margin
            );
    }
}
