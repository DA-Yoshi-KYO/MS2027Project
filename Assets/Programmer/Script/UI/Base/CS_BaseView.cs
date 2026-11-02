/* ================================================
 *  UI設計(MVPのViewの基底クラス)
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// UI設計(MVPのViewの基底クラス)
/// 描画とUIの操作の役割をもつ。ロジックは持たない。
/// Presenterを保持し、SetPresenterで紐付ける
/// </summary>
public abstract class CS_BaseView<T> : MonoBehaviour where T : CS_BasePresenter
{
    /// <summary>
    /// このViewに紐付けられたPresenter
    /// PresenterはViewの描画を制御する
    /// </summary>
    protected T _presenter;

    /// <summary>
    /// PresenterをViewに設定する
    /// Presenter側から呼び出されることが多い
    /// </summary>
    public virtual void SetPresenter(T presenter)
    {
        this._presenter = presenter;
    }
}
