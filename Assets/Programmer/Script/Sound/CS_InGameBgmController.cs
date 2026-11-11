/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-30 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// インゲーム中のプレイヤー警戒状態に応じて、
/// 通常BGMと警戒BGMを切り替える。
///
/// ローカルPlayerのCS_PlayerAlertを登録して使用する。
/// </summary>
public sealed class CS_InGameBgmController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("通常状態のBGM")]
    [SerializeField]
    private CSE_BgmId _normalBgmId =
        CSE_BgmId.InGameNormal;

    [Header("警戒状態のBGM")]
    [SerializeField]
    private CSE_BgmId _alertBgmId =
        CSE_BgmId.InGameAlert;

    [Header("BGM切り替え時間")]
    [Min(0f)]
    [SerializeField]
    private float _crossFadeTime = 1f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_PlayerAlert _playerAlert;

    private bool _isAlertBgmPlaying;

    // =========================================================
    // 初期化
    // =========================================================

    private void Start()
    {
        PlayNormalBgm();
    }

    // =========================================================
    // Player登録
    // =========================================================

    /// <summary>
    /// ローカルPlayerの警戒状態を登録する。
    /// </summary>
    public void RegisterLocalPlayer(
        CS_PlayerAlert playerAlert)
    {
        UnregisterLocalPlayer();

        _playerAlert = playerAlert;

        if (_playerAlert == null)
        {
            Debug.LogWarning(
                "[CS_InGameBgmController] " +
                "CS_PlayerAlertが未設定です。",
                this
            );

            PlayNormalBgm();
            return;
        }

        _playerAlert.onAlertChanged +=
            HandleAlertStateChanged;

        // 登録時点の状態を即時反映
        HandleAlertStateChanged(
            _playerAlert.alertState
        );
    }

    /// <summary>
    /// 登録中のPlayerから購読解除する。
    /// </summary>
    public void UnregisterLocalPlayer()
    {
        if (_playerAlert != null)
        {
            _playerAlert.onAlertChanged -=
                HandleAlertStateChanged;
        }

        _playerAlert = null;
    }

    // =========================================================
    // 警戒状態変更
    // =========================================================

    private void HandleAlertStateChanged(
        CSE_PlayerAlertState alertState)
    {
        bool isAlerted =
            alertState != CSE_PlayerAlertState.None;

        if (isAlerted)
        {
            PlayAlertBgm();
        }
        else
        {
            PlayNormalBgm();
        }
    }

    // =========================================================
    // BGM切り替え
    // =========================================================

    private void PlayNormalBgm()
    {
        if (!_isAlertBgmPlaying &&
            CS_SoundManager.instance != null &&
            CS_SoundManager.instance.currentBgmId ==
                _normalBgmId)
        {
            return;
        }

        _isAlertBgmPlaying = false;

        PlayBgm(
            _normalBgmId
        );
    }

    private void PlayAlertBgm()
    {
        if (_isAlertBgmPlaying &&
            CS_SoundManager.instance != null &&
            CS_SoundManager.instance.currentBgmId ==
                _alertBgmId)
        {
            return;
        }

        _isAlertBgmPlaying = true;

        PlayBgm(
            _alertBgmId
        );
    }

    private void PlayBgm(
        CSE_BgmId bgmId)
    {
        if (CS_SoundManager.instance == null)
        {
            Debug.LogWarning(
                "[CS_InGameBgmController] " +
                "CS_SoundManagerがありません。",
                this
            );

            return;
        }

        CS_SoundManager.instance.PlayBgm(
            bgmId,
            _crossFadeTime
        );
    }

    // =========================================================
    // 終了処理
    // =========================================================

    private void OnDestroy()
    {
        UnregisterLocalPlayer();
    }
}