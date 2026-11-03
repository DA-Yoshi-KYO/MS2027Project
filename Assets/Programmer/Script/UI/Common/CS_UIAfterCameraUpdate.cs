/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-02 | 初回作成
 * ================================================ */

using System;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// ワールドの物の位置に出すUI(ハイドマーカー・スキャンの目印)の位置の計算を、
/// カメラの位置が確定した後に行うためのクラス
///
/// ・カメラはCS_PlayerのLateUpdateでCinemachineCameraが動き、CinemachineBrainのLateUpdateで実際のカメラに反映される
///   PresenterのLateUpdateで計算すると、実行順によっては1フレーム前のカメラで計算してしまい、
///   画面揺れ・視点の回転・FOVの変化の時に目印が震える
/// ・そのため、Brainがカメラを更新し終えた時(CinemachineCore.CameraUpdatedEvent)に計算する
///   見ているカメラ以外のBrainの更新は無視する
/// ・Brainが動いていないシーンでは、PresenterのLateUpdateから LateUpdateFallback を呼んで今まで通り計算する
/// </summary>
public sealed class CS_UIAfterCameraUpdate
{
    private readonly Func<Camera> _getCamera;      // 目印を出すカメラ
    private readonly Action _onCameraUpdated;      // カメラが確定した時に行う処理(位置の計算)

    private CinemachineCore.BrainEvent _subscribedEvent;   // 購読しているイベント(再生ごとに作り直されるので、外す時用に覚えておく)
    private int _lastBrainUpdateFrame = -1;                // Brainの更新で計算した最後のフレーム

    public CS_UIAfterCameraUpdate(Func<Camera> getCamera, Action onCameraUpdated)
    {
        _getCamera = getCamera;
        _onCameraUpdated = onCameraUpdated;
    }

    /// <summary>
    /// Brainの更新の通知を受け取り始める(PresenterのOnEnableで呼ぶ)
    /// </summary>
    public void Enable()
    {
        Disable();

        _subscribedEvent = CinemachineCore.CameraUpdatedEvent;
        _subscribedEvent.AddListener(HandleCameraUpdated);
    }

    /// <summary>
    /// Brainの更新の通知を受け取るのをやめる(PresenterのOnDisableで呼ぶ)
    /// </summary>
    public void Disable()
    {
        if (_subscribedEvent == null) return;

        _subscribedEvent.RemoveListener(HandleCameraUpdated);
        _subscribedEvent = null;
    }

    /// <summary>
    /// PresenterのLateUpdateから呼ぶ。Brainが動いていない時だけ計算する
    /// </summary>
    public void LateUpdateFallback()
    {
        // 直近のフレームでBrainの更新があれば、そちらで計算する
        if (Time.frameCount - _lastBrainUpdateFrame <= 1) return;

        _onCameraUpdated();
    }

    private void HandleCameraUpdated(CinemachineBrain brain)
    {
        // 目印を出すカメラ以外のBrainは無視する
        Camera camera = _getCamera();
        if (camera != null && brain.OutputCamera != camera) return;

        _lastBrainUpdateFrame = Time.frameCount;
        _onCameraUpdated();
    }
}
