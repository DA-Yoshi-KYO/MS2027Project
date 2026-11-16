/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-26 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// 必殺技ゲージのPresenter。
///
/// ・ローカルプレイヤー番号と一致したModelだけを購読する
/// ・Bind時にキャラクター設定をViewへ渡す
/// ・ゲージ変化をViewへ通知する
/// </summary>
[RequireComponent(typeof(CS_UISpecialGaugeView))]
public class CS_UISpecialGaugePresenter :
    CS_BasePresenter
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UISpecialGaugeView _view;

    private readonly SerialDisposable
        _modelSubscription =
            new SerialDisposable();

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _view =
            GetComponent<CS_UISpecialGaugeView>();

        _view.SetPresenter(this);

        _modelSubscription.AddTo(
            _disposables
        );
    }

    private void OnEnable()
    {
        CS_UISpecialGaugeModel.OnBound +=
            HandleBound;

        CS_UISpecialGaugeModel.OnUnbound +=
            HandleUnbound;

        int localNumber =
            CS_UISpecialGaugeModel
                .localPlayerNumber;

        if (localNumber < 0)
        {
            return;
        }

        if (CS_UISpecialGaugeModel.TryGet(
                localNumber,
                out CS_UISpecialGaugeModel model))
        {
            BindModel(model);
        }
    }

    private void OnDisable()
    {
        CS_UISpecialGaugeModel.OnBound -=
            HandleBound;

        CS_UISpecialGaugeModel.OnUnbound -=
            HandleUnbound;

        UnbindModel();
    }

    // =========================================================
    // Bind / Unbind通知
    // =========================================================

    private void HandleBound(
        int playerNumber,
        CS_UISpecialGaugeModel model)
    {
        if (playerNumber !=
            CS_UISpecialGaugeModel.localPlayerNumber)
        {
            return;
        }

        BindModel(model);
    }

    private void HandleUnbound(
        int playerNumber)
    {
        if (playerNumber !=
            CS_UISpecialGaugeModel.localPlayerNumber)
        {
            return;
        }

        UnbindModel();
    }

    // =========================================================
    // Model購読
    // =========================================================

    private void BindModel(
        CS_UISpecialGaugeModel model)
    {
        UnbindModel();

        if (model == null)
        {
            return;
        }

        // 購読より先にキャラクターの見た目を設定する。
        _view.SetCharacter(
            model.characterType
        );

        // ReactivePropertyは購読直後に現在値を通知するため、
        // SetCharacter後に購読する。
        _modelSubscription.Disposable =
            model.currentGauge.Subscribe(
                current =>
                {
                    _view.UpdateGauge(
                        current,
                        model.maxGauge
                    );
                }
            );
    }

    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;

        if (_view != null)
        {
            _view.ResetGauge();
        }
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}