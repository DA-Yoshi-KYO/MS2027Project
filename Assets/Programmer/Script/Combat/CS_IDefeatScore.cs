/*
 * 倒された時にプレイヤーへ入る点数(撃破スコア)を持つもの(悪人など)が実装するインターフェース
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・プレイヤーが相手を倒した時、CS_PlayerScoringが相手のGameObject(親を含む)からこれを探して点数を読む
 *   見つからなければ、CS_PlayerScoringに設定された既定の点数を使う
 * ・悪人の種類ごとに点数が違う(データ表: A 100 / B 150)ので、悪人側で種類ごとの値を返す想定
 *     public class CS_VillainStats : NetworkBehaviour, IDefeatScore
 *     {
 *         public int defeatScore => _baseStats.defeatScore;
 *     }
 * ・暗躍ボーナスの倍率はプレイヤー側(CS_PlayerScoring)で掛けるので、ここは倍率を掛ける前の点数を返す
 */
// ========================================

public interface IDefeatScore
{
    // 倒された時の基本の点数
    int defeatScore { get; }
}
