/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// サウンド機能の動作確認用。
/// 確認完了後はシーンまたはPrefabから外す。
/// </summary>
public class CS_SoundDebug : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("確認するSE")]
    [SerializeField] private CSE_SeId _twoDimensionalSeId = CSE_SeId.PlayerDamage;
    [SerializeField] private CSE_SeId _threeDimensionalSeId = CSE_SeId.PlayerSpecialAttack;
    [SerializeField] private CSE_SeId _followSeId = CSE_SeId.PlayerAttack1;

    [Header("3D SEの確認位置")]
    [SerializeField] private Transform _nearPosition;
    [SerializeField] private Transform _farPosition;
    [SerializeField] private Transform _followTarget;

    [Header("ネットワークSE（未設定ならネットワーク確認を行わない）")]
    [SerializeField] private CS_NetworkSoundPlayer _networkSoundPlayer;

    // =========================================================
    // キー入力
    // =========================================================

    private void Update()
    {
        // 1：ローカル2D SE
        if (Input.GetKeyDown(KeyCode.Alpha1))
            PlayLocalTwoDimensionalSe();

        // 2：近距離のローカル3D SE
        if (Input.GetKeyDown(KeyCode.Alpha2))
            PlayLocalThreeDimensionalSe(_nearPosition);

        // 3：遠距離のローカル3D SE
        if (Input.GetKeyDown(KeyCode.Alpha3))
            PlayLocalThreeDimensionalSe(_farPosition);

        // 4：Transform追従のローカル3D SE
        if (Input.GetKeyDown(KeyCode.Alpha4))
            PlayLocalFollowSe();

        // 5：全SE停止
        if (Input.GetKeyDown(KeyCode.Alpha5))
            StopAllSe();

        // 6：全員向け2D SE
        if (Input.GetKeyDown(KeyCode.Alpha6))
            PlayNetworkTwoDimensionalSe();

        // 7：全員向け3D SE
        if (Input.GetKeyDown(KeyCode.Alpha7))
            PlayNetworkThreeDimensionalSe();
    }

    // =========================================================
    // ローカル再生
    // =========================================================

    private void PlayLocalTwoDimensionalSe()
    {
        if (!TryGetSoundManager(out CS_SoundManager soundManager))
            return;

        soundManager.PlaySe(_twoDimensionalSeId);
    }

    private void PlayLocalThreeDimensionalSe(Transform playPosition)
    {
        if (playPosition == null)
        {
            Debug.LogWarning(
                "[CS_SoundDebug] 3D SEの確認位置が未設定です。",
                this
            );
            return;
        }

        if (!TryGetSoundManager(out CS_SoundManager soundManager))
            return;

        soundManager.PlaySe(
            _threeDimensionalSeId,
            playPosition.position
        );
    }

    private void PlayLocalFollowSe()
    {
        if (_followTarget == null)
        {
            Debug.LogWarning(
                "[CS_SoundDebug] Follow Targetが未設定です。",
                this
            );
            return;
        }

        if (!TryGetSoundManager(out CS_SoundManager soundManager))
            return;

        soundManager.PlaySe(
            _followSeId,
            _followTarget
        );
    }

    private void StopAllSe()
    {
        if (!TryGetSoundManager(out CS_SoundManager soundManager))
            return;

        soundManager.StopAllSe();
    }

    // =========================================================
    // ネットワーク再生
    // =========================================================

    private void PlayNetworkTwoDimensionalSe()
    {
        if (!TryGetNetworkSoundPlayer())
            return;

        _networkSoundPlayer.PlaySeForEveryone(
            _twoDimensionalSeId
        );
    }

    private void PlayNetworkThreeDimensionalSe()
    {
        if (!TryGetNetworkSoundPlayer())
            return;

        if (_nearPosition == null)
        {
            Debug.LogWarning(
                "[CS_SoundDebug] Near Positionが未設定です。",
                this
            );
            return;
        }

        _networkSoundPlayer.PlaySeForEveryone(
            _threeDimensionalSeId,
            _nearPosition.position
        );
    }

    // =========================================================
    // 検証
    // =========================================================

    private bool TryGetSoundManager(
        out CS_SoundManager soundManager)
    {
        soundManager = CS_SoundManager.instance;

        if (soundManager != null)
            return true;

        Debug.LogWarning(
            "[CS_SoundDebug] CS_SoundManagerがありません。",
            this
        );
        return false;
    }

    private bool TryGetNetworkSoundPlayer()
    {
        if (_networkSoundPlayer != null)
            return true;

        Debug.LogWarning(
            "[CS_SoundDebug] Network Sound Playerが未設定です。",
            this
        );
        return false;
    }
}
