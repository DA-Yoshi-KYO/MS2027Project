/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// シーン開始時に、そのシーン用のBGMを再生する。
/// </summary>
public class CS_BgmPlayer : MonoBehaviour
{
    [Header("このシーンで再生するBGM")]
    [SerializeField] private CSE_BgmId _bgmId;

    [Header("フェード時間")]
    [Min(0f)]
    [SerializeField] private float _fadeTime = 1f;

    [Header("開始時にBGMを停止する")]
    [SerializeField] private bool _stopBgm;

    private void Start()
    {
        if (CS_SoundManager.instance == null)
        {
            Debug.LogWarning(
                "[CS_SceneBgmPlayer] CS_SoundManagerがありません。",
                this
            );
            return;
        }

        if (_stopBgm)
        {
            CS_SoundManager.instance.StopBgm(_fadeTime);
            return;
        }

        if (_bgmId == CSE_BgmId.None)
            return;

        CS_SoundManager.instance.PlayBgm(
            _bgmId,
            _fadeTime
        );
    }
}