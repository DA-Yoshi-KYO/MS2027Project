/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-19 | 初回作成
 * ================================================ */

// ========================================
/*
 * メモ
 * ・部屋に入るシーンで選んだ遊び方を、ゲームシーンまで持ち越すためのクラス
 * ・シーンをまたいで残るようにstaticで持つ
 * ・何も選ばずにゲームシーンを直接再生した時は、これまで通りマルチとして扱う
 */
// ========================================
public static class CS_GameModeStore
{
    private static CSE_GameMode _mode = CSE_GameMode.Multi;

    public static CSE_GameMode mode => _mode;
    public static bool isSolo => _mode == CSE_GameMode.Solo;

    public static void SetMode(CSE_GameMode mode)
    {
        _mode = mode;
    }
}
