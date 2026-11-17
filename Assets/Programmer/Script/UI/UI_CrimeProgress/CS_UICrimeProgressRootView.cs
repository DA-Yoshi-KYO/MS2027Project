/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-17 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 3エリア分の犯罪完遂ゲージを管理するRootView。
///
/// ・エリアごとのゲージ値を更新する
/// ・現在地のゲージを拡大する
/// ・現在地以外を縮小する
/// </summary>
public sealed class CS_UICrimeProgressRootView :
    CS_BaseView<CS_UICrimeProgressPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("AreaGauge_0 ～ AreaGauge_2")]
    [SerializeField]
    private CS_UICrimeProgressGaugeView[] _gaugeViews;

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(
        CS_UICrimeProgressPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 完遂率
    // =========================================================

    /// <summary>
    /// 指定エリアの完遂率を更新する。
    /// </summary>
    public void SetProgress(
        CSE_CrimeAreaType areaType,
        float progress)
    {
        CS_UICrimeProgressGaugeView gaugeView =
            FindGaugeView(areaType);

        if (gaugeView == null)
        {
            return;
        }

        gaugeView.SetProgress(progress);
    }

    // =========================================================
    // 現在地
    // =========================================================

    /// <summary>
    /// 現在地を設定する。
    ///
    /// 現在地のAreaGaugeを拡大してValueを表示し、
    /// それ以外を縮小してValueを非表示にする。
    /// </summary>
    public void SetCurrentArea(
        CSE_CrimeAreaType currentArea,
        bool useAnimation)
    {
        if (_gaugeViews == null)
        {
            return;
        }

        for (int i = 0;
             i < _gaugeViews.Length;
             i++)
        {
            CS_UICrimeProgressGaugeView gaugeView =
                _gaugeViews[i];

            if (gaugeView == null)
            {
                continue;
            }

            bool isCurrentArea =
                gaugeView.areaType ==
                currentArea;

            gaugeView.SetCurrentArea(
                isCurrentArea,
                useAnimation
            );
        }
    }

    /// <summary>
    /// 現在地を解除し、すべて小さい状態にする。
    /// Valueもすべて非表示にする。
    /// </summary>
    public void ClearCurrentArea()
    {
        if (_gaugeViews == null)
        {
            return;
        }

        for (int i = 0;
             i < _gaugeViews.Length;
             i++)
        {
            CS_UICrimeProgressGaugeView gaugeView =
                _gaugeViews[i];

            if (gaugeView == null)
            {
                continue;
            }

            gaugeView.SetCurrentArea(
                false,
                false
            );
        }
    }

    // =========================================================
    // 検索
    // =========================================================

    private CS_UICrimeProgressGaugeView FindGaugeView(
        CSE_CrimeAreaType areaType)
    {
        if (_gaugeViews == null)
        {
            return null;
        }

        for (int i = 0;
             i < _gaugeViews.Length;
             i++)
        {
            CS_UICrimeProgressGaugeView gaugeView =
                _gaugeViews[i];

            if (gaugeView == null)
            {
                continue;
            }

            if (gaugeView.areaType ==
                areaType)
            {
                return gaugeView;
            }
        }

        Debug.LogWarning(
            $"{nameof(CS_UICrimeProgressRootView)}: " +
            $"対応するゲージがありません: {areaType}",
            this
        );

        return null;
    }
}