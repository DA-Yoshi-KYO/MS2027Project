using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * ランダムイベントを「いつ・どこで・どれを」起こすかを決め、開催を管理するクラス
 * 発生スケジュール(CSO_RandomEventSchedule)のタイミングになったら、イベントと発生地点を抽選して開始する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ■ 置き方
 *   シーンに1つ置き(NetworkObjectも付ける)、Scheduleに発生スケジュールのアセットを設定する
 *   発生地点(CS_RandomEventPoint)はシーン上のものを起動時に全て集める
 *
 * ■ 流れ(抽選・開始・終了はサーバー、またはオフラインで行う)
 *   1. ゲームの経過時間が、スケジュールの各タイミングの時間を過ぎたら(1タイミングにつき1回)
 *   2. そのタイミングの候補からイベントを1つ、開催中でない発生地点から1つ、ランダムに選ぶ
 *   3. CSO_RandomEvent.OnStart → 毎フレーム OnUpdate → IsFinishedがtrueになったら OnEnd
 *   4. 開始・終了の時に onEventStarted / onEventEnded を呼ぶ
 *      クライアントにもRPCで知らせ、同じ内容のCS_RandomEventContextで呼ぶ(UI・ミニマップ・演出用)
 *   候補が空・空いている発生地点が無い場合は、そのタイミングでは何も起きない
 *
 * ■ 経過時間
 *   CS_TimerController(ゲームのタイマー)から取る。シーンに無ければ、このクラスが動き始めてからの時間を使う
 *   オンラインでタイマーがまだSpawnされていない間は、終了時刻が未設定で正しい値が取れないので使わない
 *   ※ CS_TimerControllerはオフライン時、経過時間を取得するたびに残り時間が減ってしまうため、
 *      毎フレームではなく checkInterval ごとに取得している
 *
 * ■ クライアントでのイベントの特定
 *   どのイベント・どの発生地点かは「タイミングの番号・候補の番号・発生地点の番号」で送る
 *   発生地点は全員で同じ順番になるよう、位置(x → z → y)で並べている
 *   途中参加したクライアントには、参加前に始まったイベントは知らされない
 *
 * ■ 後から追加する予定のもの(今は作っていない)
 *   ・イベントの終了時間 : CSO_RandomEvent.IsFinishedで context.elapsedTime を見れば、イベントごとに作れる
 *   ・イベントの範囲内だけ警察に探知されない : onEventStarted / onEventEnded と context.point を使って警察側で判定する
 */
// ========================================

public class CS_RandomEventManager : NetworkBehaviour
{
    private const float _checkInterval = 0.25f;   // 経過時間を確認する間隔(秒)

    [SerializeField]
    [Tooltip("発生スケジュール(DB_RandomEventSchedule)。いつ・どのイベントを起こすか")]
    private CSO_RandomEventSchedule _schedule;

    private CS_RandomEventPoint[] _points;
    private bool[] _isTimingDone;                 // 各タイミングを処理済みか
    private readonly List<CS_RandomEventContext> _activeEvents = new List<CS_RandomEventContext>();
    private CS_TimerController _timer;
    private float _startTime;                     // 動き始めた時刻(タイマーが無い時の経過時間に使う)
    private float _checkTimer;
    private int _nextId = 1;

    // 開催中のイベント(サーバー・クライアントとも)
    public IReadOnlyList<CS_RandomEventContext> activeEvents => _activeEvents;

    // イベントが始まった・終わった時に呼ばれる(全クライアント)。UI・ミニマップ・演出などの購読用
    public static event Action<CS_RandomEventContext> onEventStarted;
    public static event Action<CS_RandomEventContext> onEventEnded;

    // このマシンが抽選・開催の権威を持つか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    private void Awake()
    {
        _points = CollectPoints();
    }

    private void Start()
    {
        _timer = FindAnyObjectByType<CS_TimerController>();
        _startTime = Time.time;

        if (_schedule == null)
        {
            Debug.LogError("CS_RandomEventManager: Schedule が未設定です", this);
            enabled = false;
            return;
        }

        _isTimingDone = new bool[_schedule.timingCount];
        if (_points.Length == 0)
        {
            Debug.LogWarning("CS_RandomEventManager: シーンにCS_RandomEventPointが無いため、ランダムイベントは起きません", this);
        }
    }

    private void Update()
    {
        if (!hasAuthority) return;

        UpdateActiveEvents(Time.deltaTime);

        _checkTimer += Time.deltaTime;
        if (_checkTimer < _checkInterval) return;
        _checkTimer = 0f;

        CheckTimings(GetElapsedTime());
    }

