/*
 * プレイヤーが設定画面で変えられる音量の種類
 * CS_SoundVolumeController の GetVolume / SetVolume で使う
 *
 * 制作者：　中出峻輔
 */
public enum CSE_SoundVolumeType
{
    Master, // 全体
    Bgm,    // BGM(ジングルもこれに合わせる)
    Se,     // SE
}
