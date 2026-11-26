using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * 連続奇襲のコンボ数を画面に出す表示(自分の画面だけ)
 * 大きな数字(と小さなラベル)を出す
 * CS_PlayerAmbushEffectが、初めて使う時に実行時に作る(プレハブ・UICanvasには置かない)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・表示: 大きな数字(縁取り付き) + 小さなラベル(既定「COMBO」。空なら出さない)
 * ・コンボ数が増えた瞬間に、全体を大きくしてから元の大きさに戻す(パンチ)。同時に、shakeTime秒だけガタッと揺らす(徐々に収まる)
 * ・数字の大きさと揺れの強さは、呼び出し側が渡す倍率(コンボ数に応じて大きくする)。大きさは急に変わらないよう、なめらかに近づける
 * ・確定(Confirm): 連続奇襲が終わってコンボ数が確定した時に、数字をconfirmColorにして大きく弾ませ、強く揺らし、
 *   confirmHoldTime秒見せてからフェードで消す(確定の演出中に次の連続奇襲が始まったら、すぐ普段の表示に戻す)
 * ・フォントはsettings.font(空ならTextMeshProの既定のフォント)。italicがオンなら斜体にする
 * ・出さなくなったら、fadeTime秒かけて消す(消えている途中でまた増えたら、すぐ出し直す)
 * ・置き場所: 独自のCanvas(他のUIの上に出す)。位置は画面の割合で指定する
 * ・文字のフォントはTextMeshProの既定のフォント(TMP Settings)
 * ・時間はTime.timeScaleの影響を受けない時間で数える(連続奇襲のスロー中も普通に動く)
 * ・設定は CS_PlayerAmbushEffect の「コンボ数の表示」で変える
 */
// ========================================

public class CS_PlayerAmbushComboHud
{
    public struct Settings
    {
        public Vector2 anchor;          // 画面の中の位置(割合。左下が(0,0)、右上が(1,1))
        public float numberFontSize;    // 大きな数字の大きさ
        public Color numberColor;       // 大きな数字の色(透けさせたい時はアルファを下げる)
        public Color numberOutlineColor;
        public float numberOutlineWidth;    // 大きな数字の縁取りの太さ(0〜1)
        public string label;            // 小さなラベル(空なら出さない)
        public float labelFontSize;
        public Color labelColor;
        public TMP_FontAsset font;      // 文字のフォント(空ならTextMeshProの既定のフォント)
        public bool italic;             // 斜体にするか
        public float shakeAmplitude;    // コンボ数が増えた時に揺らす幅(ピクセル)
        public float shakeTime;         // 揺らす時間(秒)
        public float punchScale;        // コンボ数が増えた時に大きくする倍率
        public float punchTime;         // 元の大きさに戻るまでの時間(秒)
        public float fadeTime;          // 消えるまでの時間(秒)
        public Color confirmColor;      // 確定した時の数字の色
        public float confirmPunchScale; // 確定した時に大きくする倍率
        public float confirmPunchTime;  // 確定した時に、元の大きさに戻るまでの時間(秒)
        public float confirmShakeAmplitude; // 確定した時に揺らす幅(ピクセル)
        public float confirmHoldTime;   // 確定してから、消え始めるまでの時間(秒)
        public int sortingOrder;        // Canvasの表示順(大きいほど手前)
    }

    private readonly GameObject _root;
    private readonly Canvas _canvas;
    private readonly RectTransform _group;
    private readonly CanvasGroup _canvasGroup;
    private readonly TextMeshProUGUI _number;
    private readonly TextMeshProUGUI _label;
    private Settings _settings;
    private int _shownCount = -1;
    private float _punchStartTime = float.NegativeInfinity;
    private bool _isShowing;
    private float _targetSizeScale = 1f;    // コンボ数に応じた数字の大きさの倍率(目標)
    private float _sizeScale = 1f;          // 今の数字の大きさの倍率
    private float _shakeScale = 1f;         // コンボ数に応じた揺れの倍率(増えた時のもの)
    private bool _isConfirming;             // 確定の演出中か
    private float _confirmStartTime = float.NegativeInfinity;

    public CS_PlayerAmbushComboHud(Settings settings)
    {
        _root = new GameObject("AmbushComboHud");
        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _group = new GameObject("Group", typeof(RectTransform)).GetComponent<RectTransform>();
        _group.SetParent(_root.transform, false);
        _group.sizeDelta = Vector2.zero;
        _canvasGroup = _group.gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        _number = CreateText("Number", _group);
        _number.fontStyle = FontStyles.Bold;
        _label = CreateText("Label", _group);
        _label.fontStyle = FontStyles.Bold;

        Apply(settings);
        _canvasGroup.alpha = 0f;
        _root.SetActive(false);
    }

    private static TextMeshProUGUI CreateText(string name, RectTransform parent)
    {
        TextMeshProUGUI text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.rectTransform.sizeDelta = new Vector2(1400f, 700f);
        return text;
    }

