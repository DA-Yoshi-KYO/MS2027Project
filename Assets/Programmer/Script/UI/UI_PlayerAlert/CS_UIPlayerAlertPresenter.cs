/* ================================================
 *
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * 2026-11-02 | BindされたModelを受け取る形に変更
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// Bindされた警戒状態UIのModelを購読し、Viewの表示を切り替えるPresenter。
/// ModelはローカルPlayer側(CS_PlayerAttentionAlertUI)で生成・破棄する。
/// </summary>
[RequireComponent(typeof(CS_UIPlayerAlertView))]
public sealed class CS_UIPlayerAlertPresenter : CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIPlayerAlertView _view;

    private readonly SerialDisposable _modelSubscription =
        new SerialDisposable();

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _view = GetComponent<CS_UIPlayerAlertView>();
        _view.SetPresenter(this);
        _modelSubscription.AddTo(_disposables);
    }

    private void OnEnable()
    {
        CS_UIPlayerAlertModel.OnBound += BindModel;
        CS_UIPlayerAlertModel.OnUnbound += UnbindModel;

        // すでにBind済みのModelがあればすぐ購読する
        if (CS_UIPlayerAlertModel.TryGet(out CS_UIPlayerAlertModel model))
            BindModel(model);
        else
            _view.ApplyAlertState(CSE_PlayerAlertState.None);
    }

    private void OnDisable()
    {
        CS_UIPlayerAlertModel.OnBound -= BindModel;
        CS_UIPlayerAlertModel.OnUnbound -= UnbindModel;
        UnbindModel();
    }

    // =========================================================
    // Model 購読
    // =========================================================

    private void BindModel(CS_UIPlayerAlertModel model)
    {
        if (model == null)
        {
            UnbindModel();
            return;
        }

        // ReactivePropertyは購読した時に今の値も届くので、Bind前からの状態もそのまま反映される
        _modelSubscription.Disposable =
            model.alertState.Subscribe(_view.ApplyAlertState);
    }

    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
        _view.ApplyAlertState(CSE_PlayerAlertState.None);
    }
}
