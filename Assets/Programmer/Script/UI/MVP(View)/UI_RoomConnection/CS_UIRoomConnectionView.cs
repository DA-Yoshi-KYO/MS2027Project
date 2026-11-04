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
/// 部屋に入るシーン(RoomConnection)の画面の View。
/// ボタン・入力欄・状態表示を持ち、押されたことを Observable で外へ知らせるだけ(処理は持たない)
/// </summary>
public class CS_UIRoomConnectionView : CS_BaseView<CS_UIRoomConnectionPresenter>
{
    [SerializeField][Tooltip("部屋番号(ホストのIPアドレス)の入力欄")] private TMP_InputField _roomInput;
    [SerializeField][Tooltip("部屋を作るボタン")] private Button _hostButton;
    [SerializeField][Tooltip("参加するボタン")] private Button _joinButton;
    [SerializeField][Tooltip("ソロで遊ぶボタン(未設定ならソロは選べない)")] private Button _soloButton;
    [SerializeField][Tooltip("状態表示")] private TMP_Text _statusText;

    private readonly Subject<Unit> _onHostClicked = new Subject<Unit>();
    private readonly Subject<Unit> _onJoinClicked = new Subject<Unit>();
    private readonly Subject<Unit> _onSoloClicked = new Subject<Unit>();

    public Observable<Unit> onHostClicked => _onHostClicked;
    public Observable<Unit> onJoinClicked => _onJoinClicked;
    public Observable<Unit> onSoloClicked => _onSoloClicked;

    /// <summary>
    /// 入力欄に入っている部屋番号
    /// </summary>
    public string roomInputText => _roomInput != null ? _roomInput.text : string.Empty;

    private void Awake()
    {
        _hostButton.onClick.AddListener(() => _onHostClicked.OnNext(Unit.Default));
        _joinButton.onClick.AddListener(() => _onJoinClicked.OnNext(Unit.Default));
        if (_soloButton != null) _soloButton.onClick.AddListener(() => _onSoloClicked.OnNext(Unit.Default));
    }

    private void OnDestroy()
    {
        _onHostClicked.Dispose();
        _onJoinClicked.Dispose();
        _onSoloClicked.Dispose();
    }

    public void SetStatus(string message)
    {
        if (_statusText != null) _statusText.text = message;
    }

    public void SetInteractable(bool value)
    {
        _hostButton.interactable = value;
        _joinButton.interactable = value;
        _roomInput.interactable = value;
        if (_soloButton != null) _soloButton.interactable = value;
    }
}
