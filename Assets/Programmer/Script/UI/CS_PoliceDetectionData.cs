/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-13 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

public enum CSE_PoliceDetectionState
{
    Patrol = 0,   // 巡回中。UI非表示
    Tracking = 1, // 信号を受け取り追跡中
    Detected = 2, // プレイヤーを視認して追跡中
}

public sealed class CS_PoliceDetectionData
{
    private readonly string _policeId;
    private readonly Transform _policeTransform;
    private readonly ReactiveProperty<CSE_PoliceDetectionState> _state;

    public string policeId => _policeId;
    public Transform policeTransform => _policeTransform;
    public ReadOnlyReactiveProperty<CSE_PoliceDetectionState> state => _state;
    public CSE_PoliceDetectionState currentState => _state.Value;

    public CS_PoliceDetectionData(
        string policeId,
        Transform policeTransform,
        CSE_PoliceDetectionState initialState)
    {
        _policeId = policeId;
        _policeTransform = policeTransform;
        _state = new ReactiveProperty<CSE_PoliceDetectionState>(initialState);
    }

    public void SetState(CSE_PoliceDetectionState state)
    {
        _state.Value = state;
    }

    public void Dispose()
    {
        _state.Dispose();
    }
}

public readonly struct CS_PoliceDetectionIconData
{
    private readonly string _groupId;
    private readonly float _angle;
    private readonly float _distance;
    private readonly CSE_PoliceDetectionState _state;
    private readonly int _policeCount;

    public string groupId => _groupId;
    public float angle => _angle;
    public float distance => _distance;
    public CSE_PoliceDetectionState state => _state;
    public int policeCount => _policeCount;

    public CS_PoliceDetectionIconData(
        string groupId,
        float angle,
        float distance,
        CSE_PoliceDetectionState state,
        int policeCount)
    {
        _groupId = groupId;
        _angle = angle;
        _distance = distance;
        _state = state;
        _policeCount = policeCount;
    }
}
