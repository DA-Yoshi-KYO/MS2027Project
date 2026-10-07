using UnityEngine;

/*
 * ランダムイベントの仕組みの動作確認用イベント
 * 開始・終了をConsoleに出すだけで、ゲームには何も影響しない
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・右クリック → Create → RandomEvent → Test でアセットを作り、発生スケジュールの候補に入れて使う
 * ・終了時間(Duration)は共通設定のものを使う(テスト用アセットは10秒)
 * ・本番のスケジュールには入れないこと
 */
// ========================================

[CreateAssetMenu(fileName = "DB_RandomEventTest", menuName = "RandomEvent/Test")]
public class CSO_RandomEventTest : CSO_RandomEvent
{
    public override void OnStart(CS_RandomEventContext context)
    {
        Debug.Log($"CSO_RandomEventTest: 「{displayName}」が {context.point.name} で始まりました(経過時間 {context.startTime:0.0}秒、警察に探知されない: {blockPoliceDetection})");
    }

    public override void OnEnd(CS_RandomEventContext context)
    {
        Debug.Log($"CSO_RandomEventTest: 「{displayName}」が {context.point.name} で終わりました");
    }
}
