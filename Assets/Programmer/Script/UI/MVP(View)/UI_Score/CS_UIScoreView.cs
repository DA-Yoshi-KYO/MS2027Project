/* ================================================
 * スコアの表示だけを担当するView
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;

/// <summary>
/// スコアの表示だけを担当するView
/// Presenterから渡された値を描画するだけで、ロジックは一切持たない
/// ★ CanvasGroup + SetVisible を追加
///    → SetActive(false) せずに見た目だけ隠せるので
///      OnBound が来たときに表示できる
/// </summary>
public class CS_UIScoreView : CS_BaseView<CS_UIScorePresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [SerializeField] private TextMeshProUGUI _scoreText;

    // =========================================================
    // 内部フィールド
    // =========================================================

    // 見た目の表示 / 非表示に使う
    private CanvasGroup _canvasGroup;

    // =========================================================
    // 初期化
    // =========================================================

    protected void Awake()
    {
        // ★ CanvasGroup を取得（なければ自動追加）
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIScorePresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 表示 / 非表示
    // =========================================================

    /// <summary>
    /// 見た目だけ表示 / 非表示を切り替える
    /// SetActive(false) と違い、GameObject は有効のまま
    /// → OnBound が来たときに表示できる
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.blocksRaycasts = visible;
        _canvasGroup.interactable = visible;
    }

    // =========================================================
    // 描画（Presenter から呼ばれる）
    // =========================================================

    public void UpdateScore(int score)
    {
        if (_scoreText != null)
            _scoreText.text = score.ToString();
    }
}
