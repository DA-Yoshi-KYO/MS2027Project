using System.Text.Json;
using System.Text.Json.Serialization;
using StackPrBuilder.Models;

namespace StackPrBuilder.Services;

/// <summary>
/// gh CLI経由でPR一覧を取得する。トークン管理は不要(既にgh auth loginで
/// 認証済みの資格情報をそのまま使う)。
/// </summary>
public class GitHubService
{
    private readonly CliRunner _cli;
    private readonly string _repoPath;
    private readonly string _ghPath;

    public GitHubService(CliRunner cli, string repoPath, string ghPath = "gh")
    {
        _cli = cli;
        _repoPath = repoPath;
        _ghPath = ghPath;
    }

    public async Task<List<PullRequestSummary>> ListOpenPullRequestsAsync()
    {
        var result = await _cli.RunAsync(
            _ghPath,
            "pr list --state open --json number,title,headRefName,baseRefName --limit 100",
            _repoPath);

        if (!result.Success)
            throw new InvalidOperationException($"gh pr list に失敗しました:\n{result.StdErr}");

        var items = JsonSerializer.Deserialize<List<PrJson>>(result.StdOut, JsonOptions) ?? new();
        return items.Select(i => new PullRequestSummary
        {
            Number = i.Number,
            Title = i.Title,
            HeadRefName = i.HeadRefName,
            BaseRefName = i.BaseRefName,
        }).ToList();
    }

    /// <summary>
    /// 今ログインしているGitHubアカウントの、このリポジトリに対する権限を返す。
    /// ("ADMIN" | "MAINTAIN" | "WRITE" | "TRIAGE" | "READ" | 取得失敗時は "UNKNOWN")
    /// </summary>
    public async Task<string> GetViewerPermissionAsync()
    {
        var result = await _cli.RunAsync(_ghPath, "repo view --json viewerPermission -q .viewerPermission", _repoPath);
        if (!result.Success)
            return "UNKNOWN";

        var permission = result.StdOut.Trim();
        return string.IsNullOrEmpty(permission) ? "UNKNOWN" : permission;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private class PrJson
    {
        [JsonPropertyName("number")] public int Number { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("headRefName")] public string HeadRefName { get; set; } = "";
        [JsonPropertyName("baseRefName")] public string BaseRefName { get; set; } = "";
    }
}
