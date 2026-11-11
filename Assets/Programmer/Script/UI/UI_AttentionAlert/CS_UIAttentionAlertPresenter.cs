/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-29 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// BindされたAttentionAlert Modelを購読し、Viewの表示を切り替えるPresenter。
/// ModelはローカルPlayer側で生成・破棄する。
/// </summary>
[RequireComponent(typeof(CS_UIAttentionAlertView))]
public sealed class CS_UIAttentionAlertPresenter : CS_BasePresenter
{
    private CS_UIAttentionAlertView _view;
    private CS_UIAttentionAlertModel _model;

    private readonly SerialDisposable _modelSubscription =
        new SerialDisposable();

    private void Awake()
    {
        _view = GetComponent<CS_UIAttentionAlertView>();
        _view.SetPresenter(this);
        _modelSubscription.AddTo(_disposables);
    }

    private void OnEnable()
    {
        CS_UIAttentionAlertModel.OnBound += BindModel;
        CS_UIAttentionAlertModel.OnUnbound += UnbindModel;

        if (CS_UIAttentionAlertModel.TryGet(
            out CS_UIAttentionAlertModel model))
        {
            BindModel(model);
        }
        else
        {
            _view.SetAlerted(false);
        }
    }

    private void OnDisable()
    {
        CS_UIAttentionAlertModel.OnBound -= BindModel;
        CS_UIAttentionAlertModel.OnUnbound -= UnbindModel;
        UnbindModel();
    }

    private void BindModel(CS_UIAttentionAlertModel model)
    {
        _model = model;

        if (_model == null)
        {
            _view.SetAlerted(false);
            return;
        }

        _view.SetAlerted(_model.currentIsAlerted);

        bool isFirstNotification = true;

        _modelSubscription.Disposable =
            _model.isAlerted.Subscribe(isAlerted =>
            {
                // ReactivePropertyの購読直後の重複通知を除外する。
                if (isFirstNotification)
                {
                    isFirstNotification = false;
                    return;
                }

                _view.SetAlerted(isAlerted);
            });
    }

    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
        _model = null;
        _view.SetAlerted(false);
    }
}
