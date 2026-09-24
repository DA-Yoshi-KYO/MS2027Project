/* ================================================
 * MVPの初期化を行うクラス
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// MVPの初期化を行うクラス
/// ModelとPresenterを生成し、Viewに紐づける
/// </summary>
public abstract class CS_BaseInitMVP<TModel, TPresenter, TView> : MonoBehaviour
    where TModel : CS_BaseModel, new()
    where TPresenter : CS_BasePresenter
    where TView : CS_BaseView<TPresenter>
{
    //UIにアタッチしてるViewをインスペクターから紐づける
    [Header("対応するViewを紐づける")][SerializeField] protected TView _view;

    //Presenterのインスタンス
    protected TPresenter _presenter;

    protected virtual void Awake()
    {
        //Modelの生成
        var model = new TModel();

        //設定の初期化
        InitModel(model);

        //Presenterの生成
        _presenter = CreatePresenter(model, _view);

        //ViewをPresenterに渡す
        _view.SetPresenter(_presenter);

    }

    //設定の初期化
    protected virtual void InitModel(TModel model) { }

    //Presenterの生成を派生クラスで実装
    protected abstract TPresenter CreatePresenter(TModel model, TView view);
}
