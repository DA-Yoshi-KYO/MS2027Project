/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-19 | 初回作成
 * 2026-10-19 | ソロ用の物をプレハブから生成せず、シーンに置いた物を切り替えるように変更
 * 2026-10-22 | 遊び方ごとの物を Field_Multi / Field_Solo1 にまとめた(メモの名前を合わせた)
 * 2026-10-22 | フィールドのプレハブ(Field)の中に置くようにした。ランキングUIはCS_HideInSoloで消す
 * ================================================ */

using UnityEngine;

// ========================================
/*
 * メモ
 * ・フィールドのプレハブ(Field)の中に1つ置き、遊び方(CS_GameModeStore)に合わせて中の物を切り替える
 *   Field = Field_Common(共通) + Field_Multi + Field_Solo1 + これ。シーンにはFieldを1つ置くだけでよい
 *   マルチ: ソロ用の物を無効にする(マルチ用の配置物はそのまま動く)
 *   ソロ  : マルチ専用の物を無効にし、ソロ用の物を有効にする
 * ・建物・道路(Field_Common)は両方の遊び方で共通なので、ここには入れない
 * ・マルチ専用の物(Field_Multi): プレイヤーの開始位置、悪人スポナーと出現点、巡回警察、アイテム生成と生成点、
 *                   ランダムイベント
 * ・ソロでは要らないUI(ランキング)はシーン側にあって参照できないので、UI側にCS_HideInSoloを付けて消す
 * ・ソロ用の物(Field_Solo1): 上と同じ種類の物をソロ用のDBで設定したもの + 遊べる範囲(CS_PlayArea)と範囲の壁
 *   シーンに置いておくので、フィールドを見ながら出現点や範囲を直せる
 *   編集中に表示したまま保存しても、マルチではここで無効にするので問題ない
 * ・他のスポナーはStartで出現点を探すので、それより前(Awake)に切り替える
 *   そのため実行順を早めている(他の物のAwakeより先に無効にできる)
 * ・ソロは通信せずに遊ぶので、マルチ専用の物にNetworkObjectが含まれていても無効にしてよい
 */
// ========================================
[DefaultExecutionOrder(-1000)]
public class CS_GameModeSwitcher : MonoBehaviour
{
    [SerializeField][Tooltip("ソロの時に無効にする物(Field_Multi)")] private GameObject[] _multiOnlyObjects;
    [SerializeField][Tooltip("ソロの時だけ有効にする物(Field_Solo1)")] private GameObject _soloObjects;

    void Awake()
    {
        bool isSolo = CS_GameModeStore.isSolo;

        // 先に片方を無効にしてから、もう片方を有効にする(両方が同時に動く瞬間を作らない)
        if (isSolo)
        {
            foreach (GameObject target in _multiOnlyObjects)
            {
                if (target != null) target.SetActive(false);
            }
        }

        if (_soloObjects == null)
        {
            Debug.LogError("CS_GameModeSwitcher: Solo Objects が未設定です", this);
            return;
        }
        _soloObjects.SetActive(isSolo);
    }
}
