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
}

/// <summary>
/// ミニマップ上に表示する各エンティティのデータ
/// </summary>
public class MiniMapEntityData
{
    public string Id { get; private set; }
    public CSE_MiniMapEntityType Type { get; private set; }
    public Vector3 WorldPosition { get; set; }
    public float Rotation { get; set; } // Y軸回転（度）
    public bool IsActive { get; set; }

    public MiniMapEntityData(string id, CSE_MiniMapEntityType type, Vector3 worldPosition, float rotation = 0f)
    {
        Id = id;
        Type = type;
        WorldPosition = worldPosition;
        Rotation = rotation;
        IsActive = true;
    }
}

/// <summary>
/// ミニマップの Model
/// ・プレイヤー・敵・マルチプレイヤーの位置情報を管理する
/// ・静止画PNG + uvRect スクロール方式に対応
/// </summary>
public class CS_UIMiniMapModel : CS_BaseModel
{
    // ---- エンティティデータ ----
    private MiniMapEntityData _localPlayer;
    private readonly Dictionary<string, MiniMapEntityData> _enemies = new();
    private readonly Dictionary<string, MiniMapEntityData> _multiplayerAllies = new();

    // ---- ミニマップ設定 ----
    public float MapRadius { get; private set; } // アイコン表示範囲（ワールド単位）
    public float MapWorldSize { get; private set; } // PNG が表現するワールドの広さ（地形スケールに合わせる）
    public bool RotateWithPlayer { get; private set; } // プレイヤー向きに追従するか

    // ---- イベント ----
    public event Action OnDataChanged;

    // =========================================================
    // コンストラクタ
    // =========================================================

    public CS_UIMiniMapModel(float mapRadius = 50f, float mapWorldSize = 240f, bool rotateWithPlayer = true)
    {
        MapRadius = mapRadius;
        MapWorldSize = mapWorldSize;
        RotateWithPlayer = rotateWithPlayer;
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
            _localPlayer.WorldPosition = worldPosition;
            _localPlayer.Rotation = rotation;
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
            data.WorldPosition = worldPosition;
            data.Rotation = rotation;
            data.IsActive = true;
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
            data.IsActive = isActive;
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
            data.WorldPosition = worldPosition;
            data.Rotation = rotation;
            data.IsActive = true;
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
    // 設定変更
    // =========================================================

    public void SetMapRadius(float radius)
    {
        MapRadius = radius;
        OnDataChanged?.Invoke();
    }

    public void SetMapWorldSize(float size)
    {
        MapWorldSize = size;
        OnDataChanged?.Invoke();
    }

    public void SetRotateWithPlayer(bool rotate)
    {
        RotateWithPlayer = rotate;
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
    }
}
