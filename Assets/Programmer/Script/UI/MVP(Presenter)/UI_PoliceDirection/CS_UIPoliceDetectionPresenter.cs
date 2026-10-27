/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-13 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Safe以外の警察について、プレイヤーから見た方向と距離を計算するPresenter。
/// </summary>
public class CS_UIPoliceDetectionPresenter : CS_BasePresenter
{
    [Header("近い警察を1アイコンへまとめる")]
    [SerializeField] private bool _combineNearbyPolice = true;

    [Range(0f, 180f)]
    [SerializeField] private float _combineAngleThreshold = 15f;

    [Min(0f)]
    [SerializeField] private float _combineDistanceThreshold = 5f;

    private CS_UIPoliceDetectionModel _model;
    private CS_UIPoliceDetectionView _view;
    private Transform _playerTransform;

    private readonly List<CalculatedPolice> _calculatedPolice = new();
    private readonly List<PoliceGroup> _groups = new();
    private readonly List<CS_PoliceDetectionIconData> _renderData = new();

    private void Awake()
    {
        _view = GetComponent<CS_UIPoliceDetectionView>();

        if (_view == null)
        {
            Debug.LogError(
                "[CS_UIPoliceDetectionPresenter] Viewがありません。",
                this
            );
            return;
        }

        _view.SetPresenter(this);
    }

    public void BindModel(CS_UIPoliceDetectionModel model)
    {
        _model = model;
    }

    public void SetPlayerTransform(Transform playerTransform)
    {
        _playerTransform = playerTransform;
    }

    private void LateUpdate()
    {
        if (_model == null || _view == null || _playerTransform == null)
            return;

        BuildCalculatedPolice();
        BuildGroups();
        BuildRenderData();
        _view.Render(_renderData);
    }

    private void BuildCalculatedPolice()
    {
        _calculatedPolice.Clear();

        foreach (var pair in _model.GetPoliceMap())
        {
            CS_PoliceDetectionData data = pair.Value;

            if (data == null ||
                data.policeTransform == null ||
                data.currentState == CSE_PlayerDetectionState.Safe)
            {
                continue;
            }

            Vector3 direction =
                data.policeTransform.position - _playerTransform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
                continue;

            float distance = direction.magnitude;
            Vector3 localDirection =
                _playerTransform.InverseTransformDirection(
                    direction.normalized
                );

            float angle = Mathf.Atan2(
                localDirection.x,
                localDirection.z
            ) * Mathf.Rad2Deg;

            _calculatedPolice.Add(
                new CalculatedPolice(
                    data.policeId,
                    angle,
                    distance,
                    data.currentState
                )
            );
        }
    }

    private void BuildGroups()
    {
        _groups.Clear();

        foreach (CalculatedPolice police in _calculatedPolice)
        {
            PoliceGroup targetGroup = null;

            if (_combineNearbyPolice)
            {
                foreach (PoliceGroup group in _groups)
                {
                    float angleDifference = Mathf.Abs(
                        Mathf.DeltaAngle(group.averageAngle, police.angle)
                    );

                    float distanceDifference = Mathf.Abs(
                        group.averageDistance - police.distance
                    );

                    if (angleDifference <= _combineAngleThreshold &&
                        distanceDifference <= _combineDistanceThreshold)
                    {
                        targetGroup = group;
                        break;
                    }
                }
            }

            if (targetGroup == null)
                _groups.Add(new PoliceGroup(police));
            else
                targetGroup.Add(police);
        }
    }

    private void BuildRenderData()
    {
        _renderData.Clear();

        foreach (PoliceGroup group in _groups)
        {
            _renderData.Add(
                new CS_PoliceDetectionIconData(
                    group.groupId,
                    group.averageAngle * Mathf.Deg2Rad,
                    group.nearestDistance,
                    group.displayState,
                    group.count
                )
            );
        }
    }

    private readonly struct CalculatedPolice
    {
        public string id { get; }
        public float angle { get; }
        public float distance { get; }
        public CSE_PlayerDetectionState state { get; }

        public CalculatedPolice(
            string id,
            float angle,
            float distance,
            CSE_PlayerDetectionState state)
        {
            this.id = id;
            this.angle = angle;
            this.distance = distance;
            this.state = state;
        }
    }

    private sealed class PoliceGroup
    {
        private float _directionX;
        private float _directionY;
        private float _totalDistance;

        public string groupId { get; }
        public int count { get; private set; }
        public float nearestDistance { get; private set; } = float.MaxValue;
        public CSE_PlayerDetectionState displayState { get; private set; }

        public float averageAngle =>
            Mathf.Atan2(_directionX, _directionY) * Mathf.Rad2Deg;

        public float averageDistance =>
            count > 0 ? _totalDistance / count : 0f;

        public PoliceGroup(CalculatedPolice police)
        {
            groupId = police.id;
            displayState = CSE_PlayerDetectionState.Safe;
            Add(police);
        }

        public void Add(CalculatedPolice police)
        {
            float radians = police.angle * Mathf.Deg2Rad;

            _directionX += Mathf.Sin(radians);
            _directionY += Mathf.Cos(radians);
            _totalDistance += police.distance;
            nearestDistance = Mathf.Min(nearestDistance, police.distance);

            // SpottedをTrackedより優先する。
            if ((int)police.state > (int)displayState)
                displayState = police.state;

            count++;
        }
    }
}
