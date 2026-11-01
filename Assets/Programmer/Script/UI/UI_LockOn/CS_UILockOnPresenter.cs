/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

public enum CSE_LockOnOffscreenMode
{
    Hide = 0,
    ClampToEdge = 1,
}

/// <summary>
/// ローカルPlayer番号にBindされたModelを取得し、
/// 対象のワールド座標をUICanvas上の座標へ変換するPresenter。
/// </summary>
[RequireComponent(typeof(CS_UILockOnView))]
public class CS_UILockOnPresenter : CS_BasePresenter
{
    [Header("座標変換")]
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Camera _targetCamera;

    [Header("対象位置の補正")]
    [SerializeField] private Vector3 _worldOffset = new(0f, 1f, 0f);

    [Header("画面外の扱い")]
    [SerializeField]
    private CSE_LockOnOffscreenMode _offscreenMode =
        CSE_LockOnOffscreenMode.Hide;

    [Min(0f)]
    [SerializeField] private float _edgePadding = 40f;

    private CS_UILockOnView _view;
    private CS_UILockOnModel _model;
    private DisposableBag _disposables;

    private void Awake()
    {
        _view = GetComponent<CS_UILockOnView>();
        _view.SetPresenter(this);
        _view.SetVisible(false, false);

        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        if (_canvasRect == null && _canvas != null)
            _canvasRect = _canvas.transform as RectTransform;
    }

    private void OnEnable()
    {
        CS_UILockOnModel.OnBound += HandleModelBound;
        CS_UILockOnModel.OnUnbound += HandleModelUnbound;
        TryBindLocalModel();
    }

    private void OnDisable()
    {
        CS_UILockOnModel.OnBound -= HandleModelBound;
        CS_UILockOnModel.OnUnbound -= HandleModelUnbound;
        UnbindModel();
        _view?.SetVisible(false, false);
    }

    private void LateUpdate()
    {
        if (_model == null)
        {
            _view.SetVisible(false, false);
            return;
        }

        Transform target = _model.currentTarget;

        if (target == null)
        {
            if (_model.isLockedOn)
                _model.ClearTarget();

            _view.SetVisible(false, false);
            return;
        }

        if (_targetCamera == null)
            _targetCamera = Camera.main;

        if (_targetCamera == null || _canvasRect == null)
        {
            _view.SetVisible(false, false);
            return;
        }

        Vector3 screenPosition = _targetCamera.WorldToScreenPoint(
            target.position + _worldOffset
        );

        if (screenPosition.z <= 0f)
        {
            _view.SetVisible(false, false);
            return;
        }

        bool isOnScreen =
            screenPosition.x >= 0f &&
            screenPosition.x <= Screen.width &&
            screenPosition.y >= 0f &&
            screenPosition.y <= Screen.height;

        if (!isOnScreen &&
            _offscreenMode == CSE_LockOnOffscreenMode.Hide)
        {
            _view.SetVisible(false, false);
            return;
        }

        if (!isOnScreen)
        {
            screenPosition.x = Mathf.Clamp(
                screenPosition.x,
                _edgePadding,
                Screen.width - _edgePadding
            );

            screenPosition.y = Mathf.Clamp(
                screenPosition.y,
                _edgePadding,
                Screen.height - _edgePadding
            );
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
            _view.SetVisible(false, false);
            return;
        }

        _view.SetPosition(localPosition);
        _view.SetVisible(true);
    }

    private void TryBindLocalModel()
    {
        int playerNumber = CS_UILockOnModel.localPlayerNumber;

        if (playerNumber < 0)
            return;

        if (CS_UILockOnModel.TryGet(playerNumber, out CS_UILockOnModel model))
            BindModel(model);
    }

    private void HandleModelBound(
        int playerNumber,
        CS_UILockOnModel model)
    {
        if (playerNumber != CS_UILockOnModel.localPlayerNumber)
            return;

        BindModel(model);
    }

    private void HandleModelUnbound(int playerNumber)
    {
        if (playerNumber != CS_UILockOnModel.localPlayerNumber)
            return;

        UnbindModel();
        _view.SetVisible(false, false);
    }

    private void BindModel(CS_UILockOnModel model)
    {
        if (_model == model)
            return;

        UnbindModel();
        _model = model;

        if (_model == null)
            return;

        _model.target
            .Subscribe(HandleTargetChanged)
            .AddTo(ref _disposables);
    }

    private void HandleTargetChanged(Transform target)
    {
        if (target == null)
            _view.SetVisible(false, false);
    }

    private void UnbindModel()
    {
        _disposables.Dispose();
        _disposables = default;
        _model = null;
    }

    protected override void OnDestroy()
    {
        CS_UILockOnModel.OnBound -= HandleModelBound;
        CS_UILockOnModel.OnUnbound -= HandleModelUnbound;
        UnbindModel();
        base.OnDestroy();
    }
}
