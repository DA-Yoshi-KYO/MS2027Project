/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// CS_PlayerAlertと警戒状態UIを接続するPresenter。
/// ローカルPlayer生成後にBindPlayerAlert()を呼ぶ。
/// </summary>
public class CS_UIPlayerAlertPresenter : CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIPlayerAlertModel _model;
    private CS_UIPlayerAlertView _view;
    private CS_PlayerAlert _playerAlert;
    private bool _isBound;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _model = new CS_UIPlayerAlertModel();
        _view = GetComponent<CS_UIPlayerAlertView>();

        if (_view == null)
        {
            Debug.LogError(
                "[CS_UIPlayerAlertPresenter] " +
                "同じGameObjectにCS_UIPlayerAlertViewがありません。"
            );
            return;
        }

        _view.SetPresenter(this);
        _model.OnAlertStateChanged += HandleModelStateChanged;

        // 初期状態は非表示。
        _view.ApplyAlertState(_model.alertState);
    }

    // =========================================================
    // PlayerAlertとのBind
    // =========================================================

    /// <summary>
    /// 実行時生成されたローカルPlayerのCS_PlayerAlertをBindする。
    /// </summary>
    public void BindPlayerAlert(CS_PlayerAlert playerAlert)
    {
        if (playerAlert == null)
        {
            Debug.LogError(
                "[CS_UIPlayerAlertPresenter] playerAlertがnullです。"
            );
            return;
        }

        UnbindPlayerAlert();

        _playerAlert = playerAlert;
        _playerAlert.onAlertChanged += HandlePlayerAlertChanged;
        _isBound = true;

        // Bind前から警戒状態だった場合にも、現在値を即時反映する。
        _model.SetAlertState(_playerAlert.alertState);

        // Modelの初期値と同じNoneの場合はイベントが発生しないため、
        // Viewへ現在値を直接反映する。
        _view?.ApplyAlertState(_playerAlert.alertState);
    }

    public void UnbindPlayerAlert()
    {
        if (!_isBound || _playerAlert == null)
            return;

        _playerAlert.onAlertChanged -= HandlePlayerAlertChanged;
        _playerAlert = null;
        _isBound = false;

        _model?.SetAlertState(CSE_PlayerAlertState.None);
        _view?.ApplyAlertState(CSE_PlayerAlertState.None);
    }

    // =========================================================
    // イベント処理
    // =========================================================

    private void HandlePlayerAlertChanged(
        CSE_PlayerAlertState alertState)
    {
        _model?.SetAlertState(alertState);
    }

    private void HandleModelStateChanged(
        CSE_PlayerAlertState alertState)
    {
        _view?.ApplyAlertState(alertState);
    }

    // =========================================================
    // 終了処理
    // =========================================================

    protected override void OnDestroy()
    {
        UnbindPlayerAlert();

        if (_model != null)
        {
            _model.OnAlertStateChanged -= HandleModelStateChanged;
            _model.Dispose();
        }

        base.OnDestroy();
    }
}
