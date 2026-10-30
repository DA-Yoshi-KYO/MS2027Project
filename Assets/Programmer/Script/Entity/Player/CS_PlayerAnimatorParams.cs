using UnityEngine;

/*
 * プレイヤーのAnimator Controllerが持つべきパラメータ名(コードとController側の約束事)
 * 本番アセットに差し替えるときは、Controller側にここと同じ名前・型のパラメータを用意すればよい
 * (無いパラメータは無視されるので、全部揃っていなくても動く)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・ClaudeUsers/プレイヤー見た目の差し替えガイド.md に一覧と差し替え手順がある
 * ・名前を変えるときは、このクラスとController生成ツール
 *   (CSED_PlayerAnimatorBuilder)の両方に反映される(生成ツールもこの定数を使っている)
 */
// ========================================

public static class CS_PlayerAnimatorParams
{
    // 移動(常時更新)
    public const string speed = "Speed";           // float 0〜1 移動の速さ(通常移動の速さで割った値)
    public const string moveX = "MoveX";           // float -1〜1 体から見た左右方向の移動(右が+)
    public const string moveZ = "MoveZ";           // float -1〜1 体から見た前後方向の移動(前が+)
    public const string grounded = "Grounded";     // bool   接地しているか
    public const string dead = "Dead";             // bool   死亡中か
    public const string sprint = "Sprint";         // bool   通常の移動より速く動いているか(ダッシュ中・ダッシュボタンを押し続けている間)

    // 単発の動作(トリガー)
    public const string jump = "Jump";
    public const string dash = "Dash";
    public const string attack = "Attack";         // 発動時に comboStep を先にセットする
    public const string special = "Special";
    public const string hit = "Hit";
    public const string parry = "Parry";           // パリー(反撃)が決まった時
    public const string ambush = "Ambush";         // 奇襲の飛び込みを始めた時

    public const string comboStep = "ComboStep";   // int 通常攻撃が何段目か(0始まり)
    public const string attackSpeed = "AttackSpeed"; // float 通常攻撃のモーション速度の倍率(1で等倍。暗躍ボーナスで上がる)。攻撃のステートのSpeed Multiplierに使う
    public const string ambushSpeed = "AmbushSpeed"; // float 奇襲のモーション速度の倍率。飛び込みの距離に合わせ、当たる瞬間に打撃が来るよう再生する時に決める

    public static readonly int speedHash = Animator.StringToHash(speed);
    public static readonly int moveXHash = Animator.StringToHash(moveX);
    public static readonly int moveZHash = Animator.StringToHash(moveZ);
    public static readonly int groundedHash = Animator.StringToHash(grounded);
    public static readonly int deadHash = Animator.StringToHash(dead);
    public static readonly int sprintHash = Animator.StringToHash(sprint);
    public static readonly int jumpHash = Animator.StringToHash(jump);
    public static readonly int dashHash = Animator.StringToHash(dash);
    public static readonly int attackHash = Animator.StringToHash(attack);
    public static readonly int specialHash = Animator.StringToHash(special);
    public static readonly int hitHash = Animator.StringToHash(hit);
    public static readonly int parryHash = Animator.StringToHash(parry);
    public static readonly int ambushHash = Animator.StringToHash(ambush);
    public static readonly int comboStepHash = Animator.StringToHash(comboStep);
    public static readonly int attackSpeedHash = Animator.StringToHash(attackSpeed);
    public static readonly int ambushSpeedHash = Animator.StringToHash(ambushSpeed);
}
