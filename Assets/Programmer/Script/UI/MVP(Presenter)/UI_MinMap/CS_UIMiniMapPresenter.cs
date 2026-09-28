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
/// ・uvRect オフセット計算（背景スクロール）
/// ・アイコン座標変換（ワールド座標 → 正規化座標）
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

    public bool RotateWithPlayer => _rotateWithPlayer;
    public float MapCenterX => _mapCenterX;
    public float MapCenterZ => _mapCenterZ;

    // =========================================================
    // 初期化
    // =========================================================

    /// <summary>Controller から呼ばれる。View の参照をセットする。</summary>
    public void SetView(CS_UIMiniMapView view)
    {
        _view = view;
        _view.SetPresenter(this);
    }

    // =========================================================
    // Controller から呼ばれる更新エントリポイント
    // =========================================================

    /// <summary>
    /// Controller が Model の変更を検知したときに呼び出す。
    /// Model → ViewData 変換 → View.Render() を実行する。
    /// </summary>
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
        float mapRadius = model.MapRadius;
        float mapWorldSize = model.MapWorldSize;
        float playerRot = _rotateWithPlayer ? player.Rotation : 0f;

        // ---- uvRect オフセット計算（背景スクロール）----
        float uvX = (player.WorldPosition.x - _mapCenterX) / mapWorldSize + 0.5f;
        float uvY = (player.WorldPosition.z - _mapCenterZ) / mapWorldSize + 0.5f;

        // ---- アイコンリスト構築 ----
        var icons = new List<MiniMapIconData>();

        // 敵アイコン
        foreach (var kv in model.GetEnemies())
        {
            if (!kv.Value.IsActive) continue;
            icons.Add(WorldToMiniMapIcon(
                player.WorldPosition, kv.Value,
                mapRadius, playerRot, CSE_MiniMapEntityType.Enemy));
        }

        // マルチプレイヤー（アライ）アイコン
        foreach (var kv in model.GetMultiplayerAllies())
        {
            if (!kv.Value.IsActive) continue;
            icons.Add(WorldToMiniMapIcon(
                player.WorldPosition, kv.Value,
                mapRadius, playerRot, CSE_MiniMapEntityType.MultiplayerAlly));
        }

        // ---- コンストラクタで生成 ----
        return new MiniMapViewData(
            playerRotationY: player.Rotation,
            rotateWithPlayer: _rotateWithPlayer,
            uvOffsetX: uvX - 0.5f,
            uvOffsetY: uvY - 0.5f,
            icons: icons
        );
    }

    /// <summary>
    /// ワールド座標 → ミニマップ上の正規化座標（-1〜1）へ変換。
    /// ミニマップ外はエッジにクランプして IsEdgeClipped = true にする。
    /// </summary>
    private MiniMapIconData WorldToMiniMapIcon(
        Vector3 playerPos,
        MiniMapEntityData entity,
        float mapRadius,
        float playerRotY,
        CSE_MiniMapEntityType type)
    {
        Vector3 diff = entity.WorldPosition - playerPos;
        float dx = diff.x;
        float dz = diff.z;

        // プレイヤーの向きに合わせてアイコン座標を回転
        if (_rotateWithPlayer)
        {
            float rad = -playerRotY * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float rotX = dx * cos - dz * sin;
            float rotZ = dx * sin + dz * cos;
            dx = rotX;
            dz = rotZ;
        }

        // 正規化（-1〜1）
        float normX = dx / mapRadius;
        float normY = dz / mapRadius;

        // 円形クリッピング（範囲外はエッジにクランプ）
        float dist = Mathf.Sqrt(normX * normX + normY * normY);
        bool isClipped = dist > 1f;
        if (isClipped)
        {
            normX /= dist;
            normY /= dist;
        }

        // ---- コンストラクタで生成 ----
        return new MiniMapIconData(
            id: entity.Id,
            type: type,
            normalizedX: normX,
            normalizedY: normY,
            rotation: entity.Rotation - (_rotateWithPlayer ? playerRotY : 0f),
            isEdgeClipped: isClipped
        );
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}

// =========================================================
// Presenter ↔ View 間のデータ受け渡し用クラス
// =========================================================

/// <summary>Presenter から View へ渡す描画データ一式</summary>
public class MiniMapViewData
{
    // ---- バッキングフィールド ----
    private readonly float _playerRotationY;
    private readonly bool _rotateWithPlayer;
    private readonly float _uvOffsetX;
    private readonly float _uvOffsetY;
    private readonly List<MiniMapIconData> _icons;

    // ---- プロパティ公開（読み取り専用）----
    public float PlayerRotationY => _playerRotationY;
    public bool RotateWithPlayer => _rotateWithPlayer;
    public float UvOffsetX => _uvOffsetX;
    public float UvOffsetY => _uvOffsetY;
    public List<MiniMapIconData> Icons => _icons;

    // ---- コンストラクタ ----
    public MiniMapViewData(
        float playerRotationY,
        bool rotateWithPlayer,
        float uvOffsetX,
        float uvOffsetY,
        List<MiniMapIconData> icons)
    {
        _playerRotationY = playerRotationY;
        _rotateWithPlayer = rotateWithPlayer;
        _uvOffsetX = uvOffsetX;
        _uvOffsetY = uvOffsetY;
        _icons = icons;
    }
}

/// <summary>各アイコン 1 つ分の描画情報</summary>
public class MiniMapIconData
{
    // ---- バッキングフィールド ----
    private readonly string _id;
    private readonly CSE_MiniMapEntityType _type;
    private readonly float _normalizedX;
    private readonly float _normalizedY;
    private readonly float _rotation;
    private readonly bool _isEdgeClipped;

    // ---- プロパティ公開（読み取り専用）----
    public string Id => _id;
    public CSE_MiniMapEntityType Type => _type;
    public float NormalizedX => _normalizedX;
    public float NormalizedY => _normalizedY;
    public float Rotation => _rotation;
    public bool IsEdgeClipped => _isEdgeClipped;

    // ---- コンストラクタ ----
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
