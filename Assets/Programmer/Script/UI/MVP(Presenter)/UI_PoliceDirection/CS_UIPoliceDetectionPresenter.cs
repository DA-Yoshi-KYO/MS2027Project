/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-13 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

public class CS_UIPoliceDetectionPresenter : CS_BasePresenter
{
    [SerializeField] private bool _combineNearbyPolice = true;
    [Range(0f, 180f)][SerializeField] private float _combineAngleThreshold = 15f;
    [Min(0f)][SerializeField] private float _combineDistanceThreshold = 5f;

    private CS_UIPoliceDetectionModel _model;
    private CS_UIPoliceDetectionView _view;
    private Transform _playerTransform;
    private readonly List<CalculatedPolice> _calculated = new();
    private readonly List<PoliceGroup> _groups = new();
    private readonly List<CS_PoliceDetectionIconData> _renderData = new();

    private void Awake()
    {
        _view = GetComponent<CS_UIPoliceDetectionView>();
        if (_view != null)
            _view.SetPresenter(this);
    }

    public void BindModel(CS_UIPoliceDetectionModel model) => _model = model;
    public void SetPlayerTransform(Transform playerTransform) => _playerTransform = playerTransform;

    private void LateUpdate()
    {
        if (_model == null || _view == null || _playerTransform == null)
            return;

        _calculated.Clear();
        foreach (var pair in _model.GetPoliceMap())
        {
            CS_PoliceDetectionData data = pair.Value;
            if (data == null || data.policeTransform == null ||
                data.currentState == CSE_PoliceDetectionState.Patrol)
                continue;

            Vector3 direction = data.policeTransform.position - _playerTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                continue;

            float distance = direction.magnitude;
            Vector3 local = _playerTransform.InverseTransformDirection(direction.normalized);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            _calculated.Add(new CalculatedPolice(data.policeId, angle, distance, data.currentState));
        }

        BuildGroups();
        _renderData.Clear();
        foreach (PoliceGroup group in _groups)
        {
            _renderData.Add(new CS_PoliceDetectionIconData(
                group.groupId,
                group.averageAngle * Mathf.Deg2Rad,
                group.nearestDistance,
                group.displayState,
                group.count));
        }

        _view.Render(_renderData);
    }

    private void BuildGroups()
    {
        _groups.Clear();
        foreach (CalculatedPolice police in _calculated)
        {
            PoliceGroup target = null;
            if (_combineNearbyPolice)
            {
                foreach (PoliceGroup group in _groups)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(group.averageAngle, police.angle)) <= _combineAngleThreshold &&
                        Mathf.Abs(group.averageDistance - police.distance) <= _combineDistanceThreshold)
                    {
                        target = group;
                        break;
                    }
                }
            }

            if (target == null)
                _groups.Add(new PoliceGroup(police));
            else
                target.Add(police);
        }
    }

    private readonly struct CalculatedPolice
    {
        public string id { get; }
        public float angle { get; }
        public float distance { get; }
        public CSE_PoliceDetectionState state { get; }

        public CalculatedPolice(string id, float angle, float distance, CSE_PoliceDetectionState state)
        {
            this.id = id;
            this.angle = angle;
            this.distance = distance;
            this.state = state;
        }
    }

    private sealed class PoliceGroup
    {
        public string groupId { get; }
        public int count { get; private set; }
        public float nearestDistance { get; private set; } = float.MaxValue;
        public CSE_PoliceDetectionState displayState { get; private set; }
        private float _x;
        private float _y;
        private float _totalDistance;

        public float averageAngle => Mathf.Atan2(_x, _y) * Mathf.Rad2Deg;
        public float averageDistance => count > 0 ? _totalDistance / count : 0f;

        public PoliceGroup(CalculatedPolice police)
        {
            groupId = police.id;
            Add(police);
        }

        public void Add(CalculatedPolice police)
        {
            float rad = police.angle * Mathf.Deg2Rad;
            _x += Mathf.Sin(rad);
            _y += Mathf.Cos(rad);
            _totalDistance += police.distance;
            nearestDistance = Mathf.Min(nearestDistance, police.distance);
            if ((int)police.state > (int)displayState)
                displayState = police.state;
            count++;
        }
    }
}
