/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-20 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// タイトルメニューの Presenter
/// ・Model の選択が変わったら、View の選択フレームを動かす
/// ・Input からの通知(移動・決定・マウス)を Model の操作に変換する
/// ・Model は生成しない。Controller から BindModel() で受け取る
/// ・View / Input と同じ GameObject にアタッチする
///
/// 全体の流れ
///   キー/スティック → Input.OnNavigate → Model.MoveSelection → (自動) View.SetSelected
///   マウス移動      → Input.OnPointerMoved → View.GetItemIndexAt → Model.Select → (自動) View.SetSelected
///   決定/クリック   → Model.Decide → OnDecided → Controller がシーン遷移
/// </summary>
public class CS_UITitleMenuPresenter : CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UITitleMenuView _view;
    private CS_UITitleMenuInput _input;
    private CS_UITitleMenuModel _model;

    // Model の購読。新しく入れると、前の購読は自動で解除される
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    // 最初の選択表示かどうか(最初はアニメーションなしで即配置する)
    private bool _isFirstSelection = true;

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        _view = GetComponent<CS_UITitleMenuView>();
        _input = GetComponent<CS_UITitleMenuInput>();

        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        _input.OnNavigate += HandleNavigate;
        _input.OnSubmit += HandleSubmit;
        _input.OnPointerMoved += HandlePointerMoved;
        _input.OnClick += HandleClick;
    }

    void OnDisable()
    {
        _input.OnNavigate -= HandleNavigate;
        _input.OnSubmit -= HandleSubmit;
        _input.OnPointerMoved -= HandlePointerMoved;
        _input.OnClick -= HandleClick;
    }

    /// <summary>Controller から呼ばれる。Model の選択を購読して View に反映する</summary>
    public void BindModel(CS_UITitleMenuModel model)
    {
        _model = model;
        _isFirstSelection = true;

        // 購読した瞬間に現在の選択で1回呼ばれ、フレームが最初の項目に配置される
        _modelSubscription.Disposable = model.selectedIndex
            .Subscribe(HandleSelectedChanged);
    }

    // =========================================================
    // Model イベントハンドラ
    // =========================================================

    private void HandleSelectedChanged(int index)
    {
        // 最初の1回は即配置、以降はアニメーションで移動
        _view.SetSelected(index, !_isFirstSelection);
        if (!_isFirstSelection) CS_SoundManager.instance?.PlaySe(CSE_SeId.SystemUICursorMove);
        _isFirstSelection = false;
    }

    // =========================================================
    // Input イベントハンドラ
    // =========================================================

    private void HandleNavigate(int direction)
    {
        _model?.MoveSelection(direction);
    }

    private void HandleSubmit()
    {
        CS_SoundManager.instance?.PlaySe(CSE_SeId.SystemUIDecide);
        _model?.Decide();
    }

    /// <summary>マウスが動いた時。項目の上なら、その項目を選択する</summary>
    private void HandlePointerMoved(Vector2 screenPoint)
    {
        if (_model == null)
            return;

        int index = _view.GetItemIndexAt(screenPoint);

        if (index >= 0)
            _model.Select(index);
    }

    /// <summary>クリックされた時。項目の上なら、その項目を選択して決定する</summary>
    private void HandleClick(Vector2 screenPoint)
    {
        if (_model == null)
            return;

        int index = _view.GetItemIndexAt(screenPoint);

        if (index < 0)
            return;

        CS_SoundManager.instance?.PlaySe(CSE_SeId.SystemUIDecide);
        _model.Select(index);
        _model.Decide();
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}