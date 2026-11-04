/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-13 | 初回作成
 * ================================================ */

using System.Collections.Generic;

/// <summary>
/// 警察ごとのプレイヤー検知状態を保持するModel。
/// </summary>
public sealed class CS_UIPoliceDetectionModel : CS_BaseModel
{
    private readonly Dictionary<string, CS_PoliceDetectionData>
        _policeMap = new();

    public void RegisterPolice(CS_PoliceDetectionData policeData)
    {
        if (policeData == null ||
            string.IsNullOrWhiteSpace(policeData.policeId) ||
            policeData.policeTransform == null)
        {
            return;
        }

        if (_policeMap.TryGetValue(
            policeData.policeId,
            out CS_PoliceDetectionData oldData))
        {
            oldData.Dispose();
        }

        _policeMap[policeData.policeId] = policeData;
    }

    public void UnregisterPolice(string policeId)
    {
        if (!_policeMap.TryGetValue(
            policeId,
            out CS_PoliceDetectionData policeData))
        {
            return;
        }

        policeData.Dispose();
        _policeMap.Remove(policeId);
    }

    public void SetPoliceState(
        string policeId,
        CSE_PlayerDetectionState state)
    {
        if (_policeMap.TryGetValue(
            policeId,
            out CS_PoliceDetectionData policeData))
        {
            policeData.SetState(state);
        }
    }

    public IReadOnlyDictionary<string, CS_PoliceDetectionData>
        GetPoliceMap()
    {
        return _policeMap;
    }

    public override void Dispose()
    {
        foreach (var pair in _policeMap)
            pair.Value?.Dispose();

        _policeMap.Clear();
    }
}
