/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-05 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1人のプレイヤーの手配度で出現した警察(増援)のまとまりを管理するクラス
/// ・どのプレイヤーの手配度で出現したか(持ち主)と、そのプレイヤーの今の手配度を持つ
/// ・増援は巡回ルートを持たないので、追う標的も指示も無い時はその場で待機する
/// 増援の出現・削除はCS_PoliceWantedManagerが行う
/// </summary>
public class CS_PoliceWantedGroup : IPoliceGroup
{
    // 出現・削除を行う管理クラス
    private readonly CS_PoliceWantedManager _manager = null;

    // 手配度を上げたプレイヤー(持ち主)
    private readonly CS_PlayerHealth _owner = null;

    // 持ち主の手配度で出現した警察
    private readonly List<CS_PoliceBrain> _members = new List<CS_PoliceBrain>();

    // 増援同士で見つけた標的を共有する
    private readonly CS_PoliceSharedTarget _sharedTarget = null;

    // 持ち主の今の手配度
    private int _level = 0;

    // 手配度を上げたプレイヤー(持ち主)
    public CS_PlayerHealth owner => _owner;

    // 持ち主の手配度で出現した警察
    public IReadOnlyList<CS_PoliceBrain> members => _members;

    // 持ち主の今の手配度
    public int level => _level;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="manager">出現・削除を行う管理クラス</param>
    /// <param name="owner">手配度を上げたプレイヤー</param>
    /// <param name="shareDuration">見つけた標的を、見えなくなってからも共有し続ける時間(秒)</param>
    public CS_PoliceWantedGroup(CS_PoliceWantedManager manager, CS_PlayerHealth owner, float shareDuration)
    {
        _manager = manager;
        _owner = owner;
        _sharedTarget = new CS_PoliceSharedTarget(shareDuration);
    }

    /// <summary>
    /// 持ち主の手配度を変更するメソッド
    /// </summary>
    /// <param name="level">手配度</param>
    public void SetLevel(int level)
    {
        _level = level;
    }

    /// <summary>
    /// 出現させた警察をまとまりに加えるメソッド
    /// </summary>
    /// <param name="member">加える警察</param>
    public void AddMember(CS_PoliceBrain member)
    {
        _members.Add(member);
    }

    /// <summary>
    /// 警察をまとまりから外すメソッド(消す時に呼ぶ)
    /// </summary>
    /// <param name="member">外す警察</param>
    public void RemoveMember(CS_PoliceBrain member)
    {
        _members.Remove(member);
    }

    /// <summary>
    /// 他の処理で破棄された警察をまとまりから外すメソッド
    /// </summary>
    public void RemoveDestroyedMembers()
    {
        _members.RemoveAll(member => member == null);
    }

    /// <summary>
    /// メンバーが見つけた標的を報告するメソッド
    /// </summary>
    /// <param name="target">見つけた標的</param>
    /// <param name="priority">標的の優先度</param>
    public void ReportTarget(Transform target, int priority)
    {
        _sharedTarget.Report(target, priority);
    }

    /// <summary>
    /// 増援同士で共有している標的を取得するメソッド
    /// </summary>
    /// <param name="priority">標的の優先度</param>
    /// <returns>共有中の標的(いなければnull)</returns>
    public Transform GetSharedTarget(out int priority)
    {
        return _sharedTarget.Get(out priority);
    }

    /// <summary>
    /// 増援は巡回ルートを持たないので、その場(標的を見失って探し終えた場所)で待機させるメソッド
    /// </summary>
    /// <param name="member">待機させるメンバー</param>
    /// <param name="move">メンバーの移動</param>
    /// <returns>メンバーの状態(待機)</returns>
    public CSE_PoliceMoveState MoveIdle(CS_PoliceBrain member, CS_PoliceMove move)
    {
        move.SetDestination(member.transform.position, CSE_PoliceMoveState.Wait);
        return CSE_PoliceMoveState.Wait;
    }

    /// <summary>
    /// 増援は巡回ルートを持たないので、先頭は存在しない
    /// </summary>
    /// <param name="member">判定するメンバー</param>
    /// <returns>常にfalse</returns>
    public bool IsLeader(CS_PoliceBrain member)
    {
        return false;
    }

    /// <summary>
    /// 詰まったメンバーを消すメソッド(足りなくなった分は、次の人数の確認で持ち主の近くに出現する)
    /// </summary>
    /// <param name="member">出し直すメンバー</param>
    public void RequestRespawn(CS_PoliceBrain member)
    {
        _manager.DespawnMember(this, member);
    }
}
