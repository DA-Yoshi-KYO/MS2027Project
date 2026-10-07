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

/// <summary>
/// ミニマップの View
/// ・UICanvas 配下の MiniMap GameObject にアタッチする
/// ・静止画PNG + uvRect スクロール方式
/// ・背景とアイコンの縮尺を mapRadius で統一する
/// ・背景テクスチャの Wrap Mode は Clamp 推奨（マップ端で繰り返し表示されなくなる）
/// </summary>
public class CS_UIMiniMapView : CS_BaseView<CS_UIMiniMapPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("背景（静止画PNG をセットした RawImage）")]
    [SerializeField] private RawImage _mapImage;
    [SerializeField] private RectTransform _mapRoot;

    [Header("アイコン群の親（回転しない）")]
    [SerializeField] private RectTransform _iconsRoot;

    [Header("プレイヤーアイコン（常に中央固定）")]
    [SerializeField] private RectTransform _playerIcon;

    [Header("アイコン Prefab")]
    [SerializeField] private GameObject _enemyIconPrefab;
    [SerializeField] private GameObject _allyIconPrefab;
    [SerializeField] private GameObject _policeIconPrefab;

    [Header("表示設定")]
    [SerializeField] private float _miniMapDisplayRadius = 75f; // mapRadius に対応するpx数

    // ---- 確認用（Inspector で倍率を確認する・変更不可）----
    [Header("確認用（変更不可・再生中に更新される）")]
    [SerializeField, Min(0)] private float _debugPixelsPerMeter; // 1m あたり何px か
    [SerializeField, Min(0)] private float _debugMetersPerPixel; // 1px あたり何m か
    [SerializeField, Min(0)] private float _debugVisibleRange;   // 見えるワールドの範囲（直径m）

    // =========================================================
    // 内部フィールド
    // =========================================================

    private readonly Dictionary<string, MiniMapIconInstance> _iconPool = new();

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIMiniMapPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 描画メイン（Presenter から呼ばれる）
    // =========================================================

    public void Render(MiniMapViewData data)
    {
        // ---- ① 背景スクロール ----
        // 背景とアイコンの縮尺を mapRadius で統一する
        if (_mapImage != null)
        {
            float imageWidth = _mapImage.rectTransform.rect.width;
            float imageWorldSize = imageWidth * data.mapRadius / _miniMapDisplayRadius;
            float uvSize = imageWorldSize / data.mapWorldSize;

            // playerUvX/Y はオフセット前のUV座標（0〜1）
            _mapImage.uvRect = new Rect(
                data.playerUvX - uvSize * 0.5f,
                data.playerUvY - uvSize * 0.5f,
                uvSize,
                uvSize
            );

            // ---- 確認用：倍率を更新（Inspector で確認できる）----
            _debugPixelsPerMeter = _miniMapDisplayRadius / data.mapRadius;
            _debugMetersPerPixel = data.mapRadius / _miniMapDisplayRadius;
            _debugVisibleRange = imageWorldSize; // 見えるワールドの直径（m）
        }

        // ---- ② 背景回転 ----
        if (_mapRoot != null)
        {
            _mapRoot.localRotation = data.rotateWithPlayer
                ? Quaternion.Euler(0f, 0f, data.playerRotationY)
                : Quaternion.identity;
        }

        // ---- ③ プレイヤーアイコンは常に中央・常に上向き ----
        if (_playerIcon != null)
        {
            _playerIcon.anchoredPosition = Vector2.zero;
            _playerIcon.localRotation = Quaternion.identity;
        }

        // ---- ④ アクティブな ID セットを収集 ----
        var activeIds = new HashSet<string>();
        foreach (var icon in data.icons)
        {
            activeIds.Add(icon.id);
            RenderIcon(icon);
        }

        // ---- ⑤ 使われなくなったアイコンを非アクティブ化 ----
        foreach (var kv in _iconPool)
        {
            kv.Value.Root.gameObject.SetActive(activeIds.Contains(kv.Key));
        }
    }

    // =========================================================
    // 個別アイコン描画
    // =========================================================

    private void RenderIcon(MiniMapIconData data)
    {
        var instance = GetOrCreateIcon(data.id, data.type);
        if (instance == null) return;

        instance.Root.gameObject.SetActive(true);

        instance.Root.anchoredPosition = new Vector2(
            data.normalizedX * _miniMapDisplayRadius,
            data.normalizedY * _miniMapDisplayRadius);

        instance.Root.localRotation = Quaternion.Euler(0f, 0f, -data.rotation);

        if (data.isEdgeClipped)
        {
            instance.Root.localScale = Vector3.one * 0.7f;
            SetIconAlpha(instance, 0.6f);
        }
        else
        {
            instance.Root.localScale = Vector3.one;
            SetIconAlpha(instance, 1.0f);
        }
    }

    // =========================================================
    // アイコンプール管理
    // =========================================================

    private MiniMapIconInstance GetOrCreateIcon(string id, CSE_MiniMapEntityType type)
    {
        if (_iconPool.TryGetValue(id, out var existing))
            return existing;

        GameObject prefab = type switch
        {
            CSE_MiniMapEntityType.Enemy => _enemyIconPrefab,
            CSE_MiniMapEntityType.MultiplayerAlly => _allyIconPrefab,
            CSE_MiniMapEntityType.Police => _policeIconPrefab,
            _ => null,
        };

        if (prefab == null) return null;

        var go = Instantiate(prefab, _iconsRoot);
        var rect = go.GetComponent<RectTransform>();
        var image = go.GetComponent<Image>();

        var instance = new MiniMapIconInstance(rect, image);
        _iconPool[id] = instance;
        return instance;
    }

    private static void SetIconAlpha(MiniMapIconInstance instance, float alpha)
    {
        if (instance.Image == null) return;
        var c = instance.Image.color;
        c.a = alpha;
        instance.Image.color = c;
    }

    // =========================================================
    // クリーンアップ
    // =========================================================

    private void OnDestroy()
    {
        foreach (var kv in _iconPool)
        {
            if (kv.Value.Root != null)
                Destroy(kv.Value.Root.gameObject);
        }
        _iconPool.Clear();
    }

    // =========================================================
    // 内部クラス
    // =========================================================

    private class MiniMapIconInstance
    {
        private readonly RectTransform _root;
        private readonly Image _image;

        public RectTransform Root => _root;
        public Image Image => _image;

        public MiniMapIconInstance(RectTransform root, Image image)
        {
            _root = root;
            _image = image;
        }
    }
}
