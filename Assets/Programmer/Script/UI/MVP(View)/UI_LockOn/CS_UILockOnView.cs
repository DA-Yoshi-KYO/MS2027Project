/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UICanvas上のロックオンマーカーを表示するView。
/// </summary>
public class CS_UILockOnView :
    CS_BaseView<CS_UILockOnPresenter>
{
    [Header("ロックオンマーカー")]
    [SerializeField] private RectTransform _markerRect;
    [SerializeField] private Image _markerImage;

    [Header("ロックオン開始演出（任意）")]
    [Min(0f)]
    [SerializeField] private float _lockOnAnimDuration = 0.15f;

    [Min(1f)]
    [SerializeField] private float _lockOnStartScale = 1.4f;

    private float _animationElapsed;
    private bool _isAnimating;

    private void Awake()
    {
        if (_markerImage != null)
            _markerImage.raycastTarget = false;

        SetVisible(false, false);
    }

    public override void SetPresenter(
        CS_UILockOnPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    private void Update()
    {
        if (!_isAnimating || _markerRect == null)
            return;

        if (_lockOnAnimDuration <= 0f)
        {
            _markerRect.localScale = Vector3.one;
            _isAnimating = false;
            return;
        }

        _animationElapsed += Time.unscaledDeltaTime;
        float rate = Mathf.Clamp01(
            _animationElapsed / _lockOnAnimDuration
        );

        float scale = Mathf.Lerp(
            _lockOnStartScale,
            1f,
            rate
        );

        _markerRect.localScale = Vector3.one * scale;

        if (rate >= 1f)
            _isAnimating = false;
    }

    public void SetVisible(
        bool isVisible,
        bool playAnimation = true)
    {
        if (_markerRect == null)
            return;

        bool wasVisible = _markerRect.gameObject.activeSelf;
        _markerRect.gameObject.SetActive(isVisible);

        if (!isVisible)
        {
            _isAnimating = false;
            _markerRect.localScale = Vector3.one;
            return;
        }

        if (!wasVisible && playAnimation)
            PlayLockOnAnimation();
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        if (_markerRect != null)
            _markerRect.anchoredPosition = anchoredPosition;
    }

    private void PlayLockOnAnimation()
    {
        if (_markerRect == null)
            return;

        _animationElapsed = 0f;
        _isAnimating = true;
        _markerRect.localScale = Vector3.one * _lockOnStartScale;
    }
}
