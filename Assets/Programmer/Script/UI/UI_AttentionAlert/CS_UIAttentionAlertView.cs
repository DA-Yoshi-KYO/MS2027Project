/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-29 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 警戒中に、画面上部と下部の赤画像を
/// UVスクロールによって左から右へループ表示するView。
///
/// RectTransformの位置は変更しないため、
/// Inspectorで設定したX・Y座標は維持される。
/// </summary>
public sealed class CS_UIAttentionAlertView :
    CS_BaseView<CS_UIAttentionAlertPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("表示・非表示を切り替えるルート")]
    [SerializeField]
    private GameObject _displayRoot;

    [Header("上の赤画像")]
    [SerializeField]
    private RawImage _topRedImage;

    [Header("下の赤画像")]
    [SerializeField]
    private RawImage _bottomRedImage;

    [Header("UVスクロール")]

    [Tooltip("UVが横方向へ1周する秒数")]
    [Min(0.01f)]
    [SerializeField]
    private float _loopDuration = 2f;

    [Tooltip("横方向の移動量。1でUVを1周する")]
    [SerializeField]
    private float _uvScrollAmount = 1f;

    [Tooltip(
        "下画像の開始位置をずらす。"
        + "0なら上下同時、0.5なら半周ずらす"
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float _bottomPhaseOffset;

    [Header("移動方向")]

    [Tooltip(
        "ONなら左から右、"
        + "OFFなら右から左へ移動する"
    )]
    [SerializeField]
    private bool _moveLeftToRight = true;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private float _loopElapsed;
    private bool _isAlerted;

    private Rect _topInitialUvRect;
    private Rect _bottomInitialUvRect;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        if (_topRedImage != null)
        {
            _topRedImage.raycastTarget = false;
            _topInitialUvRect = _topRedImage.uvRect;
        }

        if (_bottomRedImage != null)
        {
            _bottomRedImage.raycastTarget = false;
            _bottomInitialUvRect = _bottomRedImage.uvRect;
        }

        // 初期状態は必ず非表示にする。
        _isAlerted = false;

        if (_displayRoot != null)
        {
            _displayRoot.SetActive(false);
        }

        ResetUvAnimation();
    }

    public override void SetPresenter(
        CS_UIAttentionAlertPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 更新
    // =========================================================

    private void Update()
    {
        if (!_isAlerted)
            return;

        UpdateUvAnimation();
    }

    // =========================================================
    // 表示・非表示
    // =========================================================

    /// <summary>
    /// 警戒UIの表示状態を変更する。
    /// </summary>
    public void SetAlerted(bool isAlerted)
    {
        bool stateChanged =
            _isAlerted != isAlerted;

        _isAlerted = isAlerted;

        if (_displayRoot != null &&
            _displayRoot.activeSelf != isAlerted)
        {
            _displayRoot.SetActive(isAlerted);
        }

        if (!stateChanged)
            return;

        if (isAlerted)
        {
            RestartUvAnimation();
        }
        else
        {
            ResetUvAnimation();
        }
    }

    // =========================================================
    // UVスクロール
    // =========================================================

    private void UpdateUvAnimation()
    {
        if (_loopDuration <= 0f)
            return;

        _loopElapsed +=
            Time.unscaledDeltaTime;

        float loopRate = Mathf.Repeat(
            _loopElapsed / _loopDuration,
            1f
        );

        float topRate = loopRate;

        float bottomRate = Mathf.Repeat(
            loopRate + _bottomPhaseOffset,
            1f
        );

        ApplyUvOffset(
            _topRedImage,
            _topInitialUvRect,
            topRate
        );

        ApplyUvOffset(
            _bottomRedImage,
            _bottomInitialUvRect,
            bottomRate
        );
    }

    private void ApplyUvOffset(
        RawImage rawImage,
        Rect initialUvRect,
        float rate)
    {
        if (rawImage == null)
            return;

        float direction =
            _moveLeftToRight ? -1f : 1f;

        float offsetX =
            rate *
            _uvScrollAmount *
            direction;

        rawImage.uvRect = new Rect(
            initialUvRect.x + offsetX,
            initialUvRect.y,
            initialUvRect.width,
            initialUvRect.height
        );
    }

    private void RestartUvAnimation()
    {
        _loopElapsed = 0f;

        ApplyUvOffset(
            _topRedImage,
            _topInitialUvRect,
            0f
        );

        ApplyUvOffset(
            _bottomRedImage,
            _bottomInitialUvRect,
            _bottomPhaseOffset
        );
    }

    private void ResetUvAnimation()
    {
        _loopElapsed = 0f;

        if (_topRedImage != null)
        {
            _topRedImage.uvRect =
                _topInitialUvRect;
        }

        if (_bottomRedImage != null)
        {
            _bottomRedImage.uvRect =
                _bottomInitialUvRect;
        }
    }

    // =========================================================
    // Inspector値の補正
    // =========================================================

    private void OnValidate()
    {
        _loopDuration =
            Mathf.Max(
                0.01f,
                _loopDuration
            );
    }
}