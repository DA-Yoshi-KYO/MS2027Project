/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// 部屋に入るシーン(RoomConnection)の画面の Presenter
///
/// ・Model の状態(状態表示・押せるか)を購読して View に反映する
/// ・View のボタン入力を、Controller が受け取れる形(「〜したい」という要求)にして外へ出す
///   参加の要求には、入力欄の部屋番号を付ける
/// ・CS_RoomConnectionController が BindModel を呼ぶ
/// </summary>
[RequireComponent(typeof(CS_UIRoomConnectionView))]
public class CS_UIRoomConnectionPresenter : CS_BasePresenter
{
    private CS_UIRoomConnectionView _view;

    /// <summary>
    /// 「部屋を作る」が押された
    /// </summary>
    public Observable<Unit> onHostRequested => _view.onHostClicked;

    /// <summary>
    /// 「参加する」が押された(入力欄の部屋番号。空なら空文字)
    /// </summary>
    public Observable<string> onJoinRequested => _view.onJoinClicked.Select(_ => _view.roomInputText.Trim());

    /// <summary>
    /// 「ソロ」が押された
    /// </summary>
    public Observable<Unit> onSoloRequested => _view.onSoloClicked;

    private void Awake()
    {
        _view = GetComponent<CS_UIRoomConnectionView>();
        _view.SetPresenter(this);
    }

    /// <summary>
    /// Model を購読して View に反映する(購読した瞬間に現在値が流れるので初期表示もここで行われる)
    /// </summary>
    public void BindModel(CS_UIRoomConnectionModel model)
    {
        model.status
            .Subscribe(_view.SetStatus)
            .AddTo(_disposables);

        model.isInteractable
            .Subscribe(_view.SetInteractable)
            .AddTo(_disposables);
    }
}
