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
/// ModelのデータをViewDataへ変換し、
/// Viewへ渡すPresenter。
/// </summary>
public class CS_UIMiniMapPresenter :
    CS_BasePresenter
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ミニマップ設定")]
    [SerializeField]
    private bool _rotateWithPlayer = true;

    [Header("マップ中心のワールド座標")]
    [SerializeField]
    private float _mapCenterX;

    [SerializeField]
    private float _mapCenterZ;

    [Header("悪人の犯罪完遂警告")]
    [Tooltip(
        "悪人アイコンの拡縮を開始する犯罪完遂率。"
    )]
    [Range(0f, 100f)]
    [SerializeField]
    private float _crimeAnimationThreshold = 80f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIMiniMapView _view;

    public bool rotateWithPlayer =>
        _rotateWithPlayer;

    public float mapCenterX =>
        _mapCenterX;

    public float mapCenterZ =>
        _mapCenterZ;

    public float crimeAnimationThreshold =>
        _crimeAnimationThreshold;

    // =========================================================
    // View
    // =========================================================

    public void SetView(
        CS_UIMiniMapView view)
    {
        _view = view;

        if (_view != null)
        {
            _view.SetPresenter(this);
        }
    }

    // =========================================================
    // Model更新
    // =========================================================

    public void OnModelUpdated(
        CS_UIMiniMapModel model)
    {
        if (_view == null ||
            model == null)
        {
            return;
        }

        MiniMapEntityData player =
            model.GetLocalPlayer();

        if (player == null)
            return;

        _view.Render(
            BuildViewData(
                model,
                player
            )
        );
    }

    private MiniMapViewData BuildViewData(
        CS_UIMiniMapModel model,
        MiniMapEntityData player)
    {
        float mapRadius =
            Mathf.Max(
                0.01f,
                model.miniMapRadius
            );

        float mapWorldSize =
            Mathf.Max(
                0.01f,
                model.miniMapWorldSize
            );

        bool rotateWithPlayer =
            _rotateWithPlayer &&
            model.rotatePlayer;

        float playerRotation =
            rotateWithPlayer
                ? player.entityRotation
                : 0f;

        float playerUvX =
            (
                player.entityWorldPosition.x -
                _mapCenterX
            ) /
            mapWorldSize +
            0.5f;

        float playerUvY =
            (
                player.entityWorldPosition.z -
                _mapCenterZ
            ) /
            mapWorldSize +
            0.5f;

        var icons =
            new List<MiniMapIconData>();

        AddEntityIcons(
            icons,
            model.GetEnemies(),
            player.entityWorldPosition,
            mapRadius,
            playerRotation,
            rotateWithPlayer,
            CSE_MiniMapEntityType.Enemy
        );

        AddEntityIcons(
            icons,
            model.GetMultiplayerAllies(),
            player.entityWorldPosition,
            mapRadius,
            playerRotation,
            rotateWithPlayer,
            CSE_MiniMapEntityType.MultiplayerAlly
        );

        AddEntityIcons(
            icons,
            model.GetPolices(),
            player.entityWorldPosition,
            mapRadius,
            playerRotation,
            rotateWithPlayer,
            CSE_MiniMapEntityType.Police
        );

        AddEntityIcons(
            icons,
            model.GetItems(),
            player.entityWorldPosition,
            mapRadius,
            playerRotation,
            rotateWithPlayer,
            CSE_MiniMapEntityType.Item
        );

        AddEntityIcons(
            icons,
            model.GetRandomEvents(),
            player.entityWorldPosition,
            mapRadius,
            playerRotation,
            rotateWithPlayer,
            CSE_MiniMapEntityType.RandomEvent
        );

        return new MiniMapViewData(
            player.entityRotation,
            rotateWithPlayer,
            playerUvX,
            playerUvY,
            mapRadius,
            mapWorldSize,
            icons
        );
    }

    private void AddEntityIcons(
        List<MiniMapIconData> icons,
        IReadOnlyDictionary<
            string,
            MiniMapEntityData
        > entities,
        Vector3 playerPosition,
        float mapRadius,
        float playerRotation,
        bool rotateWithPlayer,
        CSE_MiniMapEntityType type)
    {
        foreach (var pair in entities)
        {
            MiniMapEntityData entity =
                pair.Value;

            if (entity == null ||
                !entity.entityIsActive)
            {
                continue;
            }

            icons.Add(
                WorldToMiniMapIcon(
                    playerPosition,
                    entity,
                    mapRadius,
                    playerRotation,
                    rotateWithPlayer,
                    type
                )
            );
        }
    }

    private MiniMapIconData WorldToMiniMapIcon(
        Vector3 playerPosition,
        MiniMapEntityData entity,
        float mapRadius,
        float playerRotation,
        bool rotateWithPlayer,
        CSE_MiniMapEntityType type)
    {
        Vector3 difference =
            entity.entityWorldPosition -
            playerPosition;

        float dx = difference.x;
        float dz = difference.z;

        if (rotateWithPlayer)
        {
            float radians =
                playerRotation *
                Mathf.Deg2Rad;

            float cosine =
                Mathf.Cos(radians);

            float sine =
                Mathf.Sin(radians);

            float rotatedX =
                dx * cosine -
                dz * sine;

            float rotatedZ =
                dx * sine +
                dz * cosine;

            dx = rotatedX;
            dz = rotatedZ;
        }

        float normalizedX =
            dx / mapRadius;

        float normalizedY =
            dz / mapRadius;

        float distance =
            Mathf.Sqrt(
                normalizedX * normalizedX +
                normalizedY * normalizedY
            );

        bool isClipped =
            distance > 1f;

        if (isClipped &&
            distance > 0.0001f)
        {
            normalizedX /= distance;
            normalizedY /= distance;
        }

        bool shouldPlayScaleAnimation =
            type ==
                CSE_MiniMapEntityType.Enemy &&
            entity.crimeProgressPercent >=
                _crimeAnimationThreshold;

        float iconRotation =
            entity.entityRotation -
            (
                rotateWithPlayer
                    ? playerRotation
                    : 0f
            );

        return new MiniMapIconData(
            entity.entityId,
            type,
            normalizedX,
            normalizedY,
            iconRotation,
            isClipped,
            shouldPlayScaleAnimation,
            entity.crimeProgressPercent
        );
    }

    private void OnValidate()
    {
        _crimeAnimationThreshold =
            Mathf.Clamp(
                _crimeAnimationThreshold,
                0f,
                100f
            );
    }
}

