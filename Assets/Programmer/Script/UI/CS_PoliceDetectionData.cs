/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-13 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// ローカルプレイヤーから見た警察の検知状態。
/// </summary>
public enum CSE_PlayerDetectionState
{
    Safe = 0,    // 追跡されていない。UI非表示
    Tracked = 1, // 追跡・探索対象。5mより遠ければ黄色、5m以内なら赤
    Spotted = 2, // 警察に直接視認されている。赤＋専用アニメーション
}

/// <summary>
/// 警察1体分のプレイヤー検知UIデータ。
/// </summary>
public sealed class CS_PoliceDetectionData
{
    private readonly string _policeId;
    private readonly Transform _policeTransform;
    private readonly ReactiveProperty<CSE_PlayerDetectionState> _state;

    public string policeId => _policeId;
    public Transform policeTransform => _policeTransform;
    public ReadOnlyReactiveProperty<CSE_PlayerDetectionState> state => _state;
    public CSE_PlayerDetectionState currentState => _state.Value;

    public CS_PoliceDetectionData(
        string policeId,
        Transform policeTransform,
        CSE_PlayerDetectionState initialState)
    {
        _policeId = policeId;
        _policeTransform = policeTransform;
        _state = new ReactiveProperty<CSE_PlayerDetectionState>(initialState);
    }

    public void SetState(CSE_PlayerDetectionState state)
    {
        _state.Value = state;
    }

    public void Dispose()
    {
        _state.Dispose();
    }
}

/// <summary>
/// PresenterからViewへ渡すグループ化済みアイコンデータ。
/// </summary>
public readonly struct CS_PoliceDetectionIconData
{
    private readonly string _groupId;
    private readonly float _angle;
    private readonly float _distance;
    private readonly CSE_PlayerDetectionState _state;
    private readonly int _policeCount;

    public string groupId => _groupId;
    public float angle => _angle;
    public float distance => _distance;
    public CSE_PlayerDetectionState state => _state;
    public int policeCount => _policeCount;

    public CS_PoliceDetectionIconData(
        string groupId,
        float angle,
        float distance,
        CSE_PlayerDetectionState state,
        int policeCount)
    {
        _groupId = groupId;
        _angle = angle;
        _distance = distance;
        _state = state;
        _policeCount = policeCount;
    }
}
