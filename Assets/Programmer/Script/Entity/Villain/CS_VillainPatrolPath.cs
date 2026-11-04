using UnityEngine;

/*
 * 悪人の巡回ルート1本分の形(地点を順につないだ折れ線)
 * 進み具合(0～1。0が最初の地点、1が最後の地点)から、ルート上の位置を求める
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・CS_VillainGroupが、スポーン位置の巡回ルート(CS_VillainPatrolRoute)ごとに1つ作る
 *   ルートが無いスポーン位置では、路地裏の両端(CS_VillainSpawnPoint.GetPatrolEnds)の2点で作る
 * ・進み具合は道のりの割合(地点の数ではなく、長さで割る)。0.5ならルートの道のりのちょうど真ん中
 * ・地点はNavMeshの上に合わせた位置で受け取る(作る側で合わせる)
 */
// ========================================

public class CS_VillainPatrolPath
{
    private readonly Vector3[] _points;
    private readonly float[] _distances;   // 最初の地点から各地点までの道のり(m)
    private readonly float _length;

    public float length => _length;   // ルート全体の道のり(m)

    public CS_VillainPatrolPath(Vector3[] points)
    {
        _points = points;
        _distances = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
        {
            _distances[i] = _distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }
        _length = points.Length > 0 ? _distances[points.Length - 1] : 0f;
    }

    // 進み具合(0～1)の位置
    public Vector3 Evaluate(float progress)
    {
        if (_points.Length == 1 || _length <= 0f) return _points[0];

        float distance = Mathf.Clamp01(progress) * _length;
        for (int i = 1; i < _points.Length; i++)
        {
            if (distance > _distances[i]) continue;

            float segment = _distances[i] - _distances[i - 1];
            float t = segment > 0f ? (distance - _distances[i - 1]) / segment : 0f;
            return Vector3.Lerp(_points[i - 1], _points[i], t);
        }
        return _points[_points.Length - 1];
    }

    // 進み具合(0～1)の位置での、進む向き(水平。direction が + なら最後の地点へ向かう向き)
    public Vector3 GetForward(float progress, int direction)
    {
        const float sampleStep = 0.01f;   // 向きを求めるために、少し先の位置を見る割合

        Vector3 here = Evaluate(progress);
        Vector3 ahead = Evaluate(progress + sampleStep * direction);
        Vector3 forward = ahead - here;
        if (forward.sqrMagnitude <= 0f) forward = here - Evaluate(progress - sampleStep * direction);   // 端では手前の位置から求める
        forward.y = 0f;
        return forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
    }
}
