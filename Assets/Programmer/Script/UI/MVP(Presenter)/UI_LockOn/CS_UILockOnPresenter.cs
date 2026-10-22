/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// ロックオンModelとWorld Space Viewを接続するPresenter。
/// LockOnCanvasの生成後にBindTarget()を呼ぶ。
/// </summary>
[RequireComponent(typeof(CS_UILockOnView))]
public class CS_UILockOnPresenter : CS_BasePresenter
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("カメラ（未設定ならCamera.mainを使用）")]
    [SerializeField] private Camera _targetCamera;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UILockOnModel _model;
    private CS_UILockOnView _view;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _model = new CS_UILockOnModel();
        _view = GetComponent<CS_UILockOnView>();

        _view.SetPresenter(this);
        _model.OnLockOnVisibilityChanged +=
            HandleLockOnVisibilityChanged;

        _view.SetVisible(false);
    }

    // =========================================================
    // 更新
    // =========================================================

    private void LateUpdate()
    {
        if (!_model.isLockedOn)
            return;

        // 対象が破棄された場合は自動で解除する。
        if (_model.targetTransform == null)
        {
            ClearTarget();
            return;
        }

        if (_targetCamera == null)
            _targetCamera = Camera.main;

        _view.LookAtCamera(_targetCamera);
    }

    // =========================================================
    // 外部API
    // =========================================================

    /// <summary>
    /// ロックオン対象を設定し、LockOnCanvasを対象の子へ移動する。
    /// targetTransformには対象側のLockOnPositionを渡す。
    /// </summary>
    public void BindTarget(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            ClearTarget();
            return;
        }

        transform.SetParent(targetTransform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        _model.SetTarget(targetTransform);

        // 同じ表示状態から別ターゲットへ切り替えた場合にも表示を保証する。
        _view.SetVisible(true);
    }

    /// <summary>
    /// ロックオンを解除し、UIを非表示にする。
    /// </summary>
    public void ClearTarget()
    {
        _model.ClearTarget();
        _view.SetVisible(false);
    }

    public void SetTargetCamera(Camera targetCamera)
    {
        _targetCamera = targetCamera;
    }

    // =========================================================
    // Modelイベント
    // =========================================================

    private void HandleLockOnVisibilityChanged(bool isVisible)
    {
        _view.SetVisible(isVisible);
    }

    // =========================================================
    // 終了処理
    // =========================================================

    protected override void OnDestroy()
    {
        if (_model != null)
        {
            _model.OnLockOnVisibilityChanged -=
                HandleLockOnVisibilityChanged;

            _model.Dispose();
        }

        base.OnDestroy();
    }
}
