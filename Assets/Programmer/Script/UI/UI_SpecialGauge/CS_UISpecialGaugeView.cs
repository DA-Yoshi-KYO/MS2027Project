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
/// ・キャラクターごとに中央アイコンとゲージ色を切り替える
/// ・ゲージが満タンになるとキーガイドを表示する
/// ・キーガイドは拡大、縮小を繰り返す
/// ・満タンではなくなるとキーガイドを非表示にする
/// </summary>
public class CS_UISpecialGaugeView :
    CS_BaseView<CS_UISpecialGaugePresenter>
{
    // =========================================================
    // キャラクターごとの表示設定
    // =========================================================

    [System.Serializable]
    private sealed class CharacterGaugeSet
    {
        [SerializeField]
        private CSE_CharacterType _characterType;

        [SerializeField]
        private Sprite _iconSprite;

        public CSE_CharacterType characterType =>
            _characterType;

        public Sprite iconSprite =>
            _iconSprite;
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("ゲージ画像（円形）")]
    [SerializeField]
    private Image _gauge;

    [Header("中央アイコン")]
    [SerializeField]
    private Image _iconImage;

    [Header("キャラクターごとの表示設定")]
    [SerializeField]
    private CharacterGaugeSet[] _characterGaugeSets;

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

    private Coroutine
        _keyGuideAnimationCoroutine;

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

            _keyGuideRoot.gameObject.SetActive(
                false
            );
        }

        _isGaugeFull = false;

        if (_gauge != null)
        {
            _gauge.fillAmount = 0f;
        }
    }

    // =========================================================
    // キャラクター切り替え
    // =========================================================

    /// <summary>
    /// キャラクターに対応するアイコンとゲージ色を設定する。
    /// </summary>
    public void SetCharacter(
        CSE_CharacterType characterType)
    {
        CharacterGaugeSet gaugeSet =
            FindCharacterGaugeSet(
                characterType
            );

        if (gaugeSet == null)
        {
            Debug.LogWarning(
                $"{nameof(CS_UISpecialGaugeView)}: " +
                $"必殺技ゲージの設定がありません: " +
                $"{characterType}",
                this
            );

            ClearCharacterVisual();

            return;
        }

        if (_iconImage != null)
        {
            _iconImage.sprite =
                gaugeSet.iconSprite;

            _iconImage.enabled =
                gaugeSet.iconSprite != null;

            // 満タン時にも色を変えない。
            _iconImage.color =
                Color.white;
        }
    }

    /// <summary>
    /// キャラクターに対応する設定を探す。
    /// </summary>
    private CharacterGaugeSet FindCharacterGaugeSet(
        CSE_CharacterType characterType)
    {
        if (_characterGaugeSets == null)
        {
            return null;
        }

        for (int i = 0;
             i < _characterGaugeSets.Length;
             i++)
        {
            CharacterGaugeSet gaugeSet =
                _characterGaugeSets[i];

            if (gaugeSet == null)
            {
                continue;
            }

            if (gaugeSet.characterType ==
                characterType)
            {
                return gaugeSet;
            }
        }

        return null;
    }

    /// <summary>
    /// キャラクター設定が見つからなかった場合に
    /// 前のキャラクターの表示が残らないようにする。
    /// </summary>
    private void ClearCharacterVisual()
    {
        if (_iconImage != null)
        {
            _iconImage.sprite = null;
            _iconImage.enabled = false;
            _iconImage.color = Color.white;
        }

        if (_gauge != null)
        {
            _gauge.color = Color.white;
        }
    }

    // =========================================================
    // ゲージ描画
    // =========================================================

    public void UpdateGauge(
        float current,
        float max)
    {
        float fillAmount =
            max > 0f
                ? Mathf.Clamp01(
                    current / max
                )
                : 0f;

        if (_gauge != null)
        {
            _gauge.fillAmount =
                fillAmount;
        }

        bool isFull =
            max > 0f &&
            current >= max;

        SetGaugeFull(isFull);
    }

    /// <summary>
    /// Modelとの接続が外れたときにゲージをリセットする。
    /// </summary>
    public void ResetGauge()
    {
        if (_gauge != null)
        {
            _gauge.fillAmount = 0f;
        }

        SetGaugeFull(false);
    }

    // =========================================================
    // 満タン状態
    // =========================================================

    private void SetGaugeFull(bool isFull)
    {
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

    private void ShowKeyGuide()
    {
        if (_keyGuideRoot == null)
        {
            return;
        }

        StopKeyGuideAnimation();

        _keyGuideRoot.gameObject.SetActive(
            true
        );

        _keyGuideRoot.localScale =
            GetKeyGuideScale(
                _keyGuideMinScale
            );

        _keyGuideAnimationCoroutine =
            StartCoroutine(
                PlayKeyGuideAnimation()
            );
    }

    private void HideKeyGuide()
    {
        StopKeyGuideAnimation();

        if (_keyGuideRoot == null)
        {
            return;
        }

        _keyGuideRoot.localScale =
            _keyGuideInitialScale;

        _keyGuideRoot.gameObject.SetActive(
            false
        );
    }

    // =========================================================
    // キーガイドアニメーション
    // =========================================================

    private IEnumerator PlayKeyGuideAnimation()
    {
        while (_isGaugeFull)
        {
            yield return ScaleKeyGuide(
                fromScale:
                    _keyGuideMinScale,
                toScale:
                    _keyGuideMaxScale
            );

            if (!_isGaugeFull)
            {
                break;
            }

            yield return ScaleKeyGuide(
                fromScale:
                    _keyGuideMaxScale,
                toScale:
                    _keyGuideMinScale
            );
        }

        _keyGuideAnimationCoroutine = null;
    }

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
            GetKeyGuideScale(
                fromScale
            );

        Vector3 to =
            GetKeyGuideScale(
                toScale
            );

        while (elapsed <
               _keyGuideScaleDuration)
        {
            if (_keyGuideRoot == null ||
                !_isGaugeFull)
            {
                yield break;
            }

            elapsed +=
                Time.unscaledDeltaTime;

            float rate =
                Mathf.Clamp01(
                    elapsed /
                    _keyGuideScaleDuration
                );

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

    private void StopKeyGuideAnimation()
    {
        if (_keyGuideAnimationCoroutine ==
            null)
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

            _keyGuideRoot.gameObject.SetActive(
                false
            );
        }
    }

    private void OnEnable()
    {
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