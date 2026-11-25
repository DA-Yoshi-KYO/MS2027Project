using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * AIガイドの会話ウィンドウ(画面の下に出る、テキストだけの吹き出し)を表示するクラス
 * CS_PlayerAIGuideが実行時に作り、Show()で一言ずつ出す
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・AIガイドのキャラクターのモデル・UIのデザインがまだ無いため、仮の見た目としてコードでCanvasを作る(シーン・プレハブを増やさない)
 *   デザインができたら、このクラスごとUIのプレハブに差し替えてよい(CS_PlayerAIGuideはShow()を呼ぶだけ)
 * ・見た目: 画面下の中央に、半透明の黒い帯(名前 + 本文)。フェードで出し入れする
 * ・本文は1文字ずつ出す(_charactersPerSecond文字/秒)。出し終わってから_holdTime秒で消える
 * ・時間はTime.timeScaleの影響を受けない時間で数える(ゲームを止めている間も読める)
 * ・フォントは日本語の入ったTMPのフォント(BIZUDGothic-Bold SDF)をCS_PlayerAIGuideから受け取る
 */
// ========================================

public class CS_PlayerAIGuideWindow : MonoBehaviour
{
    private const float _fadeTime = 0.2f;           // 出し入れのフェードの時間(秒)
    private const float _charactersPerSecond = 30f; // 本文を出す速さ(文字/秒)
    private const int _sortingOrder = 50;           // 他のUIより手前に出す

    private CanvasGroup _group;
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _bodyText;
    private float _holdTime;
    private float _elapsed;
    private bool _isShowing;

    public bool isShowing => _isShowing;    // 一言を出している途中か(フェードで消える途中も含む)

    // ウィンドウを作る(CS_PlayerAIGuideが一度だけ呼ぶ)
    public static CS_PlayerAIGuideWindow Create(TMP_FontAsset font, string speakerName)
    {
        GameObject root = new GameObject("AIGuideWindow", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _sortingOrder;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 画面下の中央の帯
        RectTransform panel = CreateRect("Panel", root.transform);
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 60f);
        panel.sizeDelta = new Vector2(1100f, 150f);
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.05f, 0.1f, 0.78f);
        background.raycastTarget = false;

        // 名前
        TextMeshProUGUI nameText = CreateText("Name", panel, font, 28f, new Color(0.45f, 0.9f, 1f));
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(28f, -12f);
        nameRect.sizeDelta = new Vector2(-56f, 40f);
        nameText.text = speakerName;

        // 本文
        TextMeshProUGUI bodyText = CreateText("Body", panel, font, 34f, Color.white);
        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(28f, 14f);
        bodyRect.offsetMax = new Vector2(-28f, -54f);
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        CS_PlayerAIGuideWindow window = root.AddComponent<CS_PlayerAIGuideWindow>();
        window._group = root.GetComponent<CanvasGroup>();
        window._group.alpha = 0f;
        window._group.interactable = false;
        window._group.blocksRaycasts = false;
        window._nameText = nameText;
        window._bodyText = bodyText;
        return window;
    }

    // 一言を出す(出している途中なら、すぐに差し替える)。holdTimeは出し終わってから消えるまでの秒数
    public void Show(string message, float holdTime)
    {
        _bodyText.text = message;
        _bodyText.maxVisibleCharacters = 0;
        _holdTime = holdTime;
        _elapsed = 0f;
        _isShowing = true;
    }

    // すぐに消す
    public void Hide()
    {
        _isShowing = false;
        _group.alpha = 0f;
    }

    private void Update()
    {
        if (!_isShowing) return;

        _elapsed += Time.unscaledDeltaTime;

        int length = _bodyText.text.Length;
        float typeTime = length / _charactersPerSecond;
        _bodyText.maxVisibleCharacters = Mathf.Min(length, Mathf.FloorToInt(_elapsed * _charactersPerSecond));

        // フェードイン → 1文字ずつ出す → 保つ → フェードアウト
        float total = typeTime + _holdTime;
        float fadeIn = Mathf.Clamp01(_elapsed / _fadeTime);
        float fadeOut = Mathf.Clamp01((total + _fadeTime - _elapsed) / _fadeTime);
        _group.alpha = Mathf.Min(fadeIn, fadeOut);

        if (_elapsed >= total + _fadeTime) Hide();
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;
        return text;
    }
}
