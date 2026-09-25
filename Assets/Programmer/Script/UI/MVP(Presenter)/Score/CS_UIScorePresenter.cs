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
/// </summary>
public class CS_UIScorePresenter : CS_BasePresenter
{
    [Header("何番目のプレイヤーのスコアを表示するか")]
    [SerializeField] private int _playerNumber;

    private CS_UIScoreView _view;

    //今紐づいているModelの購読(新しいModelを入れると前の購読は自動で解除される)
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    void Awake()
    {
        _view = GetComponent<CS_UIScoreView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        CS_UIScoreModel.OnBound += HandleBound;
        CS_UIScoreModel.OnUnbound += HandleUnbound;

        //UIより先にModelがBindされていた場合
        if (CS_UIScoreModel.TryGet(_playerNumber, out var model))
        {
            gameObject.SetActive(true);
            BindModel(model);
        }
        else
        {
            //Modelが存在しない番号ならUIを非表示
            gameObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        CS_UIScoreModel.OnBound -= HandleBound;
        CS_UIScoreModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    private void HandleBound(int playerNumber, CS_UIScoreModel model)
    {
        if (playerNumber == _playerNumber)
        {
            gameObject.SetActive(true);
            BindModel(model);
        }
    }

    private void HandleUnbound(int playerNumber)
    {
        if (playerNumber == _playerNumber)
        {
            UnbindModel();
            gameObject.SetActive(false);
        }
    }

    //Modelを購読してViewに反映する
    private void BindModel(CS_UIScoreModel model)
    {
        _modelSubscription.Disposable =
            model.score.Subscribe(x => _view.UpdateScore(x));
    }

    //Modelの購読をやめる
    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
    }
}
