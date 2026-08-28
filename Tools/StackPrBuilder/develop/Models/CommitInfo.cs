namespace StackPrBuilder.Models;

/// <summary>
/// 分割対象ブランチの1コミットを表す。GroupNumber はUI上でユーザーが割り当てる
/// 「どのレイヤー(StackPRのどのPR)に属するか」の番号。
/// コミットは履歴順(古い→新しい)で並んでいる前提で、GroupNumberは非減少である必要がある。
/// </summary>
public class CommitInfo
{
    public string Sha { get; set; } = "";
    public string ShortSha => Sha.Length >= 7 ? Sha[..7] : Sha;
    public string MessageShort { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime When { get; set; }

    /// <summary>1始まりのレイヤー番号。DataGridで直接編集する。</summary>
    public int GroupNumber { get; set; } = 1;
}
