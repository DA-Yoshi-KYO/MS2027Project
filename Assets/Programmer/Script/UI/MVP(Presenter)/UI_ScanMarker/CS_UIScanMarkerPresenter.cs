/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキャンの目印の Presenter
/// Bind されたModelの相手の一覧を毎フレーム読み、頭上の位置をUICanvas上の座標へ変換してViewに渡す
///
/// ・Model・UIのどちらが先に生成されても紐づく
/// ・カメラの後ろ・画面の外にいる相手には出さない
///   画面に直接重ねるので、建物の向こうにいても見える(仕様「壁や障害物を越えて画面に表示」)
/// Modelの破棄は持ち主(使用者)が行う
/// </summary>
[RequireComponent(typeof(CS_UIScanMarkerView))]
public class CS_UIScanMarkerPresenter : CS_BasePresenter
{
    [Header("座標変換")]
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Camera _targetCamera;     // 空ならCamera.mainを使う

    [Header("目印の位置の補正")]
    [SerializeField] private Vector3 _worldOffset = new(0f, 2.2f, 0f);   // 相手の足元から、目印を出す位置までのずれ(m)

    private CS_UIScanMarkerView _view;
    private CS_UIScanMarkerModel _model;

    private void Awake()
    {
        _view = GetComponent<CS_UIScanMarkerView>();
        _view.SetPresenter(this);

        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        if (_canvasRect == null && _canvas != null)
            _canvasRect = _canvas.transform as RectTransform;
    }

    private void OnEnable()
    {
        CS_UIScanMarkerModel.OnBound += BindModel;
        CS_UIScanMarkerModel.OnUnbound += UnbindModel;

        // UIより先にModelがBindされていた場合
        if (CS_UIScanMarkerModel.TryGet(out CS_UIScanMarkerModel model))
            BindModel(model);
        else
            _view.HideFrom(0);
    }

    private void OnDisable()
    {
        CS_UIScanMarkerModel.OnBound -= BindModel;
        CS_UIScanMarkerModel.OnUnbound -= UnbindModel;
        UnbindModel();
    }

    private void LateUpdate()
    {
        if (_model == null)
            return;

        if (_targetCamera == null)
            _targetCamera = Camera.main;

        int count = 0;
        IReadOnlyList<Transform> targets = _model.targets;
        for (int i = 0; i < targets.Count; i++)
        {
            Transform target = targets[i];
            if (target == null)
                continue;

            if (!CS_UIWorldToCanvas.TryConvert(
                _targetCamera,
                _canvas,
                _canvasRect,
                target.position + _worldOffset,
                out Vector2 localPosition))
                continue;

            _view.ShowMarker(count, localPosition);
            count++;
        }

        // 使わなかった目印は隠す
        _view.HideFrom(count);
    }

    private void BindModel(CS_UIScanMarkerModel model)
    {
        _model = model;
    }

    //Modelの紐づけをやめて、目印をすべて隠す
    private void UnbindModel()
    {
        _model = null;
        _view.HideFrom(0);
    }
}
