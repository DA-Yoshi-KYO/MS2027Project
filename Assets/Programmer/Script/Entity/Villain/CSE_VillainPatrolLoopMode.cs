/*
 * 巡回パターンの、悪人1人分のルートの回り方
 * CSO_VillainPatrolPatternが持ち、CS_VillainPatrolと巡回エディタのプレビューが使う
 *
 * 制作者：　中出峻輔
 */

public enum CSE_VillainPatrolLoopMode : byte
{
    Loop,       // 周回: 最後のポイントの後、ポイント1へ戻る(1→2→…→最後→1→…)
    PingPong,   // 往復: 最後のポイントで折り返す(1→2→…→最後→…→2→1→…)
}
