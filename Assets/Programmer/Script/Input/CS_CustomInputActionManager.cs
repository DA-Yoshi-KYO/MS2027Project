using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;

public class CS_CustomInputActionManager
{
    private static CS_CustomInputActionManager _myInstance;
    public CustomInputAction customInputAction { get; private set; }

    public enum InputType
    {
        KeyboardMouse,
        Gamepad
    }
    public InputType currentInputType { get; private set; }

    /// <summary>
    /// プレイヤーのinputがコントローラーorキーマウに切り替わった時に発生するイベント
    /// </summary>
    public event Action<InputType> onInputTypeChanged;

    public static CS_CustomInputActionManager instance
    {
        get
        {
            if (_myInstance == null)
            {
                _myInstance = new CS_CustomInputActionManager();
            }
            return _myInstance;
        }
    }

    private CS_CustomInputActionManager() 
    {
        customInputAction = new CustomInputAction();
        customInputAction.Enable();
        foreach (var map in customInputAction)
        {
            map.Enable();
        }
        InputSystem.onEvent += OnInputEvent;
    }

    ~CS_CustomInputActionManager()
    {
        foreach (var map in customInputAction)
        {
            map.Disable();
        }
        customInputAction.Disable();
    }

    void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (!eventPtr.valid) return; // ノイズ除外

        // StateEvent / DeltaStateEvent 以外（デバイス接続イベント等）は無視
        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            return;

        bool hasSignificantInput = false;

        foreach (var control in device.allControls)
        {
            // ボタン系はそのまま押下判定
            if (control is ButtonControl button)
            {
                if (button.isPressed)
                {
                    hasSignificantInput = true;
                    break;
                }
            }
            // スティック・トリガー等のアナログ系はデッドゾーンでノイズ除外
            else if (control is AxisControl axis)
            {
                if (Mathf.Abs(axis.ReadValueFromEvent(eventPtr)) > 0.2f) // 閾値は調整
                {
                    hasSignificantInput = true;
                    break;
                }
            }
        }

        if (!hasSignificantInput) return;

        InputType newInputType = currentInputType;

        if (device is Keyboard || device is Mouse)
        {
            newInputType = InputType.KeyboardMouse;
        }
        else if (device is Gamepad)
        {
            newInputType = InputType.Gamepad;
        }

        if (newInputType == currentInputType) return;

        currentInputType = newInputType;
        onInputTypeChanged?.Invoke(currentInputType);
    }
}
