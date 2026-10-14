using UnityEngine;
using UnityEngine.InputSystem;

/*
 * プレイヤーのボタン入力1つ分(攻撃・必殺技・ダッシュ・アイテム使用・変身・ジャンプ)
 * 人が操作する時はInputActionをそのまま読み、NPCの時は判断(CS_PlayerNpcBrain)が押す・離すを指定する
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・各部品(CS_PlayerAttackなど)は、これまで通り WasPressedThisFrame() / IsPressed() で読むだけでよい
 *   (人かNPCかを部品側が気にしなくて済むように、InputActionと同じ名前のメソッドにしている)
 * ・NPC用のボタンは SetHeld(true/false) で押す・離す
 *   押した瞬間のフレームだけ WasPressedThisFrame() がtrueになる
 *   判断側は、部品のUpdateより先に押す必要がある(CS_PlayerNpcBrainは実行順を早めている)
 */
// ========================================

public class CS_PlayerButton
{
    private readonly InputAction _action;   // 人が操作する時の入力(NPCならnull)

    private bool _isHeld;           // NPCが押しているか
    private int _pressedFrame = -1; // NPCが押し始めたフレーム

    // 人が操作する時のボタン(InputActionを読む)
    public CS_PlayerButton(InputAction action)
    {
        _action = action;
    }

    // NPCが操作する時のボタン(SetHeldで押す・離す)
    public CS_PlayerButton()
    {
    }

    public bool WasPressedThisFrame()
    {
        if (_action != null) return _action.WasPressedThisFrame();

        return _isHeld && _pressedFrame == Time.frameCount;
    }

    public bool IsPressed()
    {
        if (_action != null) return _action.IsPressed();

        return _isHeld;
    }

    // NPC用。押す(true) / 離す(false)
    public void SetHeld(bool held)
    {
        if (held && !_isHeld)
        {
            _pressedFrame = Time.frameCount;
        }

        _isHeld = held;
    }
}
