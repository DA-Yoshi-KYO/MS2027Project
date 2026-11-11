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
/// 使ってから一定時間後に、ランダムなプレイヤーと自分の位置を入れ替える効果(位相置換機)
/// 入れ替える処理は、生成する位相置換機の実体(CS_PhaseSwap)が行う
/// </summary>
/// <remarks>
/// ・CSO_ItemDataCarriableの効果として使う(アイテム使用ボタンで発動)
/// ・Apply(target)のtargetは使用者(プレイヤー)
/// ・生成はサーバー(またはオフライン)で行う(CS_PlayerItemSlotの使用処理がサーバーで実行されるため)
/// </remarks>
[CreateAssetMenu(fileName = "DB_ItemEffectPhaseSwap", menuName = "Item/Item Effect/Phase Swap")]
public class CSO_ItemEffectPhaseSwap : CSO_ItemEffect
{
    [SerializeField]
    [Tooltip("生成する位相置換機の実体のプレハブ")]
    private CS_PhaseSwap _swapPrefab = null;

    [SerializeField, Min(0f)]
    [Tooltip("使ってから、位置を入れ替えるまでの時間(秒)")]
    private float _delay = 2.0f;

    public override void Apply(GameObject target)
    {
        if (_swapPrefab == null)
        {
            Debug.LogError("CSO_ItemEffectPhaseSwap: Swap Prefab が未設定です", this);
            return;
        }

        CS_Player user = target.GetComponentInParent<CS_Player>();
        if (user == null) return;

        CS_PhaseSwap swap = Instantiate(_swapPrefab, target.transform.position, Quaternion.identity);

        // オンライン時は、クライアントにも出す(位置の入れ替えを全員へ知らせるRpcに必要)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            swap.NetworkObject.Spawn();
        }

        swap.Begin(user, _delay);
    }
}
