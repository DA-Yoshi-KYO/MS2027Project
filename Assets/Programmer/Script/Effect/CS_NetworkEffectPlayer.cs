using Unity.Netcode;
using UnityEngine;

/*
 * エフェクトを全員の画面に出すクラス(SEのCS_NetworkSoundPlayerにあたる)
 * 送るのはIDと位置・向きだけで、各クライアントのCS_EffectManagerが自分の画面に出す
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・CS_NetworkSoundPlayerはシーンに1つ置く形だが、こちらはエフェクトを出すキャラクター(Player・Policeなど)のPrefabに付ける
 *   ・付いていく先(このキャラクター)を、IDを送らずに伝えられる
 *   ・シーンにNetworkObjectを置かなくてよい
 * ・呼べるのはOwner(またはオフライン)だけ
 *   プレイヤーの攻撃などのイベントは、操作しているクライアント(NPCはホスト)= Ownerでしか起きないので、そのまま呼べる
 *   警察・悪人はサーバーがOwnerなので、サーバーで起きたイベントから呼べる
 * ・流れ
 *   呼んだクライアントはすぐ自分の画面に出す → RPCで他の全員に送る(SendTo.NotMe) → 受け取った側も自分の画面に出す
 *   (呼んだ本人は通信を待たずに出るので、自分の攻撃のエフェクトが遅れない)
 * ・オフライン(NetworkManagerが動いていない)の時は、自分の画面に出すだけ
 * ・全員に同期されている値(HPのNetworkVariableなど)の変化を見て出す時は、これを使わずに
 *   各クライアントで CS_EffectManager.instance.Play を直接呼べばよい(二重に出ないように)
 */
// ========================================

public class CS_NetworkEffectPlayer : NetworkBehaviour
{
    // 全員の画面で、指定した位置(ワールド座標)にエフェクトを出す
    public void PlayForEveryone(CSE_EffectId id, Vector3 position, Quaternion rotation)
    {
        Send(id, position, rotation, false);
    }

    // 全員の画面で、このオブジェクトに付いていくエフェクトを出す
    // 位置・向きはこのオブジェクトから見た値(例: 足元なら Vector3.zero、胸の前なら new Vector3(0, 1, 0.5f))
    public void PlayForEveryoneAttached(CSE_EffectId id, Vector3 localPosition, Quaternion localRotation)
    {
        Send(id, localPosition, localRotation, true);
    }

    private void Send(CSE_EffectId id, Vector3 position, Quaternion rotation, bool isAttached)
    {
        if (id == CSE_EffectId.None) return;

        PlayLocal(id, position, rotation, isAttached);

        if (!IsSpawned) return;

        if (!IsOwner)
        {
            Debug.LogWarning($"[CS_NetworkEffectPlayer] Owner以外から呼ばれたため、他の人には送りません: {id}", this);
            return;
        }

        PlayOnOthersRpc(id, position, rotation, isAttached);
    }

    // Ownerから、自分以外の全員へ送る
    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Owner)]
    private void PlayOnOthersRpc(CSE_EffectId id, Vector3 position, Quaternion rotation, bool isAttached)
    {
        if (id == CSE_EffectId.None || !IsFinite(position)) return;

        PlayLocal(id, position, rotation, isAttached);
    }

    private void PlayLocal(CSE_EffectId id, Vector3 position, Quaternion rotation, bool isAttached)
    {
        CS_EffectManager manager = CS_EffectManager.instance;
        if (manager == null)
        {
            Debug.LogWarning("[CS_NetworkEffectPlayer] CS_EffectManagerがありません。シーンにEffectManagerを置いてください。", this);
            return;
        }

        if (!isAttached)
        {
            manager.Play(id, position, rotation);
            return;
        }

        manager.Play(id, transform.TransformPoint(position), transform.rotation * rotation, transform);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
