namespace StackPrBuilder.Models;

public class PrerequisiteCheckResult
{
    public List<string> Problems { get; } = new();
    public bool AllOk => Problems.Count == 0;
}
