/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-10 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 通知のController
/// ・イベント、残り時間、キル、カウントダウン通知を受け付ける
/// </summary>
public class CS_NotificationController : MonoBehaviour
{
    [Header("UICanvas配下のNotificationをセット")]
    [SerializeField] private CS_UINotificationRootView _rootView;

    [Header("デバッグキー入力")]
    [SerializeField] private bool _enableDebugInput = true;

    private CS_UINotificationModel _model;
    private CS_UINotificationPresenter _presenter;

    private void Awake()
    {
        _model = new CS_UINotificationModel();
    }

    private void Start()
    {
        if (_rootView == null)
        {
            Debug.LogError(
                "[CS_NotificationController] _rootViewが未設定です。"
            );
            return;
        }

        _presenter =
            _rootView.GetComponent<CS_UINotificationPresenter>();

        if (_presenter == null)
        {
            Debug.LogError(
                "[CS_NotificationController] " +
                "CS_UINotificationPresenterが付いていません。"
            );
            return;
        }

        _presenter.BindModel(_model);
    }

    private void Update()
    {
        if (!_enableDebugInput)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
            ShowEvent("イベントが発生しました！");

        if (Input.GetKeyDown(KeyCode.Alpha2))
            ShowTimeAlert("残り1分！");

        if (Input.GetKeyDown(KeyCode.Alpha3))
            ShowKill("Player2");

        if (Input.GetKeyDown(KeyCode.Alpha4))
            ShowKill("Player3");
    }

    public void ShowEvent(string message)
    {
        CS_SoundManager.instance?.PlaySe(CSE_SeId.EventAnnouncement);
        _model.AddNotification(
            new CS_NotificationData(
                CSE_NotificationType.Event,
                message
            )
        );
    }

    public void ShowTimeAlert(string message = "残り1分！")
    {
        CS_SoundManager.instance?.PlaySe(CSE_SeId.EventTimeAnnouncement);
        _model.AddNotification(
            new CS_NotificationData(
                CSE_NotificationType.TimeAlert,
                message
            )
        );
    }

    /// <summary>
    /// 追加: 帯画像を指定して時間通知を出す。
    /// backSpriteがnullなら従来のTimeAlert用の画像で表示する。
    /// </summary>
    public void ShowTimeAlert(string message, Sprite backSprite)
    {
        CS_SoundManager.instance?.PlaySe(CSE_SeId.EventTimeAnnouncement);
        _model.AddNotification(
            CS_NotificationData.CreateTimeAlert(message, backSprite)
        );
    }

    public void ShowKill(string playerName)
    {
        CS_SoundManager.instance?.PlaySe(CSE_SeId.EventKillLog);
        _model.AddNotification(
            CS_NotificationData.CreateKill(playerName)
        );
    }

    public void ShowCountdown(string message)
    {
        _model.ShowCountdown(message);
    }

    public void HideCountdown()
    {
        _model.HideCountdown();
    }

    private void OnDestroy()
    {
        _model?.Dispose();
    }
}