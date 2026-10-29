/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-29 | 初回作成(Issue #342)
 * ================================================ */

using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// エリア選択シーン(AreaSelect)の画面の View。
/// エリアのボタン・人数・残り時間の表示を持ち、押されたことを Observable で外へ知らせるだけ(処理は持たない)
/// </summary>
// ========================================
/*
 * メモ
 * ・配列の番号がエリアの番号(0〜2)。_areaButtons、_areaCountTexts、_selectedMarksは同じ順に並べる
 * ・エリアの名前はボタンの子のテキストに直接書いておく(仮の見た目)
 * ・_selectedMarksは自分が選んだエリアにだけ表示する目印(無ければ空でよい)
 */
// ========================================
public class CS_UIAreaSelectView : CS_BaseView<CS_UIAreaSelectPresenter>
{
    [SerializeField][Tooltip("エリアのボタン(エリアの番号順)")] private Button[] _areaButtons;
    [SerializeField][Tooltip("エリアごとの選んだ人数の表示(エリアの番号順)")] private TMP_Text[] _areaCountTexts;
    [SerializeField][Tooltip("自分が選んだエリアの目印(エリアの番号順。無ければ空でよい)")] private GameObject[] _selectedMarks;
    [SerializeField][Tooltip("残り時間の表示")] private TMP_Text _remainingTimeText;

    private readonly Subject<int> _onAreaClicked = new Subject<int>();

    public Observable<int> onAreaClicked => _onAreaClicked;

    private void Awake()
    {
        for (int i = 0; i < _areaButtons.Length; i++)
        {
            int area = i;   // ラムダで使うので、ループ変数をコピーしておく
            _areaButtons[i].onClick.AddListener(() => _onAreaClicked.OnNext(area));
        }
    }

    private void OnDestroy()
    {
        _onAreaClicked.Dispose();
    }

    public void SetRemainingSeconds(int seconds)
    {
        _remainingTimeText.text = $"{seconds}";
    }

    public void SetAreaCount(int area, int count)
    {
        if (area < 0 || area >= _areaCountTexts.Length) return;

        _areaCountTexts[area].text = $"{count}人";
    }

    public void SetSelectedArea(int area)
    {
        for (int i = 0; i < _selectedMarks.Length; i++)
        {
            if (_selectedMarks[i] != null) _selectedMarks[i].SetActive(i == area);
        }
    }

    public void SetInteractable(bool value)
    {
        foreach (Button button in _areaButtons)
        {
            button.interactable = value;
        }
    }
}
