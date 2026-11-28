/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-28 | 初回作成(Issue #569)
 * ================================================ */

using System;
using UnityEngine;

/// <summary>
/// 悪人グループとプレイヤーの戦闘に駆け付ける警察1人が所属するまとまり(1人だけのグループ)
/// ・戦闘の場所への移動は、駆け付けの指示(CS_PoliceBrain.RequestRush)で行う。途中・到着後の動きは、いつもの警察と同じ(見えれば追跡する)
/// ・追う標的も指示も無くなった時、戦闘が続いていれば、その場で待つ。戦闘が終わっていれば、消える演出のあとに消える(帰還)
/// 巡回ルートは持たない。警戒状態の増援ではないので、駆け付けている間は、プレイヤーを警戒状態にしない
/// </summary>
public class CS_PoliceBattleGroup : IPoliceGroup
{
    // メンバーが見つけた標的を、見えなくなってからも共有し続ける時間(秒)
    private const float _shareDuration = 0.5f;

    // 戦闘がまだ続いているかを調べる方法
    private readonly Func<bool> _isBattleActive = null;

    // メンバーが見つけた標的の共有情報
    private readonly CS_PoliceSharedTarget _sharedTarget = new CS_PoliceSharedTarget(_shareDuration);

    // 帰還(消える演出)を始めたか
    private bool _isReturning = false;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="isBattleActive">戦闘がまだ続いているかを返すメソッド</param>
    public CS_PoliceBattleGroup(Func<bool> isBattleActive)
    {
        _isBattleActive = isBattleActive;
    }

    /// <summary>
    /// メンバーが見つけた標的を報告するメソッド
    /// </summary>
    public void ReportTarget(Transform target, int priority)
    {
        _sharedTarget.Report(target, priority);
    }

    /// <summary>
    /// 共有している標的を取得するメソッド
    /// </summary>
    public Transform GetSharedTarget(out int priority)
    {
        return _sharedTarget.Get(out priority);
    }

    /// <summary>
    /// 追う標的も指示も無い時の行動をさせるメソッド(戦闘中はその場で待ち、終わっていれば帰還する)
    /// </summary>
    public CSE_PoliceMoveState MoveIdle(CS_PoliceBrain member, CS_PoliceMove move)
    {
        if (!_isReturning && _isBattleActive()) return CSE_PoliceMoveState.Wait;

        Return(member);
        return CSE_PoliceMoveState.Patrol;
    }

    /// <summary>
    /// 巡回ルートを直接目指す先頭のメンバーかを判定するメソッド(巡回ルートが無いので、常にfalse)
    /// </summary>
    public bool IsLeader(CS_PoliceBrain member)
    {
        return false;
    }

    /// <summary>
    /// 異常事態(詰まりなど)になったメンバーを消すメソッド(出し直さず、そのまま帰還させる)
    /// </summary>
    public void RequestRespawn(CS_PoliceBrain member)
    {
        _isReturning = true;
        CS_PoliceSpawnUtility.Despawn(member.gameObject);
    }

    /// <summary>
    /// 帰還(消える演出のあとに消える)を始めるメソッド
    /// </summary>
    private void Return(CS_PoliceBrain member)
    {
        if (_isReturning) return;

        _isReturning = true;
        if (member.TryGetComponent(out CS_PoliceFadeOut fadeOut))
        {
            fadeOut.StartFadeOut();
            return;
        }

        CS_PoliceSpawnUtility.Despawn(member.gameObject);
    }
}
