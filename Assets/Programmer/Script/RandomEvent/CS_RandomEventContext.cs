/*
 * 開催中のランダムイベント1回分の情報
 * どのイベントが、どの発生地点で、いつ始まったか と、イベントごとの状態を持つ
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・CS_RandomEventManagerが開始時に作り、終了までイベント(CSO_RandomEvent)の各メソッドに渡す
 * ・stateはイベントが自由に使う入れ物(アセットは共有されるので、開催ごとの状態はここに持つ)
 *     例) context.state = new List<CS_VillainHealth>();   // 生成した悪人
 * ・クライアントにも同じ内容で作られる(stateはサーバーにしか無い)
 */
// ========================================

public class CS_RandomEventContext
{
    private readonly int _id;
    private readonly CSO_RandomEvent _randomEvent;
    private readonly CS_RandomEventPoint _point;
    private readonly float _startTime;
    private float _elapsedTime;

    public int id => _id;                                  // 開催ごとの番号(同じイベントが何度起きても区別できる)
    public CSO_RandomEvent randomEvent => _randomEvent;    // 開催しているイベント
    public CS_RandomEventPoint point => _point;            // 発生地点
    public float startTime => _startTime;                  // 始まった時のゲームの経過時間(秒)
    public float elapsedTime => _elapsedTime;              // 始まってからの時間(秒)

    // イベントが自由に使う状態の入れ物(サーバーのみ)
    public object state { get; set; }

    public CS_RandomEventContext(int id, CSO_RandomEvent randomEvent, CS_RandomEventPoint point, float startTime)
    {
        _id = id;
        _randomEvent = randomEvent;
        _point = point;
        _startTime = startTime;
    }

    // 始まってからの時間を進める(CS_RandomEventManagerから呼ばれる)
    public void AddElapsedTime(float deltaTime)
    {
        _elapsedTime += deltaTime;
    }
}
