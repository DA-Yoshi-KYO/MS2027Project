/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-18 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// SAヒントUIのPresenter。
///
/// ・公開されたModelを購読する
/// ・Modelが保持するプレイヤーカメラを使う
/// ・プレイヤーとSAポイントの画面方向を求める
/// ・プレイヤー周囲の小さい楕円上へ矢印を配置する
/// ・CS_SAPoint.standPositionを案内先として使用する
/// ・矢印をSAポイント方向へ回転する
///
/// Model.Bind()は呼ばない。
/// </summary>
[RequireComponent(typeof(CS_UISAHintView))]
public sealed class CS_UISAHintPresenter :
    CS_BasePresenter
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("Canvas座標変換")]

    [SerializeField]
    private RectTransform _canvasRect;

    [SerializeField]
    private Canvas _canvas;

    [Header("プレイヤー表示位置補正")]

    [Tooltip(
        "プレイヤーTransformから、" +
        "矢印楕円の中心にする位置までの補正"
    )]
    [SerializeField]
    private Vector3 _playerWorldOffset =
        new Vector3(
            0f,
            1.2f,
            0f
        );

    [Header("SAポイント位置補正")]

    [Tooltip(
        "CS_SAPoint.standPositionから、" +
        "矢印が指す位置までの補正"
    )]
    [SerializeField]
    private Vector3 _pointWorldOffset =
        Vector3.zero;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UISAHintView _view;

    private CS_UISAHintModel _model;

    private CS_UIAfterCameraUpdate
        _afterCameraUpdate;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _view =
            GetComponent<CS_UISAHintView>();

        _view.SetPresenter(this);

        if (_canvas == null)
        {
            _canvas =
                GetComponentInParent<Canvas>();
        }

        if (_canvasRect == null &&
            _canvas != null)
        {
            _canvasRect =
                _canvas.transform
                as RectTransform;
        }

        _afterCameraUpdate =
            new CS_UIAfterCameraUpdate(
                GetPlayerCamera,
                UpdateIndicators
            );
    }

    private void OnEnable()
    {
        CS_UISAHintModel.OnBound +=
            SubscribeModel;

        CS_UISAHintModel.OnUnbound +=
            UnsubscribeModel;

        _afterCameraUpdate.Enable();

        if (CS_UISAHintModel.TryGet(
                out CS_UISAHintModel model))
        {
            SubscribeModel(model);
        }
        else
        {
            _view.HideAll();
        }
    }

    private void OnDisable()
    {
        CS_UISAHintModel.OnBound -=
            SubscribeModel;

        CS_UISAHintModel.OnUnbound -=
            UnsubscribeModel;

        _afterCameraUpdate.Disable();

        UnsubscribeModel();
    }

    private void LateUpdate()
    {
        _afterCameraUpdate
            .LateUpdateFallback();
    }

    // =========================================================
    // Model購読
    // =========================================================

    /// <summary>
    /// 公開されたModelを購読する。
    /// Model.Bind()を呼ぶ処理ではない。
    /// </summary>
    private void SubscribeModel(
        CS_UISAHintModel model)
    {
        UnsubscribeCurrentModel();

        _model = model;

        if (_model == null)
        {
            _view.HideAll();

            return;
        }

        _model.OnTargetsUpdated +=
            HandleTargetsUpdated;

        _view.Setup(
            _model.data
        );

        _model.RefreshTargets();

        UpdateIndicators();
    }

    /// <summary>
    /// Model購読を解除し、矢印を隠す。
    /// </summary>
    private void UnsubscribeModel()
    {
        UnsubscribeCurrentModel();

        if (_view != null)
        {
            _view.HideAll();
        }
    }

    /// <summary>
    /// 現在のModelからイベント購読だけを解除する。
    /// </summary>
    private void UnsubscribeCurrentModel()
    {
        if (_model == null)
        {
            return;
        }

        _model.OnTargetsUpdated -=
            HandleTargetsUpdated;

        _model = null;
    }

    private void HandleTargetsUpdated()
    {
        UpdateIndicators();
    }

    // =========================================================
    // プレイヤーカメラ
    // =========================================================

    private Camera GetPlayerCamera()
    {
        if (_model == null)
        {
            return null;
        }

        return _model.playerCamera;
    }

    // =========================================================
    // 矢印更新
    // =========================================================

    private void UpdateIndicators()
    {
        if (_model == null ||
            !_model.isHiding ||
            _model.player == null ||
            _model.playerCamera == null ||
            _model.data == null)
        {
            _view.HideAll();

            return;
        }

        if (_canvas == null ||
            _canvasRect == null)
        {
            _view.HideAll();

            return;
        }

        // 隠れている間はactivePointsや距離変化を反映する。
        _model.RefreshTargets();

        Camera playerCamera =
            _model.playerCamera;

        bool playerConverted =
            CS_UIWorldToCanvas.TryConvert(
                playerCamera,
                _canvas,
                _canvasRect,
                _model.player.position +
                _playerWorldOffset,
                out Vector2 playerCanvasPosition
            );

        if (!playerConverted)
        {
            _view.HideAll();

            return;
        }

        int displayCount =
            Mathf.Min(
                _model.targetCount,
                _model.data.maximumDisplayCount,
                _view.GetItemCount()
            );

        int shownCount = 0;

        for (int i = 0;
             i < displayCount;
             i++)
        {
            CS_SAPoint target =
                _model.GetTarget(i);

            if (target == null ||
                !target.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 targetWorldPosition =
                target.standPosition +
                _pointWorldOffset;

            bool targetConverted =
                CS_UIWorldToCanvas.TryConvert(
                    playerCamera,
                    _canvas,
                    _canvasRect,
                    targetWorldPosition,
                    out Vector2 targetCanvasPosition
                );

            if (!targetConverted)
            {
                continue;
            }

            Vector2 direction =
                targetCanvasPosition -
                playerCanvasPosition;

            if (direction.sqrMagnitude <=
                Mathf.Epsilon)
            {
                continue;
            }

            direction.Normalize();

            // プレイヤーを中心とする小さい楕円上へ配置する。
            Vector2 ellipseOffset =
                new Vector2(
                    direction.x *
                    _model.data.horizontalRadius,

                    direction.y *
                    _model.data.verticalRadius
                );

            Vector2 indicatorPosition =
                playerCanvasPosition +
                ellipseOffset;

            float angle =
                Mathf.Atan2(
                    direction.y,
                    direction.x
                ) *
                Mathf.Rad2Deg;

            _view.ShowIndicator(
                index: shownCount,
                anchoredPosition:
                    indicatorPosition,
                angle: angle
            );

            shownCount++;
        }

        // 使用しなかった残りの矢印を非表示にする。
        _view.HideFromIndex(
            shownCount
        );
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        UnsubscribeCurrentModel();

        base.OnDestroy();
    }
}