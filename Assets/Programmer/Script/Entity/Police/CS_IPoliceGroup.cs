/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-05 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警察1人(CS_PoliceBrain)が所属するまとまりを表すインターフェース
/// ・巡回するグループ(CS_PoliceSquad)
/// ・手配度で出現した増援のまとまり(CS_PoliceWantedGroup)
/// CS_PoliceBrainは、所属先がどちらかを気にせずに標的の共有・待機中の行動・再スポーンを依頼できる
/// </summary>
public interface IPoliceGroup
{
    /// <summary>
    /// メンバーが見つけた標的を報告するメソッド
    /// </summary>
    /// <param name="target">見つけた標的</param>
    /// <param name="priority">標的の優先度</param>
    void ReportTarget(Transform target, int priority);

    /// <summary>
    /// 仲間内で共有している標的を取得するメソッド
    /// </summary>
    /// <param name="priority">標的の優先度</param>
    /// <returns>共有中の標的(いなければnull)</returns>
    Transform GetSharedTarget(out int priority);

    /// <summary>
    /// 追う標的も指示も無い時の行動(巡回・待機)をさせるメソッド
    /// </summary>
    /// <param name="member">行動させるメンバー</param>
    /// <param name="move">メンバーの移動</param>
    /// <returns>その時のメンバーの状態</returns>
    CSE_PoliceMoveState MoveIdle(CS_PoliceBrain member, CS_PoliceMove move);

    /// <summary>
    /// 巡回ルートを直接目指す先頭のメンバーかを判定するメソッド(先頭だけが、ルートに戻れないかを判定する)
    /// </summary>
    /// <param name="member">判定するメンバー</param>
    /// <returns>先頭ならtrue</returns>
    bool IsLeader(CS_PoliceBrain member);

    /// <summary>
    /// 異常事態(詰まり・巡回ルートに戻れない)になったメンバーを出し直すメソッド
    /// </summary>
    /// <param name="member">出し直すメンバー</param>
    void RequestRespawn(CS_PoliceBrain member);
}
