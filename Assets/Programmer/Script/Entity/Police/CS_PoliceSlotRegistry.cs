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
/// 中心(標的・事件の現場)ごとの CS_PoliceSlotRing をまとめて管理するクラス
/// 同じ中心を目指す警察は、同じ CS_PoliceSlotRing に入り、別々の位置を割り当てられる
/// 中心は「目印となるオブジェクト」と「種類(追跡・駆け付け)」の組で区別する
/// </summary>
public static class CS_PoliceSlotRegistry
{
    // 中心ごとの円(目印となるオブジェクト, 種類)
    private static readonly Dictionary<(object, int), CS_PoliceSlotRing> _rings = new Dictionary<(object, int), CS_PoliceSlotRing>();

    /// <summary>
    /// 警察を、指定した中心の円に加えるメソッド(円が無ければ作る)
    /// </summary>
    /// <param name="key">中心の目印となるオブジェクト(標的のTransform、CS_PoliceGatherPointなど)</param>
    /// <param name="kind">中心の種類(同じ目印で別の目的の円を分けるための番号)</param>
    /// <param name="member">加える警察</param>
    /// <returns>加えた円</returns>
    public static CS_PoliceSlotRing Join(object key, int kind, CS_PoliceBrain member)
    {
        if (!_rings.TryGetValue((key, kind), out CS_PoliceSlotRing ring))
        {
            ring = new CS_PoliceSlotRing();
            _rings.Add((key, kind), ring);
        }

        ring.Join(member);
        return ring;
    }

    /// <summary>
    /// 警察を、指定した中心の円から外すメソッド(誰もいなくなった円は消す)
    /// </summary>
    /// <param name="key">中心の目印となるオブジェクト</param>
    /// <param name="kind">中心の種類</param>
    /// <param name="member">外す警察</param>
    public static void Leave(object key, int kind, CS_PoliceBrain member)
    {
        if (!_rings.TryGetValue((key, kind), out CS_PoliceSlotRing ring)) return;

        ring.Leave(member);
        if (ring.count == 0) _rings.Remove((key, kind));
    }

    // Domain Reloadオフ対策(再生のたびに空にする)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _rings.Clear();
    }
}
