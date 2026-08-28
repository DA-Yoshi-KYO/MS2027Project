namespace StackPrBuilder.Models;

/// <summary>
/// スタックの1レイヤー(=1本のブランチ=1つのPRになる予定のもの)。
/// ComputeLayers() でコミットのGroupNumberから機械的に組み立てられる。
/// </summary>
public class StackLayerPlan
{
    /// <summary>
    /// このリポジトリのRuleset(DevelopBranchRule)で作成が許可されているブランチ名は
    /// "stacked/**" のみのため、プレフィックスは固定にしてUI上では編集できないようにする。
    /// </summary>
    public const string BranchPrefix = "stacked/";

    public int GroupNumber { get; set; }

    /// <summary>
    /// 大要素: このスタック全体が何についての分割か(例: "PlayerStatus")。
    /// 通常はスタック内の全レイヤーで共通の値になる。
    /// </summary>
    public string MajorElement { get; set; } = "";

    /// <summary>要素: そのレイヤー固有の内容(例: "BaseCreate")。</summary>
    public string Element { get; set; } = "";

    /// <summary>実際に作成するブランチ名 ("stacked/" + 大要素 + "/" + 要素)。</summary>
    public string BranchName => $"{BranchPrefix}{MajorElement}/{Element}";

    /// <summary>このレイヤーに属するコミット(履歴順)。</summary>
    public List<CommitInfo> Commits { get; } = new();

    /// <summary>このレイヤーの先頭に立てるブランチが指す先、つまり一番新しいコミットのSHA。</summary>
    public string HeadCommitSha => Commits.Count > 0 ? Commits[^1].Sha : "";

    public string Summary =>
        Commits.Count == 0
            ? "(コミットなし)"
            : $"{Commits.Count}コミット ({Commits[0].ShortSha}..{Commits[^1].ShortSha})";
}
