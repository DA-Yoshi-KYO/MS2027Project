/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 警察のグループの隊列を管理するクラス
/// 先頭の警察が通った道筋を記録し、後ろの警察はその道筋を一定間隔あけてたどる
/// (先頭の向きではなく実際に通った道筋を使うので、先頭が曲がっても後ろの警察が回り込まない)
/// </summary>
public class CS_PoliceFormation
{
    // 先頭の通った道筋を記録する間隔(距離)
    private const float _recordStep = 0.25f;

    // 先頭の警察が通った位置の記録(新しい順)
    private readonly List<Vector3> _leaderTrail = new List<Vector3>();

    /// <summary>
    /// 先頭の警察が一定距離進むごとに、その位置を道筋として記録するメソッド
    /// </summary>
    /// <param name="leaderPosition">先頭の警察の現在位置</param>
    /// <param name="keepLength">記録しておく道筋の長さ(一番後ろの警察がたどる距離)</param>
    public void RecordLeaderPosition(Vector3 leaderPosition, float keepLength)
    {
        if (_leaderTrail.Count > 0 && (leaderPosition - _leaderTrail[0]).sqrMagnitude < _recordStep * _recordStep) return;

        _leaderTrail.Insert(0, leaderPosition);

        // 一番後ろの警察がたどる距離より古い記録は不要なので捨てる
        int maxCount = Mathf.CeilToInt(keepLength / _recordStep) + 2;
        if (_leaderTrail.Count > maxCount) _leaderTrail.RemoveRange(maxCount, _leaderTrail.Count - maxCount);
    }

    /// <summary>
    /// 記録した道筋を消すメソッド(先頭の警察が入れ替わった時に使う)
    /// </summary>
    public void Clear()
    {
        _leaderTrail.Clear();
    }

    /// <summary>
    /// 先頭の位置から道筋を指定した距離だけさかのぼった位置を取得するメソッド
    /// </summary>
    /// <param name="leaderPosition">先頭の警察の現在位置</param>
    /// <param name="distanceBehind">先頭からさかのぼる距離</param>
    /// <param name="position">さかのぼった位置</param>
    /// <returns>道筋が足りて位置が求まればtrue(出現直後など、道筋がまだ短い場合はfalse)</returns>
    public bool TryGetPositionBehind(Vector3 leaderPosition, float distanceBehind, out Vector3 position)
    {
        float remainingDistance = distanceBehind;
        Vector3 previousPoint = leaderPosition;

        // 道筋を新しい順にたどり、指定した距離に達する区間の中の位置を求める
        foreach (Vector3 trailPoint in _leaderTrail)
        {
            float segmentLength = Vector3.Distance(previousPoint, trailPoint);
            if (segmentLength >= remainingDistance)
            {
                position = Vector3.Lerp(previousPoint, trailPoint, remainingDistance / segmentLength);
                return true;
            }

            remainingDistance -= segmentLength;
            previousPoint = trailPoint;
        }

        position = previousPoint;
        return false;
    }
}