    // 時間を過ぎたタイミングを1回ずつ処理する
    private void CheckTimings(float elapsedTime)
    {
        for (int i = 0; i < _schedule.timingCount; i++)
        {
            if (_isTimingDone[i]) continue;
            if (elapsedTime < _schedule.GetTiming(i).time) continue;

            _isTimingDone[i] = true;
            TryStartEvent(i, elapsedTime);
        }
    }

    // イベントと発生地点を抽選して始める
    private void TryStartEvent(int timingIndex, float elapsedTime)
    {
        CSO_RandomEvent[] candidates = _schedule.GetTiming(timingIndex).candidates;
        int candidateIndex = PickCandidate(candidates);
        int pointIndex = PickFreePoint();
        if (candidateIndex < 0 || pointIndex < 0) return;

        CS_RandomEventContext context = new CS_RandomEventContext(_nextId++, candidates[candidateIndex], _points[pointIndex], elapsedTime);
        context.point.isOccupied = true;
        _activeEvents.Add(context);

        context.randomEvent.OnStart(context);
        onEventStarted?.Invoke(context);
        if (IsSpawned) NotifyStartedRpc(context.id, timingIndex, candidateIndex, pointIndex, elapsedTime);
    }

    // 開催中のイベントを進め、終わったものを片付ける
    private void UpdateActiveEvents(float deltaTime)
    {
        for (int i = _activeEvents.Count - 1; i >= 0; i--)
        {
            CS_RandomEventContext context = _activeEvents[i];
            context.AddElapsedTime(deltaTime);
            context.randomEvent.OnUpdate(context, deltaTime);

            if (!context.randomEvent.IsFinished(context)) continue;

            context.randomEvent.OnEnd(context);
            context.point.isOccupied = false;
            _activeEvents.RemoveAt(i);

            onEventEnded?.Invoke(context);
            if (IsSpawned) NotifyEndedRpc(context.id);
        }
    }

    // 候補から空でないものをランダムに選ぶ(無ければ-1)
    private static int PickCandidate(CSO_RandomEvent[] candidates)
    {
        List<int> valid = new List<int>();
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null) valid.Add(i);
        }
        return valid.Count > 0 ? valid[UnityEngine.Random.Range(0, valid.Count)] : -1;
    }

    // 開催中でない発生地点をランダムに選ぶ(無ければ-1)
    private int PickFreePoint()
    {
        List<int> free = new List<int>();
        for (int i = 0; i < _points.Length; i++)
        {
            if (_points[i] != null && !_points[i].isOccupied) free.Add(i);
        }
        return free.Count > 0 ? free[UnityEngine.Random.Range(0, free.Count)] : -1;
    }

    // ---- クライアントへの通知 ----

    [Rpc(SendTo.NotServer)]
    private void NotifyStartedRpc(int id, int timingIndex, int candidateIndex, int pointIndex, float startTime)
    {
        if (_schedule == null || timingIndex >= _schedule.timingCount || pointIndex >= _points.Length) return;

        CSO_RandomEvent[] candidates = _schedule.GetTiming(timingIndex).candidates;
        if (candidateIndex >= candidates.Length) return;

        CS_RandomEventContext context = new CS_RandomEventContext(id, candidates[candidateIndex], _points[pointIndex], startTime);
        _activeEvents.Add(context);
        onEventStarted?.Invoke(context);
    }

    [Rpc(SendTo.NotServer)]
    private void NotifyEndedRpc(int id)
    {
        CS_RandomEventContext context = _activeEvents.Find(active => active.id == id);
        if (context == null) return;

        _activeEvents.Remove(context);
        onEventEnded?.Invoke(context);
    }

    // ---- 補助 ----

    // シーン上の発生地点を、全員で同じ順番になるよう位置で並べて集める
    private static CS_RandomEventPoint[] CollectPoints()
    {
        CS_RandomEventPoint[] points = FindObjectsByType<CS_RandomEventPoint>(FindObjectsSortMode.None);
        Array.Sort(points, (a, b) =>
        {
            Vector3 pa = a.transform.position;
            Vector3 pb = b.transform.position;
            if (!Mathf.Approximately(pa.x, pb.x)) return pa.x.CompareTo(pb.x);
            if (!Mathf.Approximately(pa.z, pb.z)) return pa.z.CompareTo(pb.z);
            return pa.y.CompareTo(pb.y);
        });
        return points;
    }

    // ゲームの経過時間(秒)。タイマーが無いシーンでは、このクラスが動き始めてからの時間
    private float GetElapsedTime()
    {
        bool isOnline = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (_timer != null && (_timer.IsSpawned || !isOnline)) return _timer.GetElapsedTime();

        return Time.time - _startTime;
    }
}
