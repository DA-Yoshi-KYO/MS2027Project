/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UICanvas上のハイドマーカーを表示するView。
///
/// ・入力方式に応じてKeyboardとControllerを切り替える
/// ・それぞれのKey画像だけを拡縮する
/// ・Marker画像は拡縮しない
/// </summary>
public class CS_UIHideMarkerView :
    CS_BaseView<CS_UIHideMarkerPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("位置・表示を管理するハイドマーカー")]
    [SerializeField]
    private RectTransform _markerRect;

    [Header("キーボード＆マウス用")]

    [Tooltip("Keyboard全体のルート")]
    [SerializeField]
    private GameObject _keyboardMouseMarkerRoot;

    [Tooltip("Keyboard内のMarker画像")]
    [SerializeField]
    private Image _keyboardMouseMarkerImage;

    [Tooltip("Keyboard内の拡縮させるKey")]
    [SerializeField]
    private RectTransform _keyboardMouseKeyRect;

    [Tooltip("Keyboard内のKey画像")]
    [SerializeField]
    private Image _keyboardMouseKeyImage;

    [Header("コントローラー用")]

    [Tooltip("Controller全体のルート")]
    [SerializeField]
    private GameObject _controllerMarkerRoot;

    [Tooltip("Controller内のMarker画像")]
    [SerializeField]
    private Image _controllerMarkerImage;

    [Tooltip("Controller内の拡縮させるKey")]
    [SerializeField]
    private RectTransform _controllerKeyRect;

    [Tooltip("Controller内のKey画像")]
    [SerializeField]
    private Image _controllerKeyImage;

    [Header("Key拡縮アニメーション")]

    [Tooltip("最小倍率から最大倍率へ変化する秒数")]
    [Min(0.01f)]
    [SerializeField]
    private float _scaleAnimationDuration = 0.5f;

    [Tooltip("Keyの最小倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float _minimumScale = 0.7f;

    [Tooltip("Keyの最大倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float _maximumScale = 1f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private float _animationElapsed;
    private bool _isVisible;
    private bool _isKeyboardMouse;

    private Vector3 _keyboardMouseKeyInitialScale =
        Vector3.one;

    private Vector3 _controllerKeyInitialScale =
        Vector3.one;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        SetRaycastTarget(
            _keyboardMouseMarkerImage,
            false
        );

        SetRaycastTarget(
            _keyboardMouseKeyImage,
            false
        );

        SetRaycastTarget(
            _controllerMarkerImage,
            false
        );

        SetRaycastTarget(
            _controllerKeyImage,
            false
        );

        if (_keyboardMouseKeyRect != null)
        {
            _keyboardMouseKeyInitialScale =
                _keyboardMouseKeyRect.localScale;
        }

        if (_controllerKeyRect != null)
        {
            _controllerKeyInitialScale =
                _controllerKeyRect.localScale;
        }

        _isVisible = false;
        _isKeyboardMouse = true;

        if (_markerRect != null)
        {
            _markerRect.gameObject.SetActive(false);
        }

        ResetScaleAnimation();
    }

    private void OnEnable()
    {
        if (CS_CustomInputActionManager.instance == null)
        {
            return;
        }

        CS_CustomInputActionManager.instance
            .onInputTypeChanged +=
            HandleInputTypeChanged;

        ApplyInputType(
            CS_CustomInputActionManager.instance
                .currentInputType
        );
    }

    private void OnDisable()
    {
        if (CS_CustomInputActionManager.instance != null)
        {
            CS_CustomInputActionManager.instance
                .onInputTypeChanged -=
                HandleInputTypeChanged;
        }

        ResetScaleAnimation();
    }

    public override void SetPresenter(
        CS_UIHideMarkerPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 更新
    // =========================================================

    private void Update()
    {
        if (!_isVisible)
        {
            return;
        }

        UpdateScaleAnimation();
    }

    // =========================================================
    // 表示・非表示
    // =========================================================

    /// <summary>
    /// ハイドマーカー全体の表示状態を変更する。
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        if (_markerRect == null)
        {
            return;
        }

        bool wasVisible =
            _markerRect.gameObject.activeSelf;

        _isVisible = isVisible;

        if (wasVisible != isVisible)
        {
            _markerRect.gameObject.SetActive(
                isVisible
            );
        }

        if (!isVisible)
        {
            ResetScaleAnimation();
            return;
        }

        // 非表示から表示されたときだけ、
        // Keyの拡縮を最初から開始する。
        if (!wasVisible)
        {
            RestartScaleAnimation();
        }
    }

    /// <summary>
    /// ハイドマーカーのCanvas上の位置を設定する。
    /// </summary>
    public void SetPosition(
        Vector2 anchoredPosition)
    {
        if (_markerRect == null)
        {
            return;
        }

        _markerRect.anchoredPosition =
            anchoredPosition;
    }

    // =========================================================
    // 入力方式切り替え
    // =========================================================

    private void HandleInputTypeChanged(
        CS_CustomInputActionManager.InputType inputType)
    {
        ApplyInputType(inputType);

        if (_isVisible)
        {
            RestartScaleAnimation();
        }
    }

    private void ApplyInputType(
        CS_CustomInputActionManager.InputType inputType)
    {
        _isKeyboardMouse =
            inputType ==
            CS_CustomInputActionManager.InputType
                .KeyboardMouse;

        if (_keyboardMouseMarkerRoot != null)
        {
            _keyboardMouseMarkerRoot.SetActive(
                _isKeyboardMouse
            );
        }

        if (_controllerMarkerRoot != null)
        {
            _controllerMarkerRoot.SetActive(
                !_isKeyboardMouse
            );
        }

        // 入力方式を切り替えたとき、
        // 使用しない側のKeyを元の倍率に戻す。
        ResetBothKeyScales();
    }

    // =========================================================
    // Key拡縮アニメーション
    // =========================================================

    private void UpdateScaleAnimation()
    {
        RectTransform currentKeyRect =
            GetCurrentKeyRect();

        if (currentKeyRect == null)
        {
            return;
        }

        if (_scaleAnimationDuration <= 0f)
        {
            SetCurrentKeyScale(
                _maximumScale
            );

            return;
        }

        _animationElapsed +=
            Time.unscaledDeltaTime;

        float animationRate =
            _animationElapsed /
            _scaleAnimationDuration;

        // 0 → 1 → 0を繰り返す。
        float pingPongRate =
            Mathf.PingPong(
                animationRate,
                1f
            );

        // 拡大・縮小の切り返しを滑らかにする。
        float smoothRate =
            Mathf.SmoothStep(
                0f,
                1f,
                pingPongRate
            );

        float scaleMultiplier =
            Mathf.Lerp(
                _minimumScale,
                _maximumScale,
                smoothRate
            );

        SetCurrentKeyScale(
            scaleMultiplier
        );
    }

    private void RestartScaleAnimation()
    {
        _animationElapsed = 0f;

        ResetBothKeyScales();

        SetCurrentKeyScale(
            _minimumScale
        );
    }

    private void ResetScaleAnimation()
    {
        _animationElapsed = 0f;

        ResetBothKeyScales();
    }

    /// <summary>
    /// 現在使用中の入力方式に対応するKeyを取得する。
    /// </summary>
    private RectTransform GetCurrentKeyRect()
    {
        return _isKeyboardMouse
            ? _keyboardMouseKeyRect
            : _controllerKeyRect;
    }

    /// <summary>
    /// 現在使用中のKeyだけを拡縮する。
    /// </summary>
    private void SetCurrentKeyScale(
        float scaleMultiplier)
    {
        RectTransform currentKeyRect =
            GetCurrentKeyRect();

        if (currentKeyRect == null)
        {
            return;
        }

        Vector3 initialScale =
            GetCurrentKeyInitialScale();

        currentKeyRect.localScale =
            new Vector3(
                initialScale.x * scaleMultiplier,
                initialScale.y * scaleMultiplier,
                initialScale.z
            );
    }

    /// <summary>
    /// 現在使用中のKeyの初期倍率を取得する。
    /// </summary>
    private Vector3 GetCurrentKeyInitialScale()
    {
        return _isKeyboardMouse
            ? _keyboardMouseKeyInitialScale
            : _controllerKeyInitialScale;
    }

    /// <summary>
    /// 両方のKeyをInspectorで設定した初期倍率に戻す。
    /// </summary>
    private void ResetBothKeyScales()
    {
        if (_keyboardMouseKeyRect != null)
        {
            _keyboardMouseKeyRect.localScale =
                _keyboardMouseKeyInitialScale;
        }

        if (_controllerKeyRect != null)
        {
            _controllerKeyRect.localScale =
                _controllerKeyInitialScale;
        }
    }

    // =========================================================
    // 共通処理
    // =========================================================

    private static void SetRaycastTarget(
        Graphic graphic,
        bool isRaycastTarget)
    {
        if (graphic == null)
        {
            return;
        }

        graphic.raycastTarget =
            isRaycastTarget;
    }

    // =========================================================
    // Inspector値の補正
    // =========================================================

    private void OnValidate()
    {
        _scaleAnimationDuration =
            Mathf.Max(
                0.01f,
                _scaleAnimationDuration
            );

        _minimumScale =
            Mathf.Max(
                0.01f,
                _minimumScale
            );

        _maximumScale =
            Mathf.Max(
                _minimumScale,
                _maximumScale
            );
    }
}