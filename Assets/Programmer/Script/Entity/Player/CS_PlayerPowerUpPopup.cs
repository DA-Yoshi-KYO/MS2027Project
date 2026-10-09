using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーのステータスが上がった時に、頭上に「POWER UP!!」を出すクラス
 * ステータスを上げた側(CSO_ItemEffectStatBoostなど)からShowを呼ぶ
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・Showはサーバー(またはオフライン)で呼ぶ(ステータスの変更がサーバーで行われるため)
 *   オンラインでは全員にRPCで知らせ、各クライアントの手元で文字を出す(全員の画面に見える)
 * ・文字の見た目・動き(文字・色・大きさ・昇る距離・時間)は popup で調整する
 *   出す位置はプレイヤーの体の中心(transform.position)から heightOffset 上
 * ・新しいステータスアップの仕組みを作った時も、ステータスを上げた後にShowを呼べば表示される
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerPowerUpPopup : NetworkBehaviour
{
    [SerializeField, Min(0f)]
    [Tooltip("体の中心からどれだけ上に出すか(m)")]
    private float _heightOffset = 1.5f;

    [SerializeField]
    [Tooltip("表示する文字と動き")]
    private CS_FloatingText.Settings _popup = new CS_FloatingText.Settings();

    // 頭上に「POWER UP!!」を出す(サーバー、またはオフライン)
    public void Show()
    {
        if (!IsSpawned)
        {
            ShowLocal();
            return;
        }

        if (IsServer) ShowRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ShowRpc()
    {
        ShowLocal();
    }

    private void ShowLocal()
    {
        CS_FloatingText.Spawn(transform.position + Vector3.up * _heightOffset, _popup);
    }
}
