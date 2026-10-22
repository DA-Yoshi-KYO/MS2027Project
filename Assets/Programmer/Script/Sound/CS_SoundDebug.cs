/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

using UnityEngine;

public class CS_SoundDebug : MonoBehaviour
{
    private void Update()
    {
        // BGM再生
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            CS_SoundManager.instance.PlayBgm(
                CSE_BgmId.InGame,
                1f
            );
        }

        // ジングル再生
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            CS_SoundManager.instance.PlayJingle(
                CSE_JingleId.GameClear
            );
        }

        // ジングル停止
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            CS_SoundManager.instance.StopJingle();
        }
    }
}