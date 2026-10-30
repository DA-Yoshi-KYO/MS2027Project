using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーのスタン(一定時間、行動できなくなる)を管理するクラス
 * IStunnableを実装し、他のプレイヤーにパリー(CS_PlayerParry)された時や、巡回ドローンのテーザーに当たった時にスタンする
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・スタン中はCS_Player.canActがfalseになり、移動・ジャンプ・ダッシュ・攻撃・必殺技・アイテム・変身ができない
 *   (死亡中と同じ扱い。視点操作だけはできる)。攻撃・必殺技の途中だった場合は中断される
 * ・スタンが終わる時刻はNetworkVariableで持つ(書き込みはサーバーのみ)。サーバーの時刻(ServerTime)で比べるので、
 *   操作している本人の手元でも、サーバーと同じタイミングでスタンが解ける
 * ・Stun()はサーバー(またはオフライン)で呼ぶ(パリーの判定はサーバーで行うため)
 * ・スタン中にもう一度スタンされた時は、長い方に合わせる(短くはならない)
 * ・コマンド入力(CS_PlayerStunEscape)で、スタンを短くできる(Shorten。サーバー、またはオフラインのみ)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

public class CS_PlayerStun : NetworkBehaviour, IStunnable
{
    // 書き込みはサーバーのみ。スタンが終わる時刻(サーバーの時刻)
    private readonly NetworkVariable<double> _stunEndTime = new NetworkVariable<double>();

    public bool isStunned => GetCurrentTime() < _stunEndTime.Value;
    public float stunRemaining => Mathf.Max(0f, (float)(_stunEndTime.Value - GetCurrentTime()));   // HUD・見た目用

    // IStunnable実装。duration秒の間スタンさせる(サーバー、またはオフラインのみ)
    public void Stun(float duration)
    {
        if (IsSpawned && !IsServer) return;
        if (duration <= 0f) return;

        double endTime = GetCurrentTime() + duration;
        if (endTime <= _stunEndTime.Value) return;

        _stunEndTime.Value = endTime;
    }

    // スタンをseconds秒短くする(サーバー、またはオフラインのみ。CS_PlayerStunEscapeが呼ぶ)
    public void Shorten(float seconds)
    {
        if (IsSpawned && !IsServer) return;
        if (seconds <= 0f || !isStunned) return;

        _stunEndTime.Value = System.Math.Max(GetCurrentTime(), _stunEndTime.Value - seconds);
    }

    // オンラインではサーバーの時刻、オフラインではゲームの時刻
    private double GetCurrentTime()
    {
        return IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    }
}
