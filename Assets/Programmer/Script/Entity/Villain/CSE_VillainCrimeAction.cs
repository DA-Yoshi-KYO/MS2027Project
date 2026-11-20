/*
 * 悪人が今している犯罪の動作(見た目用。CS_VillainCrimeが全クライアントに同期し、CS_VillainVisualがAnimatorに渡す)
 * 値はAnimatorのCrimeActionパラメータ(int)にそのまま渡す
 *
 * 制作者：　中出峻輔
 */

public enum CSE_VillainCrimeAction : byte
{
    None,      // 犯罪の動作をしていない(巡回・戦闘など)
    Kick,      // 蹴っている
    Tamper,    // いじっている
    PickUp,    // 貨物を拾っている
    Carry,     // 貨物を運んで歩いている
    PutDown,   // 貨物を車に置いている
}
