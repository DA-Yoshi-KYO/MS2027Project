using UnityEngine;

/*
 * 攻撃1回分の情報をまとめた構造体
 * 段ごとのユニークな処理(CSO_AttackDataのoverride)へ渡す
 *
 * 制作者：　秋野翔太
 */

public readonly struct AttackContext
{
    private readonly Transform _attacker;
    private readonly int _stepIndex;

    public Transform attacker => _attacker;     // 攻撃した側
    public int stepIndex => _stepIndex;         // 何段目か(0始まり)。必殺技の場合は-1

    public AttackContext(Transform attacker, int stepIndex)
    {
        _attacker = attacker;
        _stepIndex = stepIndex;
    }
}
