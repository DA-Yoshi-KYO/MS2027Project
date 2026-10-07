using UnityEngine;

/*
 * ランダムイベント1種類分の共通の形(ScriptableObject)
 * 各イベント(大量発生・救援物資・レイド・支援物資など)は、これを継承して作る
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ■ 新しいイベントの作り方
 *   1. CSO_RandomEventを継承したクラスを作り、必要なメソッドだけoverrideする
 *        [CreateAssetMenu(menuName = "RandomEvent/Mass Spawn")]
 *        public class CSO_RandomEventMassSpawn : CSO_RandomEvent
 *        {
 *            public override void OnStart(CS_RandomEventContext context) { ...発生地点(context.point)の周りに悪人を生成... }
 *            public override bool IsFinished(CS_RandomEventContext context) { ...全員倒されたらtrue... }
 *        }
 *   2. 右クリック → Create から、そのイベントのアセットを作る
 *   3. 発生スケジュール(CSO_RandomEventSchedule)の、出したいタイミングの候補に入れる
 *
 * ■ 呼ばれる順番(すべてサーバー、またはオフラインで呼ばれる)
 *   OnStart → (毎フレーム) OnUpdate → IsFinishedがtrueになったら → OnEnd
 *   ・IsFinishedは初期状態でtrue(開始してすぐ終わる)。物を置くだけのイベントはこのままでよい
 *   ・続くイベントは、IsFinishedをoverrideして終わる条件を返す
 *
 * ■ 注意
 *   ・アセットは全ての開催で共有されるので、開催ごとの状態はこのクラスのフィールドに持たない
 *     → CS_RandomEventContext.stateに入れる(例: 生成した悪人のリスト)
 *   ・見た目やUIは各クライアントで、CS_RandomEventManager.onEventStarted / onEventEnded を購読して出す
 */
// ========================================

public abstract class CSO_RandomEvent : ScriptableObject
{
    [SerializeField]
    [Tooltip("UIなどに表示するイベント名")]
    private string _displayName = "ランダムイベント";

    public string displayName => _displayName;

    // イベントを始める(サーバー、またはオフライン)
    public virtual void OnStart(CS_RandomEventContext context)
    {
    }

    // 開催中、毎フレーム呼ばれる(サーバー、またはオフライン)
    public virtual void OnUpdate(CS_RandomEventContext context, float deltaTime)
    {
    }

    // イベントが終わったか(サーバー、またはオフライン)。初期状態は開始してすぐ終わる
    public virtual bool IsFinished(CS_RandomEventContext context)
    {
        return true;
    }

    // イベントの後片付け(サーバー、またはオフライン)
    public virtual void OnEnd(CS_RandomEventContext context)
    {
    }
}
