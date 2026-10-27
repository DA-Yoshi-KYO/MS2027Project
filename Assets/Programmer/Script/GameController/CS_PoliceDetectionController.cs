/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-13 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 警察方向UIのController。
/// 本番APIとデバッグ確認機能を持つ。
/// </summary>
public class CS_PoliceDetectionController : MonoBehaviour
{
    [Header("Presenter")]
    [SerializeField] private CS_UIPoliceDetectionPresenter _presenter;

    [Header("デバッグ機能")]
    [SerializeField] private bool _enableDebugMode;
    [SerializeField] private Transform _debugPlayerTransform;

    [Min(0.1f)]
    [SerializeField] private float _debugPoliceDistance = 15f;

    [SerializeField] private float _debugRotateSpeed = 30f;

    private CS_UIPoliceDetectionModel _model;
    private Transform _playerTransform;
    private Transform _debugPoliceRoot;
    private float _debugRotationOffset;

    private readonly Dictionary<string, DebugPoliceInfo>
        _debugPoliceMap = new();

    private void Awake()
    {
        _model = new CS_UIPoliceDetectionModel();
    }

    private void Start()
    {
        if (_presenter == null)
        {
            Debug.LogError(
                "[CS_PoliceDetectionController] Presenterが未設定です。",
                this
            );
            return;
        }

        _presenter.BindModel(_model);

        if (_enableDebugMode)
            SetupDebugMode();
        else if (_playerTransform != null)
            _presenter.SetPlayerTransform(_playerTransform);
    }

    private void Update()
    {
        if (!_enableDebugMode)
            return;

        UpdateDebugPolice();
        HandleDebugInput();
    }

    public void SetPlayerTransform(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        _playerTransform = playerTransform;
        _presenter?.SetPlayerTransform(playerTransform);
    }

    /// <summary>
    /// 警察生成時に登録する。巡回状態ならUIには表示されない。
    /// </summary>
    public void RegisterPolice(
        string policeId,
        Transform policeTransform,
        CSE_PoliceDetectionState initialState =
            CSE_PoliceDetectionState.Patrol)
    {
        if (string.IsNullOrWhiteSpace(policeId) ||
            policeTransform == null)
        {
            return;
        }

        _model.RegisterPolice(
            new CS_PoliceDetectionData(
                policeId,
                policeTransform,
                initialState
            )
        );
    }

    public void SetPoliceState(
        string policeId,
        CSE_PoliceDetectionState state)
    {
        _model.SetPoliceState(policeId, state);
    }

    public void UnregisterPolice(string policeId)
    {
        _model.UnregisterPolice(policeId);
    }

    private void SetupDebugMode()
    {
        _playerTransform =
            _debugPlayerTransform != null
                ? _debugPlayerTransform
                : transform;

        _presenter.SetPlayerTransform(_playerTransform);

        var rootObject = new GameObject("DebugPoliceRoot");
        _debugPoliceRoot = rootObject.transform;
        _debugPoliceRoot.SetParent(transform, false);

        CreateDebugPolice(
            "DebugPatrol",
            0f,
            10f,
            CSE_PoliceDetectionState.Patrol
        );

        CreateDebugPolice(
            "DebugTracking",
            90f,
            15f,
            CSE_PoliceDetectionState.Tracking
        );
    }

    private void HandleDebugInput()
    {
        // 1：巡回中。表示なし
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ClearDebugPolice();
            CreateDebugPolice(
                "DebugPatrol",
                0f,
                10f,
                CSE_PoliceDetectionState.Patrol
            );
        }

        // 2：追跡中・5mより遠い。黄色
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ClearDebugPolice();
            CreateDebugPolice(
                "DebugTrackingYellow",
                0f,
                10f,
                CSE_PoliceDetectionState.Tracking
            );
        }

        // 3：追跡中・5m以内。赤
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ClearDebugPolice();
            CreateDebugPolice(
                "DebugTrackingRed",
                0f,
                4f,
                CSE_PoliceDetectionState.Tracking
            );
        }

        // 4：全削除
        if (Input.GetKeyDown(KeyCode.Alpha4))
            ClearDebugPolice();

        // 5：同方向・近距離の2体を結合
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            ClearDebugPolice();
            CreateDebugPolice(
                "DebugGroupA",
                0f,
                8f,
                CSE_PoliceDetectionState.Tracking
            );
            CreateDebugPolice(
                "DebugGroupB",
                5f,
                9f,
                CSE_PoliceDetectionState.Tracking
            );
        }
    }

    private void CreateDebugPolice(
        string id,
        float angle,
        float distance,
        CSE_PoliceDetectionState state)
    {
        if (_debugPoliceRoot == null)
            return;

        var policeObject = new GameObject(id);
        policeObject.transform.SetParent(_debugPoliceRoot, false);

        var info = new DebugPoliceInfo(
            policeObject.transform,
            angle,
            distance
        );

        _debugPoliceMap[id] = info;
        UpdateDebugPolicePosition(info);
        RegisterPolice(id, info.transform, state);
    }

    private void UpdateDebugPolice()
    {
        _debugRotationOffset += _debugRotateSpeed * Time.deltaTime;

        foreach (var pair in _debugPoliceMap)
            UpdateDebugPolicePosition(pair.Value);
    }

    private void UpdateDebugPolicePosition(DebugPoliceInfo info)
    {
        if (_playerTransform == null || info.transform == null)
            return;

        float radians =
            (info.baseAngle + _debugRotationOffset) * Mathf.Deg2Rad;

        Vector3 localDirection = new(
            Mathf.Sin(radians),
            0f,
            Mathf.Cos(radians)
        );

        info.transform.position =
            _playerTransform.position +
            _playerTransform.TransformDirection(localDirection) *
            info.distance;
    }

    private void ClearDebugPolice()
    {
        foreach (var pair in _debugPoliceMap)
        {
            _model.UnregisterPolice(pair.Key);

            if (pair.Value.transform != null)
                Destroy(pair.Value.transform.gameObject);
        }

        _debugPoliceMap.Clear();
    }

    private void OnDestroy()
    {
        ClearDebugPolice();
        _model?.Dispose();
    }

    private sealed class DebugPoliceInfo
    {
        private readonly Transform _transform;
        private readonly float _baseAngle;
        private readonly float _distance;

        public Transform transform => _transform;
        public float baseAngle => _baseAngle;
        public float distance => _distance;

        public DebugPoliceInfo(
            Transform transform,
            float baseAngle,
            float distance)
        {
            _transform = transform;
            _baseAngle = baseAngle;
            _distance = distance;
        }
    }
}
