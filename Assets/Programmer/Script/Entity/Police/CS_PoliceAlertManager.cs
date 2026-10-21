/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーごとの警戒状態を管理するクラス(シーンに1つ置く)
/// ・警察陣営(警察・防犯装置)がプレイヤーを見つけたら、Reportで知らせる。巡回状態から、そのプレイヤーの警戒状態になる
///   警戒状態はプレイヤーごとに持ち、他のプレイヤーとは共有しない
/// ・警戒状態の間は、プレイヤーが見つかった場所の付近(設定の範囲内)にいる警察が、その場所へ向かう
///   見つかっている間は追跡し、見失ったら周囲を探す(この動きはCS_PoliceBrainが行う)
///   見つかった場所が変わったら、付近の警察へ新しい場所を知らせる
/// ・次のどちらかで警戒状態は解除される
///   1. プレイヤーが倒されてリスポーンする
///   2. 警戒状態になってから、設定の時間の間、新たに警察陣営に見つからない(見つかるたびに数え直す)
/// 状態の管理と警察への指示はサーバー(またはオフライン)だけで行う
/// </summary>
public class CS_PoliceAlertManager : MonoBehaviour
{
    // シーンに置かれている管理クラス(静的メソッドから使う)
    private static CS_PoliceAlertManager _instance = null;

    // シーンに管理クラスが無いことを警告済みか(見つかるたびに警告しないため)
    private static bool _hasWarnedNoInstance = false;

    // 付近の警察へ指示を出し直す間隔(秒)
    private const float _dispatchInterval = 0.5f;

    // 見つかった場所がこの距離より動いたら、警察へ新しい場所を知らせる(m)
    private const float _repointDistance = 4.0f;

    /// <summary>
    /// プレイヤー1人分の警戒状態
    /// </summary>
    private class AlertState
    {
        private readonly CS_PlayerHealth _player;
        private readonly Action _deathHandler;

        // 警戒状態が解除されるまでの残り時間
        private float _remaining = 0.0f;

        // 警察に知らせた、見つかった場所(同じ場所へ向かう警察は、この同じインスタンスを持つ)
        private CS_PoliceGatherPoint _point = null;

        // 見つかった場所へ向かうよう、すでに指示した警察(同じ場所には、1人に1回だけ指示する)
        private readonly HashSet<CS_PoliceBrain> _dispatched = new HashSet<CS_PoliceBrain>();

        public CS_PlayerHealth player => _player;
        public Action deathHandler => _deathHandler;
        public float remaining => _remaining;
        public CS_PoliceGatherPoint point => _point;
        public HashSet<CS_PoliceBrain> dispatched => _dispatched;

        public AlertState(CS_PlayerHealth player, Action deathHandler)
        {
            _player = player;
            _deathHandler = deathHandler;
        }

        /// <summary>
        /// 解除までの残り時間を数え直すメソッド
        /// </summary>
        public void ResetTimer(float duration)
        {
            _remaining = duration;
        }

