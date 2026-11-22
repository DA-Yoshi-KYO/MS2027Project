/* ================================================
 * Hpの描画の処理
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイヤーHPの描画を担当するView。
///
/// ・キャラクターごとに満タンハートと黒ハートを持つ
/// ・HP減少時、対象ハートを上下に振動させる
/// ・振動後、満タンハートをフェードアウトして黒ハートへ切り替える
/// ・HP回復時、満タンハートを即時表示する
/// ・HeartSlotは実行時に自動生成する
/// </summary>
public class CS_UIPlayerHpView :
    CS_BaseView<CS_UIPlayerHpPresenter>
{
    // =========================================================
    // キャラクターごとのハート設定
    // =========================================================

    [System.Serializable]
    public sealed class HeartSet
    {
        [SerializeField]
        private CSE_CharacterType _characterType;

        [SerializeField]
        private GameObject _fullHeartPrefab;

        [SerializeField]
        private GameObject _emptyHeartPrefab;

        public CSE_CharacterType characterType =>
            _characterType;

        public GameObject fullHeartPrefab =>
            _fullHeartPrefab;

        public GameObject emptyHeartPrefab =>
            _emptyHeartPrefab;
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("ハートアイコンの親")]
    [SerializeField]
    private Transform _heartsRoot;

    [Header("キャラクターごとのハートPrefab")]
    [SerializeField]
    private HeartSet[] _heartSets;

    [Header("被弾アニメーション")]

    [Tooltip("上下に揺れる時間")]
    [Min(0.01f)]
    [SerializeField]
    private float _shakeDuration = 0.18f;

    [Tooltip("上下に動く最大距離")]
    [Min(0f)]
    [SerializeField]
    private float _shakeDistance = 10f;

    [Tooltip("1秒間に上下へ振動する回数")]
    [Min(0f)]
    [SerializeField]
    private float _shakeFrequency = 35f;

    [Tooltip("満タンハートが黒Verへ切り替わる時間")]
    [Min(0.01f)]
    [SerializeField]
    private float _fadeDuration = 0.25f;

    // =========================================================
    // ハート1個分の情報
    // =========================================================

    private sealed class HeartInstance
    {
        private readonly GameObject _slotObject;

        private readonly GameObject _fullObject;

        private readonly GameObject _emptyObject;

        private readonly RectTransform _fullRect;

        private readonly CanvasGroup _fullCanvasGroup;

        private readonly Vector2 _initialPosition;

        private Coroutine _damageCoroutine;

        public GameObject slotObject =>
            _slotObject;

        public GameObject fullObject =>
            _fullObject;

        public GameObject emptyObject =>
            _emptyObject;

        public RectTransform fullRect =>
            _fullRect;

        public CanvasGroup fullCanvasGroup =>
            _fullCanvasGroup;

        public Vector2 initialPosition =>
            _initialPosition;

        public Coroutine damageCoroutine
        {
            get => _damageCoroutine;
            set => _damageCoroutine = value;
        }

        public HeartInstance(
            GameObject slotObject,
            GameObject fullObject,
            GameObject emptyObject,
            RectTransform fullRect,
            CanvasGroup fullCanvasGroup,
            Vector2 initialPosition)
        {
            _slotObject = slotObject;
            _fullObject = fullObject;
            _emptyObject = emptyObject;

            _fullRect = fullRect;
            _fullCanvasGroup = fullCanvasGroup;

            _initialPosition = initialPosition;
            _damageCoroutine = null;
        }
    }

    // =========================================================
    // 内部フィールド
    // =========================================================

    private readonly List<HeartInstance> _hearts =
        new List<HeartInstance>();

    private HeartSet _currentHeartSet;

    private int _currentMaxHp = -1;

    private int _previousHp = -1;

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(
        CS_UIPlayerHpPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // キャラクター切り替え
    // =========================================================

    /// <summary>
    /// 表示するキャラクターを設定する。
    /// キャラクターが変化した場合はハートを作り直す。
    /// </summary>
    public void SetCharacter(
        CSE_CharacterType characterType)
    {
        HeartSet heartSet =
            FindHeartSet(characterType);

        if (heartSet == _currentHeartSet)
        {
            return;
        }

        _currentHeartSet = heartSet;

        ClearHearts();

        _currentMaxHp = -1;
        _previousHp = -1;
    }

    /// <summary>
    /// キャラクターに対応するハート設定を取得する。
    /// </summary>
    private HeartSet FindHeartSet(
        CSE_CharacterType characterType)
    {
        if (_heartSets == null)
        {
            Debug.LogWarning(
                $"{nameof(CS_UIPlayerHpView)}: " +
                "HeartSetsが設定されていません。",
                this
            );

            return null;
        }

        for (int i = 0;
             i < _heartSets.Length;
             i++)
        {
            HeartSet heartSet =
                _heartSets[i];

            if (heartSet == null)
            {
                continue;
            }

            if (heartSet.characterType ==
                characterType)
            {
                return heartSet;
            }
        }

        Debug.LogWarning(
            $"{nameof(CS_UIPlayerHpView)}: " +
            $"ハート設定がありません: {characterType}",
            this
        );

        return null;
    }

    // =========================================================
    // HP描画
    // =========================================================

    /// <summary>
    /// HP表示を更新する。
    /// HPが減少した場合は被弾アニメーションを再生する。
    /// </summary>
    public void UpdateHp(
        int hp,
        int max)
    {
        if (_currentHeartSet == null)
        {
            return;
        }

        max = Mathf.Max(0, max);
        hp = Mathf.Clamp(hp, 0, max);

        // 初回表示、または最大HPが変わった場合。
        if (max != _currentMaxHp)
        {
            RebuildHearts(max);

            _currentMaxHp = max;
            _previousHp = hp;

            ApplyHpImmediately(hp);

            return;
        }

        if (_previousHp < 0)
        {
            _previousHp = hp;

            ApplyHpImmediately(hp);

            return;
        }

        if (hp < _previousHp)
        {
            PlayDamageAnimations(
                previousHp: _previousHp,
                currentHp: hp
            );
        }
        else if (hp > _previousHp)
        {
            ApplyRecovery(
                previousHp: _previousHp,
                currentHp: hp
            );
        }

        _previousHp = hp;
    }

    // =========================================================
    // 被弾アニメーション
    // =========================================================

    /// <summary>
    /// HP減少分のハートに被弾アニメーションを再生する。
    ///
    /// 5から4の場合はindex 4、
    /// 5から3の場合はindex 4とindex 3が対象になる。
    /// </summary>
    private void PlayDamageAnimations(
        int previousHp,
        int currentHp)
    {
        for (int i = previousHp - 1;
             i >= currentHp;
             i--)
        {
            if (i < 0 ||
                i >= _hearts.Count)
            {
                continue;
            }

            HeartInstance heart =
                _hearts[i];

            StopHeartAnimation(heart);

            heart.damageCoroutine =
                StartCoroutine(
                    PlayDamageAnimation(
                        heart
                    )
                );
        }
    }

    /// <summary>
    /// ハート1個分の被弾アニメーション。
    ///
    /// 1. 通常ハートを上下に振動させる
    /// 2. 振動を徐々に弱くする
    /// 3. 通常ハートをフェードアウトする
    /// 4. 背面の黒ハートだけを残す
    /// </summary>
    private IEnumerator PlayDamageAnimation(
        HeartInstance heart)
    {
        if (!IsHeartValid(heart))
        {
            if (heart != null)
            {
                heart.damageCoroutine = null;
            }

            yield break;
        }

        // 黒Verを背面に表示しておく。
        heart.emptyObject.SetActive(true);

        // 通常Verを前面に表示する。
        heart.fullObject.SetActive(true);

        heart.fullCanvasGroup.alpha = 1f;

        heart.fullRect.anchoredPosition =
            heart.initialPosition;

        // -----------------------------------------------------
        // 上下振動
        // -----------------------------------------------------

        float elapsed = 0f;

        while (elapsed < _shakeDuration)
        {
            if (!IsHeartValid(heart))
            {
                yield break;
            }

            elapsed +=
                Time.unscaledDeltaTime;

            float rate =
                Mathf.Clamp01(
                    elapsed /
                    _shakeDuration
                );

            // 時間経過に合わせて、
            // 振動の強さを徐々に小さくする。
            float strength =
                1f - rate;

            float offsetY =
                Mathf.Sin(
                    elapsed *
                    _shakeFrequency *
                    Mathf.PI *
                    2f
                ) *
                _shakeDistance *
                strength;

            // X座標は変えず、
            // Y座標だけを上下へ動かす。
            heart.fullRect.anchoredPosition =
                heart.initialPosition +
                new Vector2(
                    0f,
                    offsetY
                );

            yield return null;
        }

        if (!IsHeartValid(heart))
        {
            yield break;
        }

        // 振動終了後は元の位置へ戻す。
        heart.fullRect.anchoredPosition =
            heart.initialPosition;

        // -----------------------------------------------------
        // 黒Verへのフェード
        // -----------------------------------------------------

        elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            if (!IsHeartValid(heart))
            {
                yield break;
            }

            elapsed +=
                Time.unscaledDeltaTime;

            float rate =
                Mathf.Clamp01(
                    elapsed /
                    _fadeDuration
                );

            // 通常Verを徐々に透明にすることで、
            // 背面の黒Verが見えるようになる。
            heart.fullCanvasGroup.alpha =
                1f - rate;

            yield return null;
        }

        if (!IsHeartValid(heart))
        {
            yield break;
        }

        heart.fullRect.anchoredPosition =
            heart.initialPosition;

        heart.fullCanvasGroup.alpha = 0f;

        heart.fullObject.SetActive(false);

        // 最後は黒Verだけを残す。
        heart.emptyObject.SetActive(true);

        heart.damageCoroutine = null;
    }

    // =========================================================
    // HP回復
    // =========================================================

    /// <summary>
    /// 回復したHPに対応するハートを即時復元する。
    /// </summary>
    private void ApplyRecovery(
        int previousHp,
        int currentHp)
    {
        for (int i = previousHp;
             i < currentHp;
             i++)
        {
            if (i < 0 ||
                i >= _hearts.Count)
            {
                continue;
            }

            HeartInstance heart =
                _hearts[i];

            if (!IsHeartValid(heart))
            {
                continue;
            }

            StopHeartAnimation(heart);

            heart.fullRect.anchoredPosition =
                heart.initialPosition;

            heart.fullCanvasGroup.alpha = 1f;

            heart.fullObject.SetActive(true);
            heart.emptyObject.SetActive(false);
        }
    }

    // =========================================================
    // HP即時反映
    // =========================================================

    /// <summary>
    /// アニメーションを使用せず現在HPを反映する。
    /// 初回表示や最大HP変更時に使用する。
    /// </summary>
    private void ApplyHpImmediately(int hp)
    {
        for (int i = 0;
             i < _hearts.Count;
             i++)
        {
            HeartInstance heart =
                _hearts[i];

            if (!IsHeartValid(heart))
            {
                continue;
            }

            StopHeartAnimation(heart);

            bool isFull =
                i < hp;

            heart.fullRect.anchoredPosition =
                heart.initialPosition;

            heart.fullCanvasGroup.alpha = 1f;

            heart.fullObject.SetActive(isFull);

            heart.emptyObject.SetActive(
                !isFull
            );
        }
    }

    // =========================================================
    // ハート生成
    // =========================================================

    /// <summary>
    /// 最大HPの数だけHeartSlotを生成する。
    /// 各スロット内に黒ハートと通常ハートを重ねる。
    /// </summary>
    private void RebuildHearts(int max)
    {
        ClearHearts();

        if (_heartsRoot == null)
        {
            Debug.LogError(
                $"{nameof(CS_UIPlayerHpView)}: " +
                "HeartsRootが設定されていません。",
                this
            );

            return;
        }

        if (_currentHeartSet == null)
        {
            return;
        }

        GameObject fullHeartPrefab =
            _currentHeartSet.fullHeartPrefab;

        GameObject emptyHeartPrefab =
            _currentHeartSet.emptyHeartPrefab;

        if (fullHeartPrefab == null ||
            emptyHeartPrefab == null)
        {
            Debug.LogError(
                $"{nameof(CS_UIPlayerHpView)}: " +
                "通常ハートまたは黒ハートPrefabが" +
                "設定されていません。",
                this
            );

            return;
        }

        RectTransform fullPrefabRect =
            fullHeartPrefab
                .GetComponent<RectTransform>();

        RectTransform emptyPrefabRect =
            emptyHeartPrefab
                .GetComponent<RectTransform>();

        if (fullPrefabRect == null ||
            emptyPrefabRect == null)
        {
            Debug.LogError(
                $"{nameof(CS_UIPlayerHpView)}: " +
                "ハートPrefabのルートに" +
                "RectTransformが必要です。",
                this
            );

            return;
        }

        Vector2 slotSize =
            fullPrefabRect.rect.size;

        if (slotSize.x <= 0f ||
            slotSize.y <= 0f)
        {
            slotSize =
                fullPrefabRect.sizeDelta;
        }

        max = Mathf.Max(0, max);

        for (int i = 0;
             i < max;
             i++)
        {
            GameObject slotObject =
                new GameObject(
                    $"HeartSlot_{i}",
                    typeof(RectTransform),
                    typeof(LayoutElement)
                );

            RectTransform slotRect =
                slotObject
                    .GetComponent<RectTransform>();

            LayoutElement layoutElement =
                slotObject
                    .GetComponent<LayoutElement>();

            slotRect.SetParent(
                _heartsRoot,
                false
            );

            slotRect.localScale =
                Vector3.one;

            slotRect.sizeDelta =
                slotSize;

            layoutElement.minWidth =
                slotSize.x;

            layoutElement.minHeight =
                slotSize.y;

            layoutElement.preferredWidth =
                slotSize.x;

            layoutElement.preferredHeight =
                slotSize.y;

            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;

            // 黒Verを先に生成して背面へ置く。
            GameObject emptyObject =
                Instantiate(
                    emptyHeartPrefab,
                    slotRect
                );

            // 通常Verを後に生成して前面へ置く。
            GameObject fullObject =
                Instantiate(
                    fullHeartPrefab,
                    slotRect
                );

            RectTransform emptyRect =
                emptyObject
                    .GetComponent<RectTransform>();

            RectTransform fullRect =
                fullObject
                    .GetComponent<RectTransform>();

            if (emptyRect == null ||
                fullRect == null)
            {
                Debug.LogError(
                    $"{nameof(CS_UIPlayerHpView)}: " +
                    "生成されたハートに" +
                    "RectTransformがありません。",
                    this
                );

                Destroy(slotObject);

                continue;
            }

            SetHeartRect(emptyRect);
            SetHeartRect(fullRect);

            CanvasGroup canvasGroup =
                fullObject
                    .GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup =
                    fullObject
                        .AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            HeartInstance heart =
                new HeartInstance(
                    slotObject: slotObject,
                    fullObject: fullObject,
                    emptyObject: emptyObject,
                    fullRect: fullRect,
                    fullCanvasGroup: canvasGroup,
                    initialPosition: Vector2.zero
                );

            _hearts.Add(heart);
        }
    }

    /// <summary>
    /// ハートをHeartSlotの中央に配置する。
    /// </summary>
    private static void SetHeartRect(
        RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin =
            new Vector2(0.5f, 0.5f);

        rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);

        rectTransform.pivot =
            new Vector2(0.5f, 0.5f);

        rectTransform.anchoredPosition =
            Vector2.zero;

        rectTransform.localRotation =
            Quaternion.identity;

        rectTransform.localScale =
            Vector3.one;
    }

    // =========================================================
    // 有効性確認
    // =========================================================

    private static bool IsHeartValid(
        HeartInstance heart)
    {
        return
            heart != null &&
            heart.slotObject != null &&
            heart.fullObject != null &&
            heart.emptyObject != null &&
            heart.fullRect != null &&
            heart.fullCanvasGroup != null;
    }

    // =========================================================
    // アニメーション停止
    // =========================================================

    private void StopHeartAnimation(
        HeartInstance heart)
    {
        if (heart == null)
        {
            return;
        }

        if (heart.damageCoroutine != null)
        {
            StopCoroutine(
                heart.damageCoroutine
            );

            heart.damageCoroutine = null;
        }

        if (heart.fullRect != null)
        {
            heart.fullRect.anchoredPosition =
                heart.initialPosition;
        }
    }

    // =========================================================
    // ハート削除
    // =========================================================

    private void ClearHearts()
    {
        for (int i = 0;
             i < _hearts.Count;
             i++)
        {
            HeartInstance heart =
                _hearts[i];

            if (heart == null)
            {
                continue;
            }

            StopHeartAnimation(heart);

            if (heart.slotObject != null)
            {
                Destroy(
                    heart.slotObject
                );
            }
        }

        _hearts.Clear();
    }

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnDisable()
    {
        for (int i = 0;
             i < _hearts.Count;
             i++)
        {
            StopHeartAnimation(
                _hearts[i]
            );
        }

        if (_previousHp >= 0)
        {
            ApplyHpImmediately(
                _previousHp
            );
        }
    }

    private void OnDestroy()
    {
        ClearHearts();
    }

    private void OnValidate()
    {
        _shakeDuration =
            Mathf.Max(
                0.01f,
                _shakeDuration
            );

        _shakeDistance =
            Mathf.Max(
                0f,
                _shakeDistance
            );

        _shakeFrequency =
            Mathf.Max(
                0f,
                _shakeFrequency
            );

        _fadeDuration =
            Mathf.Max(
                0.01f,
                _fadeDuration
            );
    }
}