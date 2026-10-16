/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-19 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1つの中心(標的・事件の現場)の周りに、警察ごとの別々の位置を割り当てるクラス
/// ・警察は中心を囲む円周上に並ぶ。円周に入りきらない分は、外側の円周に並ぶ
/// ・各警察の位置は、その警察が来た方向を基準にする(中心の反対側へ回り込ませず、標的を追い越さないようにする)
///   同じ方向から来た警察は、隣と重ならないよう、来た方向の近くで少しずつずらして並べる
/// ・割り当ては、警察が加わった時と、標的が動いて位置が反対側になった警察がいる時だけ作り直す
///   (警察が減った時は作り直さず、残った警察が動かないようにする)
/// 位置の計算はサーバー(またはオフライン)だけで行う
/// </summary>
public class CS_PoliceSlotRing
{
    // 警察の1人あたりの間隔(エージェントの直径より少し広くとる)
    private const float _slotSpacing = 1.2f;

    // 隣と近すぎる位置を押し広げる計算の、最大の繰り返し回数
    private const int _spreadIterations = 40;

    // 割り当てられた位置が、警察が来た方向からこの角度(度)より離れたら、反対側とみなす
    private const float _farSideAngle = 110.0f;

    // 反対側かどうかは、警察が位置の円周から、この距離より遠い時だけ判定する(近くでは向きが安定しないため)
    private const float _farSideMinDistance = 1.5f;

    // 反対側の警察がいても、割り当てを作り直す間隔(秒)の最小(頻繁に作り直して動きが乱れないようにする)
    private const float _rebuildInterval = 0.5f;

    // 割り当てられた位置(中心から見た角度と、何周目の円周か)
    private readonly struct Slot
    {
        private readonly float _angle;
        private readonly int _layer;

        public float angle => _angle;
        public int layer => _layer;

        public Slot(float angle, int layer)
        {
            _angle = angle;
            _layer = layer;
        }
    }

    // この円に加わっている警察
    private readonly List<CS_PoliceBrain> _members = new List<CS_PoliceBrain>();

    // 警察ごとの割り当て
    private readonly Dictionary<CS_PoliceBrain, Slot> _slots = new Dictionary<CS_PoliceBrain, Slot>();

    // 割り当てを作り直す際に使い回すリスト
    private readonly List<CS_PoliceBrain> _sortBuffer = new List<CS_PoliceBrain>();
    private readonly List<float> _angleBuffer = new List<float>();

    // 次に、反対側の警察がいるかを確認してよい時刻
    private float _nextCheckTime = 0.0f;

    // 割り当てを作り直す必要があるか
    private bool _isDirty = false;

    // 加わっている警察の人数
    public int count => _members.Count;

    /// <summary>
    /// 警察を円に加えるメソッド(割り当ては、次に位置を求める時に作り直す)
    /// </summary>
    /// <param name="member">加える警察</param>
    public void Join(CS_PoliceBrain member)
    {
        if (_members.Contains(member)) return;

        _members.Add(member);
        _isDirty = true;
    }

    /// <summary>
    /// 警察を円から外すメソッド(残った警察の割り当ては変えない)
    /// </summary>
    /// <param name="member">外す警察</param>
    public void Leave(CS_PoliceBrain member)
    {
        _members.Remove(member);
        _slots.Remove(member);
    }

    /// <summary>
    /// 警察に割り当てられた位置を求めるメソッド
    /// </summary>
    /// <param name="member">警察</param>
    /// <param name="center">中心の位置</param>
    /// <param name="baseRadius">警察が最初の円周で中心から離れる距離</param>
    /// <param name="nominalRadius">円周に入る人数を決める基準の半径(最初の円周の半径)</param>
    /// <returns>割り当てられた位置</returns>
    public Vector3 GetPosition(CS_PoliceBrain member, Vector3 center, float baseRadius, float nominalRadius)
    {
        if (_isDirty || !_slots.ContainsKey(member)) Rebuild(center, nominalRadius);

        if (!_slots.TryGetValue(member, out Slot slot)) return center;

        // 標的が動いて、割り当てられた位置が中心の反対側(追い越す向き)になった警察がいれば、次回に作り直す
        if (Time.time >= _nextCheckTime && IsOnFarSide(member, center, slot.angle, baseRadius)) _isDirty = true;

        float radius = baseRadius + slot.layer * _slotSpacing;
        return center + new Vector3(Mathf.Cos(slot.angle), 0.0f, Mathf.Sin(slot.angle)) * radius;
    }

