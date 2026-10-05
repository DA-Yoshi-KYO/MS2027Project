/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// スコアの変化をViewに通知する(Model → View の一方向)
/// スコアUIにアタッチし、指定番号のScoreModelがBindされたら購読を始める
/// Model・UIのどちらが先に生成されても紐づく
/// ★ SetActive → SetVisible に変更（非表示でも OnBound を受け取れるようにする）
/// </summary>
public class CS_UIScorePresenter : CS_BasePresenter
{
    [Header("何番目のプレイヤーのスコアを表示するか")]
    [SerializeField] private int _playerNumber;

    private CS_UIScoreView _view;

    // 今紐づいているModelの購読（新しいModelを入れると前の購読は自動で解除される）
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    void Awake()
    {
        _view = GetComponent<CS_UIScoreView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);

        // ★ 起動時は非表示（SetActive(false) ではなく SetVisible(false) で隠す）
        _view.SetVisible(false);
    }

    void OnEnable()
    {
        CS_UIScoreModel.OnBound += HandleBound;
        CS_UIScoreModel.OnUnbound += HandleUnbound;

        // UIより先にModelがBindされていた場合
        if (CS_UIScoreModel.TryGet(_playerNumber, out var model))
        {
            _view.SetVisible(true);
            BindModel(model);
        }
        // Modelが存在しない番号なら非表示のまま待つ
        // （SetActive(false) しないので OnBound を受け取れる）
    }

    void OnDisable()
    {
        CS_UIScoreModel.OnBound -= HandleBound;
        CS_UIScoreModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    private void HandleBound(int playerNumber, CS_UIScoreModel model)
    {
        if (playerNumber != _playerNumber) return;

        _view.SetVisible(true);
        BindModel(model);
    }

    private void HandleUnbound(int playerNumber)
    {
        if (playerNumber != _playerNumber) return;

        UnbindModel();
        _view.SetVisible(false);
    }

    // Modelを購読してViewに反映する
    private void BindModel(CS_UIScoreModel model)
    {
        _modelSubscription.Disposable =
            model.score.Subscribe(_view.UpdateScore);
    }

    // Modelの購読をやめる
    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
    }
}
