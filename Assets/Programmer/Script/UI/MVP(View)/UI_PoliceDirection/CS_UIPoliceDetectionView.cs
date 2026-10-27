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
    [SerializeField] private Color _trackingColor = Color.yellow;
    [SerializeField] private Color _dangerColor = Color.red;

    private readonly Dictionary<string, EyeIconInstance> _iconMap = new();
    private readonly HashSet<string> _activeIds = new();
    private readonly List<string> _removeIds = new();

    public override void SetPresenter(CS_UIPoliceDetectionPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    public void Render(IReadOnlyList<CS_PoliceDetectionIconData> dataList)
    {
        _activeIds.Clear();

        foreach (CS_PoliceDetectionIconData data in dataList)
        {
            if (data.state == CSE_PoliceDetectionState.Patrol)
                continue;

            EyeIconInstance icon = GetOrCreateIcon(data.groupId);
            if (icon == null)
                continue;

            _activeIds.Add(data.groupId);
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
        if (data.state == CSE_PoliceDetectionState.Detected)
        {
            icon.image.color = _dangerColor;
            ApplyDetectedAnimation(icon.root, icon.image);
            return;
        }

        icon.image.color = data.distance <= _dangerDistance
            ? _dangerColor
            : _trackingColor;

        ApplyNormalAnimation(icon.root, icon.image);
    }

    private void ApplyDetectedAnimation(
        RectTransform iconRect,
        Image iconImage)
    {
        // TODO: 視認中のUIアニメーション確定後に実装する。
    }

    private void ApplyNormalAnimation(
        RectTransform iconRect,
        Image iconImage)
    {
        // TODO: Detected用アニメーション実装後、通常状態へ戻す処理を書く。
    }

    private static void ApplyPoliceCount(int count, TextMeshProUGUI countText)
    {
        if (countText == null)
            return;

        bool visible = count > 1;
        countText.gameObject.SetActive(visible);
        if (visible)
            countText.text = $"×{count}";
    }

    private EyeIconInstance GetOrCreateIcon(string id)
    {
        if (_iconMap.TryGetValue(id, out EyeIconInstance existing))
            return existing;

        if (_iconRoot == null || _eyeIconPrefab == null)
            return null;

        GameObject go = Instantiate(_eyeIconPrefab, _iconRoot);
        RectTransform rect = go.GetComponent<RectTransform>();
        Image image = go.GetComponentInChildren<Image>(true);
        TextMeshProUGUI countText = go.GetComponentInChildren<TextMeshProUGUI>(true);

        if (rect == null || image == null)
        {
            Destroy(go);
            return null;
        }

        image.raycastTarget = false;
        var instance = new EyeIconInstance(rect, image, countText);
        _iconMap[id] = instance;
        return instance;
    }

    private void RemoveUnusedIcons()
    {
        _removeIds.Clear();
        foreach (var pair in _iconMap)
            if (!_activeIds.Contains(pair.Key))
                _removeIds.Add(pair.Key);

        foreach (string id in _removeIds)
        {
            if (_iconMap[id].root != null)
                Destroy(_iconMap[id].root.gameObject);
            _iconMap.Remove(id);
        }
    }

    private sealed class EyeIconInstance
    {
        public RectTransform root { get; }
        public Image image { get; }
        public TextMeshProUGUI countText { get; }

        public EyeIconInstance(RectTransform root, Image image, TextMeshProUGUI countText)
        {
            this.root = root;
            this.image = image;
            this.countText = countText;
        }
    }
}
