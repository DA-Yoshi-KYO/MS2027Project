/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-17 | 初回作成
 * ================================================ */

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// エリア1か所分の犯罪完遂ゲージを表示するView。
///
/// シーン構成:
/// AreaGauge
/// ├─ GaugeBack
/// ├─ Gauge
/// ├─ Icon
/// └─ Value
///
/// ・現在地では100サイズ相当で表示する
/// ・現在地以外では50サイズ相当で表示する
/// ・現在地の場合だけValueを表示する
/// ・エリア移動時に滑らかに拡縮する
/// ・拡縮時は左端を固定し、右方向へ拡大、縮小する
/// </summary>
public sealed class CS_UICrimeProgressGaugeView :
    MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("担当するエリア")]
    [SerializeField]
    private CSE_CrimeAreaType _areaType;

    [Header("拡縮するAreaGauge本体")]
    [Tooltip(
        "AreaGauge_0など、" +
        "このスクリプトが付いているRectTransformを設定する"
    )]
    [SerializeField]
    private RectTransform _animationRoot;

    [Header("ゲージ背景")]
    [SerializeField]
    private Image _gaugeBackImage;

    [Header("犯罪完遂ゲージ")]
    [SerializeField]
    private Image _gaugeImage;

    [Header("エリアアイコン")]
    [SerializeField]
    private Image _iconImage;

    [Header("完遂率の数字")]
    [SerializeField]
    private TextMeshProUGUI _valueText;

    [Header("拡縮設定")]

    [Tooltip(
        "現在地ではないゲージの倍率。" +
        "基準サイズ100の場合、0.5で50になる"
    )]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float _inactiveScale = 0.5f;

    [Tooltip(
        "現在地のゲージの倍率。" +
        "基準サイズ100の場合、1で100になる"
    )]
    [Min(0.01f)]
    [SerializeField]
    private float _activeScale = 1f;

    [Tooltip("エリア移動時の拡縮時間")]
    [Min(0.01f)]
    [SerializeField]
    private float _scaleDuration = 0.25f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private Coroutine _scaleCoroutine;

    private Vector3 _initialScale =
        Vector3.one;

    private float _currentProgress;

    private bool _isCurrentArea;

    // =========================================================
    // 公開プロパティ
    // =========================================================

    public CSE_CrimeAreaType areaType =>
        _areaType;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        if (_animationRoot == null)
        {
            _animationRoot =
                transform as RectTransform;
        }

        if (_animationRoot != null)
        {
            SetLeftCenterTransform(
                _animationRoot
            );

            _initialScale =
                _animationRoot.localScale;
        }

        DisableRaycastTargets();

        _currentProgress = 0f;
        _isCurrentArea = false;

        if (_gaugeImage != null)
        {
            _gaugeImage.fillAmount = 0f;
        }

        if (_valueText != null)
        {
            _valueText.text = "0%";
            _valueText.gameObject.SetActive(false);
        }

        ApplyScaleImmediately(
            _inactiveScale
        );
    }


    /// <summary>
    /// UIがマウスやタッチ入力を遮らないようにする。
    /// </summary>
    private void DisableRaycastTargets()
    {
        if (_gaugeBackImage != null)
        {
            _gaugeBackImage.raycastTarget = false;
        }

        if (_gaugeImage != null)
        {
            _gaugeImage.raycastTarget = false;
        }

        if (_iconImage != null)
        {
            _iconImage.raycastTarget = false;
        }

        if (_valueText != null)
        {
            _valueText.raycastTarget = false;
        }
    }

    // =========================================================
    // 完遂率
    // =========================================================

    /// <summary>
    /// 犯罪完遂率を更新する。
    ///
    /// 現在地ではなくValueが非表示の場合も、
    /// テキストの内容は最新値に更新する。
    /// </summary>
    public void SetProgress(float progress)
    {
        _currentProgress =
            Mathf.Clamp(
                progress,
                0f,
                100f
            );

        if (_gaugeImage != null)
        {
            _gaugeImage.fillAmount =
                _currentProgress /
                100f;
        }

        if (_valueText != null)
        {
            int displayValue =
                Mathf.RoundToInt(
                    _currentProgress
                );

            _valueText.text =
                $"{displayValue}%";
        }
    }

    // =========================================================
    // 現在地
    // =========================================================

    /// <summary>
    /// このゲージが現在地かどうかを反映する。
    /// </summary>
    public void SetCurrentArea(
        bool isCurrentArea,
        bool useAnimation)
    {
        bool stateChanged =
            _isCurrentArea !=
            isCurrentArea;

        _isCurrentArea =
            isCurrentArea;

        // Valueは現在いるエリアだけ表示する。
        if (_valueText != null)
        {
            _valueText.gameObject.SetActive(
                _isCurrentArea
            );
        }

        float targetScale =
            _isCurrentArea
                ? _activeScale
                : _inactiveScale;

        // 初回反映などではアニメーションせず、
        // 正しい大きさを即座に適用する。
        if (!useAnimation)
        {
            StopScaleAnimation();

            ApplyScaleImmediately(
                targetScale
            );

            return;
        }

        // 現在地の状態が変わっていなければ、
        // 同じアニメーションを再実行しない。
        if (!stateChanged)
        {
            return;
        }

        PlayScaleAnimation(
            targetScale
        );
    }

    // =========================================================
    // 拡縮アニメーション
    // =========================================================

    /// <summary>
    /// 現在の大きさから指定倍率まで拡縮する。
    /// Pivotが左中央なので左端は動かない。
    /// </summary>
    private void PlayScaleAnimation(
        float targetScaleMultiplier)
    {
        StopScaleAnimation();

        _scaleCoroutine =
            StartCoroutine(
                ScaleAnimationCoroutine(
                    targetScaleMultiplier
                )
            );
    }

    private IEnumerator ScaleAnimationCoroutine(
        float targetScaleMultiplier)
    {
        if (_animationRoot == null)
        {
            _scaleCoroutine = null;
            yield break;
        }

        Vector3 startScale =
            _animationRoot.localScale;

        Vector3 targetScale =
            GetScale(
                targetScaleMultiplier
            );

        float elapsed = 0f;

        while (elapsed < _scaleDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float rate =
                Mathf.Clamp01(
                    elapsed /
                    _scaleDuration
                );

            // 開始と終了を滑らかにする。
            float smoothRate =
                rate *
                rate *
                (3f - 2f * rate);

            _animationRoot.localScale =
                Vector3.LerpUnclamped(
                    startScale,
                    targetScale,
                    smoothRate
                );

            yield return null;
        }

        _animationRoot.localScale =
            targetScale;

        _scaleCoroutine = null;
    }

    /// <summary>
    /// アニメーションせずに倍率を反映する。
    /// </summary>
    private void ApplyScaleImmediately(
        float scaleMultiplier)
    {
        if (_animationRoot == null)
        {
            return;
        }

        _animationRoot.localScale =
            GetScale(
                scaleMultiplier
            );
    }

    /// <summary>
    /// Inspector上の基準Scaleに倍率を掛けた値を返す。
    /// </summary>
    private Vector3 GetScale(
        float scaleMultiplier)
    {
        return new Vector3(
            _initialScale.x *
            scaleMultiplier,

            _initialScale.y *
            scaleMultiplier,

            _initialScale.z
        );
    }

    /// <summary>
    /// 再生中の拡縮アニメーションを停止する。
    /// </summary>
    private void StopScaleAnimation()
    {
        if (_scaleCoroutine == null)
        {
            return;
        }

        StopCoroutine(
            _scaleCoroutine
        );

        _scaleCoroutine = null;
    }

    // =========================================================
    // Pivot
    // =========================================================

    /// <summary>
    /// RectTransformの見た目上の位置を維持したまま、
    /// Pivotを変更する。
    ///
    /// 左中央のPivotにすることで、
    /// 左端を固定して右方向へ拡大、縮小できる。
    /// </summary>
    /// <summary>
    /// 親の左中央を基準に配置し、
    /// 拡縮時も左端を固定する。
    ///
    /// anchoredPositionは維持するため、
    /// Inspectorで設定したPos X、Pos Yはそのまま使われる。
    /// </summary>
    private static void SetLeftCenterTransform(
        RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        Vector2 currentPosition =
            rectTransform.anchoredPosition;

        rectTransform.anchorMin =
            new Vector2(0f, 0.5f);

        rectTransform.anchorMax =
            new Vector2(0f, 0.5f);

        rectTransform.pivot =
            new Vector2(0f, 0.5f);

        rectTransform.anchoredPosition =
            currentPosition;
    }
    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnDisable()
    {
        StopScaleAnimation();
    }

    private void OnDestroy()
    {
        StopScaleAnimation();
    }

    // =========================================================
    // Inspector値の補正
    // =========================================================

    private void OnValidate()
    {
        _inactiveScale =
            Mathf.Clamp(
                _inactiveScale,
                0.01f,
                1f
            );

        _activeScale =
            Mathf.Max(
                0.01f,
                _activeScale
            );

        _scaleDuration =
            Mathf.Max(
                0.01f,
                _scaleDuration
            );
    }
}