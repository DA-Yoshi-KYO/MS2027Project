/* ================================================
 * グラップルの矢印(飛んでいく方向を示す矢印)のPresenter
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-16 | 初回作成(Issue #505)
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// グラップルの矢印の Presenter
/// Bind されたModelの位置・向き・表示の有無を、Viewに渡す
///
/// ・Model・UIのどちらが先に生成されても紐づく
/// ・矢印は、面がカメラの方を向くように置く。カメラの位置が確定した後に渡す(CS_UIAfterCameraUpdate。先に計算すると画面揺れなどで矢印が震える)
/// Modelの破棄は持ち主(使用者)が行うので、ここでは購読解除だけ行う
/// </summary>
[RequireComponent(typeof(CS_UIGrappleArrowView))]
public class CS_UIGrappleArrowPresenter : CS_BasePresenter
{
    [Header("カメラ")]
    [SerializeField] private Camera _targetCamera;     // 空ならCamera.mainを使う

    private CS_UIGrappleArrowView _view;
    private CS_UIGrappleArrowModel _model;

    // 今紐づいているModelの購読(新しいModelを入れると前の購読は自動で解除される)
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    private CS_UIAfterCameraUpdate _afterCameraUpdate;

    private void Awake()
    {
        _view = GetComponent<CS_UIGrappleArrowView>();
        _view.SetPresenter(this);
        _modelSubscription.AddTo(_disposables);
        _afterCameraUpdate = new CS_UIAfterCameraUpdate(GetTargetCamera, UpdateArrow);
    }

    private void OnEnable()
    {
        CS_UIGrappleArrowModel.OnBound += BindModel;
        CS_UIGrappleArrowModel.OnUnbound += UnbindModel;
        _afterCameraUpdate.Enable();

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
        _afterCameraUpdate.Disable();
        UnbindModel();
    }

    private void LateUpdate()
    {
        // Brainが動いていないシーンでは、ここで計算する
        _afterCameraUpdate.LateUpdateFallback();
    }

    // 矢印を向けるカメラ(空ならCamera.main)
    private Camera GetTargetCamera()
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;

        return _targetCamera;
    }

    // 位置・向きをViewに渡す(出ている間と、消えるフェードの間)
    private void UpdateArrow()
    {
        if (_model == null) return;
        if (!_model.isVisible.CurrentValue && !_view.isShown) return;

        Camera targetCamera = GetTargetCamera();
        if (targetCamera == null) return;

        _view.SetPose(_model.position, _model.direction, targetCamera.transform.position);
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
