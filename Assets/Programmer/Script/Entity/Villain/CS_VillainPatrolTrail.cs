using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/*
 * 巡回の先頭が通った道(足跡)
 * 隊列で付いてくる人は、先頭の今の向きではなく、この足跡の上で「先頭から何m後ろか」の位置を歩く(ルートの外を通らないため)
 * ゲーム中(CS_VillainPatrolCrew)と、巡回エディタのプレビューの両方で使う
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・付いてくる人の位置(GetFollowerPosition)
 *   隊列の形の位置(CSO_VillainPatrolPattern.GetFormationOffset。x: 右 / z: 前)を、次のように足跡に当てはめる
 *     後ろ(-z)の分: 先頭の今の位置から、足跡を後ろ(古い方)へその距離だけたどった所
 *     横(x)の分  : その所での進む向きに対して、右(+)・左(-)へずらす
 *   → 縦一列はルートの線の上をそのままなぞる。曲がり角でも、先頭が曲がった所で曲がる
 *   → 先頭と同じ高さの横並び(横一列)は、先頭の進む向きに対して横に並ぶ
 *   あわせて、その所での進む向きも返す(付いてくる人は先頭の向きではなく、自分の進む向きを向く)
 * ・足跡は先頭が recordStep(m) 進むごとに1点ずつ足し、maxLength(m) より古い所は捨てる
 *   最初は、ルートを1周してポイント1へ戻ってくる道を逆にたどった足跡を入れておく(CreateSeed)
 *   → 湧いた時も、付いてくる人がルートの上に並ぶ(周回なら最後のポイント→ポイント1、往復ならポイント2→ポイント1の道の上)
 *   → ポイントが1つしかない時は、湧いた時の向きの真後ろへまっすぐ伸ばす
 *   足跡が足りない時は、一番古い所から同じ向きにまっすぐ伸ばす
 */
// ========================================

public class CS_VillainPatrolTrail
{
    private const float _recordStep = 0.25f;   // 足跡を足す間隔(m)
    private const int _maxSeedLegs = 64;       // 最初の足跡を作る時に、さかのぼるポイントの間の道の数の上限(全部のポイントが同じ位置の時に止まるため)

    private readonly List<Vector3> _points = new List<Vector3>();   // 古い順
    private readonly float _maxLength;

    // seed: 最初の足跡(古い順。CreateSeedで作る) / maxLength: 一番後ろの人の距離より少し長く
    public CS_VillainPatrolTrail(List<Vector3> seed, float maxLength)
    {
        _maxLength = maxLength;
        _points.AddRange(seed);
        TrimOld();
    }

    // 最初の足跡を作る(古い順)。ルートを1周してポイント1へ戻ってくる道を、ポイント1から逆にlength(m)たどる
    // points: ポイントの位置(NavMeshの上) / order: 1周で回るポイントの順番 / startForward: ポイントが1つの時に伸ばす向き
    public static List<Vector3> CreateSeed(Vector3[] points, List<int> order, Vector3 startForward, float length)
    {
        List<Vector3> backward = new List<Vector3> { points[0] };   // 新しい順(ポイント1から後ろへ)
        if (points.Length < 2)
        {
            backward.Add(points[0] - Flat(startForward).normalized * length);
            backward.Reverse();
            return backward;
        }

        // 1周(ポイント1から回って、ポイント1へ戻るまで)の並びを、後ろから前へたどる(足りなければ前の周もたどる)
        List<int> cycle = new List<int>(order) { 0 };
        float total = 0f;
        int index = cycle.Count - 1;
        for (int guard = 0; total < length && guard < _maxSeedLegs; guard++)
        {
            if (index == 0) index = cycle.Count - 1;   // 1周さかのぼった(最初も最後もポイント1)

            Vector3[] corners = GetPathCorners(points[cycle[index - 1]], points[cycle[index]]);
            for (int i = corners.Length - 2; i >= 0; i--)
            {
                total += Flat(corners[i + 1] - corners[i]).magnitude;
                backward.Add(corners[i]);
            }
            index--;
        }

        backward.Reverse();
        return backward;
    }

    // fromからtoまでNavMeshで通る道の点(通れなければ直線)
    private static Vector3[] GetPathCorners(Vector3 from, Vector3 to)
    {
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2)
        {
            return path.corners;
        }
        return new[] { from, to };
    }

    // 先頭の今の位置を足跡に足す(recordStep以上進んでいたら)
    public void Record(Vector3 position)
    {
        if (Flat(position - _points[_points.Count - 1]).magnitude < _recordStep) return;

        _points.Add(position);
        TrimOld();
    }

    // 隊列のfollowerIndex番目の人の位置と、そこでの進む向き(水平)
    // head: 先頭の今の位置 / ahead: 隊列の位置より、足跡の上でどれだけ前(先頭の側)を求めるか(m。先頭は越えない)
    public Vector3 GetFollowerPosition(Vector3 head, CSO_VillainPatrolPattern.Formation formation, int followerIndex, out Vector3 forward, float ahead = 0f)
    {
        return GetFollowerPosition(_points, head, formation, followerIndex, out forward, ahead);
    }

    // 足跡(古い順)から、隊列のfollowerIndex番目の人の位置と、そこでの進む向き(水平)を求める
    public static Vector3 GetFollowerPosition(IReadOnlyList<Vector3> trail, Vector3 head,
        CSO_VillainPatrolPattern.Formation formation, int followerIndex, out Vector3 forward, float ahead = 0f)
    {
        Vector3 offset = CSO_VillainPatrolPattern.GetFormationOffset(formation.shape, followerIndex, formation.spacing);
        float back = Mathf.Max(0f, -offset.z - ahead);

        Walk(trail, head, back, out Vector3 position, out forward);
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
        return position + right * offset.x;
    }

    // headから足跡を後ろへdistanceだけたどった位置と、そこでの進む向き
    private static void Walk(IReadOnlyList<Vector3> trail, Vector3 head, float distance, out Vector3 position, out Vector3 forward)
    {
        Vector3 newer = head;
        forward = Vector3.forward;
        for (int i = trail.Count - 1; i >= 0; i--)
        {
            Vector3 older = trail[i];
            Vector3 segment = newer - older;
            float length = Flat(segment).magnitude;
            if (length <= 0.0001f)
            {
                newer = older;
                continue;
            }

            forward = Flat(segment) / length;
            if (distance <= length)
            {
                position = Vector3.Lerp(newer, older, distance / length);
                return;
            }

            distance -= length;
            newer = older;
        }

        // 足跡が足りなければ、一番古い所から同じ向きにまっすぐ伸ばす
        position = newer - forward * distance;
    }

    // 長さがmaxLengthを超えた分の古い足跡を捨てる(2点は残す)
    private void TrimOld()
    {
        float length = 0f;
        for (int i = _points.Count - 1; i > 0; i--)
        {
            length += Flat(_points[i] - _points[i - 1]).magnitude;
            if (length <= _maxLength) continue;

            _points.RemoveRange(0, Mathf.Min(i - 1, _points.Count - 2));
            return;
        }
    }

    private static Vector3 Flat(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }
}
