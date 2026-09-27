/* ================================================
 * 　Timerの変化をViewに通知する
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// Timerの変化をViewに通知する(Presenter → View の一方向)
/// TimerのUIにアタッチし、共通のTimerModelが生成されたら購読を開始する
/// Model・TimerUIのどちらが先に生成されても自動で紐づく
/// Modelの破棄は持ち主(使用者)が行うので、Presenter側では購読解除のみ行う
/// </summary>
public class CS_UITimerPresenter : CS_BasePresenter
{
    [Header("タイマーの最大時間（秒）")]
    [SerializeField] private float _maxSeconds = 300f; // インスペクターで変更可能な初期最大時間

    private CS_UITimerView _view;   // このPresenterに紐づくView
    private CS_UITimerModel _model; // 現在紐づいているTimerModel

    // Model購読をまとめて管理する。新しいModelをBindすると前の購読は自動解除される
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    // Updateで時間を減らすかどうかのフラグ
    private bool _isRunning = false;

    void Awake()
    {
        // View取得とPresenterの紐付け
        _view = GetComponent<CS_UITimerView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        // Modelの生成・破棄イベントを購読する
        CS_UITimerModel.OnCreated += HandleCreated;
        CS_UITimerModel.OnDestroyed += HandleDestroyed;

        // Presenterより先にModelが生成されていた場合はここで紐付ける
        if (CS_UITimerModel.Instance != null)
            BindModel(CS_UITimerModel.Instance);
    }

    void OnDisable()
    {
        // イベント購読解除
        CS_UITimerModel.OnCreated -= HandleCreated;
        CS_UITimerModel.OnDestroyed -= HandleDestroyed;

        // Model購読解除
        UnbindModel();
    }

    /// <summary>
    /// Modelが生成された時に呼ばれる
    /// </summary>
    private void HandleCreated(CS_UITimerModel model)
    {
        BindModel(model);
    }

    /// <summary>
    /// Modelが破棄された時に呼ばれる
    /// </summary>
    private void HandleDestroyed()
    {
        UnbindModel();
    }

    /// <summary>
    /// Modelを購読してViewに反映する
    /// 現在時間・最大時間のどちらが変わってもViewを更新する
    /// (購読した瞬間に現在値が流れるので初期表示もここで行われる)
    /// </summary>
    private void BindModel(CS_UITimerModel model)
    {
        _model = model;

        _modelSubscription.Disposable =
            model.currentTime
                .CombineLatest(model.maxTime, (remain, max) => (remain, max))
                .Subscribe(x => _view.UpdateTimer(x.remain, x.max));
    }

    /// <summary>
    /// Modelの購読を解除する
    /// SerialDisposableに null を入れることで購読が破棄される
    /// </summary>
    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
        _model = null;
    }

    /// <summary>
    /// 毎フレーム呼ばれ、タイマーが動作中なら時間を減らす
    /// Modelの値を直接書き換えることでView側に通知が飛ぶ
    /// </summary>
    void Update()
    {
        if (!_isRunning || _model == null) return;

        float newTime = _model.currentTime.CurrentValue - Time.deltaTime;
        _model.SetTime(newTime);

        // 0秒以下になったら停止
        if (newTime <= 0f)
            _isRunning = false;
    }

    /// <summary>
    /// タイマー開始
    /// </summary>
    public void StartTimer() => _isRunning = true;

    /// <summary>
    /// タイマー停止
    /// </summary>
    public void StopTimer() => _isRunning = false;

    /// <summary>
    /// タイマーを最大時間にリセットする
    /// </summary>
    public void ResetTimer()
    {
        if (_model == null) return;
        _model.SetTime(_model.maxTime.CurrentValue);
    }
}
