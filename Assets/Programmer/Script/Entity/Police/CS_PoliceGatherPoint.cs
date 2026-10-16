/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-19 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 事件(プレイヤーの信号・変身の検知)の現場を表すクラス
/// 同じ事件に駆け付ける警察へ、同じインスタンスを渡す。
/// 警察は、このインスタンスを目印にして、現場の周りの別々の位置へ割り当てられる(全員が現場の一点に押し合わないため)
/// </summary>
public class CS_PoliceGatherPoint
{
    // 事件の現場
    private readonly Vector3 _position = Vector3.zero;

    // 事件の現場
    public Vector3 position => _position;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="position">事件の現場</param>
    public CS_PoliceGatherPoint(Vector3 position)
    {
        _position = position;
    }
}
