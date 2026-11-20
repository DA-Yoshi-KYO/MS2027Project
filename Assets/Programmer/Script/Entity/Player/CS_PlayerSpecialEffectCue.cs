using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * 必殺技を使ったこと・効果が続いていることを知らせる仮の演出
 * UI・エフェクト担当の正式な演出(カットインなど)ができたら、このクラスごと消す
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仮の演出。消す時は、このファイルを消して、Player.prefabのCS_PlayerSpecialEffectCueを外すだけでよい(他のクラスからは参照していない)
 * ・自分のプレイヤー(人が操作しているプレイヤー。CS_Player.isLocalPlayer)の画面にだけ、実行中にCanvasを組み立てて出す
 * ・カットイン: 「効果だけ」の必殺技を使った瞬間(CS_PlayerSpecialAttack.onEffectOnlyUsed)に、画面の真ん中をキャラクターの色の帯が
 *   横切り、必殺技の名前を出す(_cutInTime秒。仕様書「1秒以内とか超短くカットインを入れて」)
 *   攻撃する必殺技は攻撃のモーションが出るので、カットインは出さない
 * ・残り時間: 必殺技の効果が続いている間(CS_PlayerCharacter.isEffectActive)、画面の左に効果の名前・残り秒数・減っていくバーを出す
 *   攻撃する必殺技・効果だけの必殺技の両方で出る。効果の残り時間は同期されているので、効果が始まったのはサーバーでも、本人の画面で分かる
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerSpecialEffectCue : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset _font;                     // 日本語が出るフォント(BIZUDGothic-Bold SDF)
    [SerializeField][Min(0.1f)] private float _cutInTime = 0.9f;      // カットインを出す時間(秒)

    private CS_Player _player;
    private CS_PlayerCharacter _character;
    private CS_PlayerSpecialAttack _special;

    private GameObject _root;
    private RectTransform _band;
    private Image _bandImage;
    private TextMeshProUGUI _cutInText;
    private CanvasGroup _cutInGroup;
    private CanvasGroup _timerGroup;
    private Image _timerFill;
    private TextMeshProUGUI _timerText;

    private float _cutInElapsed = -1f;   // カットインを出してからの時間(出していなければ負)
    private float _effectDuration;       // 今続いている効果の長さ(バーの割合に使う)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _character = GetComponent<CS_PlayerCharacter>();
        _special = GetComponent<CS_PlayerSpecialAttack>();
    }

    private void OnEnable()
    {
        if (_special != null) _special.onEffectOnlyUsed += HandleEffectOnlyUsed;
    }

    private void OnDisable()
    {
        if (_special != null) _special.onEffectOnlyUsed -= HandleEffectOnlyUsed;
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root);
    }

    private void Update()
    {
        if (!_player.isLocalPlayer)
        {
            if (_root != null) _root.SetActive(false);
            return;
        }

        if (_root == null) Build();
        else if (!_root.activeSelf) _root.SetActive(true);

        UpdateCutIn(Time.unscaledDeltaTime);
        UpdateTimer();
    }

    private void HandleEffectOnlyUsed()
    {
        if (!_player.isLocalPlayer) return;
        if (_root == null) Build();

        CSO_PlayableCharacter character = _character != null ? _character.currentCharacter : null;
        Color color = character != null ? character.themeColor : Color.white;
        _bandImage.color = new Color(color.r, color.g, color.b, 0.85f);
        _cutInText.text = character != null ? $"{character.specialName}<size=60%>  {character.displayName}</size>" : "必殺技";
        _cutInElapsed = 0f;
    }

    // ========================================
    // カットイン
    // ========================================

    private void UpdateCutIn(float deltaTime)
    {
        if (_cutInElapsed < 0f)
        {
            _cutInGroup.alpha = 0f;
            return;
        }

        _cutInElapsed += deltaTime;
        float t = _cutInElapsed / _cutInTime;
        if (t >= 1f)
        {
            _cutInElapsed = -1f;
            _cutInGroup.alpha = 0f;
            return;
        }

        // 入る(最初の2割) → 止まる → 抜ける(最後の2割)。帯は左から入って右へ抜ける
        float x;
        if (t < 0.2f) x = Mathf.Lerp(-1f, 0f, EaseOut(t / 0.2f));
        else if (t > 0.8f) x = Mathf.Lerp(0f, 1f, EaseIn((t - 0.8f) / 0.2f));
        else x = 0f;

        _band.anchoredPosition = new Vector2(x * 2200f, 0f);
        _cutInGroup.alpha = 1f;
    }

    private static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private static float EaseIn(float t)
    {
        return t * t;
    }

    // ========================================
    // 残り時間
    // ========================================

    private void UpdateTimer()
    {
        bool isActive = _character != null && _character.isEffectActive;
        _timerGroup.alpha = isActive ? 1f : 0f;
        if (!isActive)
        {
            _effectDuration = 0f;
            return;
        }

        CSO_PlayableCharacter character = _character.currentCharacter;
        float remaining = _character.effectRemaining;
        if (_effectDuration <= 0f) _effectDuration = Mathf.Max(remaining, character != null ? character.effectDuration : remaining);

        Color color = character != null ? character.themeColor : Color.white;
        _timerFill.color = color;
        float rate = _effectDuration > 0f ? Mathf.Clamp01(remaining / _effectDuration) : 0f;
        _timerFill.rectTransform.localScale = new Vector3(rate, 1f, 1f);

        string effectName = character != null ? character.specialName : "必殺技";
        _timerText.text = $"{effectName}  <b>{Mathf.CeilToInt(remaining)}</b>秒";
    }

    // ========================================
    // UIを組み立てる
    // ========================================

    private void Build()
    {
        _root = new GameObject("PlayerSpecialEffectCue(仮)");
        Canvas canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // カットイン(画面の真ん中を横切る、少し斜めの帯)
        RectTransform cutIn = CreateRect("CutIn", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
        _cutInGroup = cutIn.gameObject.AddComponent<CanvasGroup>();
        _cutInGroup.alpha = 0f;
        _cutInGroup.blocksRaycasts = false;

        _band = CreateRect("Band", cutIn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2400f, 170f));
        _band.localRotation = Quaternion.Euler(0f, 0f, 6f);
        _bandImage = _band.gameObject.AddComponent<Image>();
        _bandImage.raycastTarget = false;

        _cutInText = CreateText(_band, 110f, TextAlignmentOptions.Center);
        RectTransform cutInTextRect = _cutInText.rectTransform;
        cutInTextRect.anchorMin = Vector2.zero;
        cutInTextRect.anchorMax = Vector2.one;
        cutInTextRect.sizeDelta = Vector2.zero;
        _cutInText.fontStyle = FontStyles.Bold | FontStyles.Italic;
        _cutInText.outlineWidth = 0.15f;
        _cutInText.outlineColor = new Color32(0, 0, 0, 200);

        // 残り時間(画面の左)
        RectTransform timer = CreateRect("Timer", _root.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 200f), new Vector2(420f, 64f));
        timer.pivot = new Vector2(0f, 0.5f);
        timer.anchoredPosition = new Vector2(40f, 200f);
        _timerGroup = timer.gameObject.AddComponent<CanvasGroup>();
        _timerGroup.alpha = 0f;
        _timerGroup.blocksRaycasts = false;
        Image timerBack = timer.gameObject.AddComponent<Image>();
        timerBack.color = new Color(0f, 0f, 0f, 0.6f);
        timerBack.raycastTarget = false;

        RectTransform fill = CreateRect("Fill", timer, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 8f));
        fill.pivot = new Vector2(0f, 0f);   // 左端を基準に、横の大きさ(localScale.x)で減らす
        _timerFill = fill.gameObject.AddComponent<Image>();
        _timerFill.raycastTarget = false;

        _timerText = CreateText(timer, 34f, TextAlignmentOptions.Left);
        RectTransform timerTextRect = _timerText.rectTransform;
        timerTextRect.anchorMin = Vector2.zero;
        timerTextRect.anchorMax = Vector2.one;
        timerTextRect.offsetMin = new Vector2(16f, 8f);
        timerTextRect.offsetMax = new Vector2(-12f, 0f);
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private TextMeshProUGUI CreateText(Transform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject gameObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
