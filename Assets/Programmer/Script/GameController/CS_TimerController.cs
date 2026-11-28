/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー時刻を基準に残り時間を管理するController。
/// ・Inspectorから通常の残り時間通知を追加できる
/// ・最後の数秒は1つの通知を5、4、3、2、1と更新できる
/// ・オフラインでは遷移直前にリザルトを保存する
/// ・オンラインではRPCで各端末へ保存を指示してから全員で遷移する
/// </summary>
public class CS_TimerController : NetworkBehaviour
{
    // =========================================================
    // Inspector用データ
    // =========================================================

    [Serializable]
    private class TimeNotificationSetting
    {
        [Min(0f)]
        [SerializeField] private float _remainingSeconds = 60f;

        [SerializeField] private string _message = "残り1分！";

        // 追加: この通知で使う帯画像
        [Tooltip("この通知で使う帯の画像。空なら従来の時間通知の画像")]
        [SerializeField] private Sprite _backSprite;

        [NonSerialized] private bool _hasNotified;

        public float remainingSeconds => _remainingSeconds;
        public string message => _message;
        public Sprite backSprite => _backSprite; // 追加
        public bool hasNotified => _hasNotified;

        public void MarkAsNotified()
        {
            _hasNotified = true;
        }

        public void ResetNotification()
        {
            _hasNotified = false;
        }
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("タイマーの最大時間（秒）")]
    [Min(1f)]
    [SerializeField] private float _maxTime = 300f;

    [Header("通知Controller")]
    [SerializeField] private CS_NotificationController _notificationController;

    [Header("通常の残り時間通知（＋で追加可能）")]
    [SerializeField] private List<TimeNotificationSetting> _timeNotifications = new();

    [Header("終了カウントダウン")]
    [SerializeField] private bool _enableCountdown = true;

    [Min(1)]
    [SerializeField] private int _countdownStartSeconds = 5;

    [Header("SE")]
    [SerializeField] private bool _playGameEndSe = false;   // ゲーム終了時に終了のSE(SystemGameEnd)を鳴らすか(音を作り直すまで保留のため既定オフ)

    // =========================================================
    // NetworkVariable
    // =========================================================

    private readonly NetworkVariable<double> _endTime =
        new NetworkVariable<double>(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UITimerModel _timerModel;
    private CS_SceneTransitioner _sceneTransitioner;

    private bool _isFinished;
    private CS_CrimeArea _soloArea;   // ソロで、残り時間を表示している今のエリア(エリアが変わったか調べる用)
    private bool _hasSavedResults;
    private double _offlineEndTime;
    private float _previousRemainTime;
    private int _previousCountdownSecond = -1;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _timerModel = new CS_UITimerModel(_maxTime);
        _timerModel.SetTime(_maxTime);

        _offlineEndTime = Time.timeAsDouble + _maxTime;
        _previousRemainTime = _maxTime;

        _sceneTransitioner = GetComponent<CS_SceneTransitioner>();

        if (_sceneTransitioner == null)
        {
            Debug.LogError(
                "[CS_TimerController] " +
                "同じGameObjectにCS_SceneTransitionerがありません。"
            );
        }

        ResetNotifications();
    }

    private void Start()
    {
        if (_notificationController == null)
        {
#if UNITY_2023_1_OR_NEWER
            _notificationController =
                FindFirstObjectByType<CS_NotificationController>();
#else
            _notificationController =
                FindObjectOfType<CS_NotificationController>();
#endif
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
            _endTime.Value = NetworkManager.ServerTime.Time + _maxTime;

        // RPC保存が基本だが、既存のSceneEvent保存も予備として残す。
        if (NetworkManager != null &&
            NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnSceneEvent += HandleSceneEvent;
        }

        _previousRemainTime = _maxTime;
        _hasSavedResults = false;
        ResetNotifications();
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeSceneEvent();
        base.OnNetworkDespawn();
    }

    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        if (_isFinished)
            return;

        // ソロは、全体の残り時間ではなく、今のエリアの残り時間を表示・通知する(終了はCS_SoloGameJudgeが決める)
        bool isSoloArea = TryGetSoloAreaRemainTime(out float soloRemainTime);
        float remainTime = isSoloArea ? soloRemainTime : GetRemainTimeInternal();

        _timerModel.SetTime(remainTime);
        UpdateTimeNotifications(remainTime);
        UpdateCountdownNotification(remainTime);

        _previousRemainTime = remainTime;

        if (!isSoloArea && ShouldCheckFinish() && remainTime <= 0f)
        {
            _isFinished = true;
            _notificationController?.HideCountdown();
            if (_playGameEndSe) CS_SoundManager.instance?.PlaySe(CSE_SeId.SystemGameEnd);
            onTimeUp?.Invoke();   // 結果の保存・遷移より前に知らせる(ソロの判定がリザルトの内容を決めるため)

            if (IsOnline())
            {
                // オンラインではサーバーが全端末へ保存を指示し、
                // RPCを処理する猶予を作ってからネットワークシーン遷移する。
                if (IsServer)
                    StartCoroutine(SaveAndTransitionOnline());
            }
            else
            {
                // オフラインではSceneEventもRPCも発生しないため、
                // 通常のシーン遷移より先に直接保存する。
                SaveResultsLocal();

                if (_sceneTransitioner != null)
                    _sceneTransitioner.StartTransition();
            }
        }
    }

    // =========================================================
    // ソロ：エリアごとの残り時間
    // =========================================================

