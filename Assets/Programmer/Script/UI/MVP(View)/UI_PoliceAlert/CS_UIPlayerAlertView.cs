/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警戒状態UIのView。
/// Noneでは非表示、DetectedとSearchingでは対応する表示を有効にする。
/// </summary>
public class CS_UIPlayerAlertView : CS_BaseView<CS_UIPlayerAlertPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("警戒状態UI全体")]
    [SerializeField] private GameObject _alertRoot;

    [Header("警察に発見されている時の表示")]
    [SerializeField] private GameObject _detectedRoot;

    [Header("警察に捜索されている時の表示")]
    [SerializeField] private GameObject _searchingRoot;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        ApplyAlertState(CSE_PlayerAlertState.None);
    }

    public override void SetPresenter(
        CS_UIPlayerAlertPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 表示更新
    // =========================================================

    public void ApplyAlertState(CSE_PlayerAlertState alertState)
    {
        bool isDetected =
            alertState == CSE_PlayerAlertState.Detected;

        bool isSearching =
            alertState == CSE_PlayerAlertState.Searching;

        bool shouldShow = isDetected || isSearching;

        if (_alertRoot != null)
            _alertRoot.SetActive(shouldShow);

        if (_detectedRoot != null)
            _detectedRoot.SetActive(isDetected);

        if (_searchingRoot != null)
            _searchingRoot.SetActive(isSearching);
    }
}
