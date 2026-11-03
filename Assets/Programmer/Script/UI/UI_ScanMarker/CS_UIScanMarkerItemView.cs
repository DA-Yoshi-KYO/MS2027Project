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
public class CS_UIScanMarkerItemView : MonoBehaviour
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

    private bool _isInitialized;

    /// <summary>
    /// 悪人グループとして表示する(背景・完遂度の赤い三角・マーク・距離)
    /// </summary>
    public void ApplyVillain(Sprite background, Sprite crimeFill, Sprite mark, float distance, float crimeProgress)
    {
        Initialize();

        SetSprite(_backgroundImage, background);

        if (_crimeFillImage != null)
        {
            SetActive(_crimeFillImage, true);
            SetSprite(_crimeFillImage, crimeFill);
            _crimeFillImage.fillAmount = Mathf.Clamp01(crimeProgress);
        }

        if (_villainMarkImage != null)
        {
            SetActive(_villainMarkImage, true);
            SetSprite(_villainMarkImage, mark);
        }

        SetDistance(distance);
    }

    /// <summary>
    /// 警察・巡回ドローンとして表示する(アイコン・距離)
    /// </summary>
    public void ApplyIcon(Sprite icon, float distance)
    {
        Initialize();

        SetSprite(_backgroundImage, icon);
        SetActive(_crimeFillImage, false);
        SetActive(_villainMarkImage, false);
        SetDistance(distance);
    }

    private void SetDistance(float distance)
    {
        if (_distanceText != null)
            _distanceText.text = $"{Mathf.RoundToInt(distance)}m";
    }

    private static void SetSprite(Image image, Sprite sprite)
    {
        if (image != null && sprite != null && image.sprite != sprite)
            image.sprite = sprite;
    }

    private static void SetActive(Image image, bool isActive)
    {
        if (image != null && image.gameObject.activeSelf != isActive)
            image.gameObject.SetActive(isActive);
    }

    // 設定されていない部分を作る(最初の1回だけ)
    private void Initialize()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;

        if (_backgroundImage == null)
            _backgroundImage = GetComponent<Image>();

        if (_backgroundImage != null)
        {
            _backgroundImage.color = Color.white;
            _backgroundImage.preserveAspect = true;
        }

        if (_crimeFillImage == null)
        {
            _crimeFillImage = CreateImage("CrimeFill", Vector2.zero, Vector2.one);
            _crimeFillImage.type = Image.Type.Filled;
            _crimeFillImage.fillMethod = Image.FillMethod.Vertical;
            _crimeFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            _crimeFillImage.fillAmount = 0f;
        }

        if (_villainMarkImage == null)
            _villainMarkImage = CreateImage("VillainMark", _villainMarkAnchorMin, _villainMarkAnchorMax);

        if (_distanceText == null)
            _distanceText = CreateDistanceText();
    }

    // 自分(背景)の上に重ねる Image を作る(後に作った物ほど手前に出る)
    private Image CreateImage(string objectName, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // アイコンの下に出す距離の文字
    private TextMeshProUGUI CreateDistanceText()
    {
        GameObject textObject = new GameObject("DistanceText", typeof(RectTransform));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchoredPosition = _distanceTextOffset;
        rect.sizeDelta = new Vector2(120f, 30f);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = _distanceTextSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
