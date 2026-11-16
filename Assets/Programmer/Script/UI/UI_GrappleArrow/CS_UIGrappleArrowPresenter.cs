/* ================================================
 * グラップルの矢印(飛んでいく方向を示す矢印)のPresenter
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-16 | 初回作成(Issue #505)
 * 2026-11-16 | 道筋に山形を並べる形に変更(始点・終点をViewへ渡す)
 * 2026-11-16 | 山形の向きがカメラに左右されない形になったので、カメラの位置を使う処理を外した
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// グラップルの矢印の Presenter
/// Bind されたModelの道筋の始点・終点・表示の有無を、Viewに渡す
///
/// ・Model・UIのどちらが先に生成されても紐づく
/// ・山形の向きは、飛ぶ方向だけで決まる(カメラは使わない)ので、毎フレーム(LateUpdate)道筋をViewへ渡すだけ
/// Modelの破棄は持ち主(使用者)が行うので、ここでは購読解除だけ行う
/// </summary>
[RequireComponent(typeof(CS_UIGrappleArrowView))]
public class CS_UIGrappleArrowPresenter : CS_BasePresenter
{
    private CS_UIGrappleArrowView _view;
    private CS_UIGrappleArrowModel _model;

    // 今紐づいているModelの購読(新しいModelを入れると前の購読は自動で解除される)
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    private void Awake()
    {
        _view = GetComponent<CS_UIGrappleArrowView>();
        _view.SetPresenter(this);
        _modelSubscription.AddTo(_disposables);
    }

    private void OnEnable()
    {
        CS_UIGrappleArrowModel.OnBound += BindModel;
        CS_UIGrappleArrowModel.OnUnbound += UnbindModel;

        // UIより先にModelがBindされていた場合
        if (CS_UIGrappleArrowModel.TryGet(out CS_UIGrappleArrowModel model))
            BindModel(model);
        else
            _view.SetVisible(false);
    }

    private void OnDisable()
    {
        CS_UIGrappleArrowModel.OnBound -= BindModel;
        CS_UIGrappleArrowModel.OnUnbound -= UnbindModel;
        UnbindModel();
    }

    // 道筋の始点・終点をViewに渡す(出ている間と、消えるフェードの間)
    private void LateUpdate()
    {
        if (_model == null) return;
        if (!_model.isVisible.CurrentValue && !_view.isShown) return;

        _view.SetLine(_model.startPosition, _model.endPosition);
    }

    private void BindModel(CS_UIGrappleArrowModel model)
    {
        if (_model == model) return;

        _model = model;
        _modelSubscription.Disposable = model.isVisible.Subscribe(_view.SetVisible);
    }

    private void UnbindModel()
    {
        _model = null;
        _modelSubscription.Disposable = Disposable.Empty;
        _view.SetVisible(false);
    }
}
