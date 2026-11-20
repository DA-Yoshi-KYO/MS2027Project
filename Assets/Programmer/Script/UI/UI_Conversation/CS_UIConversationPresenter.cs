/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-20 | 初回作成
 * ================================================ */

using R3;
using System;
using UnityEngine;

/// <summary>
/// 会話ウィンドウの Model と View を橋渡しする Presenter
/// ・Model は生成しない。Model.OnBound / OnUnbound を見て、Bind された Model を購読する
/// ・Model の文が変わったら View の文字を変える
/// ・Model の表示状態が変わったら View を出す / 消す
/// ・Say が来たら、表示時間が過ぎた時に Model を非表示にする(= 自動で消える)
/// ・View と同じ GameObject にアタッチする
///
/// 全体の流れ
///   Model.Say("文", 3秒)
///     → (自動) View.SetText / View.SetVisible(true)   … 文字が変わってフェードイン
///     → (自動) Presenter が 3秒のタイマーを開始
///     → 3秒後 Model.Hide()
///     → (自動) View.SetVisible(false)                  … フェードアウト
/// </summary>
public class CS_UIConversationPresenter : CS_BasePresenter
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("表示時間・フェード時間の設定(DB_UIConversationSettings)")]
    [SerializeField]
    private CSO_UIConversationSettings _settings;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIConversationView _view;

    // 今購読している Model(Bind されていなければ null)
    private CS_UIConversationModel _model;

    // Model の購読をまとめて入れる箱。新しく入れると、前の購読は自動で解除される
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    // 「消すタイマー」を入れる箱。新しく入れると、前のタイマーは自動でキャンセルされる
    // → 連続で Say された時、前の文のタイマーで新しい文が途中で消えることがない
    private readonly SerialDisposable _hideTimer = new SerialDisposable();

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        _view = GetComponent<CS_UIConversationView>();
        _modelSubscription.AddTo(_disposables); // 破棄は CS_BasePresenter の OnDestroy でまとめて行われる
        _hideTimer.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        CS_UIConversationModel.OnBound += HandleBound;
        CS_UIConversationModel.OnUnbound += HandleUnbound;

        // すでに Bind 済みの Model があれば、すぐ購読する
        if (CS_UIConversationModel.TryGet(out var model))
            BindModel(model);
    }

    void OnDisable()
    {
        CS_UIConversationModel.OnBound -= HandleBound;
        CS_UIConversationModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    // =========================================================
    // Bind / Unbind ハンドラ
    // =========================================================

    private void HandleBound(CS_UIConversationModel model)
    {
        BindModel(model);
    }

    private void HandleUnbound()
    {
        UnbindModel();

        // Model がなくなったので、表示中ならフェードアウトして消す
        _view.SetVisible(false, _settings.fadeOutSeconds);
    }

    // =========================================================
    // Model 購読
    // =========================================================

    private void BindModel(CS_UIConversationModel model)
    {
        UnbindModel();
        _model = model;

        // 3つの購読をまとめる。後で1回で解除できる
        var subscription = new CompositeDisposable();

        // Model の文が変わったら、View の文字を変える
        model.text
            .Subscribe(_view.SetText)
            .AddTo(subscription);

        // Model の表示状態が変わったら、View を出す / 消す
        // ※ 購読した瞬間に現在の値で1回呼ばれる
        model.isVisible
            .Subscribe(HandleVisibleChanged)
            .AddTo(subscription);

        // Say が呼ばれたら、消すタイマーをやり直す
        model.onSay
            .Subscribe(StartHideTimer)
            .AddTo(subscription);

        _modelSubscription.Disposable = subscription;

        // 購読を始めた時点で既に表示中なら、消すタイマーが無いので既定秒数で開始する
        if (model.isVisible.CurrentValue)
            StartHideTimer(-1f);
    }

    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
        _hideTimer.Disposable = null;
        _model = null;
    }

    // =========================================================
    // 表示の制御
    // =========================================================

    /// <summary>表示状態が変わった時。View を出す / 消す。消えた時はタイマーも止める</summary>
    private void HandleVisibleChanged(bool isVisible)
    {
        _view.SetVisible(
            isVisible,
            isVisible ? _settings.fadeInSeconds : _settings.fadeOutSeconds);

        if (!isVisible)
            _hideTimer.Disposable = null;
    }

    /// <summary>「一定時間後に Model を非表示にする」タイマーを開始する。前のタイマーは自動でキャンセルされる</summary>
    private void StartHideTimer(float duration)
    {
        _hideTimer.Disposable = Observable
            .Timer(TimeSpan.FromSeconds(ResolveDuration(duration)))
            .Subscribe(_ => _model?.Hide());
    }

    /// <summary>
    /// 「何秒表示するか」を決める
    /// ・指定が 0 以下 → 設定アセットの既定秒数
    /// ・下限(minDuration)より短ければ、下限まで引き上げる
    /// </summary>
    private float ResolveDuration(float duration)
    {
        float resolvedDuration = duration > 0f ? duration : _settings.defaultDuration;
        return Mathf.Max(resolvedDuration, _settings.minDuration);
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}
