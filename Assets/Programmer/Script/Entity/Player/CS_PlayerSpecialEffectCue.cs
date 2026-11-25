using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * 必殺技の効果が続いていることを知らせる仮の表示(残り時間)
 * UI担当の正式な表示ができたら、このクラスごと消す(必殺技を使った時のカットインはCS_PlayerSpecialCutInへ移した)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仮の演出。消す時は、このファイルを消して、Player.prefabのCS_PlayerSpecialEffectCueを外すだけでよい(他のクラスからは参照していない)
 * ・自分のプレイヤー(人が操作しているプレイヤー。CS_Player.isLocalPlayer)の画面にだけ、実行中にCanvasを組み立てて出す
 * ・残り時間: 必殺技の効果が続いている間(CS_PlayerCharacter.isEffectActive)、画面の左に効果の名前・残り秒数・減っていくバーを出す
 *   攻撃する必殺技・効果だけの必殺技の両方で出る。効果の残り時間は同期されているので、効果が始まったのはサーバーでも、本人の画面で分かる
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerSpecialEffectCue : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset _font;                     // 日本語が出るフォント(BIZUDGothic-Bold SDF)

    private CS_Player _player;
    private CS_PlayerCharacter _character;

    private GameObject _root;
    private CanvasGroup _timerGroup;
    private Image _timerFill;
    private TextMeshProUGUI _timerText;

    private float _effectDuration;       // 今続いている効果の長さ(バーの割合に使う)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _character = GetComponent<CS_PlayerCharacter>();
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

        UpdateTimer();
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
