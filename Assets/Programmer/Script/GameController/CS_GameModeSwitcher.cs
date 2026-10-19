/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-19 | 初回作成
 * ================================================ */

using UnityEngine;

// ========================================
/*
 * メモ
 * ・ゲームシーンに1つ置き、遊び方(CS_GameModeStore)に合わせてシーンの配置物を切り替える
 *   マルチ: 何もしない(シーンに置いたマルチ用の配置物がそのまま動く)
 *   ソロ  : マルチ専用の物を無効にし、ソロ用の配置プレハブを生成する
 * ・フィールド(建物・道路)は両方の遊び方で共通なので、ここには入れない
 * ・マルチ専用の物: 悪人スポナー、手配度マネージャー、アイテム生成、巡回警察、ランダムイベント、
 *                   出現点・生成点、ソロでは要らないUI(ランキング、頭上の順位など)
 * ・ソロ用の配置プレハブ: 上と同じ種類の物をソロ用のDBで設定したもの + 遊べる範囲(CS_PlayArea)と範囲の壁
 *   ランダムイベントは入れない
 * ・他のスポナーはStartで出現点を探すので、それより前(Awake)に切り替える
 *   そのため実行順を早めている
 * ・ソロは通信せずに遊ぶので、マルチ専用の物にNetworkObjectが含まれていても無効にしてよい
 */
// ========================================
[DefaultExecutionOrder(-1000)]
public class CS_GameModeSwitcher : MonoBehaviour
{
    [SerializeField][Tooltip("ソロの時に無効にする物(マルチ専用の配置物・UI)")] private GameObject[] _multiOnlyObjects;
    [SerializeField][Tooltip("ソロの時に生成する配置プレハブ")] private GameObject _soloLayoutPrefab;

    void Awake()
    {
        if (!CS_GameModeStore.isSolo) return;

        foreach (GameObject target in _multiOnlyObjects)
        {
            if (target != null) target.SetActive(false);
        }

        if (_soloLayoutPrefab == null)
        {
            Debug.LogError("CS_GameModeSwitcher: Solo Layout Prefab が未設定です", this);
            return;
        }
        Instantiate(_soloLayoutPrefab);
    }
}
