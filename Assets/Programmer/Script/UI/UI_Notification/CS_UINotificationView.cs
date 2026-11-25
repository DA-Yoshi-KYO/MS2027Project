/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-10 | 初回作成
 * ================================================ */

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通知スロット1つ分の表示を担当するView。
///
/// ・通知タイプに応じてBackImageを切り替える
/// ・BackImageを中央から横方向へ展開する
/// ・展開後、文字を120%から100%へ拡縮する
/// ・表示終了時はCanvasGroupでフェードアウトする
/// </summary>
public sealed class CS_UINotificationView :
    MonoBehaviour
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
        private Sprite _backSprite;

        public CSE_NotificationType notificationType =>
            _notificationType;

        public Sprite backSprite =>
            _backSprite;
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("UIパーツ")]

    [Tooltip("通知タイプごとに切り替える統合背景画像")]
    [SerializeField]
    private Image _backImage;

    [Tooltip("通知メッセージ")]
    [SerializeField]
    private TextMeshProUGUI _text;

    [Header("通知タイプごとの画像設定")]

    [SerializeField]
    private NotificationVisualSet[] _visualSets;

    [Header("表示アニメーション")]

    [Tooltip(
        "中央から横へ展開するBackImageのRectTransform"
    )]
    [SerializeField]
    private RectTransform _backAnimationRoot;

    [Tooltip(
        "120%から100%へ拡縮する文字のRectTransform"
    )]
    [SerializeField]
    private RectTransform _textAnimationRoot;

    [Tooltip("BackImageが展開する時間")]
    [Min(0.01f)]
    [SerializeField]
    private float _expandDuration = 0.25f;

    [Tooltip("文字が120%から100%へ戻る時間")]
    [Min(0.01f)]
    [SerializeField]
    private float _textScaleDuration = 0.18f;

    [Tooltip("文字表示開始時の倍率")]
    [Min(1f)]
    [SerializeField]
    private float _textStartScale = 1.2f;

    [Header("フェードアウト設定")]

    [Tooltip("通知が消える直前のフェード時間")]
    [Min(0.01f)]
    [SerializeField]
    private float _fadeOutDuration = 1f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CanvasGroup _canvasGroup;

    private Coroutine _showAnimationCoroutine;

    private Vector3 _backInitialScale =
        Vector3.one;

    private Vector3 _textInitialScale =
        Vector3.one;

    private bool _isVisible;

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

        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        if (_backImage != null)
        {
            _backImage.raycastTarget = false;
        }

        if (_text != null)
        {
            _text.raycastTarget = false;
        }

        // 未設定の場合はBackImage自身を使用する。
        if (_backAnimationRoot == null &&
            _backImage != null)
        {
            _backAnimationRoot =
                _backImage.rectTransform;
        }

        // 未設定の場合はText自身を使用する。
        if (_textAnimationRoot == null &&
            _text != null)
        {
            _textAnimationRoot =
                _text.rectTransform;
        }

        if (_backAnimationRoot != null)
        {
            // 中央から左右へ展開させる。
            _backAnimationRoot.pivot =
                new Vector2(0.5f, 0.5f);

            _backInitialScale =
                _backAnimationRoot.localScale;
        }

        if (_textAnimationRoot != null)
        {
            _textInitialScale =
                _textAnimationRoot.localScale;
        }

        _isVisible = false;

        SetVisible(false);
        ResetAnimationState();
    }

    // =========================================================
    // 通知表示
    // =========================================================

    /// <summary>
    /// 通知を表示し、表示アニメーションを再生する。
    /// </summary>
    public void Show(
        CS_NotificationData data)
    {
        if (data == null)
        {
            Hide();
            return;
        }

        ApplyVisual(
            data // 変更: data.type → data
        );

        ApplyMessage(
            data.message
        );

        StopShowAnimation();

        SetVisible(true);

        _showAnimationCoroutine =
            StartCoroutine(
                PlayShowAnimation()
            );
    }

    /// <summary>
    /// 表示中の通知内容を更新する。
    ///
    /// キル通知の結合やカウントダウン更新用。
    /// 表示アニメーションは再生せず、
    /// 文字と画像を更新してフェードを戻す。
    /// </summary>
    public void UpdateMessage(
        CS_NotificationData data)
    {
        if (data == null)
        {
            return;
        }

        ApplyVisual(
            data // 変更: data.type → data
        );

        ApplyMessage(
            data.message
        );

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }

        if (_text != null)
        {
            _text.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 通知を非表示にする。
    /// </summary>
    public void Hide()
    {
        StopShowAnimation();

        ResetAnimationState();

        SetVisible(false);
    }

    // =========================================================
    // 表示アニメーション
    // =========================================================

    /// <summary>
    /// 通知表示アニメーション。
    ///
    /// 1. BackImageを中央から横へ展開する
    /// 2. 展開後に文字を表示する
    /// 3. 文字を120%から100%へ戻す
    /// </summary>
    private IEnumerator PlayShowAnimation()
    {
        // -----------------------------------------------------
        // 初期状態
        // -----------------------------------------------------

        if (_backAnimationRoot != null)
        {
            _backAnimationRoot.localScale =
                new Vector3(
                    0f,
                    _backInitialScale.y,
                    _backInitialScale.z
                );
        }

        if (_text != null)
        {
            _text.gameObject.SetActive(false);
        }

        if (_textAnimationRoot != null)
        {
            _textAnimationRoot.localScale =
                GetTextScale(
                    _textStartScale
                );
        }

        // -----------------------------------------------------
        // BackImageを中央から展開
        // -----------------------------------------------------

        float elapsed = 0f;

        while (elapsed < _expandDuration)
        {
            if (!_isVisible)
            {
                yield break;
            }

            elapsed +=
                Time.unscaledDeltaTime;

            float rate =
                Mathf.Clamp01(
                    elapsed /
                    _expandDuration
                );

            // EaseInCubic
            // 前半はゆっくり、後半ほど動きを強くする。
            float easedRate =
                rate *
                rate *
                rate;

            if (_backAnimationRoot != null)
            {
                _backAnimationRoot.localScale =
                    new Vector3(
                        _backInitialScale.x *
                        easedRate,

                        _backInitialScale.y,

                        _backInitialScale.z
                    );
            }

            yield return null;
        }

        if (!_isVisible)
        {
            yield break;
        }

        if (_backAnimationRoot != null)
        {
            _backAnimationRoot.localScale =
                _backInitialScale;
        }

        // -----------------------------------------------------
        // 文字を表示
        // -----------------------------------------------------

        if (_text != null)
        {
            _text.gameObject.SetActive(true);
        }

        if (_textAnimationRoot == null)
        {
            _showAnimationCoroutine = null;
            yield break;
        }

        // -----------------------------------------------------
        // 文字を120%から100%へ戻す
        // -----------------------------------------------------

        elapsed = 0f;

        Vector3 startScale =
            GetTextScale(
                _textStartScale
            );

        Vector3 targetScale =
            _textInitialScale;

        while (elapsed < _textScaleDuration)
        {
            if (!_isVisible)
            {
                yield break;
            }

            elapsed +=
                Time.unscaledDeltaTime;

            float rate =
                Mathf.Clamp01(
                    elapsed /
                    _textScaleDuration
                );

            // SmoothStep。
            // 120%から100%へ滑らかに収束させる。
            float easedRate =
                rate *
                rate *
                (3f - 2f * rate);

            _textAnimationRoot.localScale =
                Vector3.LerpUnclamped(
                    startScale,
                    targetScale,
                    easedRate
                );

            yield return null;
        }

        if (_isVisible &&
            _textAnimationRoot != null)
        {
            _textAnimationRoot.localScale =
                targetScale;
        }

        _showAnimationCoroutine = null;
    }

    /// <summary>
    /// 初期Scaleを基準にした文字倍率を取得する。
    /// </summary>
    private Vector3 GetTextScale(
        float scaleMultiplier)
    {
        return new Vector3(
            _textInitialScale.x *
            scaleMultiplier,

            _textInitialScale.y *
            scaleMultiplier,

            _textInitialScale.z
        );
    }

    /// <summary>
    /// 表示アニメーションを停止する。
    /// </summary>
    private void StopShowAnimation()
    {
        if (_showAnimationCoroutine == null)
        {
            return;
        }

        StopCoroutine(
            _showAnimationCoroutine
        );

        _showAnimationCoroutine = null;
    }

    /// <summary>
    /// 背景と文字を通常のScaleへ戻す。
    /// </summary>
    private void ResetAnimationState()
    {
        if (_backAnimationRoot != null)
        {
            _backAnimationRoot.localScale =
                _backInitialScale;
        }

        if (_textAnimationRoot != null)
        {
            _textAnimationRoot.localScale =
                _textInitialScale;
        }
    }

    // =========================================================
    // BackImage切り替え
    // =========================================================

    /// <summary>
    /// 追加: 通知データに対応するBackImageを適用する。
    /// 時間通知で帯画像が指定されていればそれを使い、
    /// 指定がなければ通知タイプごとの画像を使う。
    /// </summary>
    private void ApplyVisual(
        CS_NotificationData data)
    {
        if (data.type == CSE_NotificationType.TimeAlert &&
            data.backSprite != null)
        {
            SetBackSprite(data.backSprite);
            return;
        }

        ApplyVisual(
            data.type
        );
    }

    /// <summary>
    /// 通知タイプに対応するBackImageを適用する。
    /// </summary>
    private void ApplyVisual(
        CSE_NotificationType notificationType)
    {
        NotificationVisualSet visualSet =
            FindVisualSet(
                notificationType
            );

        if (visualSet == null)
        {
            Debug.LogWarning(
                $"{nameof(CS_UINotificationView)}: " +
                "通知画像の設定がありません: " +
                $"{notificationType}",
                this
            );

            SetBackSprite(null);

            return;
        }

        SetBackSprite(
            visualSet.backSprite
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

        for (int i = 0;
             i < _visualSets.Length;
             i++)
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
    /// BackImageへSpriteを設定する。
    /// </summary>
    private void SetBackSprite(
        Sprite sprite)
    {
        if (_backImage == null)
        {
            return;
        }

        _backImage.sprite = sprite;
        _backImage.enabled = sprite != null;
    }

    // =========================================================
    // テキスト
    // =========================================================

    /// <summary>
    /// 通知メッセージを設定する。
    /// </summary>
    private void ApplyMessage(
        string message)
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
    /// 残り時間に応じて通知全体をフェードアウトする。
    /// Presenterから毎フレーム呼ばれる。
    /// </summary>
    public void UpdateFade(
        float remainTime)
    {
        if (_canvasGroup == null ||
            !_isVisible)
        {
            return;
        }

        if (remainTime <= 0f)
        {
            Hide();
            return;
        }

        if (remainTime <
            _fadeOutDuration)
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
    // 表示状態
    // =========================================================

    /// <summary>
    /// 通知スロットの表示状態を変更する。
    /// </summary>
    private void SetVisible(bool visible)
    {
        _isVisible = visible;

        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha =
            visible ? 1f : 0f;

        // 通知UIは入力を受け取らない。
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnDisable()
    {
        StopShowAnimation();
        ResetAnimationState();

        _isVisible = false;
    }

    private void OnDestroy()
    {
        StopShowAnimation();
    }

    // =========================================================
    // Inspector値の補正
    // =========================================================

    private void OnValidate()
    {
        _expandDuration =
            Mathf.Max(
                0.01f,
                _expandDuration
            );

        _textScaleDuration =
            Mathf.Max(
                0.01f,
                _textScaleDuration
            );

        _textStartScale =
            Mathf.Max(
                1f,
                _textStartScale
            );

        _fadeOutDuration =
            Mathf.Max(
                0.01f,
                _fadeOutDuration
            );
    }
}