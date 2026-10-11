/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-10 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通知スロット1つ分の表示を担当する View
/// ・BackImage と Text を持つ
/// ・フェードアウトは CanvasGroup.alpha で行う
/// </summary>
public class CS_UINotificationView : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("UI パーツ")]
    [SerializeField] private Image _backImage;
    [SerializeField] private TextMeshProUGUI _text;

    [Header("フェードアウト設定")]
    [SerializeField] private float _fadeOutDuration = 1f; // フェードアウト時間（秒・可変）

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CanvasGroup _canvasGroup;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 初期状態は非表示
        SetVisible(false);
    }

    // =========================================================
    // 表示
    // =========================================================

    /// <summary>通知を表示する</summary>
    public void Show(CS_NotificationData data)
    {
        if (_text != null)
            _text.text = data.message;

        _canvasGroup.alpha = 1f;
        SetVisible(true);
    }

    /// <summary>表示テキストを更新する（キル結合時）</summary>
    public void UpdateMessage(CS_NotificationData data)
    {
        if (_text != null)
            _text.text = data.message;

        // フェードアウト中でもリセット
        _canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// フェードアウトを更新する
    /// ・残り表示時間からアルファ値を計算する
    /// ・Presenter から毎フレーム呼ばれる
    /// </summary>
    public void UpdateFade(float remainTime)
    {
        if (remainTime <= 0f)
        {
            SetVisible(false);
            return;
        }

        // フェードアウト開始タイミングになったらアルファ値を下げる
        if (remainTime < _fadeOutDuration)
        {
            _canvasGroup.alpha = remainTime / _fadeOutDuration;
        }
        else
        {
            _canvasGroup.alpha = 1f;
        }
    }

    /// <summary>非表示にする</summary>
    public void Hide()
    {
        SetVisible(false);
    }

    // =========================================================
    // 内部処理
    // =========================================================

    private void SetVisible(bool visible)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }
}
