/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-26 | 初回作成
 * ================================================ */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 必殺技ゲージのUI表示。
///
/// ・円形ゲージの中央にプレイヤーアイコンを表示する
/// ・中央アイコンの色はゲージ量によって変更しない
/// ・ゲージが満タンになるとキーガイドを表示する
/// ・表示中のキーガイドは拡大・縮小を繰り返す
/// ・満タンではなくなるとキーガイドを非表示にする
/// </summary>
public class CS_UISpecialGaugeView :
    CS_BaseView<CS_UISpecialGaugePresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ゲージ画像（円形）")]
    [SerializeField]
    private Image _gauge;

    [Header("中央アイコン")]
    [SerializeField]
    private Image _iconImage;

    [Header("アイコンデータ（ScriptableObject）")]
    [SerializeField]
    private CSO_PlayerIconData _playerIconData;

    [Header("キーガイド")]

    [Tooltip(
        "ゲージ満タン時に表示するキーガイド。" +
        "拡縮させるオブジェクトのRectTransformを設定する"
    )]
    [SerializeField]
    private RectTransform _keyGuideRoot;

    [Tooltip("キーガイドの最小倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float _keyGuideMinScale = 0.9f;

    [Tooltip("キーガイドの最大倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float _keyGuideMaxScale = 1.1f;

    [Tooltip("最小から最大へ変化する時間")]
    [Min(0.01f)]
    [SerializeField]
    private float _keyGuideScaleDuration = 0.5f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private Coroutine _keyGuideAnimationCoroutine;

    private bool _isGaugeFull;

    private Vector3 _keyGuideInitialScale =
        Vector3.one;

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(
        CS_UISpecialGaugePresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        if (_keyGuideRoot != null)
        {
            _keyGuideInitialScale =
                _keyGuideRoot.localScale;

            // 初期状態は必ず非表示。
            _keyGuideRoot.gameObject.SetActive(false);
        }

        _isGaugeFull = false;
    }

    /// <summary>
    /// プレイヤー番号に対応したアイコンを設定する。
    /// </summary>
    public void SetupIcon(int playerNumber)
    {
        if (_iconImage == null ||
            _playerIconData == null)
        {
            return;
        }

        Sprite icon =
            _playerIconData.GetIcon(playerNumber);

        if (icon != null)
        {
            _iconImage.sprite = icon;
        }

        // ゲージ量による色変更は行わない。
        _iconImage.color = Color.white;
    }

    // =========================================================
    // ゲージ描画
    // =========================================================

    /// <summary>
    /// ゲージ表示を更新する。
    /// ゲージが満タンならキーガイドを表示する。
    /// </summary>
    public void UpdateGauge(
        float current,
        float max)
    {
        float fillAmount =
            max > 0f
                ? Mathf.Clamp01(current / max)
                : 0f;

        if (_gauge != null)
        {
            _gauge.fillAmount = fillAmount;
        }

        bool isFull =
            max > 0f &&
            current >= max;

        SetGaugeFull(isFull);
    }

    // =========================================================
    // 満タン状態
    // =========================================================

    /// <summary>
    /// ゲージ満タン状態に応じてキーガイドを切り替える。
    /// </summary>
    private void SetGaugeFull(bool isFull)
    {
        // 状態が変化していなければ何もしない。
        if (_isGaugeFull == isFull)
        {
            return;
        }

        _isGaugeFull = isFull;

        if (_isGaugeFull)
        {
            ShowKeyGuide();
        }
        else
        {
            HideKeyGuide();
        }
    }

    // =========================================================
    // キーガイド表示
    // =========================================================

    /// <summary>
    /// キーガイドを表示し、拡縮アニメーションを開始する。
    /// </summary>
    private void ShowKeyGuide()
    {
        if (_keyGuideRoot == null)
        {
            return;
        }

        StopKeyGuideAnimation();

        _keyGuideRoot.gameObject.SetActive(true);

        _keyGuideRoot.localScale =
            GetKeyGuideScale(_keyGuideMinScale);

        _keyGuideAnimationCoroutine =
            StartCoroutine(
                PlayKeyGuideAnimation()
            );
    }

    /// <summary>
    /// キーガイドを非表示にし、拡縮を停止する。
    /// </summary>
    private void HideKeyGuide()
    {
        StopKeyGuideAnimation();

        if (_keyGuideRoot == null)
        {
            return;
        }

        _keyGuideRoot.localScale =
            _keyGuideInitialScale;

        _keyGuideRoot.gameObject.SetActive(false);
    }

    // =========================================================
    // キーガイドアニメーション
    // =========================================================

    /// <summary>
    /// キーガイドを最小倍率と最大倍率の間で
    /// 繰り返し拡大・縮小する。
    /// </summary>
    private IEnumerator PlayKeyGuideAnimation()
    {
        while (_isGaugeFull)
        {
            // 小さい状態から大きい状態へ変化。
            yield return ScaleKeyGuide(
                fromScale: _keyGuideMinScale,
                toScale: _keyGuideMaxScale
            );

            if (!_isGaugeFull)
            {
                break;
            }

            // 大きい状態から小さい状態へ変化。
            yield return ScaleKeyGuide(
                fromScale: _keyGuideMaxScale,
                toScale: _keyGuideMinScale
            );
        }

        _keyGuideAnimationCoroutine = null;
    }

    /// <summary>
    /// 指定倍率の間を補間する。
    /// </summary>
    private IEnumerator ScaleKeyGuide(
        float fromScale,
        float toScale)
    {
        if (_keyGuideRoot == null)
        {
            yield break;
        }

        float elapsed = 0f;

        Vector3 from =
            GetKeyGuideScale(fromScale);

        Vector3 to =
            GetKeyGuideScale(toScale);

        while (elapsed < _keyGuideScaleDuration)
        {
            if (_keyGuideRoot == null ||
                !_isGaugeFull)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;

            float rate =
                Mathf.Clamp01(
                    elapsed /
                    _keyGuideScaleDuration
                );

            // 最初と最後を少し滑らかにする。
            float easedRate =
                rate *
                rate *
                (3f - 2f * rate);

            _keyGuideRoot.localScale =
                Vector3.LerpUnclamped(
                    from,
                    to,
                    easedRate
                );

            yield return null;
        }

        if (_keyGuideRoot != null &&
            _isGaugeFull)
        {
            _keyGuideRoot.localScale = to;
        }
    }

    /// <summary>
    /// 初期スケールを基準にした倍率を取得する。
    /// </summary>
    private Vector3 GetKeyGuideScale(
        float scaleMultiplier)
    {
        return new Vector3(
            _keyGuideInitialScale.x *
            scaleMultiplier,

            _keyGuideInitialScale.y *
            scaleMultiplier,

            _keyGuideInitialScale.z
        );
    }

    /// <summary>
    /// 再生中のキーガイドアニメーションを停止する。
    /// </summary>
    private void StopKeyGuideAnimation()
    {
        if (_keyGuideAnimationCoroutine == null)
        {
            return;
        }

        StopCoroutine(
            _keyGuideAnimationCoroutine
        );

        _keyGuideAnimationCoroutine = null;
    }

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnDisable()
    {
        StopKeyGuideAnimation();

        if (_keyGuideRoot != null)
        {
            _keyGuideRoot.localScale =
                _keyGuideInitialScale;

            _keyGuideRoot.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // 無効化前に満タンだった場合は、
        // 再び有効になったときに表示を再開する。
        if (_isGaugeFull)
        {
            ShowKeyGuide();
        }
    }

    private void OnDestroy()
    {
        StopKeyGuideAnimation();
    }

    private void OnValidate()
    {
        _keyGuideMinScale =
            Mathf.Max(
                0.01f,
                _keyGuideMinScale
            );

        _keyGuideMaxScale =
            Mathf.Max(
                _keyGuideMinScale,
                _keyGuideMaxScale
            );

        _keyGuideScaleDuration =
            Mathf.Max(
                0.01f,
                _keyGuideScaleDuration
            );
    }
}