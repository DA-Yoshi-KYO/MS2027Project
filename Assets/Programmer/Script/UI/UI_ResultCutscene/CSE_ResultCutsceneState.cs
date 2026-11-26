/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-25 | 初回作成
 * ================================================ */

/// <summary>
/// リザルトへのカットシーンの状態
/// ・インゲーム側: Hidden → Covering → Covered(ここでシーン遷移する)
/// ・リザルト側  : Covered → Revealing → Revealed
/// </summary>
public enum CSE_ResultCutsceneState
{
    /// <summary>画像が画面の上の外にある(まだ見えない)</summary>
    Hidden,

    /// <summary>画像が上から下へ動いている最中</summary>
    Covering,

    /// <summary>G1が画面の下の外へ出て、G2とG3が画面を覆っている</summary>
    Covered,

    /// <summary>G2が左へ、G3が右へ動いている最中</summary>
    Revealing,

    /// <summary>G2とG3が画面の左右の外へ出て、リザルトが見えている</summary>
    Revealed,
}
