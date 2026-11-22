/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// LIMITBREAKの表示を担当するView。
///
/// ・数値を白外縁、黒縁、赤文字の3層で表示する
/// ・3つのTextMeshProUGUIへ同じ数値を同期する
/// ・0%では非表示にする
/// ・値変更時は現在値から目標値へカウントする
/// </summary>
public sealed class CS_UILimitBreakView :
    CS_BaseView<CS_UILimitBreakPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("表示・非表示を切り替えるルート")]
    [SerializeField]
    private GameObject _displayRoot;

    [Header("LIMITBREAK数値・外側の白縁")]
    [SerializeField]
    private TextMeshProUGUI _outerWhiteValueText;

    [Header("LIMITBREAK数値・中央の黒縁")]
    [SerializeField]
    private TextMeshProUGUI _blackOutlineValueText;

    [Header("LIMITBREAK数値・内側の赤文字")]
    [SerializeField]
    private TextMeshProUGUI _innerRedValueText;

    [Header("カウントアニメーション")]

    [Tooltip("現在値から目標値まで変化する秒数")]
    [Min(0f)]
    [SerializeField]
    private float _countDuration = 1f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private Coroutine _countCoroutine;

    private int _displayedPercent;

    // =========================================================
    // 公開プロパティ
    // =========================================================

    public int displayedPercent =>
        _displayedPercent;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        DisableRaycastTarget(
            _outerWhiteValueText
        );

        DisableRaycastTarget(
            _blackOutlineValueText
        );

        DisableRaycastTarget(
            _innerRedValueText
        );

        _displayedPercent = 0;

        ApplyValueText(0);

        SetVisible(false);
    }

    public override void SetPresenter(
        CS_UILimitBreakPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 表示更新
    // =========================================================

    /// <summary>
    /// 現在の表示値から目標値へカウントする。
    /// </summary>
    public void AnimateTo(int targetPercent)
    {
        targetPercent =
            Mathf.Max(
                0,
                targetPercent
            );

        StopCountAnimation();

        if (targetPercent > 0 ||
            _displayedPercent > 0)
        {
            SetVisible(true);
        }

        _countCoroutine =
            StartCoroutine(
                CountTo(targetPercent)
            );
    }

    /// <summary>
    /// アニメーションせず値を即時反映する。
    /// </summary>
    public void SetImmediate(int percent)
    {
        StopCountAnimation();

        _displayedPercent =
            Mathf.Max(
                0,
                percent
            );

        ApplyValueText(
            _displayedPercent
        );

        SetVisible(
            _displayedPercent > 0
        );
    }

    // =========================================================
    // カウントアニメーション
    // =========================================================

    private IEnumerator CountTo(
        int targetPercent)
    {
        int startPercent =
            _displayedPercent;

        if (startPercent == targetPercent ||
            _countDuration <= 0f)
        {
            CompleteCount(
                targetPercent
            );

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < _countDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float rate =
                Mathf.Clamp01(
                    elapsed /
                    _countDuration
                );

            float value =
                Mathf.Lerp(
                    startPercent,
                    targetPercent,
                    rate
                );

            _displayedPercent =
                Mathf.RoundToInt(value);

            ApplyValueText(
                _displayedPercent
            );

            yield return null;
        }

        CompleteCount(
            targetPercent
        );
    }

    private void CompleteCount(
        int targetPercent)
    {
        _displayedPercent =
            targetPercent;

        ApplyValueText(
            _displayedPercent
        );

        SetVisible(
            _displayedPercent > 0
        );

        _countCoroutine = null;
    }

    // =========================================================
    // Text更新
    // =========================================================

    /// <summary>
    /// 白外縁、黒縁、赤文字の3つへ
    /// 同じパーセント文字列を設定する。
    /// </summary>
    private void ApplyValueText(int percent)
    {
        string valueText =
            $"{percent}%";

        SetText(
            _outerWhiteValueText,
            valueText
        );

        SetText(
            _blackOutlineValueText,
            valueText
        );

        SetText(
            _innerRedValueText,
            valueText
        );
    }

    /// <summary>
    /// 指定したTextMeshProUGUIへ文字列を設定する。
    /// </summary>
    private static void SetText(
        TextMeshProUGUI text,
        string value)
    {
        if (text == null)
        {
            return;
        }

        text.text = value;
    }

    /// <summary>
    /// UIテキストが入力を遮らないようにする。
    /// </summary>
    private static void DisableRaycastTarget(
        TextMeshProUGUI text)
    {
        if (text == null)
        {
            return;
        }

        text.raycastTarget = false;
    }

    // =========================================================
    // 表示・非表示
    // =========================================================

    private void SetVisible(bool isVisible)
    {
        if (_displayRoot == null)
        {
            return;
        }

        if (_displayRoot.activeSelf !=
            isVisible)
        {
            _displayRoot.SetActive(
                isVisible
            );
        }
    }

    // =========================================================
    // 終了処理
    // =========================================================

    private void StopCountAnimation()
    {
        if (_countCoroutine == null)
        {
            return;
        }

        StopCoroutine(
            _countCoroutine
        );

        _countCoroutine = null;
    }

    private void OnDisable()
    {
        StopCountAnimation();
    }

    private void OnValidate()
    {
        _countDuration =
            Mathf.Max(
                0f,
                _countDuration
            );
    }
}