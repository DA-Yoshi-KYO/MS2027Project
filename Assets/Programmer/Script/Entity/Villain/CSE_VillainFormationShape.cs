/*
 * 巡回の隊列の形(先頭に付いてくる人の並び方)
 * CSO_VillainPatrolPatternが持ち、CSO_VillainPatrolPattern.GetFormationOffsetで位置を求める
 *
 * 制作者：　中出峻輔
 */

public enum CSE_VillainFormationShape : byte
{
    Line,            // 横一列: 先頭の右・左・右…と横に並ぶ
    Column,          // 縦一列: 先頭の後ろに1列に並ぶ
    DiagonalRight,   // 斜め右: 先頭の右後ろへ斜めに並ぶ
    DiagonalLeft,    // 斜め左: 先頭の左後ろへ斜めに並ぶ
    Wedge,           // V字: 先頭の右後ろ・左後ろ…と、先頭を頂点にV字に並ぶ
}
