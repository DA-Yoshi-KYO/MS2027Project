using UnityEngine;

/*
 * プレイヤーを動かす入力の出どころ
 * 人が操作する場合はCS_PlayerInputActions(キーボード・コントローラー)、NPCの場合はCS_NpcBrainが実装する
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・CS_Playerと各操作のコンポーネント(攻撃・必殺技・変身・アイテム)は、ボタンを直接見ずにこれを見る
 *   入力の出どころを差し替えるだけで、人もNPCも同じ能力・同じ処理で動く
 * ・〇〇Pressedは「このフレームに押されたか」、〇〇Heldは「押し続けているか」
 * ・moveは視点(CS_Player.yaw)の向き基準。lookはこのフレームに視点を回す量(度)
 */
// ========================================

public interface IPlayerInputSource
{
    Vector2 move { get; }               // 移動(x: 左右、y: 前後。視点の向き基準)
    Vector2 look { get; }               // このフレームに視点を回す量(度。x: 左右、y: 上下)
    bool jumpPressed { get; }
    bool dashPressed { get; }
    bool dashHeld { get; }
    bool attackPressed { get; }
    bool specialPressed { get; }
    bool specialModifierHeld { get; }   // 必殺技のゲームパッド用コード(RT+LT)のLT側
    bool useItemPressed { get; }
    bool transformPressed { get; }
}
