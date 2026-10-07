/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-27 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ミニマップの Presenter
/// ・UICanvas/MiniMap に CS_UIMiniMapView と一緒にアタッチする
/// ・Model のデータを ViewData に変換して View へ渡す
/// </summary>
public class CS_UIMiniMapPresenter : CS_BasePresenter
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ミニマップ設定")]
    [SerializeField] private bool _rotateWithPlayer = true;

    [Header("マップ中心のワールド座標（地形の Position + サイズ / 2）")]
    [SerializeField] private float _mapCenterX = 0f;
    [SerializeField] private float _mapCenterZ = 0f;

    // =========================================================
    // バッキングフィールド
    // =========================================================

    private CS_UIMiniMapView _view;

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public bool rotateWithPlayer => _rotateWithPlayer;
    public float mapCenterX => _mapCenterX;
    public float mapCenterZ => _mapCenterZ;

    // =========================================================
    // 初期化
    // =========================================================

    public void SetView(CS_UIMiniMapView view)
    {
        _view = view;
        _view.SetPresenter(this);
    }

    // =========================================================
    // Controller から呼ばれる更新エントリポイント
    // =========================================================

    public void OnModelUpdated(CS_UIMiniMapModel model)
    {
        if (_view == null) return;

        var player = model.GetLocalPlayer();
        if (player == null) return;

        var viewData = BuildViewData(model, player);
        _view.Render(viewData);
    }

    // =========================================================
    // ViewData 構築
    // =========================================================

    private MiniMapViewData BuildViewData(CS_UIMiniMapModel model, MiniMapEntityData player)
    {
        float mapRadius = model.miniMapRadius;
        float mapWorldSize = model.miniMapWorldSize;
        float playerRot = _rotateWithPlayer ? player.entityRotation : 0f;

        // ---- uvRect オフセット計算 ----
        // プレイヤーのUV座標（オフセットを引く前の値）を ViewData に渡す
        float playerUvX = (player.entityWorldPosition.x - _mapCenterX) / mapWorldSize + 0.5f;
        float playerUvY = (player.entityWorldPosition.z - _mapCenterZ) / mapWorldSize + 0.5f;

        // ---- アイコンリスト構築 ----
        var icons = new List<MiniMapIconData>();

        // 敵アイコン
        foreach (var kv in model.GetEnemies())
        {
            if (!kv.Value.entityIsActive) continue;
            icons.Add(WorldToMiniMapIcon(
                player.entityWorldPosition, kv.Value,
                mapRadius, playerRot, CSE_MiniMapEntityType.Enemy));
        }

        // マルチプレイヤー（アライ）アイコン
        foreach (var kv in model.GetMultiplayerAllies())
        {
            if (!kv.Value.entityIsActive) continue;
            icons.Add(WorldToMiniMapIcon(
                player.entityWorldPosition, kv.Value,
                mapRadius, playerRot, CSE_MiniMapEntityType.MultiplayerAlly));
        }

        // 警察アイコン
        foreach (var kv in model.GetPolices())
        {
            if (!kv.Value.entityIsActive) continue;
            icons.Add(WorldToMiniMapIcon(
                player.entityWorldPosition, kv.Value,
                mapRadius, playerRot, CSE_MiniMapEntityType.Police));
        }

        return new MiniMapViewData(
            playerRotationY: player.entityRotation,
            rotateWithPlayer: _rotateWithPlayer,
            playerUvX: playerUvX,  // ★ オフセット前のUV座標を渡す
            playerUvY: playerUvY,  // ★ オフセット前のUV座標を渡す
            mapRadius: mapRadius,  // ★ 縮尺計算用に渡す
            mapWorldSize: mapWorldSize, // ★ 縮尺計算用に渡す
            icons: icons
        );
    }

    private MiniMapIconData WorldToMiniMapIcon(
        Vector3 playerPos,
        MiniMapEntityData entity,
        float mapRadius,
        float playerRotY,
        CSE_MiniMapEntityType type)
    {
        Vector3 diff = entity.entityWorldPosition - playerPos;
        float dx = diff.x;
        float dz = diff.z;

        if (_rotateWithPlayer)
        {
            // ★ 修正：背景と同じ向きに回す（- → +）
            float rad = playerRotY * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float rotX = dx * cos - dz * sin;
            float rotZ = dx * sin + dz * cos;
            dx = rotX;
            dz = rotZ;
        }

        float normX = dx / mapRadius;
        float normY = dz / mapRadius;

        float dist = Mathf.Sqrt(normX * normX + normY * normY);
        bool isClipped = dist > 1f;
        if (isClipped)
        {
            normX /= dist;
            normY /= dist;
        }

        return new MiniMapIconData(
            id: entity.entityId,
            type: type,
            normalizedX: normX,
            normalizedY: normY,
            rotation: entity.entityRotation - (_rotateWithPlayer ? playerRotY : 0f),
            isEdgeClipped: isClipped
        );
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}

// =========================================================
// Presenter ↔ View 間のデータ受け渡し用クラス
// =========================================================

public class MiniMapViewData
{
    // ---- バッキングフィールド ----
    private readonly float _playerRotationY;
    private readonly bool _rotateWithPlayer;
    private readonly float _playerUvX;    // ★ 追加：オフセット前のUV座標
    private readonly float _playerUvY;    // ★ 追加：オフセット前のUV座標
    private readonly float _mapRadius;    // ★ 追加：縮尺計算用
    private readonly float _mapWorldSize; // ★ 追加：縮尺計算用
    private readonly List<MiniMapIconData> _icons;

    // ---- プロパティ公開（読み取り専用）----
    public float playerRotationY => _playerRotationY;
    public bool rotateWithPlayer => _rotateWithPlayer;
    public float playerUvX => _playerUvX;
    public float playerUvY => _playerUvY;
    public float mapRadius => _mapRadius;
    public float mapWorldSize => _mapWorldSize;
    public List<MiniMapIconData> icons => _icons;

    // ---- コンストラクタ ----
    public MiniMapViewData(
        float playerRotationY,
        bool rotateWithPlayer,
        float playerUvX,
        float playerUvY,
        float mapRadius,
        float mapWorldSize,
        List<MiniMapIconData> icons)
    {
        _playerRotationY = playerRotationY;
        _rotateWithPlayer = rotateWithPlayer;
        _playerUvX = playerUvX;
        _playerUvY = playerUvY;
        _mapRadius = mapRadius;
        _mapWorldSize = mapWorldSize;
        _icons = icons;
    }
}

public class MiniMapIconData
{
    private readonly string _id;
    private readonly CSE_MiniMapEntityType _type;
    private readonly float _normalizedX;
    private readonly float _normalizedY;
    private readonly float _rotation;
    private readonly bool _isEdgeClipped;

    public string id => _id;
    public CSE_MiniMapEntityType type => _type;
    public float normalizedX => _normalizedX;
    public float normalizedY => _normalizedY;
    public float rotation => _rotation;
    public bool isEdgeClipped => _isEdgeClipped;

    public MiniMapIconData(
        string id,
        CSE_MiniMapEntityType type,
        float normalizedX,
        float normalizedY,
        float rotation,
        bool isEdgeClipped)
    {
        _id = id;
        _type = type;
        _normalizedX = normalizedX;
        _normalizedY = normalizedY;
        _rotation = rotation;
        _isEdgeClipped = isEdgeClipped;
    }
}
