/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// タイマーを管理するコントローラー
/// ・サーバーが終了時刻を NetworkVariable で同期する
/// ・各クライアントは ServerTime から残り時間を計算する（自分で減らさない）
/// ・オフライン（NetworkManager が動いていないテストシーン）でも単体で動く
/// ・経過時間・残り時間を外部から取得できる API を持つ
/// ・時間切れの判定はサーバーだけが行う
/// </summary>
public class CS_TimerController : NetworkBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("タイマーの最大時間（秒）")]
    [SerializeField] private float _maxTime = 300f; // 5分（調整可能）

    // =========================================================
    // NetworkVariable（サーバーが書き込み・全員に同期）
    // =========================================================

    // ゲームの終了時刻（ServerTime.Time ベース）
    // サーバーが OnNetworkSpawn で設定する
    private readonly NetworkVariable<double> _endTime = new NetworkVariable<double>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // =========================================================
    // 内部フィールド
    // =========================================================

    // タイマーのModel（UIはPresenterが自動で拾う）
    private CS_UITimerModel _timerModel;

    // シーン遷移担当（同じオブジェクトに付ける前提）
    private CS_SceneTransitioner _sceneTransitioner;

    // 終了フラグ（毎フレームシーン移動しないようにする）
    private bool _isFinished;

    // オフライン時の残り時間（NetworkManager が動いていない場合に使う）
    private float _offlineRemainTime;

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        // タイマーModel生成
        _timerModel = new CS_UITimerModel(_maxTime);
        _timerModel.SetTime(_maxTime);

        // オフライン用の残り時間を初期化
        _offlineRemainTime = _maxTime;

        // 同じオブジェクトに付いている SceneTransitioner を取得
        _sceneTransitioner = GetComponent<CS_SceneTransitioner>();

        if (_sceneTransitioner == null)
        {
            Debug.LogError("[CS_TimerController] 同じオブジェクトに CS_SceneTransitioner が付いていません！");
        }
    }

    /// <summary>
    /// NetworkBehaviour の OnNetworkSpawn
    /// サーバーだけ終了時刻を設定する
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // サーバーが終了時刻を設定（全員に同期される）
            _endTime.Value = GetCurrentTime() + _maxTime;
            Debug.Log($"[CS_TimerController] 終了時刻をセット : {_endTime.Value}");
        }

        // 各クライアントがシーン移動イベントで保存する
        NetworkManager.SceneManager.OnSceneEvent += HandleSceneEvent;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // A案：購読解除
        NetworkManager.SceneManager.OnSceneEvent -= HandleSceneEvent;
    }

    // =========================================================
    // シーン移動イベント
    // =========================================================

    private void HandleSceneEvent(SceneEvent sceneEvent)
    {
        // シーン移動開始前（プレイヤーが消える前）に保存する
        if (sceneEvent.SceneEventType == SceneEventType.Load)
        {
            CS_ResultDataStore.Save(CS_PlayerResultDataHolder.CollectResults());
        }
    }

    // =========================================================
    // Update
    // =========================================================

    void Update()
    {
        // 終了済みなら何もしない
        if (_isFinished) return;

        // 残り時間を計算
        float remain = CalcRemainTime();

        // Model に反映（UI に届く）
        _timerModel.SetTime(remain);

        // 時間切れ判定（サーバーだけ or オフライン時）
        if (ShouldCheckFinish() && remain <= 0f)
        {
            _isFinished = true;

            // オフライン時はここで保存してシーン移動
            if (!IsOnline())
            {
                // オフライン：その場で保存してシーン移動
                CS_ResultDataStore.Save(CS_PlayerResultDataHolder.CollectResults());
                _sceneTransitioner.StartTransition();
            }
            else
            {
                // オンライン（サーバー）：全員をまとめてシーン移動
                // 各クライアントは OnSceneEvent で保存するので
                // ここでは Save() を呼ばない
                _sceneTransitioner.StartTransition();
            }

            _sceneTransitioner.StartTransition();
        }
    }

    // =========================================================
    // 外部 API（経過時間・残り時間を他のシステムから取得できる）
    // =========================================================

    /// <summary>残り時間を取得する（ランダムイベント・悪人の時間経過で使う）</summary>
    public float GetRemainTime() => Mathf.Max(0f, CalcRemainTime());

    /// <summary>経過時間を取得する</summary>
    public float GetElapsedTime() => Mathf.Max(0f, _maxTime - CalcRemainTime());

    // =========================================================
    // 内部処理
    // =========================================================

    /// <summary>
    /// 残り時間を計算する
    /// ・オンライン : ServerTime から計算（自分で減らさない）
    /// ・オフライン : Time.deltaTime で減らす
    /// </summary>
    private float CalcRemainTime()
    {
        if (IsOnline())
        {
            // オンライン：ServerTime から残り時間を計算
            return (float)(_endTime.Value - GetCurrentTime());
        }
        else
        {
            // オフライン：Time.deltaTime で減らす
            _offlineRemainTime -= Time.deltaTime;
            return _offlineRemainTime;
        }
    }

    /// <summary>
    /// 時間切れ判定を行うべきか
    /// ・オンライン : サーバーだけ判定する
    /// ・オフライン : 常に判定する
    /// </summary>
    private bool ShouldCheckFinish()
    {
        return !IsOnline() || IsServer;
    }

    /// <summary>
    /// 現在時刻を取得する
    /// ・オンライン : ServerTime（全員で揃う）
    /// ・オフライン : Time.timeAsDouble
    /// CS_PlayerTransformation の GetCurrentTime() と同じ方式
    /// </summary>
    private double GetCurrentTime()
    {
        return IsOnline() ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    }

    /// <summary>
    /// NetworkManager がオンラインで動いているか
    /// </summary>
    private bool IsOnline()
    {
        return NetworkManager != null && NetworkManager.IsListening;
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    public override void OnDestroy()
    {
        base.OnDestroy();
        _timerModel?.Dispose();
    }
}
