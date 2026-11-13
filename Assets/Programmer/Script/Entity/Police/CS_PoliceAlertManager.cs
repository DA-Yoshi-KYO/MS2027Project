/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * 2026-10-23 | 警戒状態の本体をCS_PlayerAlertに揃え、付近の警察を向かわせる役だけにした
 * 2026-11-01 | 警戒状態になった時に、プレイヤーの近くへ増援を出すようにした(手配度は復活させない)
 * 2026-11-06 | 増援を、見つけた警察と挟む形の位置に、遠くから出すようにした
 * 2026-11-06 | 警察に追跡・ドローンに目撃・ライトに照射されている時間と、レーザーに当たった時に、プレイヤーを減点するようにした(見つかった瞬間の減点はやめた)
 * 2026-11-09 | 警戒状態を、タイマーではなく、警察・巡回ドローンの実際の状態から決めてCS_PlayerAlertへ渡すようにした
 * 2026-11-13 | 復活後の無敵中のプレイヤーは、見つかっても警戒状態にせず、減点もしないようにした(仕様書「リスポーン」: この間は警察に視認されない。Issue #453)
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 警戒状態のプレイヤーのところへ、付近の警察を向かわせるクラス(シーンに1つ置く)
/// ・警戒状態の本体(なし / 発見中 / 捜索中)は、プレイヤー側のCS_PlayerAlertが持つ。このクラスが、警察・巡回ドローンの実際の状態から決めて、毎フレーム渡す
///   発見中: そのプレイヤーを実際に見つけて追いかけている警察(または、見ている・攻撃している巡回ドローン)がいる
///   捜索中: 追いかける警察はいないが、探している・怪しんでいる・駆け付けている警察(または、見失ったあと追っている巡回ドローン)がいる
///   なし  : どちらもいない(タイマーは使わない。警察が探索を終えて巡回へ戻ると、消える)
///   増援の警察は、警戒状態を支えない(増援が警戒状態の間追い続けるため、数えると警戒状態が終わらなくなる。増援が実際に見つけて追跡している時だけ数える)
///   防犯レーザー・ライトに触れた時(ReportTouch)は、追いかける警察の状態が無いので、決められた秒数だけ「発見中」にする
/// ・警察陣営(警察・防犯装置)がプレイヤーを見つけたら、Reportで知らせる(見えている間は呼び続ける)
///   Reportは、見つかった場所を覚えて、付近の警察を向かわせる準備と、増援を出す準備をする
/// ・プレイヤーが警戒状態の間(警戒状態になった直後を含む)は、見つかった場所の付近(設定の範囲内)にいる警察が、その場所へ向かう
///   見つかっている間は追跡し、見失ったら周囲を探す(この動きはCS_PoliceBrainが行う)
///   見つかった場所が変わったら、付近の警察へ新しい場所を知らせる
/// ・プレイヤーが警戒状態になった時(警戒状態でなかったプレイヤーが見つかった時)に、そのプレイヤーの近くへ増援の警察を出す
///   同じプレイヤーには、決められた間隔(reinforceCooldown)をあけないと出さない(短く解除される分、出しすぎないようにする)
///   増援は、プレイヤーから見て、見つけた警察(防犯装置)の反対側に出る(挟む形)。警戒状態の間、そのプレイヤーの現在地を追い、
///   警戒状態が解除されたら、追う標的も指示も無くなった時に帰還する
/// ・警察に追跡されている間・巡回ドローンに見られている間・ストリートライトに照らされている間は、決められた秒数ごとに、決められた点数を
///   プレイヤーのスコアから引く(ReportExposureで知らせてもらう。途切れたら、数えた時間を捨てる)
///   複数に同時に見られていても、減点は1人分。防犯レーザーに当たった時は、決められた点数を1回引く(ReportLaserHit)
///   点数は、スコアの結果(CS_PlayerResultDataHolder)の「警察に見つかった」へ加える
/// ・警戒状態が解除されたら(CS_PlayerAlertが「なし」に戻って、少し待っても見つからなければ)、そのプレイヤーへの指示をやめる
/// 警察への指示はサーバー(またはオフライン)だけで行う
/// </summary>
public class CS_PoliceAlertManager : MonoBehaviour
{
    // シーンに置かれている管理クラス(静的メソッドから使う)
    private static CS_PoliceAlertManager _instance = null;

    // 警告を出したか(見つかるたびに警告しないため)
    private static bool _hasWarnedNoInstance = false;
    private static bool _hasWarnedNoPlayerAlert = false;

    // 付近の警察へ指示を出し直す間隔(秒)
    private const float _dispatchInterval = 0.5f;

    // 見つかった場所がこの距離より動いたら、警察へ新しい場所を知らせる(m)
    private const float _repointDistance = 4.0f;

    // 増援の出現位置が見つからない時に、反対側の向きから左右へ試す向きを広げる角度の上限(度)
    private const float _reinforceWidenAngle = 60.0f;

    // 警戒状態が「なし」になってから、この時間(秒)は、警察への指示(派遣)を続ける
    // (見つかった直後は、警察の状態が変わるまでに少し間があるため、すぐに指示をやめないようにする)
    private const float _episodeGrace = 1.0f;

    // 最後に見られていると知らされてから、この時間(秒)以内なら、今も見られているとみなす
    private const float _exposureGrace = 0.2f;

    // 見つけた警察がプレイヤーにこの距離(m)より近い時は、反対側の向きを決めない(向きが安定しないため)
    private const float _minFinderDistance = 1.0f;

    /// <summary>
    /// プレイヤー1人分の、警察への指示の記録(警戒状態そのものではなく、誰に・どこへ指示したか)
    /// </summary>
    private class DispatchState
    {
        private readonly CS_PlayerAlert _alert;

        // 警察に知らせた、見つかった場所(同じ場所へ向かう警察は、この同じインスタンスを持つ)
        private CS_PoliceGatherPoint _point = null;

        // 見つかった場所へ向かうよう、すでに指示した警察(同じ場所には、1人に1回だけ指示する)
        private readonly HashSet<CS_PoliceBrain> _dispatched = new HashSet<CS_PoliceBrain>();

        // 最後に、警戒状態だった(または見つかった)時刻
        private float _lastActiveTime = 0.0f;

        public CS_PlayerAlert alert => _alert;
        public CS_PoliceGatherPoint point => _point;
        public HashSet<CS_PoliceBrain> dispatched => _dispatched;
        public float lastActiveTime => _lastActiveTime;

        public DispatchState(CS_PlayerAlert alert)
        {
            _alert = alert;
            _lastActiveTime = Time.time;
        }

        /// <summary>
        /// 今、警戒状態だった(または見つかった)ことを覚えるメソッド
        /// </summary>
        public void MarkActive()
        {
            _lastActiveTime = Time.time;
        }

        /// <summary>
        /// 見つかった場所を新しくするメソッド(まだ誰にも指示していない状態に戻す)
        /// </summary>
        public void SetPoint(CS_PoliceGatherPoint point)
        {
            _point = point;
            _dispatched.Clear();
        }
    }

    // 警察を向かわせているプレイヤー(警戒状態の間だけ入っている)
    private readonly Dictionary<CS_PlayerAlert, DispatchState> _states = new Dictionary<CS_PlayerAlert, DispatchState>();

    // 指示をやめるプレイヤーを集める際に使い回すリスト
    private readonly List<CS_PlayerAlert> _endBuffer = new List<CS_PlayerAlert>();

    // 次に警察へ指示を出し直すまでの残り時間
    private float _dispatchTimer = 0.0f;

    // 警戒状態の判定のために、毎フレーム集めるプレイヤー(使い回す)
    private readonly HashSet<CS_PlayerHealth> _foundNow = new HashSet<CS_PlayerHealth>();
    private readonly HashSet<CS_PlayerHealth> _searchNow = new HashSet<CS_PlayerHealth>();

    // 防犯レーザー・ライトに触れたプレイヤーごとの、「発見中」にしておく終わりの時刻(秒)
    private readonly Dictionary<CS_PlayerHealth, float> _touchedUntil = new Dictionary<CS_PlayerHealth, float>();
    private readonly List<CS_PlayerHealth> _touchBuffer = new List<CS_PlayerHealth>();

    // プレイヤーごとの、最後に増援を出した時刻(秒)
    private readonly Dictionary<CS_PlayerHealth, float> _lastReinforceTimes = new Dictionary<CS_PlayerHealth, float>();

    // 見られているプレイヤーごとの、最後に見られていると知らされた時刻(秒)
    private readonly Dictionary<CS_PlayerHealth, float> _lastExposedTimes = new Dictionary<CS_PlayerHealth, float>();

    // 見られているプレイヤーごとの、減点までに数えた時間(秒。見られなくなったら消える)
    private readonly Dictionary<CS_PlayerHealth, float> _exposedTimes = new Dictionary<CS_PlayerHealth, float>();

    // 見られなくなったプレイヤーを集める際に使い回すリスト
    private readonly List<CS_PlayerHealth> _exposureBuffer = new List<CS_PlayerHealth>();

    [Header("＝＝＝ 警戒状態 ＝＝＝")]
    [SerializeField]
    [Tooltip("警戒状態の設定(付近の範囲・防犯装置に触れた時・増援・減点)。警戒状態は、警察・巡回ドローンの実際の状態から決まる")]
    private CSO_PoliceAlertData _alertData = null;

    /// <summary>
    /// プレイヤーを見つけたことを知らせるメソッド(サーバー、またはオフラインで呼ぶこと。見えている間は呼び続ける)
    /// 見つかった場所を覚えて、付近の警察を向かわせる準備と、増援を出す準備をする(警戒状態は、警察・ドローンの状態から、毎フレーム決まる)
    /// </summary>
    /// <param name="player">見つけたプレイヤー</param>
    /// <param name="position">プレイヤーが見つかった場所</param>
    /// <param name="finderPosition">見つけた警察(防犯装置)の位置。増援を、その反対側(挟む形)に出すのに使う。分からなければ省略(向きはランダム)</param>
    public static void Report(CS_PlayerHealth player, Vector3 position, Vector3? finderPosition = null)
    {
        if (player == null || CS_PoliceSquad.IsNetworkClientOnly()) return;

        // 復活後の無敵中は見つからない(警戒状態にしない・警察を向かわせない・暗躍ボーナスもリセットしない)
        if (CS_PoliceVision.IsRespawnInvincible(player)) return;

        if (!player.TryGetComponent(out CS_PlayerAlert alert))
        {
            WarnNoPlayerAlert(player);
            return;
        }

        // 管理クラスが無いシーンでは、警察を向かわせない
        if (player.isDead || !TryGetInstance(out CS_PoliceAlertManager manager)) return;

        manager.OnFound(alert, player, position, finderPosition);
    }

    /// <summary>
    /// プレイヤーが防犯レーザー・ストリートライトに触れたことを知らせるメソッド(サーバー、またはオフラインで呼ぶこと)
    /// Reportと同じ準備に加えて、決められた秒数(touchAlertDuration)だけ、警戒状態を「発見中」にしておく
    /// (これらは瞬間の出来事で、追いかける警察の状態が無いため)
    /// </summary>
    /// <param name="player">触れたプレイヤー</param>
    /// <param name="position">プレイヤーの場所</param>
    /// <param name="finderPosition">防犯装置の位置</param>
    public static void ReportTouch(CS_PlayerHealth player, Vector3 position, Vector3? finderPosition = null)
    {
        Report(player, position, finderPosition);

        if (player == null || _instance == null || _instance._alertData == null || CS_PoliceSquad.IsNetworkClientOnly() || CS_PoliceVision.IsRespawnInvincible(player)) return;

        float duration = _instance._alertData.touchAlertDuration;
        if (duration > 0.0f) _instance._touchedUntil[player] = Time.time + duration;
    }

    /// <summary>
    /// プレイヤーが、警察に追跡されている・ドローンに見られている・ライトに照らされていることを知らせるメソッド
    /// 見られている間は、毎フレーム(または短い間隔で)呼び続けること。サーバー、またはオフラインで呼ぶこと
    /// </summary>
    /// <param name="player">見られているプレイヤー</param>
    public static void ReportExposure(CS_PlayerHealth player)
    {
        if (player == null || _instance == null || CS_PoliceSquad.IsNetworkClientOnly() || CS_PoliceVision.IsRespawnInvincible(player)) return;

        _instance._lastExposedTimes[player] = Time.time;
    }

    /// <summary>
    /// プレイヤーが防犯レーザーに当たったことを知らせるメソッド(当たるたびに、決められた点数を1回減点する)
    /// サーバー、またはオフラインで呼ぶこと
    /// </summary>
    /// <param name="player">当たったプレイヤー</param>
    public static void ReportLaserHit(CS_PlayerHealth player)
    {
        if (player == null || _instance == null || _instance._alertData == null || CS_PoliceSquad.IsNetworkClientOnly() || CS_PoliceVision.IsRespawnInvincible(player)) return;

        _instance.ApplyPenalty(player, _instance._alertData.laserPenaltyPoint);
    }

    /// <summary>
    /// プレイヤーが警戒状態かを判定するメソッド
    /// </summary>
    /// <param name="player">判定するプレイヤー</param>
    /// <returns>警戒状態(発見中・捜索中)ならtrue</returns>
    public static bool IsAlert(CS_PlayerHealth player)
    {
        return player != null && player.TryGetComponent(out CS_PlayerAlert alert) && alert.isAlerted;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("CS_PoliceAlertManager: シーンに2つ以上置かれています(後から置かれた方は使われません)", this);
            return;
        }

        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        _states.Clear();
        _lastExposedTimes.Clear();
        _exposedTimes.Clear();
        _touchedUntil.Clear();
        _lastReinforceTimes.Clear();
    }

    private void Update()
    {
        if (_instance != this) return;

        UpdateAlertStates();
        UpdateExposurePenalty(Time.deltaTime);
        if (_states.Count == 0) return;

        _dispatchTimer -= Time.deltaTime;
        if (_dispatchTimer > 0.0f) return;
        _dispatchTimer = _dispatchInterval;

        EndFinishedStates();
        foreach (DispatchState state in _states.Values)
        {
            Dispatch(state);
        }
    }

    /// <summary>
    /// 有効な管理クラスを取得するメソッド(シーンに無ければ、1回だけ警告する)
    /// </summary>
    private static bool TryGetInstance(out CS_PoliceAlertManager manager)
    {
        manager = _instance;
        if (manager != null) return true;

        if (!_hasWarnedNoInstance)
        {
            _hasWarnedNoInstance = true;
            Debug.LogWarning("CS_PoliceAlertManager: シーンに置かれていないため、警戒状態でも警察は向かいません");
        }
        return false;
    }

    /// <summary>
    /// プレイヤーにCS_PlayerAlertが付いていない時に、1回だけ警告するメソッド
    /// </summary>
    private static void WarnNoPlayerAlert(CS_PlayerHealth player)
    {
        if (_hasWarnedNoPlayerAlert) return;

        _hasWarnedNoPlayerAlert = true;
        Debug.LogWarning("CS_PoliceAlertManager: プレイヤーにCS_PlayerAlertが付いていないため、警戒状態にできません", player);
    }

    /// <summary>
    /// プレイヤーが見つかった時の処理(見つかった場所を更新して、警察へ知らせる準備をする。警戒状態になった直後なら、増援も出す)
    /// </summary>
    private void OnFound(CS_PlayerAlert alert, CS_PlayerHealth player, Vector3 position, Vector3? finderPosition)
    {
        if (_alertData == null) return;

        if (!_states.TryGetValue(alert, out DispatchState state))
        {
            // 警戒状態になった時に、増援を出す(解除されるまで、見つかるたびには出さない。短い間隔では出さない)
            state = new DispatchState(alert);
            _states.Add(alert, state);
            if (CanSpawnReinforcements(player)) SpawnReinforcements(alert, player, finderPosition);
        }
        state.MarkActive();

        // 見つかった場所が大きく動いた時だけ、新しい場所として警察へ知らせ直す
        Vector3 offset = state.point != null ? position - state.point.position : Vector3.zero;
        offset.y = 0.0f;
        if (state.point != null && offset.sqrMagnitude <= _repointDistance * _repointDistance) return;

        state.SetPoint(new CS_PoliceGatherPoint(position, player));
    }

    /// <summary>
    /// 警戒状態が解除されて、少し(_episodeGrace秒)たった(またはプレイヤーがいなくなった)プレイヤーへの指示をやめるメソッド
    /// 向かっている警察への指示は取り消さない(探し終えたら、自然に巡回へ戻る)
    /// </summary>
    private void EndFinishedStates()
    {
        _endBuffer.Clear();
        foreach (DispatchState state in _states.Values)
        {
            if (state.alert == null || (!state.alert.isAlerted && Time.time - state.lastActiveTime > _episodeGrace)) _endBuffer.Add(state.alert);
        }

        foreach (CS_PlayerAlert alert in _endBuffer) _states.Remove(alert);
    }

    /// <summary>
    /// 見つかった場所の付近にいる警察のうち、まだ指示していない警察を、その場所へ向かわせるメソッド
    /// </summary>
    private void Dispatch(DispatchState state)
    {
        if (state.point == null || _alertData == null) return;

        float sqrRadius = _alertData.alertRadius * _alertData.alertRadius;
        foreach (CS_PoliceBrain brain in CS_PoliceBrain.activeBrains)
        {
            if (brain == null || !brain.isActing || state.dispatched.Contains(brain)) continue;

            // 距離は水平方向だけで測る(高さの違う場所にいる警察も、付近にいるとみなす)
            Vector3 offset = brain.transform.position - state.point.position;
            offset.y = 0.0f;
            if (offset.sqrMagnitude > sqrRadius) continue;

            brain.RequestRush(state.point);
            state.dispatched.Add(brain);
        }
    }

    /// <summary>
    /// 警察・巡回ドローンの実際の状態から、プレイヤーごとの警戒状態を決めて、CS_PlayerAlertへ渡すメソッド(毎フレーム。サーバー、またはオフラインだけ)
    /// </summary>
    private void UpdateAlertStates()
    {
        if (CS_PoliceSquad.IsNetworkClientOnly()) return;

        CollectAlertPlayers();
        RemoveExpiredTouches();

        foreach (CS_Player player in CS_Player.activePlayers)
        {
            if (player == null || !player.TryGetComponent(out CS_PlayerHealth health) || !player.TryGetComponent(out CS_PlayerAlert alert)) continue;

            CSE_PlayerAlertState state = DecideAlertState(health);
            alert.SetPoliceState(state);
            if (state != CSE_PlayerAlertState.None && _states.TryGetValue(alert, out DispatchState dispatch)) dispatch.MarkActive();
        }
    }

    /// <summary>
    /// プレイヤーの警戒状態を決めるメソッド(死亡中は「なし」。追跡されていれば「発見中」、探されていれば「捜索中」)
    /// </summary>
    private CSE_PlayerAlertState DecideAlertState(CS_PlayerHealth player)
    {
        if (player.isDead) return CSE_PlayerAlertState.None;
        if (_foundNow.Contains(player)) return CSE_PlayerAlertState.Detected;
        if (_searchNow.Contains(player)) return CSE_PlayerAlertState.Searching;
        return CSE_PlayerAlertState.None;
    }

    /// <summary>
    /// 今、プレイヤーを追跡している・探している警察と巡回ドローン(と、防犯装置に触れたプレイヤー)を集めるメソッド
    /// </summary>
    private void CollectAlertPlayers()
    {
        _foundNow.Clear();
        _searchNow.Clear();

        foreach (CS_PoliceBrain brain in CS_PoliceBrain.activeBrains)
        {
            if (brain == null || !brain.isActing) continue;

            // 追跡は、増援も数える(実際に見つけている時だけ)。探している・駆け付けているのは、増援を数えない(警戒状態を支えないため)
            CS_PlayerHealth chased = brain.chasedPlayer;
            if (chased != null) _foundNow.Add(chased);
            if (brain.isReinforcement) continue;

            CS_PlayerHealth searched = brain.searchedPlayer;
            if (searched != null) _searchNow.Add(searched);
        }

        foreach (CS_SecurityDrone drone in CS_SecurityDrone.activeDrones)
        {
            if (drone == null || !drone.TryGetAlertSubject(out CS_PlayerHealth subject, out bool isFound)) continue;

            if (isFound) _foundNow.Add(subject);
            else _searchNow.Add(subject);
        }

        foreach (KeyValuePair<CS_PlayerHealth, float> touched in _touchedUntil)
        {
            if (touched.Key != null) _foundNow.Add(touched.Key);
        }
    }

    /// <summary>
    /// 防犯装置に触れて「発見中」にしておく時間が終わったプレイヤーを、外すメソッド
    /// </summary>
    private void RemoveExpiredTouches()
    {
        _touchBuffer.Clear();
        foreach (KeyValuePair<CS_PlayerHealth, float> touched in _touchedUntil)
        {
            if (touched.Key == null || Time.time >= touched.Value) _touchBuffer.Add(touched.Key);
        }

        foreach (CS_PlayerHealth player in _touchBuffer) _touchedUntil.Remove(player);
    }

    /// <summary>
    /// 見られている時間を数え、決められた秒数がたつごとに、プレイヤーを減点するメソッド(サーバー、またはオフラインだけ)
    /// 見られなくなったプレイヤーの、数えた時間は捨てる(次に見られたら、0から数え直す)
    /// </summary>
    /// <param name="deltaTime">前のフレームからの経過時間</param>
    private void UpdateExposurePenalty(float deltaTime)
    {
        if (_alertData == null || CS_PoliceSquad.IsNetworkClientOnly()) return;

        RemoveUnexposed();
        if (_alertData.exposurePenaltyPoint <= 0) return;

        foreach (CS_PlayerHealth player in _lastExposedTimes.Keys)
        {
            _exposedTimes.TryGetValue(player, out float time);
            time += deltaTime;
            while (time >= _alertData.exposurePenaltyInterval)
            {
                time -= _alertData.exposurePenaltyInterval;
                ApplyPenalty(player, _alertData.exposurePenaltyPoint);
            }
            _exposedTimes[player] = time;
        }
    }

    /// <summary>
    /// 見られなくなった(知らされてから一定時間たった)プレイヤーを、数えている中から外すメソッド
    /// </summary>
    private void RemoveUnexposed()
    {
        _exposureBuffer.Clear();
        foreach (KeyValuePair<CS_PlayerHealth, float> pair in _lastExposedTimes)
        {
            if (pair.Key == null || pair.Key.isDead || Time.time - pair.Value > _exposureGrace) _exposureBuffer.Add(pair.Key);
        }

        foreach (CS_PlayerHealth player in _exposureBuffer)
        {
            _lastExposedTimes.Remove(player);
            _exposedTimes.Remove(player);
        }
    }

    /// <summary>
    /// プレイヤーを減点するメソッド(スコアの結果の「警察に見つかった」に加える)
    /// </summary>
    private void ApplyPenalty(CS_PlayerHealth player, int point)
    {
        if (point <= 0) return;

        if (player.TryGetComponent(out CS_PlayerResultDataHolder holder)) holder.AddFoundByPolice(-point);
    }

    /// <summary>
    /// 増援を出してよいか(前に出してから、決められた間隔がたっているか)を判定するメソッド
    /// </summary>
    private bool CanSpawnReinforcements(CS_PlayerHealth player)
    {
        return !_lastReinforceTimes.TryGetValue(player, out float last) || Time.time - last >= _alertData.reinforceCooldown;
    }

    /// <summary>
    /// 警戒状態になったプレイヤーから決められた距離の外縁に、増援の警察を出すメソッド
    /// プレイヤーから見て、見つけた警察の反対側(挟む形)に出す。複数いる時は、反対側の向きを中心に、決められた角度の間隔で左右に並べる
    /// 増援は、警戒状態の間、そのプレイヤーの現在地を追う
    /// </summary>
    /// <param name="alert">プレイヤーの警戒状態</param>
    /// <param name="player">警戒状態になったプレイヤー</param>
    /// <param name="finderPosition">見つけた警察(防犯装置)の位置(分からなければnull。その時は向きをランダムにする)</param>
    private void SpawnReinforcements(CS_PlayerAlert alert, CS_PlayerHealth player, Vector3? finderPosition)
    {
        if (!_alertData.hasReinforcement) return;

        _lastReinforceTimes[player] = Time.time;
        Vector3 playerPosition = player.transform.position;
        Vector3 oppositeDirection = GetOppositeDirection(playerPosition, finderPosition);
        Dictionary<CSE_PoliceMoveState, float> speedTable =
            CS_PoliceSpawnUtility.CreateSpeedTable(_alertData.playerBaseStats, _alertData.reinforceStatus);

        int count = _alertData.reinforceCount;
        for (int i = 0; i < count; i++)
        {
            // 反対側の向きを中心に、左右へ並べる(1体なら、ちょうど反対側)
            float yawOffset = (i - (count - 1) * 0.5f) * _alertData.reinforceSpreadAngle;
            if (!CS_PoliceSpawnUtility.TryFindSpawnPositionToward(
                playerPosition, oppositeDirection, yawOffset, _reinforceWidenAngle,
                _alertData.reinforceDistance, _alertData.reinforceDistance, out Vector3 position)) continue;

            // 出現した時に、プレイヤーの方を向いている
            Vector3 toPlayer = playerPosition - position;
            toPlayer.y = 0.0f;
            Quaternion rotation = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

            CS_PoliceBrain police = CS_PoliceSpawnUtility.Spawn(_alertData.reinforcePolice, position, rotation);
            police.Setting(new CS_PoliceInformantGroup(player, alert), speedTable, _alertData.reinforceStatus);
        }
    }

    /// <summary>
    /// プレイヤーから見て、見つけた警察の反対側の向き(水平方向)を求めるメソッド
    /// 見つけた警察の位置が分からない・プレイヤーとほぼ同じ位置の時は、向きを決められないので、ゼロを返す(出現位置の向きはランダムになる)
    /// </summary>
    private static Vector3 GetOppositeDirection(Vector3 playerPosition, Vector3? finderPosition)
    {
        if (!finderPosition.HasValue) return Vector3.zero;

        Vector3 away = playerPosition - finderPosition.Value;
        away.y = 0.0f;
        return away.sqrMagnitude < _minFinderDistance * _minFinderDistance ? Vector3.zero : away.normalized;
    }
}
