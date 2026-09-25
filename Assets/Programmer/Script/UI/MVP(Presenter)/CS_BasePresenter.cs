/* ================================================
 * 　UI設計(MVPのPresenterの基底クラス)
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-09-25 | UIにアタッチする形(MonoBehaviour)に変更
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// UI設計(MVPのPresenterの基底クラス)
/// ModelとViewを橋渡しする役割を持つ
/// UIのGameObjectにアタッチし、Modelを受け取ってViewと紐づける
/// R3の購読解除を共通化するためにCompositeDisposableを保持する
/// </summary>
public abstract class CS_BasePresenter : MonoBehaviour
{
    /// <summary>
    /// R3の購読をまとめて破棄するためのコンテナ
    /// </summary>
    protected CompositeDisposable _disposables = new CompositeDisposable();

    /// <summary>
    ///　Presenterの破棄処理
    ///　R3の購読を全て解除する
    /// </summary>
    protected virtual void OnDestroy()
    {
        _disposables.Dispose();
    }
}
