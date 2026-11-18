/* ================================================
 * 
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-18 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// インゲームUIのソロ用・マルチ用の切り替えを行うPresenter。
///
/// ・CS_UIGameModeRootView と同じGameObject(UICanvas)にアタッチする
/// ・遊び方(CS_GameModeStore)を見て、Viewに表示を切り替えさせる
/// ・Modelは作らず、遊び方を持つCS_GameModeStoreをそのまま使う
/// ・他のUIのAwakeより先に切り替えるため、実行順を早めている
///   (CS_HideInSolo / CS_GameModeSwitcher と同じ)
/// </summary>
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(CS_UIGameModeRootView))]
public sealed class CS_UIGameModeRootPresenter :
    CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIGameModeRootView _view;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _view =
            GetComponent<CS_UIGameModeRootView>();

        // ソロならソロ用、マルチならマルチ用だけを表示する。
        _view.SetMode(
            !CS_GameModeStore.isSolo
        );
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}
