/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-25 | 初回作成
 * ================================================ */

using R3;
using System;

/// <summary>
/// リザルトへのカットシーンの状態を管理するModel
/// ・今の状態(CSE_ResultCutsceneState)だけを持つ
/// ・「始めて」(RequestCover / RequestReveal)と「終わった」(NotifyCovered / NotifyRevealed)を受け付ける
/// ・状態に合わない呼び出し(例: すでに動いている最中にもう一度 RequestCover)は無視する
/// ・Presenter / View のことは知らない
/// ・MonoBehaviourではないので、使用者(CS_ResultCutsceneController)が new して生成し、Dispose する
/// </summary>
public class CS_UIResultCutsceneModel : CS_BaseModel
{
    // =========================================================
    // 状態
    // =========================================================

    // 今の状態。値が変わった時に Presenter へ自動で知らせる
    private readonly ReactiveProperty<CSE_ResultCutsceneState> _state;

    /// <summary>今の状態(外には読み取り専用で公開)</summary>
    public ReadOnlyReactiveProperty<CSE_ResultCutsceneState> state => _state;

    /// <summary>画面を覆い終わった時の通知(インゲームでは、ここでシーン遷移する)</summary>
    public event Action OnCovered;

    /// <summary>画面を開き終わった時の通知(リザルト側の演出が終わった)</summary>
    public event Action OnRevealed;

    // =========================================================
    // 生成
    // =========================================================

    /// <param name="initialState">
    /// 最初の状態。
    /// インゲームは Hidden(画面の上の外から始める)、
    /// リザルトは Covered(画面を覆った状態から始める)
    /// </param>
    public CS_UIResultCutsceneModel(
        CSE_ResultCutsceneState initialState = CSE_ResultCutsceneState.Hidden)
    {
        _state = new ReactiveProperty<CSE_ResultCutsceneState>(initialState);
    }

    // =========================================================
    // 始める
    // =========================================================

    /// <summary>
    /// 上から下へ動かして画面を覆う。Hidden の時だけ有効
    /// 戻り値: 始められたか
    /// </summary>
    public bool RequestCover()
    {
        if (_state.Value != CSE_ResultCutsceneState.Hidden)
            return false;

        _state.Value = CSE_ResultCutsceneState.Covering;
        return true;
    }

    /// <summary>
    /// G2を左へ、G3を右へ動かして画面を開く。Covered の時だけ有効
    /// 戻り値: 始められたか
    /// </summary>
    public bool RequestReveal()
    {
        if (_state.Value != CSE_ResultCutsceneState.Covered)
            return false;

        _state.Value = CSE_ResultCutsceneState.Revealing;
        return true;
    }

    // =========================================================
    // 終わった(Presenter が、Viewの動きが終わった時に呼ぶ)
    // =========================================================

    public void NotifyCovered()
    {
        if (_state.Value != CSE_ResultCutsceneState.Covering)
            return;

        _state.Value = CSE_ResultCutsceneState.Covered;
        OnCovered?.Invoke();
    }

    public void NotifyRevealed()
    {
        if (_state.Value != CSE_ResultCutsceneState.Revealing)
            return;

        _state.Value = CSE_ResultCutsceneState.Revealed;
        OnRevealed?.Invoke();
    }

    // =========================================================
    // 破棄
    // =========================================================

    public override void Dispose()
    {
        OnCovered = null;
        OnRevealed = null;
        _state.Dispose();
    }
}
