/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン遷移とフェード演出を担当するクラス
/// ・GameControllerから呼ばれる
/// ・フェード演出を追加する場合はここに書く
/// </summary>
public class CS_SceneTransitioner : MonoBehaviour
{
    /// <summary>
    /// シーン遷移を開始する
    /// 今は即移動だが、後でフェード演出を追加できる
    /// </summary>
    public void StartTransition(string sceneName)
    {
        //今は即シーン移動
        SceneManager.LoadScene(sceneName);
    }
}
