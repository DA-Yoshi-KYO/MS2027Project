/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 使用者を一定時間、透明にする効果(光学迷彩)
/// 透明になっている間の処理は、生成する光学迷彩の実体(CS_OpticalCamo)が行う
/// </summary>
/// <remarks>
/// ・CSO_ItemDataCarriableの効果として使う(アイテム使用ボタンで発動)
/// ・Apply(target)のtargetは使用者(プレイヤー)
/// ・すでに透明になっている時に使うと、透明になる時間を延ばし直す(重ねて使っても、実体は1つ)
/// ・生成はサーバー(またはオフライン)で行う(CS_PlayerItemSlotの使用処理がサーバーで実行されるため)
/// </remarks>
[CreateAssetMenu(fileName = "DB_ItemEffectOpticalCamo", menuName = "Item/Item Effect/Optical Camo")]
public class CSO_ItemEffectOpticalCamo : CSO_ItemEffect
{
    [SerializeField]
    [Tooltip("生成する光学迷彩の実体のプレハブ")]
    private CS_OpticalCamo _camoPrefab = null;

    [SerializeField, Min(0.1f)]
    [Tooltip("透明になる時間(秒)。使用者が死亡した時は、この時間より前に終わる")]
    private float _duration = 10.0f;

    public override void Apply(GameObject target)
    {
        if (_camoPrefab == null)
        {
            Debug.LogError("CSO_ItemEffectOpticalCamo: Camo Prefab が未設定です", this);
            return;
        }

        CS_Player user = target.GetComponentInParent<CS_Player>();
        if (user == null || CS_OpticalCamo.TryRefresh(user, _duration)) return;

        CS_OpticalCamo camo = Instantiate(_camoPrefab, target.transform.position, Quaternion.identity);

        // オンライン時は、クライアントにも出す(他の人の画面で使用者のモデルを隠すのに必要)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            camo.NetworkObject.Spawn();
        }

        camo.Begin(user, _duration);
    }
}
