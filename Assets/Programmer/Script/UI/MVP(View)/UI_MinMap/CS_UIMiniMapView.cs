/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-27 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>ミニマップのView。静止画PNG + uvRectスクロール方式。</summary>
public class CS_UIMiniMapView : CS_BaseView<CS_UIMiniMapPresenter>
{
    [Header("背景（静止画PNGをセットしたRawImage）")]
    [SerializeField] private RawImage _mapImage;
    [SerializeField] private RectTransform _mapRoot;

    [Header("アイコン群の親（回転しない）")]
    [SerializeField] private RectTransform _iconsRoot;

    [Header("プレイヤーアイコン（常に中央固定）")]
    [SerializeField] private RectTransform _playerIcon;

    [Header("アイコンPrefab")]
    [SerializeField] private GameObject _enemyIconPrefab;
    [SerializeField] private GameObject _allyIconPrefab;
    [SerializeField] private GameObject _policeIconPrefab;
    [SerializeField] private GameObject _itemIconPrefab;

    [Header("表示設定")]
    [SerializeField] private float _miniMapDisplayRadius = 185f;

    [Header("確認用（変更不可・再生中に更新される）")]
    [SerializeField, Min(0)] private float _debugPixelsPerMeter;
    [SerializeField, Min(0)] private float _debugMetersPerPixel;
    [SerializeField, Min(0)] private float _debugVisibleRange;

    private readonly Dictionary<string, MiniMapIconInstance> _iconPool = new();

    public override void SetPresenter(CS_UIMiniMapPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    public void Render(MiniMapViewData data)
    {
        if (_mapImage != null && data.mapRadius > 0f && _miniMapDisplayRadius > 0f)
        {
            float imageWidth = _mapImage.rectTransform.rect.width;
            float imageWorldSize = imageWidth * data.mapRadius / _miniMapDisplayRadius;
            float uvSize = imageWorldSize / data.mapWorldSize;

            _mapImage.uvRect = new Rect(
                data.playerUvX - uvSize * 0.5f,
                data.playerUvY - uvSize * 0.5f,
                uvSize,
                uvSize
            );

            _debugPixelsPerMeter = _miniMapDisplayRadius / data.mapRadius;
            _debugMetersPerPixel = data.mapRadius / _miniMapDisplayRadius;
            _debugVisibleRange = imageWorldSize;
        }

        if (_mapRoot != null)
        {
            _mapRoot.localRotation = data.rotateWithPlayer
                ? Quaternion.Euler(0f, 0f, data.playerRotationY)
                : Quaternion.identity;
        }

        if (_playerIcon != null)
        {
            _playerIcon.anchoredPosition = Vector2.zero;
            _playerIcon.localRotation = Quaternion.identity;
        }

        var activeIds = new HashSet<string>();

        foreach (MiniMapIconData icon in data.icons)
        {
            activeIds.Add(icon.id);
            RenderIcon(icon);
        }

        foreach (var pair in _iconPool)
            pair.Value.root.gameObject.SetActive(activeIds.Contains(pair.Key));
    }

    private void RenderIcon(MiniMapIconData data)
    {
        MiniMapIconInstance instance = GetOrCreateIcon(data.id, data.type);

        if (instance == null)
            return;

        instance.root.gameObject.SetActive(true);
        instance.root.anchoredPosition = new Vector2(
            data.normalizedX * _miniMapDisplayRadius,
            data.normalizedY * _miniMapDisplayRadius
        );

        instance.root.localRotation =
            Quaternion.Euler(0f, 0f, -data.rotation);

        if (data.isEdgeClipped)
        {
            instance.root.localScale = Vector3.one * 0.7f;
            SetIconAlpha(instance, 0.6f);
        }
        else
        {
            instance.root.localScale = Vector3.one;
            SetIconAlpha(instance, 1f);
        }
    }

    private MiniMapIconInstance GetOrCreateIcon(
        string id,
        CSE_MiniMapEntityType type)
    {
        if (_iconPool.TryGetValue(id, out MiniMapIconInstance existing))
            return existing;

        GameObject prefab = type switch
        {
            CSE_MiniMapEntityType.Enemy => _enemyIconPrefab,
            CSE_MiniMapEntityType.MultiplayerAlly => _allyIconPrefab,
            CSE_MiniMapEntityType.Police => _policeIconPrefab,
            CSE_MiniMapEntityType.Item => _itemIconPrefab,
            _ => null,
        };

        if (prefab == null || _iconsRoot == null)
            return null;

        GameObject iconObject = Instantiate(prefab, _iconsRoot);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        Image iconImage = iconObject.GetComponentInChildren<Image>(true);

        if (iconRect == null)
        {
            Destroy(iconObject);
            return null;
        }

        if (iconImage != null)
            iconImage.raycastTarget = false;

        var instance = new MiniMapIconInstance(iconRect, iconImage);
        _iconPool[id] = instance;
        return instance;
    }

    private static void SetIconAlpha(MiniMapIconInstance instance, float alpha)
    {
        if (instance.image == null)
            return;

        Color color = instance.image.color;
        color.a = alpha;
        instance.image.color = color;
    }

    private void OnDestroy()
    {
        foreach (var pair in _iconPool)
        {
            if (pair.Value.root != null)
                Destroy(pair.Value.root.gameObject);
        }

        _iconPool.Clear();
    }

    private class MiniMapIconInstance
    {
        private readonly RectTransform _root;
        private readonly Image _image;

        public RectTransform root => _root;
        public Image image => _image;

        public MiniMapIconInstance(RectTransform root, Image image)
        {
            _root = root;
            _image = image;
        }
    }
}
