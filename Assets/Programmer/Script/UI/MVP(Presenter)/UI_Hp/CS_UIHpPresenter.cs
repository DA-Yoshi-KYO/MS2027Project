/* ================================================
 * 　Hpの変化をViewに通知する
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using R3;

/// <summary>
/// Hpの変化をViewに通知する
/// </summary>
public class CS_UIHpPresenter : CS_BasePresenter
{
    private CS_UIHpModel _model;
    private CS_UIHpView _view;

    public CS_UIHpPresenter(CS_UIHpModel model, CS_UIHpView view)
    {
        this._model = model;
        this._view = view;

        //Hpが変わったら通知する
        model._currentHp.Subscribe(hp =>_view.UpdateHp(hp, model._maxHp.Value)).AddTo(_disposables);
    }

    //ダメージ処理
    public void Damage(int damage)
    {
        _model.SetHp(_model._currentHp.Value - damage);
    }

    //回復処理
    public void Heal(int damage)
    {
        _model.SetHp(_model._currentHp.Value + damage);
    }
}
