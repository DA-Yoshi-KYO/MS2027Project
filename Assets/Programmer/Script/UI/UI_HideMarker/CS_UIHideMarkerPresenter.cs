/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * 2026-11-02 | カメラの位置が確定した後に目印の位置を計算するようにした(目印が震える対策)
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// ハイドマーカーの Presenter
/// Bind されたModelの目印の位置(ワールド)を、毎フレームUICanvas上の座標へ変換してViewに渡す
///
/// ・Model・UIのどちらが先に生成されても紐づく
/// ・カメラの後ろ・画面の外にある時は出さない
/// ・位置の計算はカメラの位置が確定した後に行う(CS_UIAfterCameraUpdate。先に計算すると画面揺れなどで目印が震える)
/// Modelの破棄は持ち主(使用者)が行うので、ここでは購読解除だけ行う
/// </summary>
[RequireComponent(typeof(CS_UIHideMarkerView))]
public class CS_UIHideMarkerPresenter : CS_BasePresenter
{
    [Header("座標変換")]
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Camera _targetCamera;     // 空ならCamera.mainを使う

    [Header("目印の位置の補正")]
    [SerializeField] private Vector3 _worldOffset = new(0f, 1.6f, 0f);   // ハイドポイントの立つ位置から、目印を出す位置までのずれ(m)

    private CS_UIHideMarkerView _view;
    private CS_UIHideMarkerModel _model;

    //今紐づいているModelの購読(新しいModelを入れると前の購読は自動で解除される)
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    private CS_UIAfterCameraUpdate _afterCameraUpdate;

    private void Awake()
    {
        _view = GetComponent<CS_UIHideMarkerView>();
        _view.SetPresenter(this);
        _modelSubscription.AddTo(_disposables);
        _afterCameraUpdate = new CS_UIAfterCameraUpdate(GetTargetCamera, UpdateMarker);

        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        if (_canvasRect == null && _canvas != null)
            _canvasRect = _canvas.transform as RectTransform;
    }

    private void OnEnable()
    {
        CS_UIHideMarkerModel.OnBound += BindModel;
        CS_UIHideMarkerModel.OnUnbound += UnbindModel;
        _afterCameraUpdate.Enable();

        // UIより先にModelがBindされていた場合
        if (CS_UIHideMarkerModel.TryGet(out CS_UIHideMarkerModel model))
            BindModel(model);
        else
            _view.SetVisible(false);
    }

    private void OnDisable()
    {
        CS_UIHideMarkerModel.OnBound -= BindModel;
        CS_UIHideMarkerModel.OnUnbound -= UnbindModel;
        _afterCameraUpdate.Disable();
        UnbindModel();
    }

    private void LateUpdate()
    {
        // Brainが動いていないシーンでは、ここで計算する
        _afterCameraUpdate.LateUpdateFallback();
    }

    // 目印を出すカメラ(空ならCamera.main)
    private Camera GetTargetCamera()
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;

        return _targetCamera;
    }

    // 目印の位置をUICanvas上の座標へ変換して、Viewに渡す
    private void UpdateMarker()
    {
        Transform target =
            _model != null
                ? _model.currentTarget
                : null;

        if (target == null)
        {
            _view.SetVisible(false);
            return;
        }

        bool isVisible =
            CS_UIWorldToCanvas.TryConvert(
                GetTargetCamera(),
                _canvas,
                _canvasRect,
                target.position + _worldOffset,
                out Vector2 localPosition
            );

        if (isVisible)
        {
            _view.SetPosition(
                localPosition
            );
        }

        _view.SetVisible(
            isVisible
        );
    }

    //Modelの目印を購読する(目印が無くなったら隠す。位置はUpdateMarkerで追う)
    private void BindModel(CS_UIHideMarkerModel model)
    {
        _model = model;
        _modelSubscription.Disposable = model.target.Subscribe(target =>
        {
            if (target == null)
                _view.SetVisible(false);
        });
    }

    //Modelの購読をやめて隠す
    private void UnbindModel()
    {
        _model = null;
        _modelSubscription.Disposable = null;
        _view.SetVisible(false);
    }
}
