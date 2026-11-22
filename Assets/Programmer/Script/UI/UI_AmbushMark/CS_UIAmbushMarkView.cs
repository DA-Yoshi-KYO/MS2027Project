/* ================================================
 * 奇襲の星マーク(対象の悪人の位置に出る星)のView
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-15 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 星マークのView。描画とアニメーションだけを持つ。
/// ・出た時: 大きめの状態から縮みながら回って止まる(_appearDuration)
/// ・命中時: 少し膨らみながら回って、薄くなって消える(_burstDuration)
/// 時間はTime.timeScaleの影響を受けない(ヒットストップ中も止まらない)。
/// </summary>
public class CS_UIAmbushMarkView :
    CS_BaseView<CS_UIAmbushMarkPresenter>
{
    private enum State
    {
        Hidden,    // 出ていない
        Appearing, // 出て、縮みながら回っている
        Idle,      // 止まっている
        Bursting,  // 弾けている
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("位置を変更する星マーク")]
    [SerializeField] private RectTransform _markerRect;

    [Header("拡縮・回転する星(Imageが付いたもの)")]
    [SerializeField] private RectTransform _animationRoot;
    [SerializeField] private Image _starImage;

    [Header("出る時")]
    [Tooltip("大きめから縮みながら回って止まるまでの時間(秒)")]
    [Min(0.01f)]
    [SerializeField] private float _appearDuration = 0.15f;

    [Tooltip("出た瞬間の大きさ(倍)")]
    [Min(0.01f)]
    [SerializeField] private float _appearStartScale = 2.5f;

    [Tooltip("出た瞬間の回転(度)。ここから0度へ回って止まる")]
    [SerializeField] private float _appearStartRotation = 180f;

    [Header("弾ける時(命中)")]
    [Tooltip("弾けて消えるまでの時間(秒)")]
    [Min(0.01f)]
    [SerializeField] private float _burstDuration = 0.2f;

    [Tooltip("弾け終わりの大きさ(倍)")]
    [Min(0.01f)]
    [SerializeField] private float _burstEndScale = 2.2f;

    [Tooltip("弾ける間にさらに回る角度(度)")]
    [SerializeField] private float _burstRotation = 90f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private State _state = State.Hidden;
    private float _elapsed;

    public bool isBursting => _state == State.Bursting;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        if (_starImage != null)
            _starImage.raycastTarget = false;

        Hide();
    }

    private void Update()
    {
        switch (_state)
        {
            case State.Appearing:
                UpdateAppear();
                break;
            case State.Bursting:
                UpdateBurst();
                break;
        }
    }

    // =========================================================
    // 表示・非表示
    // =========================================================

    /// <summary>星を出す(出ていなければ、出るアニメーションから始める。弾けている最中なら出し直す)。</summary>
    public void Show()
    {
        if (_markerRect == null)
            return;

        if (_state == State.Appearing || _state == State.Idle)
            return;

        _markerRect.gameObject.SetActive(true);
        _state = State.Appearing;
        _elapsed = 0f;
        Apply(_appearStartScale, _appearStartRotation, 0f);
    }

    /// <summary>星をすぐ消す。</summary>
    public void Hide()
    {
        _state = State.Hidden;
        _elapsed = 0f;

        if (_markerRect != null)
            _markerRect.gameObject.SetActive(false);
    }

    /// <summary>Canvas上の位置を設定する。</summary>
    public void SetPosition(Vector2 anchoredPosition)
    {
        if (_markerRect == null)
            return;

        _markerRect.anchoredPosition = anchoredPosition;
    }

    /// <summary>命中で弾けて消えるアニメーションを始める(出ていなければ何もしない)。</summary>
    public void PlayBurst()
    {
        if (_state == State.Hidden || _state == State.Bursting)
            return;

        _state = State.Bursting;
        _elapsed = 0f;
    }

    // =========================================================
    // アニメーション
    // =========================================================

    // 大きめから縮みながら回って止まる(最初に素早く寄り、最後はゆっくり止まる)
    private void UpdateAppear()
    {
        _elapsed += Time.unscaledDeltaTime;
        float rate = Mathf.Clamp01(_elapsed / _appearDuration);
        float eased = 1f - (1f - rate) * (1f - rate);

        Apply(
            Mathf.Lerp(_appearStartScale, 1f, eased),
            Mathf.Lerp(_appearStartRotation, 0f, eased),
            Mathf.Clamp01(rate * 2.5f));

        if (rate >= 1f)
            _state = State.Idle;
    }

    // 膨らみながら回って、薄くなって消える
    private void UpdateBurst()
    {
        _elapsed += Time.unscaledDeltaTime;
        float rate = Mathf.Clamp01(_elapsed / _burstDuration);

        Apply(
            Mathf.Lerp(1f, _burstEndScale, rate),
            Mathf.Lerp(0f, _burstRotation, rate),
            1f - rate * rate);

        if (rate >= 1f)
            Hide();
    }

    private void Apply(float scale, float rotation, float alpha)
    {
        if (_animationRoot != null)
        {
            _animationRoot.localScale = Vector3.one * scale;
            _animationRoot.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        if (_starImage != null)
        {
            Color color = _starImage.color;
            color.a = alpha;
            _starImage.color = color;
        }
    }
}
