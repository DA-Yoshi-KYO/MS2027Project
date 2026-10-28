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
/// UICanvas上のハイドマーカー
/// （飛べるハイドポイントの目印）を表示するView。
///
/// ・入力方式に応じて画像を切り替える
/// ・表示中は最小倍率と最大倍率の間で拡縮を繰り返す
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

    [Header("拡縮するルート")]
    [SerializeField]
    private RectTransform _animationRoot;

    [Header("キーボード＆マウス用")]
    [SerializeField]
    private GameObject _keyboardMouseMarkerRoot;

    [SerializeField]
    private Image _keyboardMouseMarkerImage;

    [Header("コントローラー用")]
    [SerializeField]
    private GameObject _controllerMarkerRoot;

    [SerializeField]
    private Image _controllerMarkerImage;

    [Header("拡縮アニメーション")]

    [Tooltip("最小倍率から最大倍率へ変化する秒数")]
    [Min(0.01f)]
    [SerializeField]
    private float _scaleAnimationDuration = 0.5f;

    [Tooltip("拡縮アニメーションの最小倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float _minimumScale = 0.7f;

    [Tooltip("拡縮アニメーションの最大倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float _maximumScale = 1f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private float _animationElapsed;
    private bool _isVisible;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        if (_keyboardMouseMarkerImage != null)
        {
            _keyboardMouseMarkerImage.raycastTarget =
                false;
        }

        if (_controllerMarkerImage != null)
        {
            _controllerMarkerImage.raycastTarget =
                false;
        }

        _isVisible = false;

        if (_markerRect != null)
        {
            _markerRect.gameObject.SetActive(false);
        }

        ResetScaleAnimation();
    }

    private void OnEnable()
    {
        CS_CustomInputActionManager.instance
            .onInputTypeChanged +=
            HandleInputTypeChanged;

        // 現在使用中の入力方式を即座に反映する。
        ApplyInputType(
            CS_CustomInputActionManager.instance
                .currentInputType
        );
    }

    private void OnDisable()
    {
        CS_CustomInputActionManager.instance
            .onInputTypeChanged -=
            HandleInputTypeChanged;

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
            return;

        UpdateScaleAnimation();
    }

    // =========================================================
    // 表示・非表示
    // =========================================================

    /// <summary>
    /// 目印の表示・非表示を切り替える。
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        if (_markerRect == null)
            return;

        bool wasVisible =
            _markerRect.gameObject.activeSelf;

        _isVisible = isVisible;

        // 実際のActive状態を必ず同期する
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

        // 非表示から表示になった時だけ、
        // 拡縮を最初から開始する
        if (!wasVisible)
        {
            RestartScaleAnimation();
        }
    }
    /// <summary>
    /// 目印の位置（Canvas上の座標）を設定する。
    /// </summary>
    public void SetPosition(
        Vector2 anchoredPosition)
    {
        if (_markerRect == null)
            return;

        _markerRect.anchoredPosition =
            anchoredPosition;
    }

    // =========================================================
    // 入力方式による画像切り替え
    // =========================================================

    private void HandleInputTypeChanged(
        CS_CustomInputActionManager.InputType inputType)
    {
        ApplyInputType(inputType);

        // 表示中に入力方式が変わった場合は、
        // 拡縮を最初から再生する。
        if (_isVisible)
        {
            RestartScaleAnimation();
        }
    }

    private void ApplyInputType(
        CS_CustomInputActionManager.InputType inputType)
    {
        bool isKeyboardMouse =
            inputType ==
            CS_CustomInputActionManager.InputType
                .KeyboardMouse;

        if (_keyboardMouseMarkerRoot != null)
        {
            _keyboardMouseMarkerRoot.SetActive(
                isKeyboardMouse
            );
        }

        if (_controllerMarkerRoot != null)
        {
            _controllerMarkerRoot.SetActive(
                !isKeyboardMouse
            );
        }
    }

    // =========================================================
    // ループ拡縮アニメーション
    // =========================================================

    private void UpdateScaleAnimation()
    {
        if (_animationRoot == null)
            return;

        if (_scaleAnimationDuration <= 0f)
        {
            SetScale(_maximumScale);
            return;
        }

        _animationElapsed +=
            Time.unscaledDeltaTime;

        float animationRate =
            _animationElapsed /
            _scaleAnimationDuration;

        // 0 → 1 → 0 → 1を繰り返す。
        float pingPongRate = Mathf.PingPong(
            animationRate,
            1f
        );

        // 拡大・縮小の切り返しを滑らかにする。
        float smoothRate = Mathf.SmoothStep(
            0f,
            1f,
            pingPongRate
        );

        float scale = Mathf.Lerp(
            _minimumScale,
            _maximumScale,
            smoothRate
        );

        SetScale(scale);
    }

    private void RestartScaleAnimation()
    {
        _animationElapsed = 0f;
        SetScale(_minimumScale);
    }

    private void ResetScaleAnimation()
    {
        _animationElapsed = 0f;
        SetScale(_maximumScale);
    }

    private void SetScale(float scale)
    {
        if (_animationRoot == null)
            return;

        _animationRoot.localScale =
            Vector3.one * scale;
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