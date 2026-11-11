/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-28 | 初回作成
 * 2026-10-16 | マップの回転の基準を、プレイヤー本体とは別に渡せるようにした(カメラの向きで回すため。秋野)
 * ================================================ */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ミニマップのController。
/// プレイヤー、悪人、他プレイヤー、警察、アイテム、
/// ランダムイベントを管理する。
/// </summary>
public class CS_MiniMapController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ローカルプレイヤー")]
    [SerializeField]
    private Transform _localPlayerTransform;

    [SerializeField]
    private Transform _localPlayerViewTransform;

    [Header("UICanvas配下のMiniMap")]
    [SerializeField]
    private CS_UIMiniMapView _miniMapView;

    [Header("ミニマップ設定")]
    [Min(0.01f)]
    [SerializeField]
    private float _mapRadius = 50f;

    [Min(0.01f)]
    [SerializeField]
    private float _mapWorldSize = 240f;

    [SerializeField]
    private bool _rotateWithPlayer = true;

    [Header("更新間隔")]
    [Min(0.01f)]
    [SerializeField]
    private float _updateInterval = 0.05f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIMiniMapModel _model;
    private CS_UIMiniMapPresenter _presenter;

    private readonly Dictionary<string, Transform>
        _enemyTransforms = new();

    private readonly Dictionary<string, Transform>
        _allyTransforms = new();

    private readonly Dictionary<string, Transform>
        _policeTransforms = new();

    private readonly Dictionary<string, Transform>
        _itemTransforms = new();

    private readonly Dictionary<string, Transform>
        _randomEventTransforms = new();

    private float _updateTimer;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _model = new CS_UIMiniMapModel(
            _mapRadius,
            _mapWorldSize,
            _rotateWithPlayer
        );
    }

    private void Start()
    {
        if (_miniMapView == null)
        {
            Debug.LogError(
                "[CS_MiniMapController] " +
                "_miniMapViewが未設定です。",
                this
            );
            return;
        }

        _presenter =
            _miniMapView.GetComponent<
                CS_UIMiniMapPresenter
            >();

        if (_presenter == null)
        {
            Debug.LogError(
                "[CS_MiniMapController] " +
                "MiniMapにPresenterがありません。",
                this
            );
            return;
        }

        _presenter.SetView(_miniMapView);
        _model.OnDataChanged += OnModelDataChanged;

        SyncPositions();
    }

    // =========================================================
    // 更新
    // =========================================================

    private void Update()
    {
        _updateTimer += Time.deltaTime;

        if (_updateTimer < _updateInterval)
            return;

        _updateTimer = 0f;
        SyncPositions();
    }

    private void SyncPositions()
    {
        if (_model == null)
            return;

        if (_localPlayerTransform != null)
        {
            Transform rotationSource =
                _localPlayerViewTransform != null
                    ? _localPlayerViewTransform
                    : _localPlayerTransform;

            _model.SetLocalPlayer(
                _localPlayerTransform.position,
                rotationSource.eulerAngles.y
            );
        }

        SyncEntityPositions(
            _enemyTransforms,
            _model.AddOrUpdateEnemy
        );

        SyncEntityPositions(
            _allyTransforms,
            _model.AddOrUpdateMultiplayerAlly
        );

        SyncEntityPositions(
            _policeTransforms,
            _model.AddOrUpdatePolice
        );

        SyncEntityPositions(
            _itemTransforms,
            _model.AddOrUpdateItem
        );

        SyncEntityPositions(
            _randomEventTransforms,
            _model.AddOrUpdateRandomEvent
        );
    }

    private static void SyncEntityPositions(
        Dictionary<string, Transform> transforms,
        Action<string, Vector3, float> updateAction)
    {
        foreach (var pair in transforms)
        {
            if (pair.Value == null)
                continue;

            updateAction(
                pair.Key,
                pair.Value.position,
                pair.Value.eulerAngles.y
            );
        }
    }

    private void OnModelDataChanged()
    {
        _presenter?.OnModelUpdated(_model);
    }

    // =========================================================
    // ローカルプレイヤー
    // =========================================================

    public void RegisterLocalPlayer(
        Transform playerTransform,
        Transform viewTransform)
    {
        if (playerTransform == null)
        {
            Debug.LogError(
                "[CS_MiniMapController] " +
                "playerTransformがnullです。",
                this
            );
            return;
        }

        _localPlayerTransform =
            playerTransform;

        _localPlayerViewTransform =
            viewTransform != null
                ? viewTransform
                : playerTransform;

        _model.SetLocalPlayer(
            _localPlayerTransform.position,
            _localPlayerViewTransform.eulerAngles.y
        );
    }

    // =========================================================
    // 悪人
    // =========================================================

    public void RegisterEnemy(
        string id,
        Transform enemyTransform)
    {
        RegisterEntity(
            _enemyTransforms,
            id,
            enemyTransform,
            _model.AddOrUpdateEnemy
        );
    }

    public void UnregisterEnemy(string id)
    {
        _enemyTransforms.Remove(id);
        _model.RemoveEnemy(id);
    }

    public void SetEnemyActive(
        string id,
        bool isActive)
    {
        _model.SetEnemyActive(
            id,
            isActive
        );
    }

    /// <summary>
    /// 悪人の犯罪完遂率を設定する。
    /// 0～100の値を渡す。
    /// </summary>
    public void SetEnemyCrimeProgress(
        string id,
        float progressPercent)
    {
        _model?.SetEnemyCrimeProgress(
            id,
            progressPercent
        );
    }

    /// <summary>
    /// 犯罪進行率を0～1で管理している場合に使用する。
    /// </summary>
    public void SetEnemyCrimeProgressRate(
        string id,
        float progressRate)
    {
        SetEnemyCrimeProgress(
            id,
            Mathf.Clamp01(progressRate) * 100f
        );
    }

    // =========================================================
    // 他プレイヤー
    // =========================================================

    public void RegisterAlly(
        string id,
        Transform allyTransform)
    {
        RegisterEntity(
            _allyTransforms,
            id,
            allyTransform,
            _model.AddOrUpdateMultiplayerAlly
        );
    }

    public void UnregisterAlly(string id)
    {
        _allyTransforms.Remove(id);
        _model.RemoveMultiplayerAlly(id);
    }

    // =========================================================
    // 警察
    // =========================================================

    public void RegisterPolice(
        string id,
        Transform policeTransform)
    {
        RegisterEntity(
            _policeTransforms,
            id,
            policeTransform,
            _model.AddOrUpdatePolice
        );
    }

    public void UnregisterPolice(string id)
    {
        _policeTransforms.Remove(id);
        _model.RemovePolice(id);
    }

    // =========================================================
    // アイテム
    // =========================================================

    public void RegisterItem(
        string id,
        Transform itemTransform)
    {
        RegisterEntity(
            _itemTransforms,
            id,
            itemTransform,
            _model.AddOrUpdateItem
        );
    }

    public void UnregisterItem(string id)
    {
        _itemTransforms.Remove(id);
        _model.RemoveItem(id);
    }

    public void SetItemActive(
        string id,
        bool isActive)
    {
        _model.SetItemActive(
            id,
            isActive
        );
    }

    // =========================================================
    // ランダムイベント
    // =========================================================

    public void RegisterRandomEvent(
        string id,
        Transform eventTransform)
    {
        RegisterEntity(
            _randomEventTransforms,
            id,
            eventTransform,
            _model.AddOrUpdateRandomEvent
        );
    }

    public void UnregisterRandomEvent(
        string id)
    {
        _randomEventTransforms.Remove(id);
        _model.RemoveRandomEvent(id);
    }

    public void SetRandomEventActive(
        string id,
        bool isActive)
    {
        _model.SetRandomEventActive(
            id,
            isActive
        );
    }

    // =========================================================
    // 共通処理
    // =========================================================

    private static void RegisterEntity(
        Dictionary<string, Transform> transforms,
        string id,
        Transform entityTransform,
        Action<string, Vector3, float>
            updateAction)
    {
        if (string.IsNullOrWhiteSpace(id) ||
            entityTransform == null)
        {
            return;
        }

        transforms[id] = entityTransform;

        updateAction(
            id,
            entityTransform.position,
            entityTransform.eulerAngles.y
        );
    }

    public void SetMapRadius(float radius)
    {
        _model.SetMapRadius(radius);
    }

    public void SetMapWorldSize(float size)
    {
        _model.SetMapWorldSize(size);
    }

    public void SetRotateWithPlayer(bool rotate)
    {
        _model.SetRotateWithPlayer(rotate);
    }

    // =========================================================
    // 終了処理
    // =========================================================

    private void OnDestroy()
    {
        if (_model == null)
            return;

        _model.OnDataChanged -=
            OnModelDataChanged;

        _model.Dispose();
        _model = null;
    }

    private void OnValidate()
    {
        _mapRadius =
            Mathf.Max(0.01f, _mapRadius);

        _mapWorldSize =
            Mathf.Max(0.01f, _mapWorldSize);

        _updateInterval =
            Mathf.Max(0.01f, _updateInterval);
    }
}