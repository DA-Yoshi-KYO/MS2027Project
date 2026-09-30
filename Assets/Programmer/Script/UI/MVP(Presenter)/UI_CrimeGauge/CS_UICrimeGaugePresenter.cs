/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// 悪人の犯罪完遂ゲージの Presenter
/// ・ゲージ Prefab の子に CS_UICrimeGaugeView と一緒にアタッチする
/// ・親 Transform をキーに Model を探して購読する
/// ・Model の値変化を View に伝える
/// </summary>
public class CS_UICrimeGaugePresenter : CS_BasePresenter
{
    private CS_UICrimeGaugeView _view;

    // Model を探すキー（直下に置かれている親の Transform）
    private Transform _parentTransform;

    // 今紐づいている Model の購読（新しい Model を入れると前の購読は自動で解除される）
    private readonly SerialDisposable _modelSubscription = new SerialDisposable();

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        _view = GetComponent<CS_UICrimeGaugeView>();
        _modelSubscription.AddTo(_disposables);
        _view.SetPresenter(this);
    }

    void OnEnable()
    {
        _parentTransform = transform.parent;

        CS_UICrimeGaugeModel.OnBound += HandleBound;
        CS_UICrimeGaugeModel.OnUnbound += HandleUnbound;

        // すでに Bind 済みの Model があれば即購読
        if (_parentTransform != null &&
            CS_UICrimeGaugeModel.TryGet(_parentTransform, out var model))
        {
            BindModel(model);
        }
    }

    void OnDisable()
    {
        CS_UICrimeGaugeModel.OnBound -= HandleBound;
        CS_UICrimeGaugeModel.OnUnbound -= HandleUnbound;
        UnbindModel();
    }

    // =========================================================
    // Model バインド管理
    // =========================================================

    private void HandleBound(Transform parentTransform, CS_UICrimeGaugeModel model)
    {
        if (parentTransform == _parentTransform) BindModel(model);
    }

    private void HandleUnbound(Transform parentTransform)
    {
        if (parentTransform == _parentTransform) UnbindModel();
    }

    /// <summary>Model を購読して View に反映する</summary>
    private void BindModel(CS_UICrimeGaugeModel model)
    {
        // currentValue・maxValue のどちらが変わっても View を更新する
        // （購読した瞬間に現在値が流れるので、初期表示もここで行われる）
        _modelSubscription.Disposable = model.currentValue
            .CombineLatest(model.maxValue, (value, max) => (value, max))
            .Subscribe(x => _view.UpdateGauge(x.value, x.max));
    }

    /// <summary>Model の購読をやめる</summary>
    private void UnbindModel()
    {
        _modelSubscription.Disposable = null;
    }
}
