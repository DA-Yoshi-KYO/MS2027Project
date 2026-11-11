/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-02 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スキャンの目印1つ分の View。
/// 種類ごとのアイコン・距離・犯罪完遂度(悪人グループだけ)を出す
///
/// ・悪人グループ: 背景(灰色の三角)の上に、完遂度の分だけ下から赤い三角を溜め、一番上に悪人のマークを重ねる
/// ・警察・巡回ドローン: アイコンを1枚出す(背景のImageの画像を差し替える)
/// ・背景は自分の Image。赤い三角・悪人のマーク・距離の文字は、Inspector で設定されていなければ実行時に作る
///   (プレハブを触らなくても表示できるように)
/// </summary>
public class CS_UIScanMarkerItemView :
    MonoBehaviour
{
    [Header("表示(空なら実行時に作る)")]
    [SerializeField] private Image _backgroundImage;         // 悪人は背景の三角、警察・ドローンはアイコン(空なら自分のImage)
    [SerializeField] private Image _crimeFillImage;          // 完遂度の分だけ下から溜まる赤い三角(Filled・縦・下から)
    [SerializeField] private Image _villainMarkImage;        // 悪人のマーク(背景の上に重ねる)
    [SerializeField] private TextMeshProUGUI _distanceText;  // 距離(例「12m」)

    [Header("実行時に作る時の設定")]
    [Tooltip("悪人のマークを置く範囲(背景に対する割合。左下)")]
    [SerializeField] private Vector2 _villainMarkAnchorMin = new(0.072f, 0.073f);

    [Tooltip("悪人のマークを置く範囲(背景に対する割合。右上)")]
    [SerializeField] private Vector2 _villainMarkAnchorMax = new(0.928f, 0.927f);

    [SerializeField] private float _distanceTextSize = 24f;
    [SerializeField] private Vector2 _distanceTextOffset = new(0f, -40f);

    // =========================================================
    // 追加：波アニメーション設定
    // =========================================================

    [Header("表示時の波アニメーション")]

    [Tooltip(
        "アイコンが表示されてから動き始めるまでの秒数"
    )]
    [Min(0f)]
    [SerializeField]
    private float _waveStartDelay = 0.5f;

    [Tooltip(
        "アイコンが上方向へ移動する高さ。Canvas上のピクセル"
    )]
    [Min(0f)]
    [SerializeField]
    private float _waveHeight = 20f;

    [Tooltip(
        "元の位置から上へ移動する秒数"
    )]
    [Min(0.01f)]
    [SerializeField]
    private float _waveUpDuration = 0.15f;

    [Tooltip(
        "上の位置から元の位置へ戻る秒数"
    )]
    [Min(0.01f)]
    [SerializeField]
    private float _waveDownDuration = 0.25f;

    private bool _isInitialized;

    // =========================================================
    // 追加：位置・アニメーション状態
    // =========================================================

    private RectTransform _rectTransform;

    private Vector2 _baseAnchoredPosition;

    private float _waveElapsed;

    private float _currentWaveOffsetY;

    private bool _isWaveAnimating;

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        _rectTransform =
            transform as RectTransform;
    }

    private void Update()
    {
        UpdateWaveAnimation();
    }

    private void OnDisable()
    {
        StopWaveAnimation();
    }

    // =========================================================
    // 追加：基準位置
    // =========================================================

    /// <summary>
    /// Presenterから渡されたCanvas上の基準位置を設定する。
    /// 波アニメーション中は、基準位置へYオフセットを加える。
    /// </summary>
    public void SetBasePosition(
        Vector2 anchoredPosition)
    {
        _baseAnchoredPosition =
            anchoredPosition;

        ApplyAnimatedPosition();
    }

    // =========================================================
    // 追加：波アニメーション
    // =========================================================

    /// <summary>
    /// 表示後、少し待ってから上昇・下降する
    /// 波アニメーションを開始する。
    /// </summary>
    public void PlayWaveAnimation()
    {
        _waveElapsed = 0f;
        _currentWaveOffsetY = 0f;
        _isWaveAnimating = true;

        ApplyAnimatedPosition();
    }

    private void UpdateWaveAnimation()
    {
        if (!_isWaveAnimating)
            return;

        _waveElapsed +=
            Time.unscaledDeltaTime;

        // 表示直後は指定時間待機する。
        if (_waveElapsed < _waveStartDelay)
        {
            _currentWaveOffsetY = 0f;
            ApplyAnimatedPosition();
            return;
        }

        float animationElapsed =
            _waveElapsed -
            _waveStartDelay;

        // 上昇
        if (animationElapsed < _waveUpDuration)
        {
            float rate =
                animationElapsed /
                _waveUpDuration;

            float smoothRate =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    rate
                );

            _currentWaveOffsetY =
                Mathf.Lerp(
                    0f,
                    _waveHeight,
                    smoothRate
                );

            ApplyAnimatedPosition();
            return;
        }

        float downElapsed =
            animationElapsed -
            _waveUpDuration;

        // 下降
        if (downElapsed < _waveDownDuration)
        {
            float rate =
                downElapsed /
                _waveDownDuration;

            float smoothRate =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    rate
                );

            _currentWaveOffsetY =
                Mathf.Lerp(
                    _waveHeight,
                    0f,
                    smoothRate
                );

            ApplyAnimatedPosition();
            return;
        }

        // 1回だけ動かして終了する。
        StopWaveAnimation();
    }

    private void StopWaveAnimation()
    {
        _isWaveAnimating = false;
        _waveElapsed = 0f;
        _currentWaveOffsetY = 0f;

        ApplyAnimatedPosition();
    }

    private void ApplyAnimatedPosition()
    {
        if (_rectTransform == null)
        {
            _rectTransform =
                transform as RectTransform;
        }

        if (_rectTransform == null)
            return;

        _rectTransform.anchoredPosition =
            _baseAnchoredPosition +
            Vector2.up *
            _currentWaveOffsetY;
    }

    /// <summary>
    /// 悪人グループとして表示する(背景・完遂度の赤い三角・マーク・距離)
    /// </summary>
    public void ApplyVillain(
        Sprite background,
        Sprite crimeFill,
        Sprite mark,
        float distance,
        float crimeProgress)
    {
        Initialize();

        SetSprite(
            _backgroundImage,
            background
        );

        if (_crimeFillImage != null)
        {
            SetActive(
                _crimeFillImage,
                true
            );

            SetSprite(
                _crimeFillImage,
                crimeFill
            );

            _crimeFillImage.fillAmount =
                Mathf.Clamp01(
                    crimeProgress
                );
        }

        if (_villainMarkImage != null)
        {
            SetActive(
                _villainMarkImage,
                true
            );

            SetSprite(
                _villainMarkImage,
                mark
            );
        }

        SetDistance(distance);
    }

    /// <summary>
    /// 警察・巡回ドローンとして表示する(アイコン・距離)
    /// </summary>
    public void ApplyIcon(
        Sprite icon,
        float distance)
    {
        Initialize();

        SetSprite(
            _backgroundImage,
            icon
        );

        SetActive(
            _crimeFillImage,
            false
        );

        SetActive(
            _villainMarkImage,
            false
        );

        SetDistance(distance);
    }

    private void SetDistance(
        float distance)
    {
        if (_distanceText != null)
        {
            _distanceText.text =
                $"{Mathf.RoundToInt(distance)}m";
        }
    }

    private static void SetSprite(
        Image image,
        Sprite sprite)
    {
        if (image != null &&
            sprite != null &&
            image.sprite != sprite)
        {
            image.sprite = sprite;
        }
    }

    private static void SetActive(
        Image image,
        bool isActive)
    {
        if (image != null &&
            image.gameObject.activeSelf !=
                isActive)
        {
            image.gameObject.SetActive(
                isActive
            );
        }
    }

    // 設定されていない部分を作る(最初の1回だけ)
    private void Initialize()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;

        if (_backgroundImage == null)
        {
            _backgroundImage =
                GetComponent<Image>();
        }

        if (_backgroundImage != null)
        {
            _backgroundImage.color =
                Color.white;

            _backgroundImage.preserveAspect =
                true;
        }

        if (_crimeFillImage == null)
        {
            _crimeFillImage =
                CreateImage(
                    "CrimeFill",
                    Vector2.zero,
                    Vector2.one
                );

            _crimeFillImage.type =
                Image.Type.Filled;

            _crimeFillImage.fillMethod =
                Image.FillMethod.Vertical;

            _crimeFillImage.fillOrigin =
                (int)Image
                    .OriginVertical.Bottom;

            _crimeFillImage.fillAmount =
                0f;
        }

        if (_villainMarkImage == null)
        {
            _villainMarkImage =
                CreateImage(
                    "VillainMark",
                    _villainMarkAnchorMin,
                    _villainMarkAnchorMax
                );
        }

        if (_distanceText == null)
        {
            _distanceText =
                CreateDistanceText();
        }
    }

    // 自分(背景)の上に重ねる Image を作る(後に作った物ほど手前に出る)
    private Image CreateImage(
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject imageObject =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image)
            );

        RectTransform rect =
            imageObject.GetComponent<
                RectTransform
            >();

        rect.SetParent(
            transform,
            false
        );

        rect.anchorMin =
            anchorMin;

        rect.anchorMax =
            anchorMax;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        Image image =
            imageObject.GetComponent<Image>();

        image.preserveAspect =
            true;

        image.raycastTarget =
            false;

        return image;
    }

    // アイコンの下に出す距離の文字
    private TextMeshProUGUI CreateDistanceText()
    {
        GameObject textObject =
            new GameObject(
                "DistanceText",
                typeof(RectTransform)
            );

        RectTransform rect =
            textObject.GetComponent<
                RectTransform
            >();

        rect.SetParent(
            transform,
            false
        );

        rect.anchoredPosition =
            _distanceTextOffset;

        rect.sizeDelta =
            new Vector2(
                120f,
                30f
            );

        TextMeshProUGUI text =
            textObject.AddComponent<
                TextMeshProUGUI
            >();

        text.fontSize =
            _distanceTextSize;

        text.alignment =
            TextAlignmentOptions.Center;

        text.color =
            Color.white;

        text.raycastTarget =
            false;

        return text;
    }

    private void OnValidate()
    {
        _waveStartDelay =
            Mathf.Max(
                0f,
                _waveStartDelay
            );

        _waveHeight =
            Mathf.Max(
                0f,
                _waveHeight
            );

        _waveUpDuration =
            Mathf.Max(
                0.01f,
                _waveUpDuration
            );

        _waveDownDuration =
            Mathf.Max(
                0.01f,
                _waveDownDuration
            );
    }
}