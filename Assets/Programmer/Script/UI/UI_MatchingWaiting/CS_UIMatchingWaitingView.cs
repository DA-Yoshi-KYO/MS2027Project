/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 待機シーン(WaitingforaMatch)の画面の View。
/// 参加人数の表示とボタンを持ち、押されたことを Observable で外へ知らせるだけ(処理は持たない)
/// </summary>
public class CS_UIMatchingWaitingView : CS_BaseView<CS_UIMatchingWaitingPresenter>
{
    [SerializeField][Tooltip("参加人数の表示")] private TMP_Text _playerCountText;
    [SerializeField][Tooltip("開始ボタン(ホストのみ表示)")] private Button _startButton;
    [SerializeField][Tooltip("戻るボタン")] private Button _backButton;

    private readonly Subject<Unit> _onStartClicked = new Subject<Unit>();
    private readonly Subject<Unit> _onBackClicked = new Subject<Unit>();

    public Observable<Unit> onStartClicked => _onStartClicked;
    public Observable<Unit> onBackClicked => _onBackClicked;

    private void Awake()
    {
        _startButton.onClick.AddListener(() => _onStartClicked.OnNext(Unit.Default));
        _backButton.onClick.AddListener(() => _onBackClicked.OnNext(Unit.Default));
    }

    private void OnDestroy()
    {
        _onStartClicked.Dispose();
        _onBackClicked.Dispose();
    }

    public void SetPlayerCount(int count, int max)
    {
        _playerCountText.text = $"Players {count} / {max}";
    }

    public void SetStartVisible(bool value)
    {
        _startButton.gameObject.SetActive(value);
    }

    public void SetInteractable(bool value)
    {
        _startButton.interactable = value;
        _backButton.interactable = value;
    }
}
