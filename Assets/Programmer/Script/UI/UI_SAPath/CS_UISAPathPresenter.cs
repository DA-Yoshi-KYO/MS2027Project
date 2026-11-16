/* ================================================
 * 動くSAポイントの軌道表示のPresenter
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-16 | 初回作成(Issue #507)
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// 動くSAポイントの軌道表示の Presenter
/// Bind されたModelの、軌道の登録・削除・出す消すを、Viewに渡す
///
/// ・Model・UIのどちらが先に生成されても紐づく(紐づいた時に、Modelに今ある軌道をすべてViewへ取り込む)
/// ・軌道はワールドの座標のまま描くので、カメラの位置は使わない
/// Modelの破棄は持ち主(使用者)が行うので、ここでは購読解除だけ行う
/// </summary>
[RequireComponent(typeof(CS_UISAPathView))]
public class CS_UISAPathPresenter : CS_BasePresenter
{
    private CS_UISAPathView _view;
    private CS_UISAPathModel _model;

    // 今紐づいているModelの購読(新しいModelを入れると前の購読は自動で解除される)
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    private void Awake()
    {
        _view = GetComponent<CS_UISAPathView>();
        _view.SetPresenter(this);
        _modelSubscription.AddTo(_disposables);
    }

    private void OnEnable()
    {
        CS_UISAPathModel.OnBound += BindModel;
        CS_UISAPathModel.OnUnbound += UnbindModel;

        // UIより先にModelがBindされていた場合
        if (CS_UISAPathModel.TryGet(out CS_UISAPathModel model))
            BindModel(model);
    }

    private void OnDisable()
    {
        CS_UISAPathModel.OnBound -= BindModel;
        CS_UISAPathModel.OnUnbound -= UnbindModel;
        UnbindModel();
    }

    private void BindModel(CS_UISAPathModel model)
    {
        if (_model == model) return;

        _view.Clear();
        _model = model;

        // 今ある軌道を取り込む
        foreach (CS_UISAPathModel.PathEntry entry in model.entries)
        {
            _view.AddPath(entry.id, entry.points);
            _view.SetVisible(entry.id, entry.isVisible);
        }

        CompositeDisposable subscriptions = new CompositeDisposable();
        model.onAdded.Subscribe(entry =>
        {
            _view.AddPath(entry.id, entry.points);
            _view.SetVisible(entry.id, entry.isVisible);
        }).AddTo(subscriptions);
        model.onRemoved.Subscribe(_view.RemovePath).AddTo(subscriptions);
        model.onVisibleChanged.Subscribe(entry => _view.SetVisible(entry.id, entry.isVisible)).AddTo(subscriptions);
        _modelSubscription.Disposable = subscriptions;
    }

    private void UnbindModel()
    {
        _model = null;
        _modelSubscription.Disposable = Disposable.Empty;
        _view.Clear();
    }
}
