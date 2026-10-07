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
 * ・duration 秒たったら終わる(イベントごとに終わる条件を作る例にもなっている)
 * ・本番のスケジュールには入れないこと
 */
// ========================================

[CreateAssetMenu(fileName = "DB_RandomEventTest", menuName = "RandomEvent/Test")]
public class CSO_RandomEventTest : CSO_RandomEvent
{
    [SerializeField, Min(0f)]
    [Tooltip("始まってから終わるまでの時間(秒)")]
    private float _duration = 10f;

    public override void OnStart(CS_RandomEventContext context)
    {
        Debug.Log($"CSO_RandomEventTest: 「{displayName}」が {context.point.name} で始まりました(経過時間 {context.startTime:0.0}秒)");
    }

    public override bool IsFinished(CS_RandomEventContext context)
    {
        return context.elapsedTime >= _duration;
    }

    public override void OnEnd(CS_RandomEventContext context)
    {
        Debug.Log($"CSO_RandomEventTest: 「{displayName}」が {context.point.name} で終わりました");
    }
}
