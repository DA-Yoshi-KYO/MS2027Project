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
/// ・警察は中心を囲む円周上に等間隔で並ぶ。円周に入りきらない分は、外側の円周に並ぶ
/// ・警察が来た方向の順番を保ったまま割り当てるので、経路が交差しにくい
/// ・割り当ては、警察が加わった時だけ作り直す(減った時は作り直さず、残った警察が動かないようにする)
/// 位置の計算はサーバー(またはオフライン)だけで行う
/// </summary>
public class CS_PoliceSlotRing
{
    // 警察の1人あたりの間隔(エージェントの直径より少し広くとる)
    private const float _slotSpacing = 1.2f;

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

        float radius = baseRadius + slot.layer * _slotSpacing;
        return center + new Vector3(Mathf.Cos(slot.angle), 0.0f, Mathf.Sin(slot.angle)) * radius;
    }

    /// <summary>
    /// 全員の割り当てを作り直すメソッド
    /// 中心に近い警察から内側の円周に並べ、同じ円周の中では来た方向の順番を保って等間隔に並べる
    /// </summary>
    /// <param name="center">中心の位置</param>
    /// <param name="nominalRadius">最初の円周の半径</param>
    private void Rebuild(Vector3 center, float nominalRadius)
    {
        _isDirty = false;
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

            AssignLayer(start, layerCount, layer, center);

            start += layerCount;
            layer++;
        }
    }

    /// <summary>
    /// 1つの円周に並ぶ警察へ、等間隔の角度を割り当てるメソッド
    /// </summary>
    /// <param name="start">並べる警察の、並べ替えたリスト内の開始位置</param>
    /// <param name="layerCount">この円周に並べる人数</param>
    /// <param name="layer">何周目の円周か</param>
    /// <param name="center">中心の位置</param>
    private void AssignLayer(int start, int layerCount, int layer, Vector3 center)
    {
        // 来た方向(中心から見た角度)の順に並べ直す
        _sortBuffer.Sort(start, layerCount, Comparer<CS_PoliceBrain>.Create((a, b) => GetAngle(a, center).CompareTo(GetAngle(b, center))));

        // 一番小さい角度の警察の来た方向から始めて、等間隔に割り当てる(順番が同じなので、経路が交差しにくい)
        float firstAngle = GetAngle(_sortBuffer[start], center);
        for (int i = 0; i < layerCount; i++)
        {
            _slots[_sortBuffer[start + i]] = new Slot(firstAngle + 2.0f * Mathf.PI * i / layerCount, layer);
        }
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
