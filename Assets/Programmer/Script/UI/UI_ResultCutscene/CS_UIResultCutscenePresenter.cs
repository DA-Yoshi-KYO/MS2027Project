/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-25 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// リザルトへのカットシーンの Presenter
/// ・Model の状態が変わったら、View に動かす/置くように伝える
/// ・View の動きが終わったら、Model に「終わった」と伝える
/// ・Model は生成しない。Controller から BindModel() で受け取る
/// ・View と同じ GameObject にアタッチする
///
/// 状態と View の対応
///   Hidden    → View.SetHidden()                       画面の上の外に置く
///   Covering  → View.PlayCover() → 終わったら Model.NotifyCovered()
///   Covered   → View.SetCovered()                      画面を覆った状態に置く
///   Revealing → View.PlayReveal() → 終わったら Model.NotifyRevealed()
///   Revealed  → View.SetRevealed()                     左右の外に置く
///   Closing   → View.PlayClose() → 終わったら Model.NotifyClosed()   追加(開く動きの逆再生)
///   Closed    → View.SetCovered()                      追加(閉じ終わりは、覆った状態と同じ姿)
/// </summary>
public class CS_UIResultCutscenePresenter : CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIResultCutsceneView _view;
    private CS_UIResultCutsceneModel _model;

    // Model の購読。新しく入れると、前の購読は自動で解除される
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        _view = GetComponent<CS_UIResultCutsceneView>();

        if (_view == null)
        {
            Debug.LogError(
                $"{nameof(CS_UIResultCutscenePresenter)}: " +
                "同じGameObjectに CS_UIResultCutsceneView が必要です。",
                this
            );

            return;
        }

        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    /// <summary>
    /// Controller から呼ばれる。Model の状態を購読して View に反映する。
    /// 購読した瞬間に現在の状態で1回呼ばれるので、最初の位置はここで決まる。
    /// </summary>
    public void BindModel(CS_UIResultCutsceneModel model)
    {
        if (_view == null)
        {
            return;
        }

        _model = model;

        _modelSubscription.Disposable = model.state
            .Subscribe(HandleStateChanged);
    }

    // =========================================================
    // Model イベントハンドラ
    // =========================================================

    private void HandleStateChanged(CSE_ResultCutsceneState state)
    {
        switch (state)
        {
            case CSE_ResultCutsceneState.Hidden:
                _view.SetHidden();
                break;

            case CSE_ResultCutsceneState.Covering:
                // 動きが終わったら Model に伝える
                _view.PlayCover(_model.NotifyCovered);
                break;

            case CSE_ResultCutsceneState.Covered:
                _view.SetCovered();
                break;

            case CSE_ResultCutsceneState.Revealing:
                _view.PlayReveal(_model.NotifyRevealed);
                break;

            case CSE_ResultCutsceneState.Revealed:
                _view.SetRevealed();
                break;

            case CSE_ResultCutsceneState.Closing:
                _view.PlayClose(_model.NotifyClosed);
                break;

            case CSE_ResultCutsceneState.Closed:
                // 閉じ終わりは、画面を覆った状態と同じ姿(開く Timeline の最初)
                _view.SetCovered();
                break;
        }
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}
