/* ================================================
 * 奇襲の星マーク(対象の悪人の位置に出る星)のPresenter
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-15 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// ローカルPlayer番号にBindされたModelを取得し、
/// 対象のワールド座標をUICanvas上の座標へ変換してViewに渡すPresenter(CS_UILockOnPresenterと同じ形)。
/// 位置の計算はカメラの位置が確定した後に行う(CS_UIAfterCameraUpdate)。
/// 命中で弾ける間は、最後の位置に星を残す。
/// </summary>
[RequireComponent(typeof(CS_UIAmbushMarkView))]
public class CS_UIAmbushMarkPresenter : CS_BasePresenter
{
    [Header("座標変換")]
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Camera _targetCamera;

    [Header("対象位置の補正")]
    [SerializeField] private Vector3 _worldOffset = new(0f, 1.2f, 0f);

    private CS_UIAmbushMarkView _view;
    private CS_UIAmbushMarkModel _model;
    private DisposableBag _modelDisposables;
    private CS_UIAfterCameraUpdate _afterCameraUpdate;

    private void Awake()
    {
        _view = GetComponent<CS_UIAmbushMarkView>();
        _view.SetPresenter(this);
        _view.Hide();
        _afterCameraUpdate = new CS_UIAfterCameraUpdate(GetTargetCamera, UpdateMark);

        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        if (_canvasRect == null && _canvas != null)
            _canvasRect = _canvas.transform as RectTransform;
    }

    private void OnEnable()
    {
        CS_UIAmbushMarkModel.OnBound += HandleModelBound;
        CS_UIAmbushMarkModel.OnUnbound += HandleModelUnbound;
        _afterCameraUpdate.Enable();
        TryBindLocalModel();
    }

    private void OnDisable()
    {
        CS_UIAmbushMarkModel.OnBound -= HandleModelBound;
        CS_UIAmbushMarkModel.OnUnbound -= HandleModelUnbound;
        _afterCameraUpdate.Disable();
        UnbindModel();
        _view?.Hide();
    }

    private void LateUpdate()
    {
        // Brainが動いていないシーンでは、ここで計算する
        _afterCameraUpdate.LateUpdateFallback();
    }

    // 星を出すカメラ(空ならCamera.main)
    private Camera GetTargetCamera()
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;

        return _targetCamera;
    }

    // 対象の位置をUICanvas上の座標へ変換して、Viewに渡す
    private void UpdateMark()
    {
        if (_model == null)
        {
            _view.Hide();
            return;
        }

        Transform target = _model.currentTarget;

        // 対象がいない時は消す(弾けている間だけは、最後の位置に残す)
        if (target == null)
        {
            if (_model.hasTarget)
                _model.ClearTarget();

            if (!_view.isBursting)
                _view.Hide();
            return;
        }

        Camera targetCamera = GetTargetCamera();
        if (targetCamera == null || _canvasRect == null)
        {
            _view.Hide();
            return;
        }

        Vector3 screenPosition = targetCamera.WorldToScreenPoint(
            target.position + _worldOffset
        );

        // カメラの後ろ・画面外にいる時は出さない
        bool isOnScreen =
            screenPosition.z > 0f &&
            screenPosition.x >= 0f &&
            screenPosition.x <= Screen.width &&
            screenPosition.y >= 0f &&
            screenPosition.y <= Screen.height;

        if (!isOnScreen)
        {
            _view.Hide();
            return;
        }

        Camera uiCamera =
            _canvas != null &&
            _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            screenPosition,
            uiCamera,
            out Vector2 localPosition))
        {
            _view.Hide();
            return;
        }

        _view.SetPosition(localPosition);
        _view.Show();
    }

    private void TryBindLocalModel()
    {
        int playerNumber = CS_UIAmbushMarkModel.localPlayerNumber;

        if (playerNumber < 0)
            return;

        if (CS_UIAmbushMarkModel.TryGet(playerNumber, out CS_UIAmbushMarkModel model))
            BindModel(model);
    }

    private void HandleModelBound(
        int playerNumber,
        CS_UIAmbushMarkModel model)
    {
        if (playerNumber != CS_UIAmbushMarkModel.localPlayerNumber)
            return;

        BindModel(model);
    }

    private void HandleModelUnbound(int playerNumber)
    {
        if (playerNumber != CS_UIAmbushMarkModel.localPlayerNumber)
            return;

        UnbindModel();
        _view.Hide();
    }

    private void BindModel(CS_UIAmbushMarkModel model)
    {
        if (_model == model)
            return;

        UnbindModel();
        _model = model;

        if (_model == null)
            return;

        _model.target
            .Subscribe(HandleTargetChanged)
            .AddTo(ref _modelDisposables);

        _model.onBurst
            .Subscribe(_ => _view.PlayBurst())
            .AddTo(ref _modelDisposables);
    }

    // 対象が外れた時は、弾けている最中でなければ消す(弾ける時はBurstの通知で先に始まっている)
    private void HandleTargetChanged(Transform target)
    {
        if (target == null && !_view.isBursting)
            _view.Hide();
    }

    private void UnbindModel()
    {
        _modelDisposables.Dispose();
        _modelDisposables = default;
        _model = null;
    }

    protected override void OnDestroy()
    {
        CS_UIAmbushMarkModel.OnBound -= HandleModelBound;
        CS_UIAmbushMarkModel.OnUnbound -= HandleModelUnbound;
        UnbindModel();
        base.OnDestroy();
    }
}
