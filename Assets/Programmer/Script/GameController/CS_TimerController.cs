/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー時刻を基準に残り時間を管理するController。
/// ・Inspectorから通常の残り時間通知を追加できる
/// ・最後の数秒は1つの通知を5、4、3、2、1と更新できる
/// ・オフラインでは通常シーン遷移の直前にリザルトを保存する
/// </summary>
public class CS_TimerController : NetworkBehaviour
{
    [Serializable]
    private class TimeNotificationSetting
    {
        [Min(0f)]
        [SerializeField] private float _remainingSeconds = 60f;

        [SerializeField] private string _message = "残り1分！";

        [NonSerialized] private bool _hasNotified;

        public float remainingSeconds => _remainingSeconds;
        public string message => _message;
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

    private readonly NetworkVariable<double> _endTime =
        new NetworkVariable<double>(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private CS_UITimerModel _timerModel;
    private CS_SceneTransitioner _sceneTransitioner;

    private bool _isFinished;
    private double _offlineEndTime;
    private float _previousRemainTime;
    private int _previousCountdownSecond = -1;

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

        if (NetworkManager != null &&
            NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnSceneEvent += HandleSceneEvent;
        }

        _previousRemainTime = _maxTime;
        ResetNotifications();
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeSceneEvent();
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (_isFinished)
            return;

        float remainTime = GetRemainTimeInternal();

        _timerModel.SetTime(remainTime);
        UpdateTimeNotifications(remainTime);
        UpdateCountdownNotification(remainTime);

        _previousRemainTime = remainTime;

        if (ShouldCheckFinish() && remainTime <= 0f)
        {
            _isFinished = true;
            _notificationController?.HideCountdown();

            // オフラインではNetworkManager.SceneManager.OnSceneEventが
            // 発生しないため、通常のシーン遷移より先に保存する。
            if (!IsOnline())
            {
                CS_ResultDataStore.Save(
                    CS_PlayerResultDataHolder.CollectResults()
                );
            }

            if (_sceneTransitioner != null)
                _sceneTransitioner.StartTransition();
        }
    }

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
            _notificationController.ShowTimeAlert(setting.message);
        }
    }

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

    /// <summary>
    /// NetworkManager.SceneManagerによるオンラインシーン移動時に、
    /// 各クライアントが自分のResultDataStoreへ保存する。
    /// </summary>
    private void HandleSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType != SceneEventType.Load)
            return;

        CS_ResultDataStore.Save(
            CS_PlayerResultDataHolder.CollectResults()
        );
    }

    private void UnsubscribeSceneEvent()
    {
        if (NetworkManager != null &&
            NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnSceneEvent -= HandleSceneEvent;
        }
    }

    public override void OnDestroy()
    {
        UnsubscribeSceneEvent();
        _timerModel?.Dispose();
        base.OnDestroy();
    }
}
