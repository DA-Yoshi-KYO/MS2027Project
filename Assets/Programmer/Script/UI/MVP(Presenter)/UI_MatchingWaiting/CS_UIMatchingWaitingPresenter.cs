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
/// 待機シーン(WaitingforaMatch)の画面の Presenter
///
/// ・Model の状態(参加人数・開始ボタンを出すか・押せるか)を購読して View に反映する
/// ・View のボタン入力を、Controller が受け取れる形(「〜したい」という要求)にして外へ出す
/// ・CS_MatchingWaitingController が BindModel を呼ぶ
/// </summary>
[RequireComponent(typeof(CS_UIMatchingWaitingView))]
public class CS_UIMatchingWaitingPresenter : CS_BasePresenter
{
    private CS_UIMatchingWaitingView _view;

    /// <summary>
    /// 開始ボタンが押された
    /// </summary>
    public Observable<Unit> onStartRequested => _view.onStartClicked;

    /// <summary>
    /// 戻るボタンが押された
    /// </summary>
    public Observable<Unit> onBackRequested => _view.onBackClicked;

    private void Awake()
    {
        _view = GetComponent<CS_UIMatchingWaitingView>();
        _view.SetPresenter(this);
    }

    /// <summary>
    /// Model を購読して View に反映する(購読した瞬間に現在値が流れるので初期表示もここで行われる)
    /// </summary>
    public void BindModel(CS_UIMatchingWaitingModel model)
    {
        model.playerCount
            .CombineLatest(model.maxPlayers, (count, max) => (count, max))
            .Subscribe(x => _view.SetPlayerCount(x.count, x.max))
            .AddTo(_disposables);

        model.isStartVisible
            .Subscribe(_view.SetStartVisible)
            .AddTo(_disposables);

        model.isInteractable
            .Subscribe(_view.SetInteractable)
            .AddTo(_disposables);
    }
}
