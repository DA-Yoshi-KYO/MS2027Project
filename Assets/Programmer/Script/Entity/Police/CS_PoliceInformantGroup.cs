/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * 2026-11-01 | 警戒状態の間だけ追う使い方(増援)を追加
 * ================================================ */

using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 内通フォンで呼ばれた警察・警戒状態の増援の警察1人が所属するまとまり(1人だけのグループ)
/// ・一定時間(増援は、標的が警戒状態の間)、標的のプレイヤーの現在地を取得して追跡する(見失っても、現在地へ向かい続ける)
/// ・プレイヤーを見つけて警戒状態にしたら、そのあとは通常の警察と同じ判断(追跡・探索)に任せる
/// ・追跡する時間が過ぎて、追跡も探索もしなくなったら、消える演出のあとに消える(帰還)
/// 巡回ルートは持たない
/// </summary>
public class CS_PoliceInformantGroup : IPoliceGroup
{
    // メンバーが見つけた標的を、見えなくなってからも共有し続ける時間(秒)
    private const float _shareDuration = 0.5f;

    // 標的の現在地の近くの歩ける場所(NavMesh)を探す距離
    private const float _homingSampleDistance = 5.0f;

    // 追跡する標的のプレイヤー
    private readonly CS_PlayerHealth _targetPlayer = null;

    // 現在地を取得して追跡するのをやめる時刻(増援は、警戒状態が続く間だけ追うので、無限大)
    private readonly float _homingEndTime = 0.0f;

    // 標的の警戒状態(増援のみ。これが解除されたら追跡をやめる。内通フォンで呼ばれた警察はnull)
    private readonly CS_PlayerAlert _targetAlert = null;

    // メンバーが見つけた標的の共有情報
    private readonly CS_PoliceSharedTarget _sharedTarget = new CS_PoliceSharedTarget(_shareDuration);

    // 帰還(消える演出)を始めたか
    private bool _isReturning = false;

    /// <summary>
    /// 標的と追跡する時間を指定して作るコンストラクタ
    /// </summary>
    /// <param name="targetPlayer">現在地を追跡するプレイヤー</param>
    /// <param name="homingDuration">現在地を取得して追跡する時間(秒)</param>
    public CS_PoliceInformantGroup(CS_PlayerHealth targetPlayer, float homingDuration)
    {
        _targetPlayer = targetPlayer;
        _homingEndTime = Time.time + homingDuration;
    }

    /// <summary>
    /// 標的が警戒状態の間だけ追跡する(増援の)グループを作るコンストラクタ
    /// 警戒状態が解除されたら、追う標的も指示も無くなった時に帰還する
    /// </summary>
    /// <param name="targetPlayer">現在地を追跡するプレイヤー</param>
    /// <param name="targetAlert">標的の警戒状態</param>
    public CS_PoliceInformantGroup(CS_PlayerHealth targetPlayer, CS_PlayerAlert targetAlert)
    {
        _targetPlayer = targetPlayer;
        _targetAlert = targetAlert;
        _homingEndTime = float.PositiveInfinity;
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
    /// 共有している標的を取得するメソッド
    /// </summary>
    /// <param name="priority">標的の優先度</param>
    /// <returns>共有中の標的(いなければnull)</returns>
    public Transform GetSharedTarget(out int priority)
    {
        return _sharedTarget.Get(out priority);
    }

    /// <summary>
    /// 追う標的も指示も無い時の行動をさせるメソッド
    /// 追跡する時間の間は標的の現在地へ向かい、過ぎていたら帰還する
    /// </summary>
    /// <param name="member">行動させるメンバー</param>
    /// <param name="move">メンバーの移動</param>
    /// <returns>その時のメンバーの状態</returns>
    public CSE_PoliceMoveState MoveIdle(CS_PoliceBrain member, CS_PoliceMove move)
    {
        if (!_isReturning && IsHoming())
        {
            move.SetDestination(GetHomingDestination(), CSE_PoliceMoveState.Rush);
            return CSE_PoliceMoveState.Rush;
        }

        Return(member);
        return CSE_PoliceMoveState.Patrol;
    }

    /// <summary>
    /// 巡回ルートを直接目指す先頭のメンバーかを判定するメソッド(巡回ルートが無いので、常にfalse)
    /// </summary>
    /// <param name="member">判定するメンバー</param>
    /// <returns>常にfalse</returns>
    public bool IsLeader(CS_PoliceBrain member)
    {
        return false;
    }

    /// <summary>
    /// 異常事態(詰まりなど)になったメンバーを消すメソッド(出し直す出現位置が無いので、そのまま帰還させる)
    /// </summary>
    /// <param name="member">消すメンバー</param>
    public void RequestRespawn(CS_PoliceBrain member)
    {
        _isReturning = true;
        CS_PoliceSpawnUtility.Despawn(member.gameObject);
    }

    /// <summary>
    /// 標的の現在地を取得して追跡する時間内かを判定するメソッド
    /// 標的が死亡した・いなくなった時や、標的の警戒状態が解除された時(増援のみ)は、追跡する理由が無いので時間内でも終わる
    /// </summary>
    /// <returns>追跡する時間内ならtrue</returns>
    private bool IsHoming()
    {
        if (_targetPlayer == null || _targetPlayer.isDead || Time.time >= _homingEndTime) return false;

        return _targetAlert == null || _targetAlert.isAlerted;
    }

    /// <summary>
    /// 標的の現在地の近くの、歩ける場所(NavMesh上)を取得するメソッド
    /// 近くに歩ける場所が無い時は、現在地そのものを返す
    /// </summary>
    /// <returns>目指す位置</returns>
    private Vector3 GetHomingDestination()
    {
        Vector3 position = _targetPlayer.transform.position;
        return NavMesh.SamplePosition(position, out NavMeshHit hit, _homingSampleDistance, NavMesh.AllAreas) ? hit.position : position;
    }

    /// <summary>
    /// 帰還(消える演出のあとに消える)を始めるメソッド
    /// </summary>
    /// <param name="member">帰還するメンバー</param>
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
