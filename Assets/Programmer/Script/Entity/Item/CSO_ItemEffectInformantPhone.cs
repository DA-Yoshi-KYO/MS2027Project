/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 使用者以外のプレイヤーの近くに警察を呼ぶ効果(内通フォン)
/// 呼ぶ人数・出現する距離・追跡する時間は、内通フォンのデータ(CSO_PoliceInformantData)で決める
/// </summary>
/// <remarks>
/// ・CSO_ItemDataCarriableの効果として使う(アイテム使用ボタンで発動)
/// ・Apply(target)のtargetは使用者(プレイヤー)。使用者の近くには警察を呼ばない
/// ・警察の出現はサーバー(またはオフライン)で行う(CS_PlayerItemSlotの使用処理がサーバーで実行されるため)
/// </remarks>
[CreateAssetMenu(fileName = "DB_ItemEffectInformantPhone", menuName = "Item/Item Effect/Informant Phone")]
public class CSO_ItemEffectInformantPhone : CSO_ItemEffect
{
    [SerializeField]
    [Tooltip("内通フォンのデータ(呼ぶ警察・人数・出現する距離・追跡する時間)")]
    private CSO_PoliceInformantData _informantData = null;

    public override void Apply(GameObject target)
    {
        CS_PoliceInformantSummoner.Summon(target, _informantData);
    }
}
