/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-13 | 初回作成
 * ================================================ */

using System.Collections.Generic;

/// <summary>
/// シグナル検知中・発見済みの警察だけを保持するModel。
/// </summary>
public class CS_UIPoliceDetectionModel : CS_BaseModel
{
    private readonly Dictionary<string, CS_PoliceDetectionData>
        _visiblePoliceMap = new();

    public void RegisterPolice(CS_PoliceDetectionData policeData)
    {
        if (policeData == null ||
            string.IsNullOrWhiteSpace(policeData.policeId) ||
            policeData.policeTransform == null)
        {
            return;
        }

        if (policeData.currentPoliceState ==
            CSE_PoliceDetectionState.Hidden)
        {
            UnregisterPolice(policeData.policeId);
            policeData.Dispose();
            return;
        }

        // 同じIDの古いデータがあれば破棄して差し替える。
        if (_visiblePoliceMap.TryGetValue(
            policeData.policeId,
            out CS_PoliceDetectionData oldData))
        {
            oldData.Dispose();
        }

        _visiblePoliceMap[policeData.policeId] = policeData;
    }

    public void UnregisterPolice(string policeId)
    {
        if (string.IsNullOrWhiteSpace(policeId))
            return;

        if (_visiblePoliceMap.TryGetValue(
            policeId,
            out CS_PoliceDetectionData policeData))
        {
            policeData.Dispose();
            _visiblePoliceMap.Remove(policeId);
        }
    }

    public void SetPoliceState(
        string policeId,
        CSE_PoliceDetectionState policeState)
    {
        if (!_visiblePoliceMap.TryGetValue(
            policeId,
            out CS_PoliceDetectionData policeData))
        {
            return;
        }

        // ReactivePropertyへ状態変更を通知する。
        policeData.SetPoliceState(policeState);

        if (policeState == CSE_PoliceDetectionState.Hidden)
            UnregisterPolice(policeId);
    }

    public IReadOnlyDictionary<string, CS_PoliceDetectionData>
        GetVisiblePoliceMap()
    {
        return _visiblePoliceMap;
    }

    public override void Dispose()
    {
        foreach (var pair in _visiblePoliceMap)
            pair.Value?.Dispose();

        _visiblePoliceMap.Clear();
    }
}
