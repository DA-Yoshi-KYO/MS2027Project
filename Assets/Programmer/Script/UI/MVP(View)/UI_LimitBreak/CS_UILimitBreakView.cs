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
/// ラベルと数値を別のTextMeshProUGUIで表示する。
/// 0%では非表示にし、値変更時は現在値から目標値へカウントする。
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

    [Header("LIMITBREAKラベル")]
    [SerializeField]
    private TextMeshProUGUI _limitBreakLabelText;

    [Header("LIMITBREAK数値")]
    [SerializeField]
    private TextMeshProUGUI _limitBreakValueText;

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

    public int displayedPercent =>
        _displayedPercent;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        if (_limitBreakLabelText != null)
        {
            _limitBreakLabelText.text =
                "LIMIT\nBREAK";

            _limitBreakLabelText.raycastTarget =
                false;
        }

        if (_limitBreakValueText != null)
        {
            _limitBreakValueText.raycastTarget =
                false;
        }

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
        targetPercent = Mathf.Max(
            0,
            targetPercent
        );

        StopCountAnimation();

        if (targetPercent > 0 ||
            _displayedPercent > 0)
        {
            SetVisible(true);
        }

        _countCoroutine = StartCoroutine(
            CountTo(targetPercent)
        );
    }

    /// <summary>
    /// アニメーションせず値を即時反映する。
    /// </summary>
    public void SetImmediate(int percent)
    {
        StopCountAnimation();

        _displayedPercent = Mathf.Max(
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

            float rate = Mathf.Clamp01(
                elapsed / _countDuration
            );

            float value = Mathf.Lerp(
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

    private void ApplyValueText(
        int percent)
    {
        if (_limitBreakValueText == null)
            return;

        _limitBreakValueText.text =
            $":{percent}%";
    }

    // =========================================================
    // 表示・非表示
    // =========================================================

    private void SetVisible(bool isVisible)
    {
        if (_displayRoot == null)
            return;

        if (_displayRoot.activeSelf != isVisible)
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
            return;

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
        _countDuration = Mathf.Max(
            0f,
            _countDuration
        );
    }
}