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
/// </summary>
public class CS_UIScoreView : CS_BaseView<CS_UIScorePresenter>
{
    [SerializeField] private TextMeshProUGUI _scoreText;

    public void UpdateScore(int score)
    {
        _scoreText.text = score.ToString();
    }
}