    /// <summary>
    /// 全員の割り当てを作り直すメソッド
    /// 中心に近い警察から内側の円周に並べ、同じ円周の中では、各警察が来た方向の近くに並べる
    /// </summary>
    /// <param name="center">中心の位置</param>
    /// <param name="nominalRadius">最初の円周の半径</param>
    private void Rebuild(Vector3 center, float nominalRadius)
    {
        _isDirty = false;
        _nextCheckTime = Time.time + _rebuildInterval;
        _slots.Clear();

        // 破棄された警察は外す
        _members.RemoveAll(member => member == null);
        if (_members.Count == 0) return;

        // 中心に近い順に並べる(近い警察ほど内側の円周に入る)
        _sortBuffer.Clear();
        _sortBuffer.AddRange(_members);
        _sortBuffer.Sort((a, b) => FlatSqrDistance(a, center).CompareTo(FlatSqrDistance(b, center)));

        int start = 0;
        int layer = 0;
        while (start < _sortBuffer.Count)
        {
            // この円周に入れる人数(円周の長さ ÷ 間隔)
            float layerRadius = nominalRadius + layer * _slotSpacing;
            int capacity = Mathf.Max(1, Mathf.FloorToInt(2.0f * Mathf.PI * layerRadius / _slotSpacing));
            int layerCount = Mathf.Min(capacity, _sortBuffer.Count - start);

            AssignLayer(start, layerCount, layer, layerRadius, center);

            start += layerCount;
            layer++;
        }
    }

    /// <summary>
    /// 1つの円周に並ぶ警察へ、角度を割り当てるメソッド
    /// 各警察が来た方向をそのまま角度にして、隣と近すぎる分だけ押し広げる(中心の反対側へ回り込ませない)
    /// </summary>
    /// <param name="start">並べる警察の、並べ替えたリスト内の開始位置</param>
    /// <param name="layerCount">この円周に並べる人数</param>
    /// <param name="layer">何周目の円周か</param>
    /// <param name="layerRadius">この円周の半径</param>
    /// <param name="center">中心の位置</param>
    private void AssignLayer(int start, int layerCount, int layer, float layerRadius, Vector3 center)
    {
        // 来た方向(中心から見た角度)の順に並べ直す
        _sortBuffer.Sort(start, layerCount, Comparer<CS_PoliceBrain>.Create((a, b) => GetAngle(a, center).CompareTo(GetAngle(b, center))));

        _angleBuffer.Clear();
        for (int i = 0; i < layerCount; i++)
        {
            _angleBuffer.Add(GetAngle(_sortBuffer[start + i], center));
        }

        SpreadAngles(layerRadius);

        for (int i = 0; i < layerCount; i++)
        {
            _slots[_sortBuffer[start + i]] = new Slot(_angleBuffer[i], layer);
        }
    }

    /// <summary>
    /// 隣と近すぎる角度を、順番を保ったまま少しずつ押し広げるメソッド(円周なので、最後と最初も隣として扱う)
    /// </summary>
    /// <param name="radius">円周の半径</param>
    private void SpreadAngles(float radius)
    {
        int count = _angleBuffer.Count;
        if (count < 2) return;

        // 隣の警察との間隔(_slotSpacing)をあけるのに必要な、最小の角度の差
        float minGap = 2.0f * Mathf.Asin(Mathf.Clamp(_slotSpacing / (2.0f * radius), 0.0f, 1.0f));

        for (int iteration = 0; iteration < _spreadIterations; iteration++)
        {
            bool isChanged = false;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                float gap = _angleBuffer[next] - _angleBuffer[i];
                if (next == 0) gap += 2.0f * Mathf.PI;
                if (gap >= minGap) continue;

                // 近すぎる2人を、半分ずつ反対向きにずらす
                float push = (minGap - gap) * 0.5f;
                _angleBuffer[i] -= push;
                _angleBuffer[next] += push;
                isChanged = true;
            }

            if (!isChanged) break;
        }
    }

    /// <summary>
    /// 警察に割り当てられた位置が、警察が来た方向の反対側になっているかを判定するメソッド
    /// </summary>
    /// <param name="member">警察</param>
    /// <param name="center">中心の位置</param>
    /// <param name="slotAngle">割り当てられた角度</param>
    /// <param name="baseRadius">最初の円周の半径</param>
    /// <returns>反対側ならtrue</returns>
    private static bool IsOnFarSide(CS_PoliceBrain member, Vector3 center, float slotAngle, float baseRadius)
    {
        Vector3 offset = member.transform.position - center;
        offset.y = 0.0f;

        // 円周の近くまで来ている警察は、向きが安定しないので判定しない
        float minDistance = baseRadius + _farSideMinDistance;
        if (offset.sqrMagnitude < minDistance * minDistance) return false;

        float memberAngle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
        float difference = Mathf.Abs(Mathf.DeltaAngle(memberAngle, slotAngle * Mathf.Rad2Deg));
        return difference > _farSideAngle;
    }

    /// <summary>
    /// 中心から警察までの水平方向の距離の2乗を求めるメソッド
    /// </summary>
    private static float FlatSqrDistance(CS_PoliceBrain member, Vector3 center)
    {
        Vector3 offset = member.transform.position - center;
        offset.y = 0.0f;
        return offset.sqrMagnitude;
    }

    /// <summary>
    /// 中心から見た警察の角度(水平方向、ラジアン)を求めるメソッド
    /// </summary>
    private static float GetAngle(CS_PoliceBrain member, Vector3 center)
    {
        Vector3 offset = member.transform.position - center;
        return Mathf.Atan2(offset.z, offset.x);
    }
}