    // 設定を反映する(Inspectorで変えた値を、再生中にも反映できるよう毎回呼んでよい)
    public void Apply(Settings settings)
    {
        _settings = settings;
        _canvas.sortingOrder = settings.sortingOrder;
        _group.anchorMin = settings.anchor;
        _group.anchorMax = settings.anchor;

        // フォント(変えたら縁取りを入れ直す。縁取りはフォントのマテリアルに入るため)
        TMP_FontAsset font = settings.font != null ? settings.font : TMP_Settings.defaultFontAsset;
        if (font != null && _number.font != font)
        {
            _number.font = font;
            _label.font = font;
            _number.outlineWidth = settings.numberOutlineWidth;
            _number.outlineColor = settings.numberOutlineColor;
        }
        FontStyles style = settings.italic ? FontStyles.Bold | FontStyles.Italic : FontStyles.Bold;
        if (_number.fontStyle != style) _number.fontStyle = style;
        if (_label.fontStyle != style) _label.fontStyle = style;

        if (!Mathf.Approximately(_number.fontSize, settings.numberFontSize)) _number.fontSize = settings.numberFontSize;
        if (_number.outlineColor != settings.numberOutlineColor) _number.outlineColor = settings.numberOutlineColor;
        if (!Mathf.Approximately(_number.outlineWidth, settings.numberOutlineWidth)) _number.outlineWidth = settings.numberOutlineWidth;   // 縁取りは、この文字だけのマテリアルに入る

        if (_label.text != settings.label) _label.text = settings.label;
        _label.gameObject.SetActive(!string.IsNullOrEmpty(settings.label));
        if (!Mathf.Approximately(_label.fontSize, settings.labelFontSize)) _label.fontSize = settings.labelFontSize;
        _label.color = settings.labelColor;
        _label.rectTransform.anchoredPosition = new Vector2(0f, -settings.numberFontSize * 0.55f);
    }

    // コンボ数を出す(増えた時は弾ませて揺らす)
    // sizeScale: 数字の大きさの倍率 / shakeScale: 揺れの強さの倍率(どちらもコンボ数に応じて呼び出し側が決める)
    public void Show(int count, float sizeScale = 1f, float shakeScale = 1f)
    {
        _isConfirming = false;   // 確定の演出中に次の連続奇襲が始まったら、普段の表示に戻す
        _shakeScale = shakeScale;
        if (!_root.activeSelf)
        {
            _root.SetActive(true);
            _sizeScale = sizeScale;   // 出し始めは、いきなりその大きさで出す
        }
        _isShowing = true;
        _targetSizeScale = sizeScale;

        if (count == _shownCount) return;
        if (count > _shownCount) _punchStartTime = Time.unscaledTime;
        _shownCount = count;
        _number.text = count.ToString();
    }

    // 出さなくする(fadeTime秒かけて消える)
    public void Hide()
    {
        _isShowing = false;
    }

    // コンボ数が確定した(連続奇襲が終わった)。強調してから、confirmHoldTime秒後に消え始める
    public void Confirm()
    {
        if (!_isShowing) return;

        _isShowing = false;
        _isConfirming = true;
        _confirmStartTime = Time.unscaledTime;
    }

    // 毎フレーム呼ぶ(弾みと、出し入れのフェード)
    public void Tick()
    {
        if (!_root.activeSelf) return;

        float now = Time.unscaledTime;
        float confirmElapsed = now - _confirmStartTime;
        bool isHolding = _isConfirming && confirmElapsed < _settings.confirmHoldTime;

        float fade = _settings.fadeTime > 0f ? Time.unscaledDeltaTime / _settings.fadeTime : 1f;
        _canvasGroup.alpha = _isShowing || isHolding ? 1f : Mathf.MoveTowards(_canvasGroup.alpha, 0f, fade);

        // 確定したら強調の色にする
        _number.color = _isConfirming ? _settings.confirmColor : _settings.numberColor;

        float t = _settings.punchTime > 0f ? Mathf.Clamp01((now - _punchStartTime) / _settings.punchTime) : 1f;
        float scale = Mathf.Lerp(_settings.punchScale, 1f, 1f - (1f - t) * (1f - t));
        if (_isConfirming)
        {
            // 確定の弾み(普段の弾みより大きく)
            float c = _settings.confirmPunchTime > 0f ? Mathf.Clamp01(confirmElapsed / _settings.confirmPunchTime) : 1f;
            scale *= Mathf.Lerp(_settings.confirmPunchScale, 1f, 1f - (1f - c) * (1f - c));
        }

        // コンボ数に応じた大きさへ、なめらかに近づける
        _sizeScale = Mathf.Lerp(_sizeScale, _targetSizeScale, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        _group.localScale = Vector3.one * (scale * _sizeScale);

        // 増えた瞬間(と確定した瞬間)に、ガタッと揺らす(徐々に収まる)
        Vector2 offset = Vector2.zero;
        float shake = _settings.shakeTime > 0f ? (now - _punchStartTime) / _settings.shakeTime : 1f;
        if (shake < 1f) offset += Random.insideUnitCircle * (_settings.shakeAmplitude * _shakeScale * (1f - shake));
        float confirmShake = _settings.shakeTime > 0f ? confirmElapsed / _settings.shakeTime : 1f;
        if (_isConfirming && confirmShake < 1f) offset += Random.insideUnitCircle * (_settings.confirmShakeAmplitude * (1f - confirmShake));
        _group.anchoredPosition = offset;

        if (!_isShowing && !isHolding && _canvasGroup.alpha <= 0f)
        {
            _root.SetActive(false);
            _shownCount = -1;
            _isConfirming = false;
        }
    }

    public void Dispose()
    {
        if (_root != null) Object.Destroy(_root);
    }
}
