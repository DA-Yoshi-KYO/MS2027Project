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
/// ローカルプレイヤーから見た警察の角度・距離を計算し、
/// 近い方向・距離の警察をまとめてViewへ渡すPresenter。
/// 状態はReactivePropertyに保持され、方向・距離だけ毎フレーム計算する。
/// </summary>
public class CS_UIPoliceDetectionPresenter : CS_BasePresenter
{
    [Header("近い警察を1アイコンへまとめる")]
    [SerializeField] private bool _combineNearbyPolice = true;

    [Header("まとめる最大角度差（度）")]
    [Range(0f, 180f)]
    [SerializeField] private float _combineAngleThreshold = 15f;

    [Header("まとめる最大距離差（m）")]
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
                "[CS_UIPoliceDetectionPresenter] Viewがありません。"
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
        if (_model == null ||
            _view == null ||
            _playerTransform == null)
        {
            return;
        }

        BuildCalculatedPolice();
        BuildGroups();
        BuildRenderData();
        _view.Render(_renderData);
    }

    private void BuildCalculatedPolice()
    {
        _calculatedPolice.Clear();

        foreach (var pair in _model.GetVisiblePoliceMap())
        {
            CS_PoliceDetectionData policeData = pair.Value;

            if (policeData == null ||
                policeData.policeTransform == null ||
                policeData.currentPoliceState ==
                    CSE_PoliceDetectionState.Hidden)
            {
                continue;
            }

            Vector3 worldDirection =
                policeData.policeTransform.position -
                _playerTransform.position;

            worldDirection.y = 0f;

            if (worldDirection.sqrMagnitude <= 0.0001f)
                continue;

            float distance = worldDirection.magnitude;

            Vector3 localDirection =
                _playerTransform.InverseTransformDirection(
                    worldDirection.normalized
                );

            float angle = Mathf.Atan2(
                localDirection.x,
                localDirection.z
            ) * Mathf.Rad2Deg;

            _calculatedPolice.Add(
                new CalculatedPolice(
                    policeData.policeId,
                    angle,
                    distance,
                    policeData.currentPoliceState
                )
            );
        }
    }

    private void BuildGroups()
    {
        _groups.Clear();

        foreach (CalculatedPolice police in _calculatedPolice)
        {
            if (!_combineNearbyPolice)
            {
                _groups.Add(new PoliceGroup(police));
                continue;
            }

            PoliceGroup targetGroup = null;

            foreach (PoliceGroup group in _groups)
            {
                float angleDifference = Mathf.Abs(
                    Mathf.DeltaAngle(
                        group.averageAngle,
                        police.angle
                    )
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

    protected override void OnDestroy()
    {
        _calculatedPolice.Clear();
        _groups.Clear();
        _renderData.Clear();
        base.OnDestroy();
    }

    private readonly struct CalculatedPolice
    {
        private readonly string _policeId;
        private readonly float _angle;
        private readonly float _distance;
        private readonly CSE_PoliceDetectionState _policeState;

        public string policeId => _policeId;
        public float angle => _angle;
        public float distance => _distance;
        public CSE_PoliceDetectionState policeState => _policeState;

        public CalculatedPolice(
            string policeId,
            float angle,
            float distance,
            CSE_PoliceDetectionState policeState)
        {
            _policeId = policeId;
            _angle = angle;
            _distance = distance;
            _policeState = policeState;
        }
    }

    private class PoliceGroup
    {
        private readonly string _groupId;
        private float _directionX;
        private float _directionY;
        private float _totalDistance;
        private float _nearestDistance;
        private CSE_PoliceDetectionState _displayState;
        private int _count;

        public string groupId => _groupId;
        public int count => _count;
        public float nearestDistance => _nearestDistance;
        public CSE_PoliceDetectionState displayState => _displayState;

        public float averageAngle =>
            Mathf.Atan2(_directionX, _directionY) * Mathf.Rad2Deg;

        public float averageDistance =>
            _count > 0 ? _totalDistance / _count : 0f;

        public PoliceGroup(CalculatedPolice police)
        {
            _groupId = police.policeId;
            _nearestDistance = float.MaxValue;
            _displayState = CSE_PoliceDetectionState.Hidden;
            Add(police);
        }

        public void Add(CalculatedPolice police)
        {
            float angleRadians = police.angle * Mathf.Deg2Rad;

            _directionX += Mathf.Sin(angleRadians);
            _directionY += Mathf.Cos(angleRadians);
            _totalDistance += police.distance;

            _nearestDistance = Mathf.Min(
                _nearestDistance,
                police.distance
            );

            if ((int)police.policeState > (int)_displayState)
                _displayState = police.policeState;

            _count++;
        }
    }
}
