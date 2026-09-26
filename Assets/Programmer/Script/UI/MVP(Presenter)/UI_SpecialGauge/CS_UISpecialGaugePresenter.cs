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
/// 必殺技ゲージ（Special Gauge）のPresenter
/// Model → View の一方向でUIを更新する
/// HPゲージPresenterと同じ構造
/// </summary>
public class CS_UISpecialGaugePresenter : CS_BasePresenter
{
    [Header("このUIが表示するプレイヤー番号")]
    [SerializeField] private int _playerNumber;

    private CS_UISpecialGaugeView _view;
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    void Awake()
    {
        _view = GetComponent<CS_UISpecialGaugeView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        // ModelのBind/Unbindを監視
        CS_UISpecialGaugeModel.OnBound += HandleBound;
        CS_UISpecialGaugeModel.OnUnbound += HandleUnbound;

        // Model が存在するなら通常通り Bind
        if (CS_UISpecialGaugeModel.TryGet(_playerNumber, out var model))
        {
            BindModel(model);
        }
    }

    void OnDisable()
    {
        CS_UISpecialGaugeModel.OnBound -= HandleBound;
        CS_UISpecialGaugeModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    private void HandleBound(int playerNumber, CS_UISpecialGaugeModel model)
    {
        if (playerNumber == _playerNumber)
        {
            _view.SetVisible(true);   // ← 見た目だけ表示
            BindModel(model);
        }
    }

    private void HandleUnbound(int playerNumber)
    {
        if (playerNumber == _playerNumber)
        {
            UnbindModel();
            _view.SetVisible(false);  // ← 見た目だけ非表示
        }
    }

    private void BindModel(CS_UISpecialGaugeModel model)
    {
        // Modelの値を購読してViewに反映
        _modelSubscription.Disposable =
            model.currentGauge.Subscribe(x => _view.UpdateGauge(x, model.maxGauge));
    }

    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
    }
}
