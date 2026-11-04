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
/// プレイヤー・敵・他プレイヤー・警察・アイテム・ランダムイベントを管理する。
/// ローカルプレイヤーは本体Transformから位置、viewTransformから向きを取得する。
/// </summary>
public class CS_MiniMapController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ローカルプレイヤー（動的生成の場合は空でOK）")]
    [SerializeField] private Transform _localPlayerTransform;
    [SerializeField] private Transform _localPlayerViewTransform;

    [Header("UICanvas配下のMiniMapを直接セット")]
    [SerializeField] private CS_UIMiniMapView _miniMapView;

    [Header("ミニマップ設定")]
    [SerializeField] private float _mapRadius = 50f;
    [SerializeField] private float _mapWorldSize = 240f;
    [SerializeField] private bool _rotateWithPlayer = true;

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
    private const float _updateInterval = 0.05f;

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
                "[CS_MiniMapController] _miniMapViewが未設定です。"
            );
            return;
        }

        _presenter =
            _miniMapView.GetComponent<CS_UIMiniMapPresenter>();

        if (_presenter == null)
        {
            Debug.LogError(
                "[CS_MiniMapController] MiniMapにPresenterがありません。"
            );
            return;
        }

        _presenter.SetView(_miniMapView);
        _model.OnDataChanged += OnModelDataChanged;
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
        // 位置はPlayer本体、向きはviewTransformを使用する。
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

    private void SyncEntityPositions(
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

    /// <summary>
    /// Player(Clone)生成後に呼ぶ。
    /// playerTransformは位置、viewTransformはミニマップの向きに使用する。
    /// </summary>
    public void RegisterLocalPlayer(
        Transform playerTransform,
        Transform viewTransform)
    {
        if (playerTransform == null)
        {
            Debug.LogError(
                "[CS_MiniMapController] playerTransformがnullです。"
            );
            return;
        }

        _localPlayerTransform = playerTransform;
        _localPlayerViewTransform =
            viewTransform != null
                ? viewTransform
                : playerTransform;

        // 登録したフレームから表示できるよう、直ちにModelへ反映する。
        _model.SetLocalPlayer(
            _localPlayerTransform.position,
            _localPlayerViewTransform.eulerAngles.y
        );
    }

    // =========================================================
    // 敵
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

    public void SetItemActive(string id, bool isActive)
    {
        _model.SetItemActive(id, isActive);
    }

    // =========================================================
    // ランダムイベント
    // =========================================================

    /// <summary>
    /// ランダムイベント発生時に、発生地点のTransformを登録する。
    /// </summary>
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

    /// <summary>
    /// ランダムイベント終了・クリア時に登録解除する。
    /// </summary>
    public void UnregisterRandomEvent(string id)
    {
        _randomEventTransforms.Remove(id);
        _model.RemoveRandomEvent(id);
    }

    public void SetRandomEventActive(
        string id,
        bool isActive)
    {
        _model.SetRandomEventActive(id, isActive);
    }

    // =========================================================
    // 共通処理
    // =========================================================

    private void RegisterEntity(
        Dictionary<string, Transform> transforms,
        string id,
        Transform entityTransform,
        Action<string, Vector3, float> updateAction)
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

    // =========================================================
    // 終了処理
    // =========================================================

    private void OnDestroy()
    {
        if (_model == null)
            return;

        _model.OnDataChanged -= OnModelDataChanged;
        _model.Dispose();
    }
}
