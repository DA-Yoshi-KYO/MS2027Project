/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-17 | 初回作成
 * ================================================ */

using System;
using UnityEngine;

/// <summary>
/// 犯罪完遂ゲージのPresenter。
///
/// ・公開されたModelを購読する
/// ・完遂率の変更をViewへ渡す
/// ・現在地の変更をViewへ渡す
///
/// ModelのBind()は呼ばない。
/// Modelを所有するゲーム側がBind()を呼び、
/// PresenterはOnBoundを受け取って購読する。
/// </summary>
[RequireComponent(
    typeof(CS_UICrimeProgressRootView))]
public sealed class CS_UICrimeProgressPresenter :
    CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UICrimeProgressRootView _view;

    private CS_UICrimeProgressModel _model;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _view =
            GetComponent<
                CS_UICrimeProgressRootView
            >();

        _view.SetPresenter(this);
    }

    private void OnEnable()
    {
        // Modelが公開されたら購読する。
        CS_UICrimeProgressModel.OnBound +=
            SubscribeModel;

        // Modelの公開が解除されたら購読を解除する。
        CS_UICrimeProgressModel.OnUnbound +=
            UnsubscribeModel;

        // Presenterより先にModelが公開済みだった場合。
        if (CS_UICrimeProgressModel.TryGet(
                out CS_UICrimeProgressModel model))
        {
            SubscribeModel(model);
        }
        else
        {
            _view.ClearCurrentArea();
        }
    }

    private void OnDisable()
    {
        CS_UICrimeProgressModel.OnBound -=
            SubscribeModel;

        CS_UICrimeProgressModel.OnUnbound -=
            UnsubscribeModel;

        UnsubscribeModel();
    }

    // =========================================================
    // Model購読
    // =========================================================

    /// <summary>
    /// 公開されたModelを購読する。
    /// Model.Bind()を行う処理ではない。
    /// </summary>
    private void SubscribeModel(
        CS_UICrimeProgressModel model)
    {
        // 以前のModelがあれば先に購読解除する。
        UnsubscribeCurrentModel();

        _model = model;

        if (_model == null)
        {
            _view.ClearCurrentArea();
            return;
        }

        _model.OnProgressChanged +=
            HandleProgressChanged;

        _model.OnCurrentAreaChanged +=
            HandleCurrentAreaChanged;

        // 購読開始時点の状態をUIへ反映する。
        ApplyCurrentModelState();
    }

    /// <summary>
    /// 現在購読しているModelとの接続を解除し、
    /// UIを未選択状態に戻す。
    /// </summary>
    private void UnsubscribeModel()
    {
        UnsubscribeCurrentModel();

        if (_view != null)
        {
            _view.ClearCurrentArea();
        }
    }

    /// <summary>
    /// 現在のModelからイベント購読だけを解除する。
    /// UIのリセットは行わない。
    /// </summary>
    private void UnsubscribeCurrentModel()
    {
        if (_model == null)
        {
            return;
        }

        _model.OnProgressChanged -=
            HandleProgressChanged;

        _model.OnCurrentAreaChanged -=
            HandleCurrentAreaChanged;

        _model = null;
    }

    // =========================================================
    // 初期状態の反映
    // =========================================================

    /// <summary>
    /// 購読開始時点の全エリアの完遂率と現在地を
    /// Viewへ反映する。
    /// </summary>
    private void ApplyCurrentModelState()
    {
        if (_model == null)
        {
            return;
        }

        Array areaValues =
            Enum.GetValues(
                typeof(CSE_CrimeAreaType)
            );

        foreach (
            CSE_CrimeAreaType areaType
            in areaValues)
        {
            _view.SetProgress(
                areaType,
                _model.GetProgress(areaType)
            );
        }

        if (_model.hasCurrentArea)
        {
            // 初回反映では拡縮アニメーションを使用しない。
            _view.SetCurrentArea(
                _model.currentArea,
                false
            );
        }
        else
        {
            _view.ClearCurrentArea();
        }
    }

    // =========================================================
    // Modelイベント
    // =========================================================

    private void HandleProgressChanged(
        CSE_CrimeAreaType areaType,
        float progress)
    {
        _view.SetProgress(
            areaType,
            progress
        );
    }

    private void HandleCurrentAreaChanged(
        CSE_CrimeAreaType areaType)
    {
        // 実際のエリア移動時は拡縮アニメーションを再生する。
        _view.SetCurrentArea(
            areaType,
            true
        );
    }

    // =========================================================
    // 破棄
    // =========================================================

    protected override void OnDestroy()
    {
        UnsubscribeCurrentModel();

        base.OnDestroy();
    }
}