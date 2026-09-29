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
/// ・背景を uvRect でスクロール・回転して移動を表現する
/// ・プレイヤー・敵・アライのアイコンを重ねて表示する
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

    [Header("表示設定")]
    [SerializeField] private float _miniMapDisplayRadius = 75f;

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
        if (_mapImage != null)
        {
            _mapImage.uvRect = new Rect(
                data.uvOffsetX,
                data.uvOffsetY,
                1f,
                1f
            );
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

        // 正規化座標（-1〜1）→ UI ピクセル座標
        instance.Root.anchoredPosition = new Vector2(
            data.normalizedX * _miniMapDisplayRadius,
            data.normalizedY * _miniMapDisplayRadius);

        // アイコンの回転
        instance.Root.localRotation = Quaternion.Euler(0f, 0f, -data.rotation);

        // エッジクリップ時は小さく・半透明
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
    // 内部クラス（バッキングフィールド + => プロパティ）
    // =========================================================

    private class MiniMapIconInstance
    {
        // ---- バッキングフィールド ----
        private readonly RectTransform _root;
        private readonly Image _image;

        // ---- プロパティ公開（読み取り専用）----
        public RectTransform Root => _root;
        public Image Image => _image;

        // ---- コンストラクタ ----
        public MiniMapIconInstance(RectTransform root, Image image)
        {
            _root = root;
            _image = image;
        }
    }
}
