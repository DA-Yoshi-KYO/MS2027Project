/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-29 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 使用者が立っている場所に煙幕(CS_SmokeScreen)を張る効果
/// 煙の大きさ・時間は煙幕側(CS_SmokeScreen)で決める
/// </summary>
/// <remarks>
/// ・CSO_ItemDataCarriableの効果として使う(アイテム使用ボタンで発動)
/// ・Apply(target)のtargetは使用者(プレイヤー)
/// ・生成はサーバー(またはオフライン)で行う(CS_PlayerItemSlotの使用処理がサーバーで実行されるため)
/// </remarks>
[CreateAssetMenu(fileName = "DB_ItemEffectSmoke", menuName = "Item/Item Effect/Smoke")]
public class CSO_ItemEffectSmoke : CSO_ItemEffect
{
    [SerializeField]
    [Tooltip("張る煙幕のプレハブ")]
    private CS_SmokeScreen _smokePrefab = null;

    public override void Apply(GameObject target)
    {
        if (_smokePrefab == null)
        {
            Debug.LogError("CSO_ItemEffectSmoke: Smoke Prefab が未設定です", this);
            return;
        }

        CS_SmokeScreen smoke = Instantiate(_smokePrefab, target.transform.position, Quaternion.identity);

        // オンライン時は、クライアントにも煙幕を出す
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            smoke.NetworkObject.Spawn();
        }
    }
}
