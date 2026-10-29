using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * 世界観「治安維持AI AEGISの定期メンテナンス中の5分間」を、画面の表示で見せるクラス【仮の演出】
 * ・画面の左に、AEGISのシステム表示(メンテナンス中・犯罪件数・刑期短縮)を出す
 * ・開始時・終了時・犯罪が完遂された時に、画面の中央に帯の演出を出す
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・世界観(Confluence「プレイヤー」「ゲームループ」「フィールド」、10/05のMTGの原稿より)
 *   犯罪件数0を保ってきた治安維持AI「AEGIS」が、月に一度のメンテナンスで止まる5分間だけ、
 *   政府に拘束されたダークヒーローの拘束が解け、変形して悪人を裁く(倒すほど刑期が短くなる)
 *   「犯罪件数0の平和が揺らぐとき…」(ゲーム名: ORDER:ZERO)
 * ・表示するもの
 *   左のシステム表示: 「AEGIS / 治安維持AI 定期メンテナンス中」「犯罪件数」「刑期短縮(自分のスコア = 日数)」
 *     犯罪件数は、自分のプレイヤーのスコアの「犯罪を完遂された数」(全員に同じ数が入る)。増えた時は赤く震える
 *   開始の帯: ゲーム開始から_openingDuration秒。「メンテナンス開始 → 拘束解除・変形承認 → 正体を隠して悪を裁け」
 *   終了の帯: 残り時間が_endingLead秒を切ったら。「メンテナンス終了 → AEGIS再起動・拘束を再開」
 *   犯罪の帯: 犯罪件数が増えた時に_alertDuration秒。最初の1件だけ「犯罪件数0の平和が揺らいでいる」と出す
 * ・時間はCS_TimerController(同じオブジェクト、またはシーン内)から読む。スコアはCS_Player.localPlayerから読む
 * ・自分の画面だけの表示で、通信はしない。オフラインのテストシーンでも動く
 * ・文字はNoto Sans JP(Font)。この中に無い文字は表示されないので、文言を変える時は文字が入っているか確かめる
 * ・仮の演出。本番のUIができたら、GameControllerManager.prefabからこのコンポーネントを外す
 */
// ========================================

public class CS_AegisHud : MonoBehaviour
{
    [Header("見た目")]
    [SerializeField] private TMP_FontAsset _font;                                         // 日本語の入ったフォント(空ならTMPの既定)
    [SerializeField] private Color _systemColor = new Color(0.35f, 0.9f, 1f, 0.95f);     // AEGISの色(水色)
    [SerializeField] private Color _maintenanceColor = new Color(1f, 0.65f, 0.15f, 1f);  // メンテナンス中の色(オレンジ)
    [SerializeField] private Color _alertColor = new Color(1f, 0.2f, 0.25f, 1f);         // 犯罪の警告の色(赤)
    [SerializeField] private Vector2 _panelPosition = new Vector2(40f, -300f);           // 左のシステム表示の位置(画面の左上からのピクセル。1920×1080基準)
    [SerializeField] private int _sortingOrder = 40;                                     // 他のUIより手前に出す順番

    [Header("演出の時間(秒)")]
    [SerializeField][Min(0f)] private float _openingDuration = 4.5f;
    [SerializeField][Min(0f)] private float _endingLead = 2.5f;
    [SerializeField][Min(0f)] private float _alertDuration = 2.2f;
    [SerializeField][Min(1f)] private float _typeSpeed = 40f;     // 帯の文字が出る速さ(文字/秒)

    private static readonly string[] _openingTitles = { "AEGIS 定期メンテナンス開始", "拘束 解除 ◆ 変形 承認", "正体を隠し、悪を裁け" };
    private static readonly string[] _openingSubs = { "治安維持AI 一時停止", "残り時間 5分", "犯罪件数0の平和を、守り抜け" };

    private CS_TimerController _timer;
    private RectTransform _root;
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _crimeText;
    private TextMeshProUGUI _sentenceText;
    private RectTransform _crimeRect;
    private Image _band;
    private TextMeshProUGUI _bandTitle;
    private TextMeshProUGUI _bandSub;

    private int _lastCrimeCount;
    private float _alertEndTime = -1f;
    private bool _hasShownFirstCrime;
    private bool _isFirstAlert;          // 今出している犯罪の帯が、最初の1件のものか
    private string _currentBandTitle;
    private float _bandStartTime;

    private void Start()
    {
        _timer = GetComponent<CS_TimerController>();
        if (_timer == null) _timer = FindAnyObjectByType<CS_TimerController>();

        BuildCanvas();
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
    }

    private void Update()
    {
        if (_root == null) return;

        CS_ResultData.Score score = GetLocalScore(out bool hasPlayer);
        _root.gameObject.SetActive(hasPlayer);
        if (!hasPlayer) return;

        UpdatePanel(score);
        UpdateBand(score.crimeCompletedCount);
    }

    // ========================================
    // 左のシステム表示
    // ========================================

    private void UpdatePanel(CS_ResultData.Score score)
    {
        bool isBlinkOn = Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
        string mark = isBlinkOn ? "●" : "○";
        _statusText.text = $"<size=46><b>AEGIS</b></size>\n<color=#{ColorUtility.ToHtmlStringRGB(_maintenanceColor)}>{mark} 定期メンテナンス中</color>　治安維持AI 停止";

        int crimes = score.crimeCompletedCount;
        bool isAlerting = Time.unscaledTime < _alertEndTime;
        _crimeText.color = crimes > 0 ? _alertColor : _systemColor;
        _crimeText.text = $"犯罪件数　{crimes:000} 件";
        _crimeRect.anchoredPosition = isAlerting ? Random.insideUnitCircle * 4f : Vector2.zero;   // 増えた直後は震える

        int total = score.defeatVillainScore + score.foundByPoliceScore + score.crimeCompletedScore + score.defeatPlayerScore;
        _sentenceText.text = $"刑期短縮　{Mathf.Max(0, total)} 日";
    }

