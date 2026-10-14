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
/// 警察視認アイコンを、非表示の楕円軌道上へ表示するView。
/// ・5m以内は赤
/// ・シグナル検知中は黄色
/// ・Hiddenは表示しない
/// ・Found時のアニメーションは関数だけ用意する
/// </summary>
public class CS_UIPoliceDetectionView :
    CS_BaseView<CS_UIPoliceDetectionPresenter>
{
    [Header("アイコンの親")]
    [SerializeField] private RectTransform _iconRoot;

    [Header("目アイコンPrefab")]
    [SerializeField] private GameObject _eyeIconPrefab;

    [Header("非表示の楕円軌道")]
    [Min(0f)]
    [SerializeField] private float _ellipseRadiusX = 700f;

    [Min(0f)]
    [SerializeField] private float _ellipseRadiusY = 350f;

    [Header("距離・シグナルによる色")]
    [Min(0f)]
    [SerializeField] private float _dangerDistance = 5f;

    [SerializeField] private Color _dangerColor = Color.red;
    [SerializeField] private Color _signalColor = Color.yellow;

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
            if (data.policeState == CSE_PoliceDetectionState.Hidden)
                continue;

            EyeIconInstance icon = GetOrCreateIcon(data.groupId);

            if (icon == null)
                continue;

            _activeGroupIds.Add(data.groupId);
            icon.root.gameObject.SetActive(true);

            float positionX =
                Mathf.Sin(data.angle) * _ellipseRadiusX;

            float positionY =
                Mathf.Cos(data.angle) * _ellipseRadiusY;

            icon.root.anchoredPosition =
                new Vector2(positionX, positionY);

            ApplyDetectionColor(
                data.distance,
                data.policeState,
                icon.image
            );

            ApplyFoundAnimation(
                data.policeState,
                icon.root,
                icon.image
            );

            ApplyPoliceCount(data.policeCount, icon.countText);
        }

        RemoveUnusedIcons();
    }

    /// <summary>
    /// 5m以内なら赤、それ以外のシグナル検知中・発見済みは黄色。
    /// </summary>
    private void ApplyDetectionColor(
        float distance,
        CSE_PoliceDetectionState policeState,
        Image iconImage)
    {
        if (iconImage == null)
            return;

        if (distance <= _dangerDistance)
        {
            iconImage.color = _dangerColor;
            return;
        }

        iconImage.color = _signalColor;
    }

    /// <summary>
    /// 発見済み状態のUIアニメーション変更用。
    /// デザイン未定のため、現時点では何もしない。
    /// </summary>
    private void ApplyFoundAnimation(
        CSE_PoliceDetectionState policeState,
        RectTransform iconRect,
        Image iconImage)
    {
        if (policeState != CSE_PoliceDetectionState.Found)
            return;

        // TODO:
        // 見つかった時のデザインが決まったら、
        // 点滅、拡縮、揺れ、AnimatorのTriggerなどを実装する。
    }

    private void ApplyPoliceCount(
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
        if (_iconMap.TryGetValue(
            groupId,
            out EyeIconInstance existing))
        {
            return existing;
        }

        if (_eyeIconPrefab == null || _iconRoot == null)
            return null;

        GameObject iconObject = Instantiate(
            _eyeIconPrefab,
            _iconRoot
        );

        RectTransform iconRect =
            iconObject.GetComponent<RectTransform>();

        Image iconImage =
            iconObject.GetComponentInChildren<Image>(true);

        TextMeshProUGUI countText =
            iconObject.GetComponentInChildren<TextMeshProUGUI>(true);

        if (iconRect == null || iconImage == null)
        {
            Destroy(iconObject);
            return null;
        }

        iconImage.raycastTarget = false;

        var instance = new EyeIconInstance(
            iconRect,
            iconImage,
            countText
        );

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
            EyeIconInstance icon = _iconMap[groupId];

            if (icon.root != null)
                Destroy(icon.root.gameObject);

            _iconMap.Remove(groupId);
        }
    }

    private void OnDestroy()
    {
        foreach (var pair in _iconMap)
        {
            if (pair.Value.root != null)
                Destroy(pair.Value.root.gameObject);
        }

        _iconMap.Clear();
        _activeGroupIds.Clear();
        _removeGroupIds.Clear();
    }

    private class EyeIconInstance
    {
        private readonly RectTransform _root;
        private readonly Image _image;
        private readonly TextMeshProUGUI _countText;

        public RectTransform root => _root;
        public Image image => _image;
        public TextMeshProUGUI countText => _countText;

        public EyeIconInstance(
            RectTransform root,
            Image image,
            TextMeshProUGUI countText)
        {
            _root = root;
            _image = image;
            _countText = countText;
        }
    }
}
