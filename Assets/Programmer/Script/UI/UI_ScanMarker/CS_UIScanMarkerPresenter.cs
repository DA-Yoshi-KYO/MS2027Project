/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * 2026-11-02 | 種類・距離・犯罪完遂度もViewに渡すようにした
 * 2026-11-02 | カメラの位置が確定した後に目印の位置を計算するようにした(目印が震える対策)
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキャンの目印の Presenter
/// Bind されたModelの表示内容の一覧を毎フレーム読み、頭上の位置をUICanvas上の座標へ変換して、
/// 種類・距離・犯罪完遂度と一緒にViewに渡す
///
/// ・Model・UIのどちらが先に生成されても紐づく
/// ・カメラの後ろ・画面の外にいる相手には出さない
///   画面に直接重ねるので、建物の向こうにいても見える(仕様「壁や障害物を越えて画面に表示」)
/// ・位置の計算はカメラの位置が確定した後に行う(CS_UIAfterCameraUpdate。先に計算すると画面揺れなどで目印が震える)
/// Modelの破棄は持ち主(使用者)が行う
/// </summary>
[RequireComponent(typeof(CS_UIScanMarkerView))]
public class CS_UIScanMarkerPresenter :
    CS_BasePresenter
{
    [Header("座標変換")]
    [SerializeField]
    private RectTransform _canvasRect;

    [SerializeField]
    private Canvas _canvas;

    [SerializeField]
    private Camera _targetCamera;     // 空ならCamera.mainを使う

    [Header("目印の位置の補正")]
    [SerializeField]
    private Vector3 _worldOffset =
        new(0f, 2.2f, 0f);   // 相手の足元から、目印を出す位置までのずれ(m)

    // =========================================================
    // 追加：時間差表示
    // =========================================================

    [Header("スキャン波による時間差表示")]

    [Tooltip("スキャン波が1秒間に進む距離(m)")]
    [Min(0.01f)]
    [SerializeField]
    private float _scanWaveSpeed = 20f;

    [Tooltip(
        "スキャン波が対象を通過してから、" +
        "アイコンを表示するまでの待機時間"
    )]
    [Min(0f)]
    [SerializeField]
    private float _markerRevealDelay = 0.0f;

    private CS_UIScanMarkerView _view;
    private CS_UIScanMarkerModel _model;
    private CS_UIAfterCameraUpdate _afterCameraUpdate;

    // =========================================================
    // 追加：距離順に並べるための作業用リスト
    // =========================================================

    private readonly List<CS_UIScanMarkerData>
        _sortedTargets =
            new List<CS_UIScanMarkerData>();

    // =========================================================
    // 追加：スキャン開始からの経過時間
    // =========================================================

    private float _scanElapsedTime;
    private bool _isScanSequenceActive;

    private void Awake()
    {
        _view =
            GetComponent<CS_UIScanMarkerView>();

        _view.SetPresenter(this);

        _afterCameraUpdate =
            new CS_UIAfterCameraUpdate(
                GetTargetCamera,
                UpdateMarkers
            );

        if (_canvas == null)
            _canvas =
                GetComponentInParent<Canvas>();

        if (_canvasRect == null &&
            _canvas != null)
        {
            _canvasRect =
                _canvas.transform as RectTransform;
        }
    }

    private void OnEnable()
    {
        CS_UIScanMarkerModel.OnBound +=
            BindModel;

        CS_UIScanMarkerModel.OnUnbound +=
            UnbindModel;

        _afterCameraUpdate.Enable();

        // UIより先にModelがBindされていた場合
        if (CS_UIScanMarkerModel.TryGet(
            out CS_UIScanMarkerModel model))
        {
            BindModel(model);
        }
        else
        {
            _view.HideFrom(0);
        }
    }

    private void OnDisable()
    {
        CS_UIScanMarkerModel.OnBound -=
            BindModel;

        CS_UIScanMarkerModel.OnUnbound -=
            UnbindModel;

        _afterCameraUpdate.Disable();

        UnbindModel();
    }

    private void LateUpdate()
    {
        // Brainが動いていないシーンでは、ここで計算する
        _afterCameraUpdate
            .LateUpdateFallback();
    }

    // 目印を出すカメラ(空ならCamera.main)
    private Camera GetTargetCamera()
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;

        return _targetCamera;
    }

    // 相手の頭上の位置をUICanvas上の座標へ変換して、目印を出す
    private void UpdateMarkers()
    {
        if (_model == null)
            return;

        IReadOnlyList<CS_UIScanMarkerData>
            targets = _model.targets;

        // 対象が無い場合は全非表示にし、
        // 次のスキャンに備えて時間をリセットする。
        if (targets == null ||
            targets.Count == 0)
        {
            ResetScanSequence();
            _view.HideFrom(0);
            return;
        }

        Camera targetCamera =
            GetTargetCamera();

        if (targetCamera == null)
        {
            _view.HideFrom(0);
            return;
        }

        // 対象が初めて入ったフレームを
        // スキャン開始時刻として扱う。
        if (!_isScanSequenceActive)
        {
            BeginScanSequence();
        }

        _scanElapsedTime +=
            Time.unscaledDeltaTime;

        // =====================================================
        // 追加：近い順へ並べる
        // =====================================================

        _sortedTargets.Clear();

        for (int i = 0;
             i < targets.Count;
             i++)
        {
            _sortedTargets.Add(
                targets[i]
            );
        }

        _sortedTargets.Sort(
            CompareByDistance
        );

        int count = 0;

        for (int i = 0;
             i < _sortedTargets.Count;
             i++)
        {
            CS_UIScanMarkerData target =
                _sortedTargets[i];

            // =================================================
            // 追加：距離に応じた表示時刻
            // =================================================

            float revealTime =
                CalculateRevealTime(
                    target.distance
                );

            // まだスキャン波が到達していない、
            // または到達後の待機中なら表示しない。
            if (_scanElapsedTime <
                revealTime)
            {
                continue;
            }

            if (!CS_UIWorldToCanvas.TryConvert(
                targetCamera,
                _canvas,
                _canvasRect,
                target.worldPosition +
                    _worldOffset,
                out Vector2 localPosition))
            {
                continue;
            }

            _view.ShowMarker(
                count,
                localPosition,
                target
            );

            count++;
        }

        // 使わなかった目印は隠す
        _view.HideFrom(count);
    }

    // =========================================================
    // 追加：距離順
    // =========================================================

    private static int CompareByDistance(
        CS_UIScanMarkerData left,
        CS_UIScanMarkerData right)
    {
        return left.distance.CompareTo(
            right.distance
        );
    }

    // =========================================================
    // 追加：スキャン波の到達時間
    // =========================================================

    private float CalculateRevealTime(
        float distance)
    {
        float safeDistance =
            Mathf.Max(
                0f,
                distance
            );

        float safeWaveSpeed =
            Mathf.Max(
                0.01f,
                _scanWaveSpeed
            );

        float waveArrivalTime =
            safeDistance /
            safeWaveSpeed;

        return
            waveArrivalTime +
            _markerRevealDelay;
    }

    // =========================================================
    // 追加：スキャン表示の開始・リセット
    // =========================================================

    private void BeginScanSequence()
    {
        _isScanSequenceActive = true;
        _scanElapsedTime = 0f;

        // 前回表示していたアイコンがある場合でも、
        // 今回のスキャンでは近い対象から表示し直す。
        _view.HideFrom(0);
    }

    private void ResetScanSequence()
    {
        _isScanSequenceActive = false;
        _scanElapsedTime = 0f;
        _sortedTargets.Clear();
    }

    private void BindModel(
        CS_UIScanMarkerModel model)
    {
        _model = model;

        // 追加：
        // 新しいModelへBindされた時は、
        // スキャン表示を最初から開始する。
        ResetScanSequence();
    }

    //Modelの紐づけをやめて、目印をすべて隠す
    private void UnbindModel()
    {
        _model = null;

        // 追加：
        // 次のBind時に近い対象から表示し直す。
        ResetScanSequence();

        _view.HideFrom(0);
    }

    private void OnValidate()
    {
        _scanWaveSpeed =
            Mathf.Max(
                0.01f,
                _scanWaveSpeed
            );

        _markerRevealDelay =
            Mathf.Max(
                0f,
                _markerRevealDelay
            );
    }
}