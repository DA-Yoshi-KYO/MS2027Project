/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * 2026-10-23 | 警戒状態の本体をCS_PlayerAlertに揃え、付近の警察を向かわせる役だけにした
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 警戒状態のプレイヤーのところへ、付近の警察を向かわせるクラス(シーンに1つ置く)
/// ・警戒状態の本体(なし / 発見中 / 捜索中、解除までの時間)は、プレイヤー側のCS_PlayerAlertが持つ。このクラスは状態を持たない
/// ・警察陣営(警察・防犯装置)がプレイヤーを見つけたら、Reportで知らせる(見えている間は呼び続ける)
///   ReportはCS_PlayerAlert.NotifyDetected()を呼んで警戒状態にし、見つかった場所を覚える
/// ・プレイヤーが警戒状態(CS_PlayerAlertが「なし」以外)の間は、見つかった場所の付近(設定の範囲内)にいる警察が、その場所へ向かう
///   見つかっている間は追跡し、見失ったら周囲を探す(この動きはCS_PoliceBrainが行う)
///   見つかった場所が変わったら、付近の警察へ新しい場所を知らせる
/// ・警戒状態が解除されたら(CS_PlayerAlertが「なし」に戻ったら)、そのプレイヤーへの指示をやめる
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

        public CS_PlayerAlert alert => _alert;
        public CS_PoliceGatherPoint point => _point;
        public HashSet<CS_PoliceBrain> dispatched => _dispatched;

        public DispatchState(CS_PlayerAlert alert)
        {
            _alert = alert;
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

    [Header("＝＝＝ 警戒状態 ＝＝＝")]
    [SerializeField]
    [Tooltip("警戒状態の設定(付近の範囲)。警戒状態が続く時間は、プレイヤー側のCS_PlayerAlertで設定する")]
    private CSO_PoliceAlertData _alertData = null;

    /// <summary>
    /// プレイヤーを見つけたことを知らせるメソッド(サーバー、またはオフラインで呼ぶこと。見えている間は呼び続ける)
    /// プレイヤーを警戒状態(CS_PlayerAlert)にし、付近の警察を見つかった場所へ向かわせる
    /// </summary>
    /// <param name="player">見つけたプレイヤー</param>
    /// <param name="position">プレイヤーが見つかった場所</param>
    public static void Report(CS_PlayerHealth player, Vector3 position)
    {
        if (player == null || CS_PoliceSquad.IsNetworkClientOnly()) return;

        if (!player.TryGetComponent(out CS_PlayerAlert alert))
        {
            WarnNoPlayerAlert(player);
            return;
        }

        alert.NotifyDetected();

        // 管理クラスが無いシーンでも、警戒状態にはなる(警察を向かわせないだけ)
        if (TryGetInstance(out CS_PoliceAlertManager manager)) manager.OnFound(alert, player, position);
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
    }

    private void Update()
    {
        if (_instance != this || _states.Count == 0) return;

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
    /// プレイヤーが見つかった時の処理(警戒状態なら、見つかった場所を更新して、警察へ知らせる準備をする)
    /// </summary>
    private void OnFound(CS_PlayerAlert alert, CS_PlayerHealth player, Vector3 position)
    {
        // 死亡中などで警戒状態にならなかった時は、警察を向かわせない
        if (_alertData == null || !alert.isAlerted) return;

        if (!_states.TryGetValue(alert, out DispatchState state))
        {
            state = new DispatchState(alert);
            _states.Add(alert, state);
        }

        // 見つかった場所が大きく動いた時だけ、新しい場所として警察へ知らせ直す
        Vector3 offset = state.point != null ? position - state.point.position : Vector3.zero;
        offset.y = 0.0f;
        if (state.point != null && offset.sqrMagnitude <= _repointDistance * _repointDistance) return;

        state.SetPoint(new CS_PoliceGatherPoint(position, player));
    }

    /// <summary>
    /// 警戒状態が解除された(またはプレイヤーがいなくなった)プレイヤーへの指示をやめるメソッド
    /// 向かっている警察への指示は取り消さない(探し終えたら、自然に巡回へ戻る)
    /// </summary>
    private void EndFinishedStates()
    {
        _endBuffer.Clear();
        foreach (DispatchState state in _states.Values)
        {
            if (state.alert == null || !state.alert.isAlerted) _endBuffer.Add(state.alert);
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
}
