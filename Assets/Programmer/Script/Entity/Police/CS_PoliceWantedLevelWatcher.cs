/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-14 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;

/// <summary>
/// プレイヤーの手配度(CS_PlayerWantedLevel)の変化を購読し、警察の増援の管理クラスへ知らせるクラス
/// ・すでに出現しているプレイヤーと、あとから出現(リスポーン以外の出現・後から接続)するプレイヤーの両方を購読する
/// ・購読した時点の手配度もすぐに知らせる(購読より前に手配度が上がっていた場合のため)
/// プレイヤー側のスクリプトは変更せず、プレイヤー側が用意しているイベントを警察側から購読する
/// </summary>
public class CS_PoliceWantedLevelWatcher
{
    // 購読中のプレイヤーと、手配度の変化を受け取る処理(購読を解除する時に使う)
    private readonly Dictionary<CS_PlayerWantedLevel, Action<int>> _subscriptions = new Dictionary<CS_PlayerWantedLevel, Action<int>>();

    // 購読を解除する際に、プレイヤーを集めて使い回すリスト
    private readonly List<CS_PlayerWantedLevel> _unsubscribeBuffer = new List<CS_PlayerWantedLevel>();

    // 手配度が変わった時に呼ぶ処理(プレイヤー, 手配度)
    private readonly Action<CS_PlayerHealth, int> _onLevelChanged = null;

    // 購読を始めているか
    private bool _isStarted = false;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="onLevelChanged">手配度が変わった時に呼ぶ処理(プレイヤー, 手配度)</param>
    public CS_PoliceWantedLevelWatcher(Action<CS_PlayerHealth, int> onLevelChanged)
    {
        _onLevelChanged = onLevelChanged;
    }

    /// <summary>
    /// 購読を始めるメソッド(すでに出現しているプレイヤーも購読する)
    /// </summary>
    public void Start()
    {
        if (_isStarted) return;
        _isStarted = true;

        // 出現・消滅のイベントを先に購読してから一覧を調べる(間に出現したプレイヤーを取りこぼさないため)
        CS_PlayerWantedLevel.onAnySpawned += Subscribe;
        CS_PlayerWantedLevel.onAnyDespawned += Unsubscribe;

        foreach (CS_PlayerWantedLevel player in CS_PlayerWantedLevel.activePlayers)
        {
            Subscribe(player);
        }
    }

    /// <summary>
    /// 購読をすべて解除するメソッド(管理クラスが消える時に呼ぶ)
    /// 管理クラスより長く残るプレイヤーが、破棄された管理クラスを呼ばないようにするために必要
    /// </summary>
    public void Stop()
    {
        if (!_isStarted) return;
        _isStarted = false;

        CS_PlayerWantedLevel.onAnySpawned -= Subscribe;
        CS_PlayerWantedLevel.onAnyDespawned -= Unsubscribe;

        _unsubscribeBuffer.AddRange(_subscriptions.Keys);
        foreach (CS_PlayerWantedLevel player in _unsubscribeBuffer)
        {
            Unsubscribe(player);
        }
        _unsubscribeBuffer.Clear();

        // 破棄済みのプレイヤー(Unityのnull)は上で外せないので、辞書からも消しておく
        _subscriptions.Clear();
    }

    /// <summary>
    /// プレイヤーの手配度の変化を購読し、今の手配度を知らせるメソッド
    /// </summary>
    /// <param name="player">購読するプレイヤー</param>
    private void Subscribe(CS_PlayerWantedLevel player)
    {
        if (player == null || _subscriptions.ContainsKey(player)) return;

        // CS_PlayerWantedLevelはCS_PlayerHealthと同じオブジェクトに付いている(RequireComponent)
        CS_PlayerHealth health = player.GetComponent<CS_PlayerHealth>();
        if (health == null) return;

        Action<int> handler = level => _onLevelChanged(health, level);
        player.onLevelChanged += handler;
        _subscriptions.Add(player, handler);

        _onLevelChanged(health, player.level);
    }

    /// <summary>
    /// プレイヤーの手配度の変化の購読を解除するメソッド
    /// </summary>
    /// <param name="player">解除するプレイヤー</param>
    private void Unsubscribe(CS_PlayerWantedLevel player)
    {
        if (player == null || !_subscriptions.TryGetValue(player, out Action<int> handler)) return;

        player.onLevelChanged -= handler;
        _subscriptions.Remove(player);
    }
}
