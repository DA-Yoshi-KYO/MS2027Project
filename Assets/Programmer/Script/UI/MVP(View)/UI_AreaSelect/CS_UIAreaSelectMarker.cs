/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-30 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// エリア選択画面で、1つのエリアの上に出す印(ボタン・名前・人数・選択中の目印)
/// CS_UIAreaSelectView がエリアの数だけプレハブから生成して使う(処理は持たない)
/// </summary>
// ========================================
/*
 * メモ
 * ・プレハブのルートに付け、子のボタン・テキスト・目印を Inspector で設定する
 * ・位置は SetScreenPosition で、親(印を並べる RectTransform)の中の割合(左下(0, 0)〜右上(1, 1))で決める
 *   親を画面いっぱいにしておけば、見下ろしカメラのビューポート座標がそのまま使える
 * ・_nameText と _selectedMark は無くてもよい
 */
// ========================================
public class CS_UIAreaSelectMarker : MonoBehaviour
{
    [SerializeField][Tooltip("押すとこのエリアを選ぶボタン")] private Button _button;
    [SerializeField][Tooltip("エリアの名前の表示(無くてもよい)")] private TMP_Text _nameText;
    [SerializeField][Tooltip("このエリアを選んだ人数の表示")] private TMP_Text _countText;
    [SerializeField][Tooltip("自分がこのエリアを選んでいる時だけ表示する目印(無くてもよい)")] private GameObject _selectedMark;

    public Button button => _button;

    public void SetName(string areaName)
    {
        if (_nameText != null) _nameText.text = areaName;
    }

    public void SetCount(int count)
    {
        _countText.text = $"{count}";
    }

    public void SetSelected(bool isSelected)
    {
        if (_selectedMark != null) _selectedMark.SetActive(isSelected);
    }

    public void SetScreenPosition(Vector2 position)
    {
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = position;
        rect.anchorMax = position;
        rect.anchoredPosition = Vector2.zero;
    }
}
