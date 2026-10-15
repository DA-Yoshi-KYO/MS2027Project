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
/// 警察視認警告UIのController。
/// ・本番ではPlayerとPoliceから登録を受ける
/// ・デバッグモードではPlayer・Police側の実装なしで表示確認できる
/// </summary>
public class CS_PoliceDetectionController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("警察視認警告UIのPresenter")]
    [SerializeField]
    private CS_UIPoliceDetectionPresenter _presenter;

    [Header("デバッグ機能")]
    [SerializeField] private bool _enableDebugMode = true;

    [Header("デバッグ基準位置（未設定ならController自身）")]
    [SerializeField] private Transform _debugPlayerTransform;

    [Header("デバッグ警察との距離")]
    [Min(0.1f)]
    [SerializeField] private float _debugPoliceDistance = 15f;

    [Header("デバッグ警察の回転速度（度/秒）")]
    [SerializeField] private float _debugRotateSpeed = 30f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIPoliceDetectionModel _model;
    private Transform _playerTransform;

    private readonly Dictionary<string, DebugPoliceInfo>
        _debugPoliceMap = new();

    private Transform _debugPoliceRoot;
    private float _debugRotationOffset;
    private float _savedDebugRotateSpeed = 30f;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _model = new CS_UIPoliceDetectionModel();
    }

    private void Start()
    {
        if (_presenter == null)
        {
            Debug.LogError(
                "[CS_PoliceDetectionController] Presenterが未設定です。"
            );
            return;
        }

        _presenter.BindModel(_model);

        if (_enableDebugMode)
        {
            SetupDebugMode();
            return;
        }

        // PlayerがControllerのStartより先に登録された場合にも対応する。
        if (_playerTransform != null)
            _presenter.SetPlayerTransform(_playerTransform);
    }

    private void Update()
    {
        if (!_enableDebugMode)
            return;

        UpdateDebugPolice();
        HandleDebugInput();
    }

    // =========================================================
    // デバッグモード
    // =========================================================

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

        _savedDebugRotateSpeed =
            Mathf.Approximately(_debugRotateSpeed, 0f)
                ? 30f
                : _debugRotateSpeed;

        // 初期状態として、前・右・後ろへシグナル検知中の警察を配置する。
        CreateDebugPolice(
            "DebugPolice_Front",
            0f,
            _debugPoliceDistance,
            CSE_PoliceDetectionState.SignalDetected
        );

        CreateDebugPolice(
            "DebugPolice_Right",
            90f,
            _debugPoliceDistance,
            CSE_PoliceDetectionState.SignalDetected
        );

        CreateDebugPolice(
            "DebugPolice_Back",
            180f,
            _debugPoliceDistance,
            CSE_PoliceDetectionState.SignalDetected
        );

        Debug.Log(
            "[PoliceDetection Debug] " +
            "1:前方追加 / 2:右追加 / 3:後方追加 / " +
            "4:全削除 / 5:結合確認 / 6:発見状態 / " +
            "7:5m以内赤色 / Space:回転停止・再開"
        );
    }

    private void CreateDebugPolice(
        string policeId,
        float baseAngleDegree,
        float distance,
        CSE_PoliceDetectionState policeState)
    {
        if (_debugPoliceMap.ContainsKey(policeId))
            return;

        if (_debugPoliceRoot == null)
            return;

        var policeObject = new GameObject(policeId);
        Transform policeTransform = policeObject.transform;
        policeTransform.SetParent(_debugPoliceRoot, false);

        var debugInfo = new DebugPoliceInfo(
            policeTransform,
            baseAngleDegree,
            distance,
            policeState
        );

        _debugPoliceMap.Add(policeId, debugInfo);

        UpdateDebugPolicePosition(debugInfo);

        RegisterPolice(
            policeId,
            policeTransform,
            policeState
        );
    }

    private void UpdateDebugPolicePosition(DebugPoliceInfo debugInfo)
    {
        if (_playerTransform == null ||
            debugInfo == null ||
            debugInfo.transform == null)
        {
            return;
        }

        float angleDegree =
            debugInfo.baseAngleDegree +
            _debugRotationOffset;

        float angleRadians = angleDegree * Mathf.Deg2Rad;

        Vector3 localDirection = new Vector3(
            Mathf.Sin(angleRadians),
            0f,
            Mathf.Cos(angleRadians)
        );

        Vector3 worldDirection =
            _playerTransform.TransformDirection(localDirection);

        debugInfo.transform.position =
            _playerTransform.position +
            worldDirection * debugInfo.distance;
    }

    private void UpdateDebugPolice()
    {
        if (_playerTransform == null)
            return;

        _debugRotationOffset +=
            _debugRotateSpeed * Time.deltaTime;

        foreach (var pair in _debugPoliceMap)
            UpdateDebugPolicePosition(pair.Value);
    }

    private void HandleDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            CreateDebugPolice(
                $"DebugPolice_Front_{_debugPoliceMap.Count}",
                0f,
                _debugPoliceDistance,
                CSE_PoliceDetectionState.SignalDetected
            );
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            CreateDebugPolice(
                $"DebugPolice_Right_{_debugPoliceMap.Count}",
                90f,
                _debugPoliceDistance,
                CSE_PoliceDetectionState.SignalDetected
            );
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            CreateDebugPolice(
                $"DebugPolice_Back_{_debugPoliceMap.Count}",
                180f,
                _debugPoliceDistance,
                CSE_PoliceDetectionState.SignalDetected
            );
        }

        // 仮警察と対応するUIをすべて削除する。
        if (Input.GetKeyDown(KeyCode.Alpha4))
            ClearDebugPolice();

        // 角度差5度、距離差1mの2体を作成して「×2」を確認する。
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            ClearDebugPolice();

            CreateDebugPolice(
                "DebugPolice_GroupA",
                0f,
                8f,
                CSE_PoliceDetectionState.SignalDetected
            );

            CreateDebugPolice(
                "DebugPolice_GroupB",
                5f,
                9f,
                CSE_PoliceDetectionState.SignalDetected
            );

            CreateDebugPolice(
                "DebugPolice_Back",
                180f,
                20f,
                CSE_PoliceDetectionState.SignalDetected
            );
        }

        // 発見状態を登録する。アニメーション関数呼び出し確認用。
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            ClearDebugPolice();

            CreateDebugPolice(
                "DebugPolice_Found",
                0f,
                10f,
                CSE_PoliceDetectionState.Found
            );
        }

        // 5m以内を登録して赤色表示を確認する。
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            ClearDebugPolice();

            CreateDebugPolice(
                "DebugPolice_Danger",
                0f,
                4f,
                CSE_PoliceDetectionState.SignalDetected
            );
        }

        if (Input.GetKeyDown(KeyCode.Space))
            ToggleDebugRotation();
    }

    private void ToggleDebugRotation()
    {
        if (Mathf.Approximately(_debugRotateSpeed, 0f))
        {
            _debugRotateSpeed = _savedDebugRotateSpeed;
        }
        else
        {
            _savedDebugRotateSpeed = _debugRotateSpeed;
            _debugRotateSpeed = 0f;
        }
    }

    private void ClearDebugPolice()
    {
        foreach (var pair in _debugPoliceMap)
        {
            _model?.UnregisterPolice(pair.Key);

            if (pair.Value != null &&
                pair.Value.transform != null)
            {
                Destroy(pair.Value.transform.gameObject);
            }
        }

        _debugPoliceMap.Clear();
    }

    // =========================================================
    // 本番用 外部API
    // =========================================================

    public void SetPlayerTransform(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        _playerTransform = playerTransform;

        if (_presenter != null)
            _presenter.SetPlayerTransform(_playerTransform);
    }

    /// <summary>
    /// シグナル検知または発見時に登録する。
    /// Hiddenを渡した場合は表示しない。
    /// </summary>
    public void RegisterPolice(
        string policeId,
        Transform policeTransform,
        CSE_PoliceDetectionState policeState)
    {
        if (_model == null ||
            policeTransform == null ||
            string.IsNullOrWhiteSpace(policeId))
        {
            return;
        }

        _model.RegisterPolice(
            new CS_PoliceDetectionData(
                policeId,
                policeTransform,
                policeState
            )
        );
    }

    public void UnregisterPolice(string policeId)
    {
        _model?.UnregisterPolice(policeId);
    }

    public void SetPoliceState(
        string policeId,
        CSE_PoliceDetectionState policeState)
    {
        _model?.SetPoliceState(policeId, policeState);
    }

    // =========================================================
    // 終了処理
    // =========================================================

    private void OnDestroy()
    {
        ClearDebugPolice();
        _model?.Dispose();
    }

    // =========================================================
    // デバッグ用データ
    // =========================================================

    private class DebugPoliceInfo
    {
        private readonly Transform _transform;
        private readonly float _baseAngleDegree;
        private readonly float _distance;
        private readonly CSE_PoliceDetectionState _policeState;

        public Transform transform => _transform;
        public float baseAngleDegree => _baseAngleDegree;
        public float distance => _distance;
        public CSE_PoliceDetectionState policeState => _policeState;

        public DebugPoliceInfo(
            Transform transform,
            float baseAngleDegree,
            float distance,
            CSE_PoliceDetectionState policeState)
        {
            _transform = transform;
            _baseAngleDegree = baseAngleDegree;
            _distance = distance;
            _policeState = policeState;
        }
    }
}
