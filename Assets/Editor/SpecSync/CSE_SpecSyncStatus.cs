/*
 * 仕様データ同期ツールで、1項目ごとの比較結果
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

public enum CSE_SpecSyncStatus
{
    Changed,    // 仕様とアセットの値が違う(適用できる)
    Same,       // 一致している
    NoNumber,   // 仕様のセルに数値が無い(例: ー(不死))
    Skipped,    // 対応表で同期しないと決めている列
    Unmapped,   // 対応表に書かれていない列
    Error,      // 表やアセット、項目が見つからない
}