/// <summary>
/// Viewへ渡すミニマップ全体のデータ。
/// </summary>
public class MiniMapViewData
{
    private readonly float _playerRotationY;
    private readonly bool _rotateWithPlayer;
    private readonly float _playerUvX;
    private readonly float _playerUvY;
    private readonly float _mapRadius;
    private readonly float _mapWorldSize;
    private readonly List<MiniMapIconData> _icons;

    public float playerRotationY =>
        _playerRotationY;

    public bool rotateWithPlayer =>
        _rotateWithPlayer;

    public float playerUvX =>
        _playerUvX;

    public float playerUvY =>
        _playerUvY;

    public float mapRadius =>
        _mapRadius;

    public float mapWorldSize =>
        _mapWorldSize;

    public IReadOnlyList<MiniMapIconData>
        icons => _icons;

    public MiniMapViewData(
        float playerRotationY,
        bool rotateWithPlayer,
        float playerUvX,
        float playerUvY,
        float mapRadius,
        float mapWorldSize,
        List<MiniMapIconData> icons)
    {
        _playerRotationY =
            playerRotationY;

        _rotateWithPlayer =
            rotateWithPlayer;

        _playerUvX =
            playerUvX;

        _playerUvY =
            playerUvY;

        _mapRadius =
            mapRadius;

        _mapWorldSize =
            mapWorldSize;

        _icons =
            icons ??
            new List<MiniMapIconData>();
    }
}

/// <summary>
/// Viewへ渡すアイコン1件分のデータ。
/// </summary>
public class MiniMapIconData
{
    private readonly string _id;
    private readonly CSE_MiniMapEntityType _type;
    private readonly float _normalizedX;
    private readonly float _normalizedY;
    private readonly float _rotation;
    private readonly bool _isEdgeClipped;
    private readonly bool _shouldPlayScaleAnimation;
    private readonly float _crimeProgressPercent;

    public string id => _id;

    public CSE_MiniMapEntityType type =>
        _type;

    public float normalizedX =>
        _normalizedX;

    public float normalizedY =>
        _normalizedY;

    public float rotation =>
        _rotation;

    public bool isEdgeClipped =>
        _isEdgeClipped;

    public bool shouldPlayScaleAnimation =>
        _shouldPlayScaleAnimation;

    public float crimeProgressPercent =>
        _crimeProgressPercent;

    public MiniMapIconData(
        string id,
        CSE_MiniMapEntityType type,
        float normalizedX,
        float normalizedY,
        float rotation,
        bool isEdgeClipped,
        bool shouldPlayScaleAnimation,
        float crimeProgressPercent)
    {
        _id = id;
        _type = type;
        _normalizedX = normalizedX;
        _normalizedY = normalizedY;
        _rotation = rotation;
        _isEdgeClipped = isEdgeClipped;
        _shouldPlayScaleAnimation =
            shouldPlayScaleAnimation;

        _crimeProgressPercent =
            crimeProgressPercent;
    }
}