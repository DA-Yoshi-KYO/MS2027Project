using UnityEngine;

/*
 * 悪人のAnimator Controllerが持つべきパラメータ名(コードとController側の約束事)
 * 本番アセットに差し替えるときは、Controller側にここと同じ名前・型のパラメータを用意すればよい
 * (無いパラメータは無視されるので、全部揃っていなくても動く)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・パラメータを更新するのはCS_VillainVisual
 * ・名前を変えるときは、このクラスと仮アセット用のController生成ツール
 *   (CSED_VillainTempVisualBuilder)の両方に反映される(生成ツールもこの定数を使っている)
 */
// ========================================

public static class CS_VillainAnimatorParams
{
    // 移動(常時更新)
    public const string speed = "Speed";           // float 0〜  移動の速さ(プレイヤーの通常移動の速さで割った値)
    public const string moveX = "MoveX";           // float      体から見た左右方向の移動(右が+)
    public const string moveZ = "MoveZ";           // float      体から見た前後方向の移動(前が+)

    // 攻撃(常時更新)
    public const string charging = "Charging";     // bool 攻撃の溜め中か
    public const string attacking = "Attacking";   // bool 攻撃判定が出ているか

    // 単発の動作(トリガー)
    public const string hit = "Hit";               // ノックバックした瞬間

    public static readonly int speedHash = Animator.StringToHash(speed);
    public static readonly int moveXHash = Animator.StringToHash(moveX);
    public static readonly int moveZHash = Animator.StringToHash(moveZ);
    public static readonly int chargingHash = Animator.StringToHash(charging);
    public static readonly int attackingHash = Animator.StringToHash(attacking);
    public static readonly int hitHash = Animator.StringToHash(hit);
}
