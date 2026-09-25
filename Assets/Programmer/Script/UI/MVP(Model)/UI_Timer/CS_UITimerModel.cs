/* ================================================
 *  Timerの状態を保持
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using R3;
using System;
using UnityEngine;

/// <summary>
/// Timerの状態を保持する(Modelの役割)
/// UIのクラスを一切参照せず、時間の状態のみを管理する
/// Timerはゲーム全体で1つだけ存在する前提のため、Instanceとして公開される
/// Model → View の一方向で、UI側はこのModelを購読して表示を更新する
/// </summary>
public class CS_UITimerModel : CS_BaseModel
{
    // ---------------- Modelの公開 ----------------

    /// <summary>
    /// 現在有効なTimerModelのインスタンス
    /// Timerは1つだけ存在するため、番号管理は不要
    /// Presenter側はこのInstanceを参照して購読する
    /// </summary>
    public static CS_UITimerModel Instance { get; private set; }

    /// <summary>
    /// Modelが生成された時の通知
    /// Presenterが後から生成された場合でも、このイベントで紐付けられる
    /// </summary>
    public static event Action<CS_UITimerModel> OnCreated;

    /// <summary>
    /// Modelが破棄された時の通知
    /// Presenter側は購読解除を行う
    /// </summary>
    public static event Action OnDestroyed;

    // ---------------- 状態 ----------------

    /// <summary>
    /// 現在の残り時間(秒)
    /// ReactivePropertyなので、値が変化すると購読者(View/Presenter)に通知される
    /// </summary>
    private readonly ReactiveProperty<float> _currentTime;

    /// <summary>
    /// タイマーの最大時間(秒)
    /// </summary>
    private readonly ReactiveProperty<float> _maxTime;

    //外部からは読み取り専用として公開
    public ReadOnlyReactiveProperty<float> currentTime => _currentTime;
    public ReadOnlyReactiveProperty<float> maxTime => _maxTime;

    // ---------------- コンストラクタ ----------------

    /// <summary>
    /// 最大時間を指定してTimerModelを生成する
    /// 現在時間は最大時間で初期化される
    /// ModelはMonoBehaviourではないため new で生成される
    /// </summary>
    public CS_UITimerModel(float maxSeconds)
    {
        // 最大時間は1秒以上に補正
        _maxTime = new ReactiveProperty<float>(Mathf.Max(1f, maxSeconds));

        // 現在時間は最大時間で開始
        _currentTime = new ReactiveProperty<float>(_maxTime.Value);

        // このModelを唯一のInstanceとして公開
        Instance = this;

        // Presenter側に「Modelが生成された」ことを通知
        OnCreated?.Invoke(this);
    }

    // ---------------- 値の変更 ----------------

    /// <summary>
    /// 残り時間を設定する
    /// 0〜最大時間の範囲にClampされる
    /// </summary>
    public void SetTime(float sec)
    {
        _currentTime.Value = Mathf.Clamp(sec, 0f, _maxTime.Value);
    }

    /// <summary>
    /// 最大時間を設定する
    /// 現在時間が最大時間を超えていた場合は切り詰める
    /// </summary>
    public void SetMaxTime(float sec)
    {
        _maxTime.Value = Mathf.Max(1f, sec);
        SetTime(_currentTime.Value);
    }

    // ---------------- 破棄 ----------------

    /// <summary>
    /// Modelの破棄処理
    /// Presenter側は購読解除を行うため、ここでは通知のみ行う
    /// ReactivePropertyのDisposeも忘れずに行う
    /// </summary>
    public override void Dispose()
    {
        // Instanceを無効化
        Instance = null;

        // Presenter側に「Modelが破棄された」ことを通知
        OnDestroyed?.Invoke();

        // ReactivePropertyの破棄
        _currentTime.Dispose();
        _maxTime.Dispose();
    }
}
