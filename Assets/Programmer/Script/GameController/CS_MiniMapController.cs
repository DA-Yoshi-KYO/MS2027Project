/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ミニマップのController。
/// プレイヤー・敵・他プレイヤー・警察・アイテムのアイコンを管理する。
/// </summary>
public class CS_MiniMapController : MonoBehaviour
{
    [Header("プレイヤー本体のTransform（動的生成の場合は空でOK）")]
    [SerializeField] private Transform _localPlayerTransform;

    [Header("UICanvas配下のMiniMapを直接セット")]
    [SerializeField] private CS_UIMiniMapView _miniMapView;

    [Header("ミニマップ設定")]
    [SerializeField] private float _mapRadius = 50f;
    [SerializeField] private float _mapWorldSize = 240f;
    [SerializeField] private bool _rotateWithPlayer = true;

    private CS_UIMiniMapModel _model;
    private CS_UIMiniMapPresenter _presenter;

    private readonly Dictionary<string, Transform> _enemyTransforms = new();
    private readonly Dictionary<string, Transform> _allyTransforms = new();
    private readonly Dictionary<string, Transform> _policeTransforms = new();
    private readonly Dictionary<string, Transform> _itemTransforms = new();

    private float _updateTimer;
    private const float _updateInterval = 0.05f;

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
            Debug.LogError("[CS_MiniMapController] _miniMapViewが未設定です。");
            return;
        }

        _presenter = _miniMapView.GetComponent<CS_UIMiniMapPresenter>();

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
        if (_localPlayerTransform != null)
        {
            _model.SetLocalPlayer(
                _localPlayerTransform.position,
                _localPlayerTransform.eulerAngles.y
            );
        }

        SyncEntityPositions(_enemyTransforms, _model.AddOrUpdateEnemy);
        SyncEntityPositions(_allyTransforms, _model.AddOrUpdateMultiplayerAlly);
        SyncEntityPositions(_policeTransforms, _model.AddOrUpdatePolice);
        SyncEntityPositions(_itemTransforms, _model.AddOrUpdateItem);
    }

    private void SyncEntityPositions(
        Dictionary<string, Transform> transforms,
        System.Action<string, Vector3, float> updateAction)
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

    public void RegisterLocalPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            Debug.LogError("[CS_MiniMapController] playerTransformがnullです。");
            return;
        }

        _localPlayerTransform = playerTransform;
    }

    public void RegisterEnemy(string id, Transform enemyTransform)
    {
        RegisterEntity(_enemyTransforms, id, enemyTransform, _model.AddOrUpdateEnemy);
    }

    public void UnregisterEnemy(string id)
    {
        _enemyTransforms.Remove(id);
        _model.RemoveEnemy(id);
    }

    public void RegisterAlly(string id, Transform allyTransform)
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

    public void RegisterPolice(string id, Transform policeTransform)
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

    /// <summary>全種類共通のアイコンでアイテムを登録する。</summary>
    public void RegisterItem(string id, Transform itemTransform)
    {
        RegisterEntity(
            _itemTransforms,
            id,
            itemTransform,
            _model.AddOrUpdateItem
        );
    }

    /// <summary>取得・消滅したアイテムをミニマップから削除する。</summary>
    public void UnregisterItem(string id)
    {
        _itemTransforms.Remove(id);
        _model.RemoveItem(id);
    }

    public void SetItemActive(string id, bool isActive)
    {
        _model.SetItemActive(id, isActive);
    }

    private void RegisterEntity(
        Dictionary<string, Transform> transforms,
        string id,
        Transform entityTransform,
        System.Action<string, Vector3, float> updateAction)
    {
        if (string.IsNullOrWhiteSpace(id) || entityTransform == null)
            return;

        transforms[id] = entityTransform;
        updateAction(id, entityTransform.position, entityTransform.eulerAngles.y);
    }

    public void SetMapRadius(float radius) => _model.SetMapRadius(radius);
    public void SetMapWorldSize(float size) => _model.SetMapWorldSize(size);

    private void OnDestroy()
    {
        if (_model == null)
            return;

        _model.OnDataChanged -= OnModelDataChanged;
        _model.Dispose();
    }
}
