/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-18 | 初回作成
 * ================================================ */

using UnityEngine;

// ========================================
/*
 * メモ
 * ・インゲームUI(UICanvas)のルートに付けると、ソロ用・マルチ用のパネルを切り替える
 * ・CS_UIGameModeRootView と同じGameObjectに付ける
 * ・他の物のAwakeより先に切り替えるため、実行順を早めている(CS_HideInSoloと同じ)
 */
// ========================================
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(CS_UIGameModeRootView))]
public class CS_UIGameModeSwitcher : MonoBehaviour
{
    void Awake()
    {
        var view = GetComponent<CS_UIGameModeRootView>();
        view.SetMode(!CS_GameModeStore.isSolo);
    }
}