    // ========================================
    // 中央の帯(開始・犯罪・終了)
    // ========================================

    private void UpdateBand(int crimeCount)
    {
        if (crimeCount > _lastCrimeCount)
        {
            _alertEndTime = Time.unscaledTime + _alertDuration;
            _isFirstAlert = !_hasShownFirstCrime;
            _hasShownFirstCrime = true;
        }
        _lastCrimeCount = crimeCount;

        float elapsed = _timer != null ? _timer.GetElapsedTime() : Time.timeSinceLevelLoad;
        float remain = _timer != null ? _timer.GetRemainTime() : float.MaxValue;

        if (_timer != null && remain <= _endingLead)
        {
            ShowBand("メンテナンス終了 ◆ AEGIS 再起動", "拘束を再開します", _systemColor);
        }
        else if (Time.unscaledTime < _alertEndTime)
        {
            string sub = _isFirstAlert ? $"犯罪件数 {crimeCount} ◆ 犯罪件数0の平和が揺らいでいる" : $"犯罪件数 {crimeCount}";
            ShowBand("警告：犯罪の発生を確認", sub, _alertColor);
        }
        else if (elapsed < _openingDuration)
        {
            int index = Mathf.Clamp(Mathf.FloorToInt(elapsed / (_openingDuration / _openingTitles.Length)), 0, _openingTitles.Length - 1);
            string sub = index == 1 && _timer != null ? $"残り時間 {Mathf.CeilToInt((elapsed + remain) / 60f)}分" : _openingSubs[index];
            ShowBand(_openingTitles[index], sub, index == 0 ? _maintenanceColor : _systemColor);
        }
        else
        {
            HideBand();
        }
    }

    private void ShowBand(string title, string sub, Color color)
    {
        if (title != _currentBandTitle)
        {
            _currentBandTitle = title;
            _bandStartTime = Time.unscaledTime;
        }

        _band.gameObject.SetActive(true);
        _bandTitle.text = title;
        _bandSub.text = sub;
        _bandTitle.color = color;

        // 文字を1文字ずつ出す(タイプライター)。出始めは帯を少し光らせる
        int visible = Mathf.FloorToInt((Time.unscaledTime - _bandStartTime) * _typeSpeed);
        _bandTitle.maxVisibleCharacters = visible;
        _bandSub.maxVisibleCharacters = Mathf.Max(0, visible - title.Length);

        float flash = Mathf.Clamp01(1f - (Time.unscaledTime - _bandStartTime) * 4f);
        _band.color = Color.Lerp(new Color(0f, 0.02f, 0.05f, 0.7f), new Color(color.r, color.g, color.b, 0.5f), flash);
    }

    private void HideBand()
    {
        _currentBandTitle = null;
        _band.gameObject.SetActive(false);
    }

    // ========================================
    // 作る
    // ========================================

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("AegisHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = canvasObject.GetComponent<RectTransform>();

        // 左のシステム表示
        RectTransform panel = CreateRect("Panel", _root, new Vector2(0f, 1f), new Vector2(0f, 1f), _panelPosition, new Vector2(520f, 200f), new Vector2(0f, 1f));
        Image line = CreateRect("Line", panel, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(4f, 0f), new Vector2(0f, 0.5f)).gameObject.AddComponent<Image>();
        line.color = _systemColor;
        line.raycastTarget = false;

        _statusText = CreateText("Status", panel, new Vector2(18f, 0f), new Vector2(500f, 100f), 24f, _systemColor, TextAlignmentOptions.TopLeft);
        _crimeText = CreateText("Crime", CreateRect("CrimeRoot", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -110f), new Vector2(500f, 40f), new Vector2(0f, 1f)), Vector2.zero, new Vector2(500f, 40f), 30f, _systemColor, TextAlignmentOptions.TopLeft);
        _crimeRect = _crimeText.rectTransform;
        _sentenceText = CreateText("Sentence", panel, new Vector2(18f, -150f), new Vector2(500f, 40f), 26f, _systemColor, TextAlignmentOptions.TopLeft);

        // 中央の帯
        RectTransform bandRect = CreateRect("Band", _root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 140f), new Vector2(0f, 170f), new Vector2(0.5f, 0.5f));
        _band = bandRect.gameObject.AddComponent<Image>();
        _band.raycastTarget = false;
        _bandTitle = CreateText("Title", bandRect, new Vector2(0f, 26f), new Vector2(1600f, 80f), 60f, _systemColor, TextAlignmentOptions.Center, true);
        _bandSub = CreateText("Sub", bandRect, new Vector2(0f, -46f), new Vector2(1600f, 50f), 30f, Color.white, TextAlignmentOptions.Center, true);
        _band.gameObject.SetActive(false);
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private TextMeshProUGUI CreateText(string name, RectTransform parent, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment, bool isCentered = false)
    {
        Vector2 anchor = isCentered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        RectTransform rect = CreateRect(name, parent, anchor, anchor, position, size, anchor);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    // 自分のプレイヤーのスコア(自分のプレイヤーがいなければhasPlayer = false)
    private static CS_ResultData.Score GetLocalScore(out bool hasPlayer)
    {
        CS_Player player = CS_Player.localPlayer;
        CS_PlayerResultDataHolder holder = player != null ? player.GetComponent<CS_PlayerResultDataHolder>() : null;
        hasPlayer = holder != null;
        return hasPlayer ? holder.score : default;
    }
}
