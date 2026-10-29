/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// BindされたLIMITBREAK Modelを購読し、Viewへ表示値を渡すPresenter。
/// ModelはPlayer側で生成・破棄する。
/// </summary>
[RequireComponent(typeof(CS_UILimitBreakView))]
public sealed class CS_UILimitBreakPresenter : CS_BasePresenter
{
    private CS_UILimitBreakView _view;
    private CS_UILimitBreakModel _model;

    private readonly SerialDisposable _modelSubscription =
        new SerialDisposable();

    private void Awake()
    {
        _view = GetComponent<CS_UILimitBreakView>();
        _view.SetPresenter(this);
        _modelSubscription.AddTo(_disposables);
    }

    private void OnEnable()
    {
        CS_UILimitBreakModel.OnBound += BindModel;
        CS_UILimitBreakModel.OnUnbound += UnbindModel;

        if (CS_UILimitBreakModel.TryGet(
            out CS_UILimitBreakModel model))
        {
            BindModel(model);
        }
        else
        {
            _view.SetImmediate(0);
        }
    }

    private void OnDisable()
    {
        CS_UILimitBreakModel.OnBound -= BindModel;
        CS_UILimitBreakModel.OnUnbound -= UnbindModel;
        UnbindModel();
    }

    private void BindModel(CS_UILimitBreakModel model)
    {
        _model = model;

        if (_model == null)
        {
            _view.SetImmediate(0);
            return;
        }

        // Bind時は現在値を即時反映し、以後の変更だけアニメーションする。
        _view.SetImmediate(_model.currentBonusPercent);

        bool isFirstNotification = true;

        _modelSubscription.Disposable =
            _model.bonusPercent.Subscribe(percent =>
            {
                // ReactivePropertyは購読時に現在値を通知するため、
                // Bind直後の重複アニメーションを防ぐ。
                if (isFirstNotification)
                {
                    isFirstNotification = false;
                    return;
                }

                _view.AnimateTo(percent);
            });
    }

    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
        _model = null;
        _view.SetImmediate(0);
    }
}
