/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-27 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ミニマップに表示するエンティティの種類
/// </summary>
public enum CSE_MiniMapEntityType
{
    Player,
    Enemy,
    MultiplayerAlly,
    Police,
    Max,
}

/// <summary>
/// ミニマップ上に表示する各エンティティのデータ
/// </summary>
public class MiniMapEntityData
{
    public string entityId { get; private set; }
    public CSE_MiniMapEntityType entityType { get; private set; }
    public Vector3 entityWorldPosition { get; set; }
    public float entityRotation { get; set; }
    public bool entityIsActive { get; set; }

    public MiniMapEntityData(string id, CSE_MiniMapEntityType type, Vector3 worldPosition, float rotation = 0f)
    {
        entityId = id;
        entityType = type;
        entityWorldPosition = worldPosition;
        entityRotation = rotation;
        entityIsActive = true;
    }
}

/// <summary>
/// ミニマップの Model
/// ・プレイヤー・敵・マルチプレイヤー・警察の位置情報を管理する
/// ・静止画PNG + uvRect スクロール方式に対応
/// </summary>
public class CS_UIMiniMapModel : CS_BaseModel
{
    // ---- エンティティデータ ----
    private MiniMapEntityData _localPlayer;
    private readonly Dictionary<string, MiniMapEntityData> _enemies = new();
    private readonly Dictionary<string, MiniMapEntityData> _multiplayerAllies = new();
    private readonly Dictionary<string, MiniMapEntityData> _polices = new(); // ★ 追加

    // ---- ミニマップ設定 ----
    public float miniMapRadius { get; private set; }
    public float miniMapWorldSize { get; private set; }
    public bool rotatePlayer { get; private set; }

    // ---- イベント ----
    public event Action OnDataChanged;

    // =========================================================
    // コンストラクタ
    // =========================================================

    public CS_UIMiniMapModel(float mapRadius = 50f, float mapWorldSize = 240f, bool rotateWithPlayer = true)
    {
        miniMapRadius = mapRadius;
        miniMapWorldSize = mapWorldSize;
        rotatePlayer = rotateWithPlayer;
    }

    // =========================================================
    // ローカルプレイヤー
    // =========================================================

    public void SetLocalPlayer(Vector3 worldPosition, float rotation)
    {
        if (_localPlayer == null)
            _localPlayer = new MiniMapEntityData("LocalPlayer", CSE_MiniMapEntityType.Player, worldPosition, rotation);
        else
        {
            _localPlayer.entityWorldPosition = worldPosition;
            _localPlayer.entityRotation = rotation;
        }
        OnDataChanged?.Invoke();
    }

    public MiniMapEntityData GetLocalPlayer() => _localPlayer;

    // =========================================================
    // 敵
    // =========================================================

    public void AddOrUpdateEnemy(string id, Vector3 worldPosition, float rotation = 0f)
    {
        if (_enemies.TryGetValue(id, out var data))
        {
            data.entityWorldPosition = worldPosition;
            data.entityRotation = rotation;
            data.entityIsActive = true;
        }
        else
        {
            _enemies[id] = new MiniMapEntityData(id, CSE_MiniMapEntityType.Enemy, worldPosition, rotation);
        }
        OnDataChanged?.Invoke();
    }

    public void RemoveEnemy(string id)
    {
        if (_enemies.Remove(id))
            OnDataChanged?.Invoke();
    }

    public void SetEnemyActive(string id, bool isActive)
    {
        if (_enemies.TryGetValue(id, out var data))
        {
            data.entityIsActive = isActive;
            OnDataChanged?.Invoke();
        }
    }

    public IReadOnlyDictionary<string, MiniMapEntityData> GetEnemies() => _enemies;

    // =========================================================
    // マルチプレイヤー（他プレイヤー）
    // =========================================================

    public void AddOrUpdateMultiplayerAlly(string id, Vector3 worldPosition, float rotation = 0f)
    {
        if (_multiplayerAllies.TryGetValue(id, out var data))
        {
            data.entityWorldPosition = worldPosition;
            data.entityRotation = rotation;
            data.entityIsActive = true;
        }
        else
        {
            _multiplayerAllies[id] = new MiniMapEntityData(id, CSE_MiniMapEntityType.MultiplayerAlly, worldPosition, rotation);
        }
        OnDataChanged?.Invoke();
    }

    public void RemoveMultiplayerAlly(string id)
    {
        if (_multiplayerAllies.Remove(id))
            OnDataChanged?.Invoke();
    }

    public IReadOnlyDictionary<string, MiniMapEntityData> GetMultiplayerAllies() => _multiplayerAllies;

    // =========================================================
    // 警察
    // =========================================================

    public void AddOrUpdatePolice(string id, Vector3 worldPosition, float rotation = 0f)
    {
        if (_polices.TryGetValue(id, out var data))
        {
            data.entityWorldPosition = worldPosition;
            data.entityRotation = rotation;
            data.entityIsActive = true;
        }
        else
        {
            _polices[id] = new MiniMapEntityData(id, CSE_MiniMapEntityType.Police, worldPosition, rotation);
        }
        OnDataChanged?.Invoke();
    }

    public void RemovePolice(string id)
    {
        if (_polices.Remove(id))
            OnDataChanged?.Invoke();
    }

    public void SetPoliceActive(string id, bool isActive)
    {
        if (_polices.TryGetValue(id, out var data))
        {
            data.entityIsActive = isActive;
            OnDataChanged?.Invoke();
        }
    }

    public IReadOnlyDictionary<string, MiniMapEntityData> GetPolices() => _polices;

    // =========================================================
    // 設定変更
    // =========================================================

    public void SetMapRadius(float radius)
    {
        miniMapRadius = radius;
        OnDataChanged?.Invoke();
    }

    public void SetMapWorldSize(float size)
    {
        miniMapWorldSize = size;
        OnDataChanged?.Invoke();
    }

    public void SetRotateWithPlayer(bool rotate)
    {
        rotatePlayer = rotate;
        OnDataChanged?.Invoke();
    }

    // =========================================================
    // IDisposable
    // =========================================================

    public override void Dispose()
    {
        OnDataChanged = null;
        _enemies.Clear();
        _multiplayerAllies.Clear();
        _polices.Clear();
    }
}
