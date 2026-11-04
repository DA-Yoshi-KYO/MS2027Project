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
 *            public override bool IsFinished(CS_RandomEventContext context) { ...全員倒されたらtrue(時間切れより早く終わる)... }
 *        }
 *   2. 右クリック → Create から、そのイベントのアセットを作る
 *   3. 発生スケジュール(CSO_RandomEventSchedule)の、出したいタイミングの候補に入れる
 *
 * ■ 呼ばれる順番(すべてサーバー、またはオフラインで呼ばれる)
 *   OnStart → (毎フレーム) OnUpdate → 終わる条件を満たしたら → OnEnd
 *   終わる条件 : 始まってから duration 秒たった、または IsFinished が true を返した
 *   ・duration は全イベント共通の終了時間(初期値60秒)。0以下にすると時間では終わらない
 *   ・時間より早く終わる条件(例: 出した悪人が全員倒された)があるイベントは、IsFinishedをoverrideする
 *
 * ■ 警察の介入
 *   blockPoliceDetection をオンにしたイベントは、開催中、発生地点の範囲(CS_RandomEventPoint.radius)の中で
 *   警察に探知されない。警察側は CS_RandomEventManager.IsPoliceDetectionBlocked(位置) で問い合わせる
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

    [SerializeField]
    [Tooltip("始まってから終わるまでの時間(秒)。0以下にすると時間では終わらない")]
    private float _duration = 60f;

    [SerializeField]
    [Tooltip("開催中、発生地点の範囲の中では警察に探知されないようにする")]
    private bool _blockPoliceDetection;

    public string displayName => _displayName;
    public float duration => _duration;
    public bool blockPoliceDetection => _blockPoliceDetection;

    // イベントを終えるか(CS_RandomEventManagerが毎フレーム確認する)。時間切れか、イベントごとの終わる条件を満たした時
    public bool ShouldEnd(CS_RandomEventContext context)
    {
        bool isTimeUp = _duration > 0f && context.elapsedTime >= _duration;
        return isTimeUp || IsFinished(context);
    }

    // イベントを始める(サーバー、またはオフライン)
    public virtual void OnStart(CS_RandomEventContext context)
    {
    }

    // 開催中、毎フレーム呼ばれる(サーバー、またはオフライン)
    public virtual void OnUpdate(CS_RandomEventContext context, float deltaTime)
    {
    }

    // 時間より早く終わる条件を満たしたか(サーバー、またはオフライン)。初期状態は時間切れまで続く
    public virtual bool IsFinished(CS_RandomEventContext context)
    {
        return false;
    }

    // イベントの後片付け(サーバー、またはオフライン)
    public virtual void OnEnd(CS_RandomEventContext context)
    {
    }
}
