using UnityEngine;

/*
 * NPCがプレイヤーを操作する時の入力(移動・向き・各ボタン)
 * 判断(CS_PlayerNpcBrain)が書き込み、CS_Playerと各部品が人の入力と同じように読む
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・move は人のスティック入力と同じ(x: 左右, y: 前後)。向き(yaw)を基準に動く
 * ・yaw は人のカメラの左右の向きにあたる。体はこの向きへ回り、向き終わってから移動する(人と同じ挙動)
 * ・ボタンは SetHeld(true/false) で押す・離す(CS_PlayerButton参照)
 */
// ========================================

public class CS_PlayerNpcInput
{
    public Vector2 move;    // 移動入力(-1〜1)
    public float yaw;       // 向き(度)

    public readonly CS_PlayerButton jump = new CS_PlayerButton();
    public readonly CS_PlayerButton attack = new CS_PlayerButton();
    public readonly CS_PlayerButton special = new CS_PlayerButton();
    public readonly CS_PlayerButton dash = new CS_PlayerButton();
    public readonly CS_PlayerButton useItem = new CS_PlayerButton();

    // すべてのボタンを離し、止まる
    public void Clear()
    {
        move = Vector2.zero;
        jump.SetHeld(false);
        attack.SetHeld(false);
        special.SetHeld(false);
        dash.SetHeld(false);
        useItem.SetHeld(false);
    }
}
