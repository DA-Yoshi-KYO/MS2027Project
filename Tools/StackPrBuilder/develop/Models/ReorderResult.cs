namespace StackPrBuilder.Models;

public class ReorderResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<CommitInfo> NewCommits { get; } = new();
}
