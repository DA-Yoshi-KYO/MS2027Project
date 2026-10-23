/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ネットワーク上の全プレイヤーへSE再生イベントを通知する。
/// 音自体は同期せず、各クライアントのCS_SoundManagerがローカル再生する。
///
/// このコンポーネントは、NetworkObjectが付いたシーン常駐オブジェクトへ付ける。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class CS_NetworkSoundPlayer : NetworkBehaviour
{
    // =========================================================
    // 全員へ2D SEを再生
    // =========================================================

    /// <summary>
    /// 全クライアントで2D SEを再生する。
    /// サーバー・クライアントのどちらからでも呼び出せる。
    /// オフライン時はローカル再生する。
    /// </summary>
    public void PlaySeForEveryone(CSE_SeId seId)
    {
        if (seId == CSE_SeId.None)
            return;

        if (!IsSpawned)
        {
            PlayLocalSe(seId);
            return;
        }

        if (IsServer)
        {
            PlaySeOnEveryoneRpc(seId);
            return;
        }

        RequestPlaySeRpc(seId);
    }

    // =========================================================
    // 全員へ3D SEを再生
    // =========================================================

    /// <summary>
    /// 全クライアントで、指定したワールド座標から3D SEを再生する。
    /// サーバー・クライアントのどちらからでも呼び出せる。
    /// オフライン時はローカル再生する。
    /// </summary>
    public void PlaySeForEveryone(
        CSE_SeId seId,
        Vector3 worldPosition)
    {
        if (seId == CSE_SeId.None)
            return;

        if (!IsSpawned)
        {
            PlayLocalSe(seId, worldPosition);
            return;
        }

        if (IsServer)
        {
            PlaySeAtPositionOnEveryoneRpc(
                seId,
                worldPosition
            );
            return;
        }

        RequestPlaySeAtPositionRpc(
            seId,
            worldPosition
        );
    }

    // =========================================================
    // Client → Server
    // =========================================================

    /// <summary>
    /// クライアントからサーバーへ2D SE再生を要求する。
    /// RequireOwnership=falseにより、このNetworkObjectのOwner以外も呼べる。
    /// </summary>
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RequestPlaySeRpc(CSE_SeId seId)
    {
        if (!IsValidSeId(seId))
            return;

        PlaySeOnEveryoneRpc(seId);
    }

    /// <summary>
    /// クライアントからサーバーへ3D SE再生を要求する。
    /// </summary>
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RequestPlaySeAtPositionRpc(
        CSE_SeId seId,
        Vector3 worldPosition)
    {
        if (!IsValidSeId(seId) ||
            !IsFinite(worldPosition))
        {
            return;
        }

        PlaySeAtPositionOnEveryoneRpc(
            seId,
            worldPosition
        );
    }

    // =========================================================
    // Server → Everyone
    // =========================================================

    /// <summary>
    /// ホストを含む全クライアントで2D SEを1回だけ再生する。
    /// </summary>
    [Rpc(SendTo.Everyone)]
    private void PlaySeOnEveryoneRpc(CSE_SeId seId)
    {
        PlayLocalSe(seId);
    }

    /// <summary>
    /// ホストを含む全クライアントで同じ位置から3D SEを再生する。
    /// </summary>
    [Rpc(SendTo.Everyone)]
    private void PlaySeAtPositionOnEveryoneRpc(
        CSE_SeId seId,
        Vector3 worldPosition)
    {
        PlayLocalSe(
            seId,
            worldPosition
        );
    }

    // =========================================================
    // ローカル再生
    // =========================================================

    private static void PlayLocalSe(CSE_SeId seId)
    {
        if (CS_SoundManager.instance == null)
        {
            Debug.LogWarning(
                "[CS_NetworkSoundPlayer] CS_SoundManagerがありません。"
            );
            return;
        }

        CS_SoundManager.instance.PlaySe(seId);
    }

    private static void PlayLocalSe(
        CSE_SeId seId,
        Vector3 worldPosition)
    {
        if (CS_SoundManager.instance == null)
        {
            Debug.LogWarning(
                "[CS_NetworkSoundPlayer] CS_SoundManagerがありません。"
            );
            return;
        }

        CS_SoundManager.instance.PlaySe(
            seId,
            worldPosition
        );
    }

    // =========================================================
    // 入力検証
    // =========================================================

    private static bool IsValidSeId(CSE_SeId seId)
    {
        return seId != CSE_SeId.None;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) &&
               float.IsFinite(value.y) &&
               float.IsFinite(value.z);
    }
}
