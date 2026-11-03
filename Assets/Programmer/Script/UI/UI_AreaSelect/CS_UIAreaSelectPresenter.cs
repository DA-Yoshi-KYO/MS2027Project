/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-29 | 初回作成(Issue #342)
 * 2026-10-30 | エリアの数だけViewに印を作らせ、エリアの画面上の位置を渡すようにした
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// エリア選択シーン(AreaSelect)の画面の Presenter
///
/// ・Model の状態(残り秒数・エリアごとの人数・選んだエリア・押せるか)を購読して View に反映する
/// ・View のボタン入力を、Controller が受け取れる形(「このエリアを選びたい」という要求)にして外へ出す
/// ・CS_AreaSelectController が BindModel を呼ぶ
/// </summary>
[RequireComponent(typeof(CS_UIAreaSelectView))]
public class CS_UIAreaSelectPresenter : CS_BasePresenter
{
    private CS_UIAreaSelectView _view;

    /// <summary>
    /// エリアのボタンが押された(値はエリアの番号)
    /// </summary>
    public Observable<int> onAreaRequested => _view.onAreaClicked;

    private void Awake()
    {
        _view = GetComponent<CS_UIAreaSelectView>();
        _view.SetPresenter(this);
    }

    /// <summary>
    /// Model を購読して View に反映する(購読した瞬間に現在値が流れるので初期表示もここで行われる)
    /// </summary>
    public void BindModel(CS_UIAreaSelectModel model)
    {
        _view.CreateMarkers(model.areaCount);

        model.remainingSeconds
            .Subscribe(_view.SetRemainingSeconds)
            .AddTo(_disposables);

        for (int area = 0; area < model.areaCount; area++)
        {
            int target = area;  // ラムダで使うので、ループ変数をコピーしておく
            model.GetAreaCount(target)
                .Subscribe(count => _view.SetAreaCount(target, count))
                .AddTo(_disposables);

            model.GetAreaScreenPosition(target)
                .Where(position => position != CS_UIAreaSelectModel.unknownScreenPosition)
                .Subscribe(position => _view.SetAreaScreenPosition(target, position))
                .AddTo(_disposables);
        }

        model.selectedArea
            .Subscribe(_view.SetSelectedArea)
            .AddTo(_disposables);

        model.isInteractable
            .Subscribe(_view.SetInteractable)
            .AddTo(_disposables);
    }
}
