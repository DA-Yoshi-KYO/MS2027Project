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
/// ミニマップの Controller
/// ・GameControllerManager にアタッチする
/// ・プレイヤー・敵・マルチプレイヤー・警察のアイコンを管理する
/// </summary>
public class CS_MiniMapController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("プレイヤー本体の Transform（動的生成の場合は空でOK）")]
    [SerializeField] private Transform _localPlayerTransform;

    [Header("UICanvas 配下の MiniMap を直接セット")]
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

    private readonly Dictionary<string, Transform> _enemyTransforms = new();
    private readonly Dictionary<string, Transform> _allyTransforms = new();
    private readonly Dictionary<string, Transform> _policeTransforms = new();

    private float _updateTimer;
    private const float _updateInterval = 0.05f;

    // =========================================================
    // Awake : Model 生成
    // =========================================================

    private void Awake()
    {
        _model = new CS_UIMiniMapModel(_mapRadius, _mapWorldSize, _rotateWithPlayer);
    }

    // =========================================================
    // Start : Presenter 取得 → MVP 組み立て
    // =========================================================

    private void Start()
    {
        if (_miniMapView == null)
        {
            Debug.LogError("[CS_MiniMapController] _miniMapView が未設定です！");
            return;
        }

        _presenter = _miniMapView.GetComponent<CS_UIMiniMapPresenter>();

        if (_presenter == null)
        {
            Debug.LogError("[CS_MiniMapController] UICanvas/MiniMap に CS_UIMiniMapPresenter が付いていません！");
            return;
        }

        _presenter.SetView(_miniMapView);
        _model.OnDataChanged += OnModelDataChanged;
    }

    // =========================================================
    // Update : 位置情報を Model へ毎フレーム書き込む
    // =========================================================

    private void Update()
    {
        _updateTimer += Time.deltaTime;
        if (_updateTimer < _updateInterval) return;
        _updateTimer = 0f;

        SyncPositions();
    }

    private void SyncPositions()
    {
        // ローカルプレイヤー
        if (_localPlayerTransform != null)
        {
            _model.SetLocalPlayer(
                _localPlayerTransform.position,
                _localPlayerTransform.eulerAngles.y
            );
        }

        // 敵
        foreach (var kv in _enemyTransforms)
        {
            if (kv.Value != null)
                _model.AddOrUpdateEnemy(kv.Key, kv.Value.position, kv.Value.eulerAngles.y);
        }

        // アライ
        foreach (var kv in _allyTransforms)
        {
            if (kv.Value != null)
                _model.AddOrUpdateMultiplayerAlly(kv.Key, kv.Value.position, kv.Value.eulerAngles.y);
        }

        // 警察
        foreach (var kv in _policeTransforms)
        {
            if (kv.Value != null)
                _model.AddOrUpdatePolice(kv.Key, kv.Value.position, kv.Value.eulerAngles.y);
        }
    }

    // =========================================================
    // Model → Presenter → View への通知
    // =========================================================

    private void OnModelDataChanged()
    {
        _presenter.OnModelUpdated(_model);
    }

    // =========================================================
    // 外部 API
    // =========================================================

    /// <summary>Player(Clone) 生成後に呼ぶ</summary>
    public void RegisterLocalPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            Debug.LogError("[CS_MiniMapController] playerTransform が null です！");
            return;
        }
        _localPlayerTransform = playerTransform;
    }

    /// <summary>敵をミニマップに登録する（スポーン時に呼ぶ）</summary>
    public void RegisterEnemy(string id, Transform enemyTransform)
    {
        if (enemyTransform == null) return;
        _enemyTransforms[id] = enemyTransform;
        _model.AddOrUpdateEnemy(id, enemyTransform.position, enemyTransform.eulerAngles.y);
    }

    /// <summary>敵をミニマップから削除する（死亡時に呼ぶ）</summary>
    public void UnregisterEnemy(string id)
    {
        _enemyTransforms.Remove(id);
        _model.RemoveEnemy(id);
    }

    /// <summary>マルチプレイヤー（アライ）をミニマップに登録する</summary>
    public void RegisterAlly(string id, Transform allyTransform)
    {
        if (allyTransform == null) return;
        _allyTransforms[id] = allyTransform;
        _model.AddOrUpdateMultiplayerAlly(id, allyTransform.position, allyTransform.eulerAngles.y);
    }

    /// <summary>マルチプレイヤー（アライ）をミニマップから削除する</summary>
    public void UnregisterAlly(string id)
    {
        _allyTransforms.Remove(id);
        _model.RemoveMultiplayerAlly(id);
    }

    /// <summary>警察をミニマップに登録する（スポーン時に呼ぶ）★ 追加</summary>
    public void RegisterPolice(string id, Transform policeTransform)
    {
        if (policeTransform == null) return;
        _policeTransforms[id] = policeTransform;
        _model.AddOrUpdatePolice(id, policeTransform.position, policeTransform.eulerAngles.y);
    }

    /// <summary>警察をミニマップから削除する（退場時に呼ぶ）★ 追加</summary>
    public void UnregisterPolice(string id)
    {
        _policeTransforms.Remove(id);
        _model.RemovePolice(id);
    }

    /// <summary>アイコン表示範囲を変更する</summary>
    public void SetMapRadius(float radius) => _model.SetMapRadius(radius);

    /// <summary>マップ画像のワールドサイズを変更する</summary>
    public void SetMapWorldSize(float size) => _model.SetMapWorldSize(size);

    // =========================================================
    // OnDestroy : Model を破棄
    // =========================================================

    private void OnDestroy()
    {
        if (_model != null)
        {
            _model.OnDataChanged -= OnModelDataChanged;
            _model.Dispose();
        }
    }
}
