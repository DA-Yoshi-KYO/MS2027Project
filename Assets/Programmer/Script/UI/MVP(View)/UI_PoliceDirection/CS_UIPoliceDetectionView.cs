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
    [SerializeField] private RectTransform _iconRoot;
    [SerializeField] private GameObject _eyeIconPrefab;

    [Header("非表示の楕円軌道")]
    [SerializeField] private float _ellipseRadiusX = 700f;
    [SerializeField] private float _ellipseRadiusY = 350f;

    [Header("色設定")]
    [Min(0f)]
    [SerializeField] private float _dangerDistance = 5f;
    [SerializeField] private Color _trackedColor = Color.yellow;
    [SerializeField] private Color _dangerColor = Color.red;

    private readonly Dictionary<string, EyeIconInstance> _iconMap = new();
    private readonly HashSet<string> _activeGroupIds = new();
    private readonly List<string> _removeGroupIds = new();

    public override void SetPresenter(
        CS_UIPoliceDetectionPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    public void Render(
        IReadOnlyList<CS_PoliceDetectionIconData> iconDataList)
    {
        _activeGroupIds.Clear();

        foreach (CS_PoliceDetectionIconData data in iconDataList)
        {
            if (data.state == CSE_PlayerDetectionState.Safe)
                continue;

            EyeIconInstance icon = GetOrCreateIcon(data.groupId);

            if (icon == null)
                continue;

            _activeGroupIds.Add(data.groupId);
            icon.root.gameObject.SetActive(true);

            icon.root.anchoredPosition = new Vector2(
                Mathf.Sin(data.angle) * _ellipseRadiusX,
                Mathf.Cos(data.angle) * _ellipseRadiusY
            );

            ApplyAppearance(data, icon);
            ApplyPoliceCount(data.policeCount, icon.countText);
        }

        RemoveUnusedIcons();
    }

    private void ApplyAppearance(
        CS_PoliceDetectionIconData data,
        EyeIconInstance icon)
    {
        if (data.state == CSE_PlayerDetectionState.Spotted)
        {
            icon.image.color = _dangerColor;
            ApplySpottedAnimation(icon.root, icon.image);
            return;
        }

        // Trackedは距離で黄色と赤を切り替える。
        icon.image.color = data.distance <= _dangerDistance
            ? _dangerColor
            : _trackedColor;

        ApplyTrackedAnimation(icon.root, icon.image);
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

        bool showCount = policeCount > 1;
        countText.gameObject.SetActive(showCount);

        if (showCount)
            countText.text = $"×{policeCount}";
    }

    private EyeIconInstance GetOrCreateIcon(string groupId)
    {
        if (_iconMap.TryGetValue(groupId, out EyeIconInstance existing))
            return existing;

        if (_iconRoot == null || _eyeIconPrefab == null)
            return null;

        GameObject iconObject = Instantiate(_eyeIconPrefab, _iconRoot);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        Image iconImage = iconObject.GetComponentInChildren<Image>(true);
        TextMeshProUGUI countText =
            iconObject.GetComponentInChildren<TextMeshProUGUI>(true);

        if (iconRect == null || iconImage == null)
        {
            Destroy(iconObject);
            return null;
        }

        iconImage.raycastTarget = false;

        var instance = new EyeIconInstance(iconRect, iconImage, countText);
        _iconMap[groupId] = instance;
        return instance;
    }

    private void RemoveUnusedIcons()
    {
        _removeGroupIds.Clear();

        foreach (var pair in _iconMap)
        {
            if (!_activeGroupIds.Contains(pair.Key))
                _removeGroupIds.Add(pair.Key);
        }

        foreach (string groupId in _removeGroupIds)
        {
            if (_iconMap[groupId].root != null)
                Destroy(_iconMap[groupId].root.gameObject);

            _iconMap.Remove(groupId);
        }
    }

    private sealed class EyeIconInstance
    {
        public RectTransform root { get; }
        public Image image { get; }
        public TextMeshProUGUI countText { get; }

        public EyeIconInstance(
            RectTransform root,
            Image image,
            TextMeshProUGUI countText)
        {
            this.root = root;
            this.image = image;
            this.countText = countText;
        }
    }
}