        /// <summary>
        /// 解除までの残り時間を進めるメソッド
        /// </summary>
        public void Tick(float deltaTime)
        {
            _remaining -= deltaTime;
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

    // 警戒状態のプレイヤー
    private readonly Dictionary<CS_PlayerHealth, AlertState> _states = new Dictionary<CS_PlayerHealth, AlertState>();

    // 解除するプレイヤーを集める際に使い回すリスト
    private readonly List<CS_PlayerHealth> _endBuffer = new List<CS_PlayerHealth>();

    // 次に警察へ指示を出し直すまでの残り時間
    private float _dispatchTimer = 0.0f;

    [Header("＝＝＝ 警戒状態 ＝＝＝")]
    [SerializeField]
    [Tooltip("警戒状態の設定(付近の範囲・警戒状態が続く時間)")]
    private CSO_PoliceAlertData _alertData = null;

    /// <summary>
    /// プレイヤーを見つけたことを知らせるメソッド(サーバー、またはオフラインで呼ぶこと)
    /// 警戒状態でなければ警戒状態になり、すでに警戒状態なら、解除までの時間を数え直す
    /// </summary>
    /// <param name="player">見つけたプレイヤー</param>
    /// <param name="position">プレイヤーが見つかった場所</param>
    public static void Report(CS_PlayerHealth player, Vector3 position)
    {
        if (player == null || CS_PoliceSquad.IsNetworkClientOnly()) return;
        if (!TryGetInstance(out CS_PoliceAlertManager manager)) return;

        manager.OnFound(player, position);
    }

    /// <summary>
    /// プレイヤーが警戒状態かを判定するメソッド
    /// </summary>
    /// <param name="player">判定するプレイヤー</param>
    /// <returns>警戒状態ならtrue(サーバー、またはオフラインでだけ正しい)</returns>
    public static bool IsAlert(CS_PlayerHealth player)
    {
        return _instance != null && player != null && _instance._states.ContainsKey(player);
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

        // 警戒状態の解除と、プレイヤーの死亡の購読を外す
        foreach (AlertState state in _states.Values)
        {
            if (state.player != null) state.player.onDeath -= state.deathHandler;
        }
        _states.Clear();
    }

    private void Update()
    {
        if (_instance != this || _states.Count == 0) return;

        EndExpiredStates(Time.deltaTime);

        _dispatchTimer -= Time.deltaTime;
        if (_dispatchTimer > 0.0f) return;
        _dispatchTimer = _dispatchInterval;

        foreach (AlertState state in _states.Values)
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
            Debug.LogWarning("CS_PoliceAlertManager: シーンに置かれていないため、警戒状態は使えません");
        }
        return false;
    }

    /// <summary>
    /// プレイヤーが見つかった時の処理(警戒状態にする・時間を数え直す・見つかった場所を更新する)
    /// </summary>
    private void OnFound(CS_PlayerHealth player, Vector3 position)
    {
        if (_alertData == null) return;

        if (!_states.TryGetValue(player, out AlertState state))
        {
            state = StartAlert(player);
        }

        state.ResetTimer(_alertData.alertDuration);

        // 見つかった場所が大きく動いた時だけ、新しい場所として警察へ知らせ直す
        Vector3 offset = state.point != null ? position - state.point.position : Vector3.zero;
        offset.y = 0.0f;
        if (state.point != null && offset.sqrMagnitude <= _repointDistance * _repointDistance) return;

        state.SetPoint(new CS_PoliceGatherPoint(position, player));
    }

    /// <summary>
    /// プレイヤーを警戒状態にするメソッド(プレイヤーが倒されたら解除するよう、死亡を購読する)
    /// </summary>
    private AlertState StartAlert(CS_PlayerHealth player)
    {
        AlertState state = new AlertState(player, () => EndAlert(player));

        player.onDeath += state.deathHandler;
        _states.Add(player, state);
        return state;
    }

    /// <summary>
    /// プレイヤーの警戒状態を解除するメソッド
    /// 向かっている警察への指示は取り消さない(探し終えたら、自然に巡回へ戻る)
    /// </summary>
    private void EndAlert(CS_PlayerHealth player)
    {
        if (!_states.TryGetValue(player, out AlertState state)) return;

        // プレイヤーが破棄されている時は、購読を外す必要が無い
        if (player != null) player.onDeath -= state.deathHandler;
        _states.Remove(player);
    }

    /// <summary>
    /// 時間が経った・プレイヤーがいなくなった警戒状態を解除するメソッド
    /// </summary>
    private void EndExpiredStates(float deltaTime)
    {
        _endBuffer.Clear();
        foreach (AlertState state in _states.Values)
        {
            state.Tick(deltaTime);
            if (state.player == null || state.remaining <= 0.0f) _endBuffer.Add(state.player);
        }

        foreach (CS_PlayerHealth player in _endBuffer) EndAlert(player);
    }

    /// <summary>
    /// 見つかった場所の付近にいる警察のうち、まだ指示していない警察を、その場所へ向かわせるメソッド
    /// </summary>
    private void Dispatch(AlertState state)
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
