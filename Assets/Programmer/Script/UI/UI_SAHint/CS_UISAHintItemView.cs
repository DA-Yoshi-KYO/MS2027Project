/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-18 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SAヒント矢印1つ分のView。
///
/// ・矢印の位置を設定する
/// ・SAポイント方向へ回転する
/// ・表示中はゆるい拡縮を繰り返す
/// </summary>
public sealed class CS_UISAHintItemView :
    MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("位置と回転を管理するルート")]
    [SerializeField]
    private RectTransform _indicatorRoot;

    [Header("拡縮するルート")]
    [SerializeField]
    private RectTransform _animationRoot;

    [Header("水色の矢印画像")]
    [SerializeField]
    private Image _arrowImage;

    [Header("矢印の角度補正")]

    [Tooltip(
        "矢印画像が右向きなら0、" +
        "上向きなら-90を設定する"
    )]
    [SerializeField]
    private float _angleOffset;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private float _animationElapsed;

    private float _phaseOffset;

    private float _minimumScale = 0.9f;

    private float _maximumScale = 1.05f;

    private float _scaleDuration = 0.8f;

    private Vector3 _initialScale =
        Vector3.one;

    private bool _isVisible;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        if (_indicatorRoot == null)
        {
            _indicatorRoot =
                transform as RectTransform;
        }

        if (_animationRoot == null)
        {
            _animationRoot =
                _indicatorRoot;
        }

        if (_animationRoot != null)
        {
            _initialScale =
                _animationRoot.localScale;
        }

        if (_arrowImage != null)
        {
            _arrowImage.raycastTarget = false;
        }

        SetVisible(false);
    }

    // =========================================================
    // セットアップ
    // =========================================================

    public void Setup(
        CSO_UISAHintData data,
        float phaseOffset)
    {
        if (data != null)
        {
            _minimumScale =
                data.minimumScale;

            _maximumScale =
                data.maximumScale;

            _scaleDuration =
                data.scaleDuration;
        }

        _phaseOffset =
            Mathf.Repeat(
                phaseOffset,
                1f
            );

        ResetAnimation();
    }

    // =========================================================
    // 表示
    // =========================================================

    public void Show(
        Vector2 anchoredPosition,
        float angle)
    {
        if (_indicatorRoot == null)
        {
            return;
        }

        bool wasVisible =
            _isVisible;

        _indicatorRoot.anchoredPosition =
            anchoredPosition;

        _indicatorRoot.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                angle + _angleOffset
            );

        SetVisible(true);

        if (!wasVisible)
        {
            RestartAnimation();
        }
    }

    public void Hide()
    {
        SetVisible(false);

        ResetAnimation();
    }

    private void SetVisible(bool isVisible)
    {
        _isVisible = isVisible;

        if (_indicatorRoot == null)
        {
            return;
        }

        if (_indicatorRoot.gameObject.activeSelf !=
            isVisible)
        {
            _indicatorRoot.gameObject.SetActive(
                isVisible
            );
        }
    }

    // =========================================================
    // 拡縮アニメーション
    // =========================================================

    private void Update()
    {
        if (!_isVisible ||
            _animationRoot == null)
        {
            return;
        }

        if (_scaleDuration <= 0f)
        {
            SetScale(_maximumScale);
            return;
        }

        _animationElapsed +=
            Time.unscaledDeltaTime;

        float animationRate =
            _animationElapsed /
            _scaleDuration;

        float pingPongRate =
            Mathf.PingPong(
                animationRate +
                _phaseOffset,
                1f
            );

        float smoothRate =
            Mathf.SmoothStep(
                0f,
                1f,
                pingPongRate
            );

        float scale =
            Mathf.Lerp(
                _minimumScale,
                _maximumScale,
                smoothRate
            );

        SetScale(scale);
    }

    private void RestartAnimation()
    {
        _animationElapsed = 0f;

        float initialMultiplier =
            Mathf.Lerp(
                _minimumScale,
                _maximumScale,
                _phaseOffset
            );

        SetScale(
            initialMultiplier
        );
    }

    private void ResetAnimation()
    {
        _animationElapsed = 0f;

        if (_animationRoot != null)
        {
            _animationRoot.localScale =
                _initialScale;
        }
    }

    private void SetScale(float multiplier)
    {
        if (_animationRoot == null)
        {
            return;
        }

        _animationRoot.localScale =
            new Vector3(
                _initialScale.x *
                multiplier,

                _initialScale.y *
                multiplier,

                _initialScale.z
            );
    }
}