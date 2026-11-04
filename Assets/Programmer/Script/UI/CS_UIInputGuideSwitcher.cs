/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-19 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 最後に使用された入力デバイスに応じて、
/// キーボード＆マウス用とゲームパッド用の操作表を切り替える。
/// UICanvas/Guid にアタッチする。
/// </summary>
public class CS_UIInputGuideSwitcher : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("キーボード＆マウス用の操作表")]
    [SerializeField] private GameObject _keyboardMouseGuide;

    [Header("ゲームパッド用の操作表")]
    [SerializeField] private GameObject _gamepadGuide;

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnEnable()
    {
        CS_CustomInputActionManager.instance.onInputTypeChanged +=
            HandleInputTypeChanged;

        // 有効化した時点の入力タイプで初期表示する。
        ApplyGuide(
            CS_CustomInputActionManager.instance.currentInputType
        );
    }

    private void OnDisable()
    {
        // シーン遷移やGameObjectの無効化後に呼ばれ続けないよう解除する。
        CS_CustomInputActionManager.instance.onInputTypeChanged -=
            HandleInputTypeChanged;
    }

    // =========================================================
    // 入力タイプ変更
    // =========================================================

    private void HandleInputTypeChanged(
        CS_CustomInputActionManager.InputType inputType)
    {
        ApplyGuide(inputType);
    }

    private void ApplyGuide(
        CS_CustomInputActionManager.InputType inputType)
    {
        bool usesKeyboardMouse =
            inputType ==
            CS_CustomInputActionManager.InputType.KeyboardMouse;

        if (_keyboardMouseGuide != null)
            _keyboardMouseGuide.SetActive(usesKeyboardMouse);

        if (_gamepadGuide != null)
            _gamepadGuide.SetActive(!usesKeyboardMouse);
    }
}
