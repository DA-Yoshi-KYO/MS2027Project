/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-16 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// ゲームモードに応じて、
/// ソロ用UIとマルチ用UIを切り替える。
/// </summary>
public sealed class CS_UIGameModeRootView :
    MonoBehaviour
{
    [Header("ソロ用UI")]
    [SerializeField]
    private GameObject _soloPanel;

    [Header("マルチ用UI")]
    [SerializeField]
    private GameObject _multiPanel;

    /// <summary>
    /// ソロ・マルチの表示を切り替える。
    /// </summary>
    public void SetMode(bool isMultiplayer)
    {
        if (_soloPanel != null)
        {
            _soloPanel.SetActive(
                !isMultiplayer
            );
        }

        if (_multiPanel != null)
        {
            _multiPanel.SetActive(
                isMultiplayer
            );
        }
    }
}