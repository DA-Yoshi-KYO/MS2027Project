/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-10 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通知スロット1つ分の表示を担当するView。
///
/// ・通知タイプに応じて5種類の画像を切り替える
/// ・通知メッセージを表示する
/// ・CanvasGroupを使用してフェードアウトする
/// </summary>
public class CS_UINotificationView : MonoBehaviour
{
    // =========================================================
    // 通知タイプごとの画像設定
    // =========================================================

    /// <summary>
    /// 通知タイプ1種類分の画像設定。
    /// </summary>
    [System.Serializable]
    private sealed class NotificationVisualSet
    {
        [SerializeField]
        private CSE_NotificationType _notificationType;

        [SerializeField]
        private Sprite _backgroundSprite;

        [SerializeField]
        private Sprite _backSprite;

        [SerializeField]
        private Sprite _iconSprite;

        [SerializeField]
        private Sprite _barUpSprite;

        [SerializeField]
        private Sprite _barDownSprite;

        public CSE_NotificationType notificationType =>
            _notificationType;

        public Sprite backgroundSprite =>
            _backgroundSprite;

        public Sprite backSprite =>
            _backSprite;

        public Sprite iconSprite =>
            _iconSprite;

        public Sprite barUpSprite =>
            _barUpSprite;

        public Sprite barDownSprite =>
            _barDownSprite;
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("UIパーツ")]

    [Tooltip("通知の背景Image")]
    [SerializeField]
    private Image _backgroundImage;

    [Tooltip("通知のBack用Image")]
    [SerializeField]
    private Image _backImage;

    [Tooltip("通知タイプを表すIcon用Image")]
    [SerializeField]
    private Image _iconImage;

    [Tooltip("通知上部のBar用Image")]
    [SerializeField]
    private Image _barUpImage;

    [Tooltip("通知下部のBar用Image")]
    [SerializeField]
    private Image _barDownImage;

    [Tooltip("通知メッセージ")]
    [SerializeField]
    private TextMeshProUGUI _text;

    [Header("通知タイプごとの画像設定")]

    [SerializeField]
    private NotificationVisualSet[] _visualSets;

    [Header("フェードアウト設定")]

    [Tooltip("通知が消える直前のフェード時間")]
    [Min(0.01f)]
    [SerializeField]
    private float _fadeOutDuration = 1f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CanvasGroup _canvasGroup;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _canvasGroup =
            GetComponent<CanvasGroup>();

        if (_canvasGroup == null)
        {
            _canvasGroup =
                gameObject.AddComponent<CanvasGroup>();
        }

        // 通知UIは入力判定を受け取らない。
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        // 初期状態は非表示。
        SetVisible(false);
    }

    // =========================================================
    // 通知表示
    // =========================================================

    /// <summary>
    /// 通知を表示する。
    /// 通知タイプに応じて5種類の画像を切り替える。
    /// </summary>
    public void Show(CS_NotificationData data)
    {
        if (data == null)
        {
            Hide();
            return;
        }

        ApplyVisual(data.type);
        ApplyMessage(data.message);

        SetVisible(true);
    }

    /// <summary>
    /// 表示中の通知内容を更新する。
    /// キル通知の結合やカウントダウン更新時に使用する。
    /// </summary>
    public void UpdateMessage(
        CS_NotificationData data)
    {
        if (data == null)
        {
            return;
        }

        ApplyVisual(data.type);
        ApplyMessage(data.message);

        // 更新時はフェードをリセットする。
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
    }

    /// <summary>
    /// 通知を非表示にする。
    /// </summary>
    public void Hide()
    {
        SetVisible(false);
    }

    // =========================================================
    // 画像切り替え
    // =========================================================

    /// <summary>
    /// 通知タイプに対応する5種類の画像を適用する。
    /// </summary>
    private void ApplyVisual(
        CSE_NotificationType notificationType)
    {
        NotificationVisualSet visualSet =
            FindVisualSet(notificationType);

        if (visualSet == null)
        {
            Debug.LogWarning(
                $"{nameof(CS_UINotificationView)}: " +
                $"通知画像の設定がありません: {notificationType}",
                this
            );

            // 前回の通知画像が残らないようにする。
            ClearVisuals();

            return;
        }

        SetImageSprite(
            _backgroundImage,
            visualSet.backgroundSprite
        );

        SetImageSprite(
            _backImage,
            visualSet.backSprite
        );

        SetImageSprite(
            _iconImage,
            visualSet.iconSprite
        );

        SetImageSprite(
            _barUpImage,
            visualSet.barUpSprite
        );

        SetImageSprite(
            _barDownImage,
            visualSet.barDownSprite
        );
    }

    /// <summary>
    /// 通知タイプに対応する画像設定を探す。
    /// </summary>
    private NotificationVisualSet FindVisualSet(
        CSE_NotificationType notificationType)
    {
        if (_visualSets == null)
        {
            return null;
        }

        for (int i = 0; i < _visualSets.Length; i++)
        {
            NotificationVisualSet visualSet =
                _visualSets[i];

            if (visualSet == null)
            {
                continue;
            }

            if (visualSet.notificationType ==
                notificationType)
            {
                return visualSet;
            }
        }

        return null;
    }

    /// <summary>
    /// ImageへSpriteを設定する。
    /// Spriteが未設定なら、そのImageを非表示にする。
    /// </summary>
    private static void SetImageSprite(
        Image image,
        Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    /// <summary>
    /// 5種類の画像をすべてクリアする。
    /// </summary>
    private void ClearVisuals()
    {
        SetImageSprite(
            _backgroundImage,
            null
        );

        SetImageSprite(
            _backImage,
            null
        );

        SetImageSprite(
            _iconImage,
            null
        );

        SetImageSprite(
            _barUpImage,
            null
        );

        SetImageSprite(
            _barDownImage,
            null
        );
    }

    // =========================================================
    // テキスト
    // =========================================================

    /// <summary>
    /// 通知メッセージを設定する。
    /// </summary>
    private void ApplyMessage(string message)
    {
        if (_text == null)
        {
            return;
        }

        _text.text =
            string.IsNullOrEmpty(message)
                ? string.Empty
                : message;
    }

    // =========================================================
    // フェードアウト
    // =========================================================

    /// <summary>
    /// 残り表示時間から透明度を更新する。
    /// Presenterから毎フレーム呼ばれる。
    /// </summary>
    public void UpdateFade(float remainTime)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        if (remainTime <= 0f)
        {
            SetVisible(false);
            return;
        }

        if (remainTime < _fadeOutDuration)
        {
            _canvasGroup.alpha =
                Mathf.Clamp01(
                    remainTime /
                    _fadeOutDuration
                );
        }
        else
        {
            _canvasGroup.alpha = 1f;
        }
    }

    // =========================================================
    // 表示・非表示
    // =========================================================

    /// <summary>
    /// 通知スロットの表示状態を切り替える。
    /// </summary>
    private void SetVisible(bool visible)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha =
            visible ? 1f : 0f;

        // 表示中でも通知UIは入力を遮らない。
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    // =========================================================
    // Inspector値の補正
    // =========================================================

    private void OnValidate()
    {
        _fadeOutDuration =
            Mathf.Max(
                0.01f,
                _fadeOutDuration
            );
    }
}