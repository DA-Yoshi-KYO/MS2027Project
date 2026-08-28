namespace StackPrBuilder.Models;

/// <summary>gh pr list --json ... の1件分。</summary>
public class PullRequestSummary
{
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string HeadRefName { get; set; } = "";
    public string BaseRefName { get; set; } = "";

    public override string ToString() => $"#{Number} {Title} ({HeadRefName} -> {BaseRefName})";
}
