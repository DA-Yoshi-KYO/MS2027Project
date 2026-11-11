/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-13 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイヤー検知UIを非表示の楕円軌道上へ表示するView。
/// </summary>
public class CS_UIPoliceDetectionView :
    CS_BaseView<CS_UIPoliceDetectionPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [SerializeField]
    private RectTransform _iconRoot;

    [Header("状態別の目アイコンPrefab")]

    [Tooltip("追跡状態で使用する黄色の△アイコンPrefab")]
    [SerializeField]
    private GameObject _trackedEyeIconPrefab;

    [Tooltip("直接視認状態で使用する赤色の△アイコンPrefab")]
    [SerializeField]
    private GameObject _spottedEyeIconPrefab;

    [Header("非表示の楕円軌道")]

    [SerializeField]
    private float _ellipseRadiusX = 700f;

    [SerializeField]
    private float _ellipseRadiusY = 350f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private readonly Dictionary<
        string,
        EyeIconInstance
    > _iconMap = new();

    private readonly HashSet<string>
        _activeGroupIds = new();

    private readonly List<string>
        _removeGroupIds = new();

    // =========================================================
    // Presenter
    // =========================================================

    public override void SetPresenter(
        CS_UIPoliceDetectionPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 描画
    // =========================================================

    public void Render(
        IReadOnlyList<
            CS_PoliceDetectionIconData
        > iconDataList)
    {
        _activeGroupIds.Clear();

        foreach (
            CS_PoliceDetectionIconData data
            in iconDataList)
        {
            if (data.state ==
                CSE_PlayerDetectionState.Safe)
            {
                continue;
            }

            EyeIconInstance icon =
                GetOrCreateIcon(
                    data.groupId,
                    data.state
                );

            if (icon == null)
                continue;

            _activeGroupIds.Add(
                data.groupId
            );

            if (!icon.root.gameObject.activeSelf)
            {
                icon.root.gameObject.SetActive(
                    true
                );
            }

            // 既存の楕円軌道上へ配置する。
            icon.root.anchoredPosition =
                new Vector2(
                    Mathf.Sin(data.angle) *
                        _ellipseRadiusX,

                    Mathf.Cos(data.angle) *
                        _ellipseRadiusY
                );

            // 前方は△、後方は▽にする。
            ApplyDirection(
                data.angle,
                icon
            );

            ApplyAppearance(
                data,
                icon
            );

            ApplyPoliceCount(
                data.policeCount,
                icon.countText
            );
        }

        RemoveUnusedIcons();
    }

    // =========================================================
    // 向き
    // =========================================================

    /// <summary>
    /// プレイヤー前方にいる警察は△、
    /// 後方にいる警察は▽になるようにアイコン画像を回転する。
    /// 人数表示は回転させない。
    /// </summary>
    /// <summary>
    /// 三角アイコンの先端が、
    /// 楕円の中心から外側へ向くように回転する。
    /// 人数表示は回転させない。
    /// </summary>
    private static void ApplyDirection(
        float angle,
        EyeIconInstance icon)
    {
        if (icon == null ||
            icon.image == null)
        {
            return;
        }

        // 位置計算が
        // X = Sin(angle)
        // Y = Cos(angle)
        // なので、angleをZ軸の時計回り方向へ変換する。
        float directionRotation =
            -angle * Mathf.Rad2Deg;

        icon.image.rectTransform.localRotation =
            icon.baseImageRotation *
            Quaternion.Euler(
                0f,
                0f,
                directionRotation
            );
    }

    // =========================================================
    // 見た目
    // =========================================================

    private void ApplyAppearance(
        CS_PoliceDetectionIconData data,
        EyeIconInstance icon)
    {
        if (data.state ==
            CSE_PlayerDetectionState.Spotted)
        {
            ApplySpottedAnimation(
                icon.root,
                icon.image
            );

            return;
        }

        ApplyTrackedAnimation(
            icon.root,
            icon.image
        );
    }

    private void ApplySpottedAnimation(
        RectTransform iconRect,
        Image iconImage)
    {
        // TODO: 直接視認中のUIアニメーション確定後に実装する。
    }

    private void ApplyTrackedAnimation(
        RectTransform iconRect,
        Image iconImage)
    {
        // TODO: Spotted用アニメーション実装後、通常表示へ戻す処理を書く。
    }

    private static void ApplyPoliceCount(
        int policeCount,
        TextMeshProUGUI countText)
    {
        if (countText == null)
            return;

        bool showCount =
            policeCount > 1;

        countText.gameObject.SetActive(
            showCount
        );

        if (showCount)
        {
            countText.text =
                $"×{policeCount}";
        }
    }

    // =========================================================
    // アイコン生成
    // =========================================================

    private EyeIconInstance GetOrCreateIcon(
        string groupId,
        CSE_PlayerDetectionState state)
    {
        if (_iconMap.TryGetValue(
            groupId,
            out EyeIconInstance existing))
        {
            // 状態が変わっていなければ、
            // 既存のPrefabインスタンスを使用する。
            if (existing.state == state)
            {
                return existing;
            }

            // TrackedとSpottedが切り替わった場合は、
            // 状態に対応するPrefabへ差し替える。
            RemoveIcon(groupId);
        }

        GameObject prefab =
            GetPrefab(state);

        if (_iconRoot == null ||
            prefab == null)
        {
            return null;
        }

        GameObject iconObject =
            Instantiate(
                prefab,
                _iconRoot
            );

        RectTransform iconRect =
            iconObject.GetComponent<
                RectTransform
            >();

        Image iconImage =
            iconObject.GetComponentInChildren<
                Image
            >(true);

        TextMeshProUGUI countText =
            iconObject.GetComponentInChildren<
                TextMeshProUGUI
            >(true);

        if (iconRect == null ||
            iconImage == null)
        {
            Destroy(iconObject);
            return null;
        }

        iconImage.raycastTarget =
            false;

        var instance =
            new EyeIconInstance(
                iconRect,
                iconImage,
                countText,
                state
            );

        _iconMap[groupId] =
            instance;

        return instance;
    }

    /// <summary>
    /// 検知状態に対応するPrefabを取得する。
    /// </summary>
    private GameObject GetPrefab(
        CSE_PlayerDetectionState state)
    {
        return state switch
        {
            CSE_PlayerDetectionState.Tracked =>
                _trackedEyeIconPrefab,

            CSE_PlayerDetectionState.Spotted =>
                _spottedEyeIconPrefab,

            _ => null,
        };
    }

    // =========================================================
    // アイコン削除
    // =========================================================

    private void RemoveUnusedIcons()
    {
        _removeGroupIds.Clear();

        foreach (var pair in _iconMap)
        {
            if (!_activeGroupIds.Contains(
                pair.Key))
            {
                _removeGroupIds.Add(
                    pair.Key
                );
            }
        }

        foreach (
            string groupId
            in _removeGroupIds)
        {
            RemoveIcon(groupId);
        }
    }

    private void RemoveIcon(
        string groupId)
    {
        if (!_iconMap.TryGetValue(
            groupId,
            out EyeIconInstance icon))
        {
            return;
        }

        if (icon.root != null)
        {
            Destroy(
                icon.root.gameObject
            );
        }

        _iconMap.Remove(groupId);
    }

    // =========================================================
    // 終了処理
    // =========================================================

    private void OnDestroy()
    {
        _removeGroupIds.Clear();

        foreach (var pair in _iconMap)
        {
            _removeGroupIds.Add(
                pair.Key
            );
        }

        foreach (
            string groupId
            in _removeGroupIds)
        {
            RemoveIcon(groupId);
        }

        _removeGroupIds.Clear();
        _activeGroupIds.Clear();
    }

    // =========================================================
    // アイコンインスタンス
    // =========================================================

    private sealed class EyeIconInstance
    {
        public RectTransform root
        {
            get;
        }

        public Image image
        {
            get;
        }

        public TextMeshProUGUI countText
        {
            get;
        }

        /// <summary>
        /// このインスタンスが使用している状態。
        /// 状態変更時のPrefab差し替え判定に使う。
        /// </summary>
        public CSE_PlayerDetectionState state
        {
            get;
        }

        /// <summary>
        /// Prefabに設定されているImageの初期回転。
        /// 前後判定による回転を毎フレーム安全に適用するため保持する。
        /// </summary>
        public Quaternion baseImageRotation
        {
            get;
        }

        public EyeIconInstance(
            RectTransform root,
            Image image,
            TextMeshProUGUI countText,
            CSE_PlayerDetectionState state)
        {
            this.root = root;
            this.image = image;
            this.countText = countText;
            this.state = state;

            baseImageRotation =
                image != null
                    ? image.rectTransform
                        .localRotation
                    : Quaternion.identity;
        }
    }
}