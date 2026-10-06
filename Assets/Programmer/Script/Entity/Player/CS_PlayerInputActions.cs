using UnityEngine;
using UnityEngine.InputSystem;

/*
 * 人が操作するプレイヤーの入力(キーボード+マウス、コントローラー)
 * CS_CustomInputActionManagerが持つPlayerアクションマップを読み、IPlayerInputSourceとして渡す
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・CS_Playerが、自分が操作するプレイヤーになった時に作る(NPCでは作らない)
 * ・アクション自体は共有のシングルトンが持つので、ここではDisable/Disposeしない
 * ・視点はマウスとスティックを合わせて「このフレームに回す角度」にする
 *   マウスは「1フレームの移動量」なのでdeltaTimeを掛けない
 *   スティックは「傾き」なので速度として扱いdeltaTimeを掛ける
 */
// ========================================

public class CS_PlayerInputActions : IPlayerInputSource
{
    private readonly CustomInputAction.PlayerActions _actions;
    private readonly float _mouseSensitivity;
    private readonly float _stickSensitivity;

    public CS_PlayerInputActions(float mouseSensitivity, float stickSensitivity)
    {
        _actions = CS_CustomInputActionManager.instance.customInputAction.Player;
        _mouseSensitivity = mouseSensitivity;
        _stickSensitivity = stickSensitivity;
    }

    public Vector2 move => _actions.Move.ReadValue<Vector2>();

    public Vector2 look
    {
        get
        {
            Vector2 mouseLook = _actions.Look.ReadValue<Vector2>() * _mouseSensitivity;
            Vector2 stickLook = _actions.LookStick.ReadValue<Vector2>() * _stickSensitivity * Time.deltaTime;
            return mouseLook + stickLook;
        }
    }

    public bool jumpPressed => _actions.Jump.WasPressedThisFrame();
    public bool dashPressed => _actions.Dash.WasPressedThisFrame();
    public bool dashHeld => _actions.Dash.IsPressed();
    public bool attackPressed => _actions.Attack.WasPressedThisFrame();
    public bool specialPressed => _actions.Special.WasPressedThisFrame();
    public bool specialModifierHeld => _actions.SpecialModifier.IsPressed();
    public bool useItemPressed => _actions.UseItem.WasPressedThisFrame();
    public bool transformPressed => _actions.Transformation.WasPressedThisFrame();
}