    /// <summary>
    /// ソロ(エリア制)なら、今のエリアの残り時間を返す
    /// エリアが変わった時は、表示の最大時間を新しいエリアの制限時間にし、通知とカウントダウンを最初からにする
    /// </summary>
    /// <returns>ソロのエリア制で動いているならtrue(マルチはfalse)</returns>
    private bool TryGetSoloAreaRemainTime(out float remainTime)
    {
        remainTime = 0f;
        CS_SoloAreaProgression progression = CS_SoloAreaProgression.instance;
        if (progression == null || IsOnline())
            return false;

        CS_CrimeArea area = progression.currentArea;
        if (area != _soloArea)
        {
            // 新しいエリアに入った時
            _soloArea = area;
            _timerModel.SetMaxTime(progression.areaTimeLimit);
            ResetNotifications();
            _notificationController?.HideCountdown();
            _previousRemainTime = progression.areaRemainingTime;
        }

        remainTime = progression.areaRemainingTime;
        return true;
    }

    // =========================================================
    // 時間切れ以外の終了（ソロ用）
    // =========================================================

    /// <summary>時間切れで終了する時に呼ばれる（結果の保存・シーン遷移より前）</summary>
    public event Action onTimeUp;

    /// <summary>
    /// 時間切れを待たずに今すぐ終了する（ソロ用。オフライン専用）
    /// 結果を保存してリザルトへ遷移する
    /// </summary>
    public void FinishNow()
    {
        if (_isFinished || IsOnline())
            return;

        _isFinished = true;
        if (_playGameEndSe) CS_SoundManager.instance?.PlaySe(CSE_SeId.SystemGameEnd);
        _notificationController?.HideCountdown();
        SaveResultsLocal();

        if (_sceneTransitioner != null)
            _sceneTransitioner.StartTransition();
    }

    // =========================================================
    // オンライン保存・遷移
    // =========================================================

    private IEnumerator SaveAndTransitionOnline()
    {
        // ホストを含む全クライアントで、それぞれのstatic Storeへ保存する。
        SaveResultsRpc();

        // RPCを送信・実行するための猶予を1フレーム作る。
        yield return null;

        if (_sceneTransitioner != null)
            _sceneTransitioner.StartTransition();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SaveResultsRpc()
    {
        SaveResultsLocal();
    }

    private void SaveResultsLocal()
    {
        if (_hasSavedResults)
            return;

        _hasSavedResults = true;

        var results = CS_PlayerResultDataHolder.CollectResults();
        CS_ResultDataStore.Save(results);

        Debug.Log(
            "[CS_TimerController] リザルトデータを保存しました。"
        );
    }

    // =========================================================
    // 通常の残り時間通知
    // =========================================================

    private void UpdateTimeNotifications(float remainTime)
    {
        if (_notificationController == null)
            return;

        foreach (TimeNotificationSetting setting in _timeNotifications)
        {
            if (setting == null || setting.hasNotified)
                continue;

            bool crossedThreshold =
                _previousRemainTime > setting.remainingSeconds &&
                remainTime <= setting.remainingSeconds;

            if (!crossedThreshold)
                continue;

            setting.MarkAsNotified();

            // 変更: 帯画像も一緒に渡す
            _notificationController.ShowTimeAlert(setting.message, setting.backSprite);
        }
    }

    // =========================================================
    // 終了カウントダウン通知
    // =========================================================

    private void UpdateCountdownNotification(float remainTime)
    {
        if (!_enableCountdown || _notificationController == null)
            return;

        int countdownSecond = Mathf.CeilToInt(remainTime);

        if (countdownSecond <= 0)
        {
            if (_previousCountdownSecond != 0)
                _notificationController.HideCountdown();

            _previousCountdownSecond = 0;
            return;
        }

        if (countdownSecond > _countdownStartSeconds)
            return;

        if (countdownSecond == _previousCountdownSecond)
            return;

        _previousCountdownSecond = countdownSecond;

        _notificationController.ShowCountdown(
            countdownSecond.ToString()
        );
    }

    private void ResetNotifications()
    {
        foreach (TimeNotificationSetting setting in _timeNotifications)
            setting?.ResetNotification();

        _previousCountdownSecond = -1;
    }

    // =========================================================
    // 外部API
    // =========================================================

    public float GetRemainTime()
    {
        return Mathf.Max(0f, GetRemainTimeInternal());
    }

    public float GetElapsedTime()
    {
        return Mathf.Clamp(
            _maxTime - GetRemainTimeInternal(),
            0f,
            _maxTime
        );
    }

    // =========================================================
    // 時刻計算
    // =========================================================

    private float GetRemainTimeInternal()
    {
        if (IsOnline())
        {
            if (_endTime.Value <= 0d)
                return _maxTime;

            return Mathf.Max(
                0f,
                (float)(_endTime.Value - NetworkManager.ServerTime.Time)
            );
        }

        return Mathf.Max(
            0f,
            (float)(_offlineEndTime - Time.timeAsDouble)
        );
    }

    private bool IsOnline()
    {
        return NetworkManager != null &&
               NetworkManager.IsListening;
    }

    private bool ShouldCheckFinish()
    {
        return !IsOnline() || IsServer;
    }

    // =========================================================
    // 既存のSceneEvent保存（予備）
    // =========================================================

    /// <summary>
    /// RPC保存が基本。
    /// SceneEventは既存処理を残した予備経路で、二重保存はフラグで防ぐ。
    /// </summary>
    private void HandleSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType != SceneEventType.Load)
            return;

        SaveResultsLocal();
    }

    private void UnsubscribeSceneEvent()
    {
        if (NetworkManager != null &&
            NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnSceneEvent -= HandleSceneEvent;
        }
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    public override void OnDestroy()
    {
        UnsubscribeSceneEvent();
        _timerModel?.Dispose();
        base.OnDestroy();
    }
}