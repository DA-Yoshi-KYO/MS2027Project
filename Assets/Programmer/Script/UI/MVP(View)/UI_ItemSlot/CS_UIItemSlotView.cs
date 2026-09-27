/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-27 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイテムスロットの見た目（View）
/// Presenter から渡された Sprite を表示したり、
/// スロット自体の表示・非表示を CanvasGroup で制御する役割を持つ。
/// </summary>
public class CS_UIItemSlotView : CS_BaseView<CS_UIItemSlotPresenter>
{
    [SerializeField] private Image _icon;   // アイテムのアイコン画像（外部から渡される）
    private CanvasGroup _canvasGroup;       // スロットの表示・非表示を制御するための CanvasGroup

    void Awake()
    {
        // 同じ GameObject に付いている CanvasGroup を取得
        // これが null だと SetVisible が動かないので必須
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// アイコン画像を差し替える
    /// Presenter → View の方向で呼ばれる
    /// </summary>
    public void SetIcon(Sprite sprite)
    {
        _icon.sprite = sprite;
    }

    /// <summary>
    /// スロットの表示・非表示を切り替える
    /// </summary>
    public void SetVisible(bool visible)
    {
        _icon.gameObject.SetActive(visible);
    }
}
