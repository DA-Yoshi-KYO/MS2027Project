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
/// 警察視認UIで使用する状態。
/// </summary>
public enum CSE_PoliceDetectionState
{
    Hidden = 0,
    SignalDetected = 1,
    Found = 2,
}

/// <summary>
/// 視認警告UIへ表示する警察1体分のデータ。
/// 警察状態はReactivePropertyで保持する。
/// </summary>
public class CS_PoliceDetectionData
{
    private readonly string _policeId;
    private readonly Transform _policeTransform;

    private readonly ReactiveProperty<CSE_PoliceDetectionState>
        _policeState;

    public string policeId => _policeId;
    public Transform policeTransform => _policeTransform;

    /// <summary>状態変更を購読する場合に使用する。</summary>
    public ReadOnlyReactiveProperty<CSE_PoliceDetectionState>
        policeState => _policeState;

    /// <summary>現在の状態をその場で取得する場合に使用する。</summary>
    public CSE_PoliceDetectionState currentPoliceState =>
        _policeState.Value;

    public CS_PoliceDetectionData(
        string policeId,
        Transform policeTransform,
        CSE_PoliceDetectionState policeState)
    {
        _policeId = policeId;
        _policeTransform = policeTransform;

        _policeState =
            new ReactiveProperty<CSE_PoliceDetectionState>(
                policeState
            );
    }

    public void SetPoliceState(
        CSE_PoliceDetectionState policeState)
    {
        _policeState.Value = policeState;
    }

    public void Dispose()
    {
        _policeState.Dispose();
    }
}

/// <summary>
/// PresenterからViewへ渡す、まとめ済みアイコン1件分のデータ。
/// </summary>
public readonly struct CS_PoliceDetectionIconData
{
    private readonly string _groupId;
    private readonly float _angle;
    private readonly float _distance;
    private readonly CSE_PoliceDetectionState _policeState;
    private readonly int _policeCount;

    public string groupId => _groupId;
    public float angle => _angle;
    public float distance => _distance;
    public CSE_PoliceDetectionState policeState => _policeState;
    public int policeCount => _policeCount;

    public CS_PoliceDetectionIconData(
        string groupId,
        float angle,
        float distance,
        CSE_PoliceDetectionState policeState,
        int policeCount)
    {
        _groupId = groupId;
        _angle = angle;
        _distance = distance;
        _policeState = policeState;
        _policeCount = policeCount;
    }
}
