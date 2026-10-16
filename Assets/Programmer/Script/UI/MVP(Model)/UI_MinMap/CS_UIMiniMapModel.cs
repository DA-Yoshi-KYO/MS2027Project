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

/// <summary>ミニマップに表示するエンティティの種類</summary>
public enum CSE_MiniMapEntityType
{
    Player,
    Enemy,
    MultiplayerAlly,
    Police,
    Item,
    Max,
}

/// <summary>ミニマップ上に表示する各エンティティのデータ</summary>
public class MiniMapEntityData
{
    public string entityId { get; private set; }
    public CSE_MiniMapEntityType entityType { get; private set; }
    public Vector3 entityWorldPosition { get; set; }
    public float entityRotation { get; set; }
    public bool entityIsActive { get; set; }

    public MiniMapEntityData(
        string id,
        CSE_MiniMapEntityType type,
        Vector3 worldPosition,
        float rotation = 0f)
    {
        entityId = id;
        entityType = type;
        entityWorldPosition = worldPosition;
        entityRotation = rotation;
        entityIsActive = true;
    }
}

/// <summary>
/// ミニマップのModel。
/// プレイヤー・敵・他プレイヤー・警察・アイテムの位置情報を管理する。
/// </summary>
public class CS_UIMiniMapModel : CS_BaseModel
{
    private MiniMapEntityData _localPlayer;
    private readonly Dictionary<string, MiniMapEntityData> _enemies = new();
    private readonly Dictionary<string, MiniMapEntityData> _multiplayerAllies = new();
    private readonly Dictionary<string, MiniMapEntityData> _polices = new();
    private readonly Dictionary<string, MiniMapEntityData> _items = new();

    public float miniMapRadius { get; private set; }
    public float miniMapWorldSize { get; private set; }
    public bool rotatePlayer { get; private set; }

    public event Action OnDataChanged;

    public CS_UIMiniMapModel(
        float mapRadius = 50f,
        float mapWorldSize = 240f,
        bool rotateWithPlayer = true)
    {
        miniMapRadius = mapRadius;
        miniMapWorldSize = mapWorldSize;
        rotatePlayer = rotateWithPlayer;
    }

    public void SetLocalPlayer(Vector3 worldPosition, float rotation)
    {
        if (_localPlayer == null)
        {
            _localPlayer = new MiniMapEntityData(
                "LocalPlayer",
                CSE_MiniMapEntityType.Player,
                worldPosition,
                rotation
            );
        }
        else
        {
            _localPlayer.entityWorldPosition = worldPosition;
            _localPlayer.entityRotation = rotation;
        }

        OnDataChanged?.Invoke();
    }

    public MiniMapEntityData GetLocalPlayer() => _localPlayer;

    public void AddOrUpdateEnemy(string id, Vector3 worldPosition, float rotation = 0f)
    {
        AddOrUpdate(_enemies, id, CSE_MiniMapEntityType.Enemy, worldPosition, rotation);
    }

    public void RemoveEnemy(string id) => Remove(_enemies, id);
    public void SetEnemyActive(string id, bool isActive) => SetActive(_enemies, id, isActive);
    public IReadOnlyDictionary<string, MiniMapEntityData> GetEnemies() => _enemies;

    public void AddOrUpdateMultiplayerAlly(string id, Vector3 worldPosition, float rotation = 0f)
    {
        AddOrUpdate(
            _multiplayerAllies,
            id,
            CSE_MiniMapEntityType.MultiplayerAlly,
            worldPosition,
            rotation
        );
    }

    public void RemoveMultiplayerAlly(string id) => Remove(_multiplayerAllies, id);
    public IReadOnlyDictionary<string, MiniMapEntityData> GetMultiplayerAllies() => _multiplayerAllies;

    public void AddOrUpdatePolice(string id, Vector3 worldPosition, float rotation = 0f)
    {
        AddOrUpdate(_polices, id, CSE_MiniMapEntityType.Police, worldPosition, rotation);
    }

    public void RemovePolice(string id) => Remove(_polices, id);
    public void SetPoliceActive(string id, bool isActive) => SetActive(_polices, id, isActive);
    public IReadOnlyDictionary<string, MiniMapEntityData> GetPolices() => _polices;

    // =========================================================
    // アイテム
    // =========================================================

    public void AddOrUpdateItem(string id, Vector3 worldPosition, float rotation = 0f)
    {
        AddOrUpdate(_items, id, CSE_MiniMapEntityType.Item, worldPosition, rotation);
    }

    public void RemoveItem(string id) => Remove(_items, id);
    public void SetItemActive(string id, bool isActive) => SetActive(_items, id, isActive);
    public IReadOnlyDictionary<string, MiniMapEntityData> GetItems() => _items;

    private void AddOrUpdate(
        Dictionary<string, MiniMapEntityData> entities,
        string id,
        CSE_MiniMapEntityType type,
        Vector3 worldPosition,
        float rotation)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (entities.TryGetValue(id, out MiniMapEntityData data))
        {
            data.entityWorldPosition = worldPosition;
            data.entityRotation = rotation;
            data.entityIsActive = true;
        }
        else
        {
            entities[id] = new MiniMapEntityData(id, type, worldPosition, rotation);
        }

        OnDataChanged?.Invoke();
    }

    private void Remove(Dictionary<string, MiniMapEntityData> entities, string id)
    {
        if (entities.Remove(id))
            OnDataChanged?.Invoke();
    }

    private void SetActive(
        Dictionary<string, MiniMapEntityData> entities,
        string id,
        bool isActive)
    {
        if (!entities.TryGetValue(id, out MiniMapEntityData data))
            return;

        data.entityIsActive = isActive;
        OnDataChanged?.Invoke();
    }

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

    public override void Dispose()
    {
        OnDataChanged = null;
        _enemies.Clear();
        _multiplayerAllies.Clear();
        _polices.Clear();
        _items.Clear();
        _localPlayer = null;
    }
}
