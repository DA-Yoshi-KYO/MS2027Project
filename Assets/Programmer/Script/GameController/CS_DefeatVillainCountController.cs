/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 倒した悪人の数の Controller
/// ・GameControllerManager にアタッチする
/// ・CS_UIDefeatVillainCountModel を生成・保持・破棄する
/// ・CS_UIDefeatVillainCountPresenter は UICanvas 配下の View と同じ GameObject から取得する
/// ・外部システム（敵死亡スクリプト等）から IncrementCount() を呼ぶ
/// </summary>
public class CS_DefeatVillainCountController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("UICanvas 配下の DefeatVillainCount を直接セット")]
    [SerializeField] private CS_UIDefeatVillainCountView _view;

    [Header("撃破カウント設定")]
    [SerializeField] private int _maxCount = 10; // 目標撃破数（可変）

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIDefeatVillainCountModel _model;
    private CS_UIDefeatVillainCountPresenter _presenter;

    // =========================================================
    // Awake : Model 生成
    // =========================================================

    private void Awake()
    {
        // 0 からスタート
        _model = new CS_UIDefeatVillainCountModel(_maxCount);
    }

    // =========================================================
    // Start : Presenter 取得 → MVP 組み立て
    // =========================================================

    private void Start()
    {
        if (_view == null)
        {
            Debug.LogError("[CS_DefeatVillainCountController] _view が未設定です！" +
                           " UICanvas 配下の CS_UIDefeatVillainCountView を Inspector でセットしてください。");
            return;
        }

        // Presenter は View と同じ GameObject から取得
        _presenter = _view.GetComponent<CS_UIDefeatVillainCountPresenter>();

        if (_presenter == null)
        {
            Debug.LogError("[CS_DefeatVillainCountController] CS_UIDefeatVillainCountPresenter が付いていません！");
            return;
        }

        // Model を Presenter に渡して購読開始
        _presenter.BindModel(_model);
    }

    // =========================================================
    // 外部 API（敵死亡スクリプトから呼ぶ）
    // =========================================================

    /// <summary>悪人を1人倒したときに呼ぶ</summary>
    public void IncrementCount()
    {
        _model.IncrementCount();

        if (_model.IsCompleted)
        {
            Debug.Log("[CS_DefeatVillainCountController] 目標達成！");
            OnCompleted();
        }
    }

    /// <summary>撃破数を直接セットする</summary>
    public void SetCount(int count) => _model.SetCount(count);

    /// <summary>目標撃破数を変更する（可変対応）</summary>
    public void SetMaxCount(int maxCount) => _model.SetMaxCount(maxCount);

    /// <summary>カウントをリセットする</summary>
    public void ResetCount() => _model.Reset();

    // =========================================================
    // 目標達成時の処理
    // =========================================================

    /// <summary>
    /// 目標達成時に呼ばれる。
    /// 必要に応じてオーバーライドまたは外部から処理を追加する。
    /// </summary>
    protected virtual void OnCompleted()
    {
        // 例：クリア演出・次のステージ移行等をここに追加
    }

    // =========================================================
    // OnDestroy : Model を破棄
    // =========================================================

    private void OnDestroy()
    {
        _model?.Dispose();
    }
}
