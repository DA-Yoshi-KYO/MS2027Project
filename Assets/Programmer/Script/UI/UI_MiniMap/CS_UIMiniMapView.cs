/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-27 | 初回作成
 * 2026-11-15 | アイコンのプールのキーを構造体にして、毎フレームの文字列生成とforeachのGCをなくした(負荷の軽減)
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ミニマップのView。
/// 静止画PNGとRawImageのuvRectで背景を表示する。
/// 悪人の犯罪完遂率が閾値以上の場合、
/// 三角形アイコンを拡縮させる。
/// </summary>
public class CS_UIMiniMapView :
    CS_BaseView<CS_UIMiniMapPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("背景")]
    [SerializeField]
    private RawImage _mapImage;

    [SerializeField]
    private RectTransform _mapRoot;

    [Header("アイコン群の親")]
    [SerializeField]
    private RectTransform _iconsRoot;

    [Header("プレイヤーアイコン")]
    [SerializeField]
    private RectTransform _playerIcon;

    [Header("アイコンPrefab")]
    [Tooltip(
        "三角形画像を設定した悪人用Prefab。"
    )]
    [SerializeField]
    private GameObject _enemyIconPrefab;

    [SerializeField]
    private GameObject _allyIconPrefab;

    [SerializeField]
    private GameObject _policeIconPrefab;

    [SerializeField]
    private GameObject _itemIconPrefab;

    [SerializeField]
    private GameObject _randomEventIconPrefab;

    [Header("表示設定")]
    [Min(0.01f)]
    [SerializeField]
    private float _miniMapDisplayRadius = 185f;

    [Header("範囲外アイコン")]
    [Min(0.01f)]
    [SerializeField]
    private float _edgeClippedScale = 0.7f;

    [Range(0f, 1f)]
    [SerializeField]
    private float _edgeClippedAlpha = 0.6f;

    [Header("犯罪完遂警告アニメーション")]
    [Tooltip("拡縮の最小倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float _crimeWarningMinimumScale = 0.75f;

    [Tooltip("拡縮の最大倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float _crimeWarningMaximumScale = 1.15f;

    [Tooltip(
        "最小倍率から最大倍率へ移動する秒数"
    )]
    [Min(0.01f)]
    [SerializeField]
    private float _crimeWarningScaleDuration = 0.6f;

    [Tooltip(
        "アニメーション開始時の位相をIDごとにずらす"
    )]
    [SerializeField]
    private bool _offsetAnimationById = true;

    [Header("確認用")]
    [SerializeField, Min(0f)]
    private float _debugPixelsPerMeter;

    [SerializeField, Min(0f)]
    private float _debugMetersPerPixel;

    [SerializeField, Min(0f)]
    private float _debugVisibleRange;

    // =========================================================
    // 内部フィールド
    // =========================================================

    // プールのキー(種別+ID)。毎フレーム文字列を作らないよう、構造体で扱う
    private readonly struct IconKey : System.IEquatable<IconKey>
    {
        private readonly CSE_MiniMapEntityType _type;
        private readonly string _id;

        public IconKey(
            CSE_MiniMapEntityType type,
            string id)
        {
            _type = type;
            _id = id;
        }

        public bool Equals(IconKey other)
        {
            return _type == other._type &&
                _id == other._id;
        }

        public override bool Equals(object obj)
        {
            return obj is IconKey other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)_type * 397) ^
                (_id != null ? _id.GetHashCode() : 0);
        }
    }

    private readonly Dictionary<
        IconKey,
        MiniMapIconInstance
    > _iconPool = new();

    private readonly HashSet<IconKey>
        _activeIds = new();

    // =========================================================
    // Presenter
    // =========================================================

    public override void SetPresenter(
        CS_UIMiniMapPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 更新
    // =========================================================

    private void Update()
    {
        UpdateCrimeWarningAnimations();
    }

    // =========================================================
    // 描画
    // =========================================================

    public void Render(MiniMapViewData data)
    {
        if (data == null)
            return;

        RenderMapBackground(data);
        RenderPlayerIcon();

        _activeIds.Clear();

        // foreachだと列挙子がGCを出すため、forで回す
        IReadOnlyList<MiniMapIconData> icons =
            data.icons;

        for (int i = 0; i < icons.Count; i++)
        {
            MiniMapIconData icon = icons[i];

            IconKey poolKey =
                new IconKey(
                    icon.type,
                    icon.id
                );

            _activeIds.Add(poolKey);

            RenderIcon(
                icon,
                poolKey
            );
        }

        foreach (var pair in _iconPool)
        {
            bool isActive =
                _activeIds.Contains(pair.Key);

            if (pair.Value.root != null &&
                pair.Value.root.gameObject
                    .activeSelf != isActive)
            {
                pair.Value.root.gameObject
                    .SetActive(isActive);
            }

            if (!isActive)
            {
                pair.Value.ResetAnimation();
            }
        }
    }

    private void RenderMapBackground(
        MiniMapViewData data)
    {
        if (_mapImage != null &&
            data.mapRadius > 0f &&
            data.mapWorldSize > 0f &&
            _miniMapDisplayRadius > 0f)
        {
            float imageWidth =
                _mapImage.rectTransform
                    .rect.width;

            if (imageWidth > 0f)
            {
                float imageWorldSize =
                    imageWidth *
                    data.mapRadius /
                    _miniMapDisplayRadius;

                float uvSize =
                    imageWorldSize /
                    data.mapWorldSize;

                _mapImage.uvRect =
                    new Rect(
                        data.playerUvX -
                            uvSize * 0.5f,
                        data.playerUvY -
                            uvSize * 0.5f,
                        uvSize,
                        uvSize
                    );

                _debugPixelsPerMeter =
                    _miniMapDisplayRadius /
                    data.mapRadius;

                _debugMetersPerPixel =
                    data.mapRadius /
                    _miniMapDisplayRadius;

                _debugVisibleRange =
                    imageWorldSize;
            }
        }

        if (_mapRoot != null)
        {
            _mapRoot.localRotation =
                data.rotateWithPlayer
                    ? Quaternion.Euler(
                        0f,
                        0f,
                        data.playerRotationY
                    )
                    : Quaternion.identity;
        }
    }

    private void RenderPlayerIcon()
    {
        if (_playerIcon == null)
            return;

        _playerIcon.anchoredPosition =
            Vector2.zero;

        _playerIcon.localRotation =
            Quaternion.identity;
    }

    private void RenderIcon(
        MiniMapIconData data,
        IconKey poolKey)
    {
        MiniMapIconInstance instance =
            GetOrCreateIcon(
                poolKey,
                data.type,
                data.id
            );

        if (instance == null)
            return;

        if (!instance.root.gameObject.activeSelf)
        {
            instance.root.gameObject
                .SetActive(true);
        }

        instance.root.anchoredPosition =
            new Vector2(
                data.normalizedX *
                    _miniMapDisplayRadius,
                data.normalizedY *
                    _miniMapDisplayRadius
            );

        instance.root.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -data.rotation
            );

        instance.baseScale =
            data.isEdgeClipped
                ? _edgeClippedScale
                : 1f;

        instance.isScaleAnimating =
            data.shouldPlayScaleAnimation;

        if (!instance.isScaleAnimating)
        {
            instance.scaleAnimationElapsed =
                0f;

            instance.root.localScale =
                Vector3.one *
                instance.baseScale;
        }
        else if (!instance.wasScaleAnimating)
        {
            instance.scaleAnimationElapsed =
                _offsetAnimationById
                    ? instance.animationOffsetRate *
                        _crimeWarningScaleDuration
                    : 0f;

            instance.root.localScale =
                Vector3.one *
                instance.baseScale *
                _crimeWarningMinimumScale;
        }

        instance.wasScaleAnimating =
            instance.isScaleAnimating;

        SetIconAlpha(
            instance,
            data.isEdgeClipped
                ? _edgeClippedAlpha
                : 1f
        );
    }

    // =========================================================
    // 拡縮アニメーション
    // =========================================================

    private void UpdateCrimeWarningAnimations()
    {
        if (_crimeWarningScaleDuration <= 0f)
            return;

        foreach (var pair in _iconPool)
        {
            MiniMapIconInstance instance =
                pair.Value;

            if (instance == null ||
                instance.root == null ||
                !instance.root.gameObject
                    .activeInHierarchy ||
                !instance.isScaleAnimating)
            {
                continue;
            }

            instance.scaleAnimationElapsed +=
                Time.unscaledDeltaTime;

            float pingPongRate =
                Mathf.PingPong(
                    instance.scaleAnimationElapsed /
                        _crimeWarningScaleDuration,
                    1f
                );

            float smoothRate =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    pingPongRate
                );

            float animationScale =
                Mathf.Lerp(
                    _crimeWarningMinimumScale,
                    _crimeWarningMaximumScale,
                    smoothRate
                );

            instance.root.localScale =
                Vector3.one *
                instance.baseScale *
                animationScale;
        }
    }

    // アイコンの作成時に1回だけ、IDごとの位相のずれ(0～1)を求める
    private static float GetAnimationOffsetRate(
        CSE_MiniMapEntityType type,
        string id)
    {
        if (string.IsNullOrEmpty(id))
            return 0f;

        int hash =
            $"{type}:{id}".GetHashCode();

        return Mathf.Abs(
            hash % 1000
        ) /
        1000f;
    }

    // =========================================================
    // アイコン生成
    // =========================================================

    private MiniMapIconInstance GetOrCreateIcon(
        IconKey poolKey,
        CSE_MiniMapEntityType type,
        string id)
    {
        if (_iconPool.TryGetValue(
            poolKey,
            out MiniMapIconInstance existing))
        {
            return existing;
        }

        GameObject prefab =
            GetIconPrefab(type);

        if (prefab == null ||
            _iconsRoot == null)
        {
            return null;
        }

        GameObject iconObject =
            Instantiate(
                prefab,
                _iconsRoot
            );

        RectTransform iconRect =
            iconObject.GetComponent<
                RectTransform
            >();

        Image iconImage =
            iconObject.GetComponentInChildren<
                Image
            >(true);

        if (iconRect == null)
        {
            Destroy(iconObject);
            return null;
        }

        if (iconImage != null)
        {
            iconImage.raycastTarget =
                false;
        }

        var instance =
            new MiniMapIconInstance(
                iconRect,
                iconImage
            );

        instance.animationOffsetRate =
            GetAnimationOffsetRate(
                type,
                id
            );

        _iconPool[poolKey] =
            instance;

        return instance;
    }

    private GameObject GetIconPrefab(
        CSE_MiniMapEntityType type)
    {
        return type switch
        {
            CSE_MiniMapEntityType.Enemy =>
                _enemyIconPrefab,

            CSE_MiniMapEntityType
                .MultiplayerAlly =>
                _allyIconPrefab,

            CSE_MiniMapEntityType.Police =>
                _policeIconPrefab,

            CSE_MiniMapEntityType.Item =>
                _itemIconPrefab,

            CSE_MiniMapEntityType
                .RandomEvent =>
                _randomEventIconPrefab,

            _ => null,
        };
    }

    private static void SetIconAlpha(
        MiniMapIconInstance instance,
        float alpha)
    {
        if (instance.image == null)
            return;

        Color color =
            instance.image.color;

        color.a =
            Mathf.Clamp01(alpha);

        instance.image.color =
            color;
    }

    // =========================================================
    // 終了処理
    // =========================================================

    private void OnDestroy()
    {
        foreach (var pair in _iconPool)
        {
            if (pair.Value.root != null)
            {
                Destroy(
                    pair.Value.root.gameObject
                );
            }
        }

        _iconPool.Clear();
        _activeIds.Clear();
    }

    private void OnValidate()
    {
        _miniMapDisplayRadius =
            Mathf.Max(
                0.01f,
                _miniMapDisplayRadius
            );

        _edgeClippedScale =
            Mathf.Max(
                0.01f,
                _edgeClippedScale
            );

        _crimeWarningMinimumScale =
            Mathf.Max(
                0.01f,
                _crimeWarningMinimumScale
            );

        _crimeWarningMaximumScale =
            Mathf.Max(
                _crimeWarningMinimumScale,
                _crimeWarningMaximumScale
            );

        _crimeWarningScaleDuration =
            Mathf.Max(
                0.01f,
                _crimeWarningScaleDuration
            );
    }

    // =========================================================
    // アイコンインスタンス
    // =========================================================

    private sealed class MiniMapIconInstance
    {
        public RectTransform root
        {
            get;
        }

        public Image image
        {
            get;
        }

        public bool isScaleAnimating
        {
            get;
            set;
        }

        public bool wasScaleAnimating
        {
            get;
            set;
        }

        public float scaleAnimationElapsed
        {
            get;
            set;
        }

        public float baseScale
        {
            get;
            set;
        } = 1f;

        // IDごとの拡縮アニメーションの位相のずれ(0～1)
        public float animationOffsetRate
        {
            get;
            set;
        }

        public MiniMapIconInstance(
            RectTransform root,
            Image image)
        {
            this.root = root;
            this.image = image;
        }

        public void ResetAnimation()
        {
            isScaleAnimating = false;
            wasScaleAnimating = false;
            scaleAnimationElapsed = 0f;
            baseScale = 1f;

            if (root != null)
            {
                root.localScale =
                    Vector3.one;
            }
        }
    }
}