using LibGit2Sharp;
using StackPrBuilder.Models;

namespace StackPrBuilder.Services;

/// <summary>
/// LibGit2Sharpによるローカルリポジトリの読み取り・ブランチ作成。
/// push や gh コマンドはCliRunner側(git.exe / gh.exe)に任せ、
/// 既存の資格情報(Git Credential Manager)をそのまま使う。
/// </summary>
public class GitService
{
    private readonly string _repoPath;

    public GitService(string repoPath)
    {
        _repoPath = repoPath;
    }

    public IReadOnlyList<string> GetLocalBranchNames()
    {
        using var repo = new Repository(_repoPath);
        return repo.Branches
            .Where(b => !b.IsRemote)
            .Select(b => b.FriendlyName)
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>
    /// ローカルブランチ + リモート追跡ブランチ(origin/xxx等、HEADを除く)の両方を返す。
    /// 分割対象ブランチの選択候補には、まだローカルにチェックアウトしていない
    /// リモートブランチも含めたいのでこちらを使う。
    /// </summary>
    public IReadOnlyList<string> GetAllBranchNames()
    {
        using var repo = new Repository(_repoPath);
        return repo.Branches
            .Where(b => !(b.IsRemote && b.FriendlyName.EndsWith("/HEAD", StringComparison.Ordinal)))
            .Select(b => b.FriendlyName)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>
    /// sourceBranch にあって baseBranch には無いコミットを、古い→新しい順で返す。
    /// baseBranchはローカルブランチが無ければ origin/<baseBranch> にフォールバックする。
    /// </summary>
    public List<CommitInfo> GetCommitsAheadOfBase(string baseBranch, string sourceBranch)
    {
        using var repo = new Repository(_repoPath);

        var baseTip = ResolveTip(repo, baseBranch)
            ?? throw new InvalidOperationException($"base branchが見つかりません: {baseBranch}");
        var sourceTip = ResolveTip(repo, sourceBranch)
            ?? throw new InvalidOperationException($"対象branchが見つかりません: {sourceBranch}");

        var filter = new CommitFilter
        {
            IncludeReachableFrom = sourceTip,
            ExcludeReachableFrom = baseTip,
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse,
        };

        return repo.Commits.QueryBy(filter)
            .Select(c => new CommitInfo
            {
                Sha = c.Sha,
                MessageShort = c.MessageShort,
                Author = c.Author.Name,
                When = c.Author.When.LocalDateTime,
            })
            .ToList();
    }

    private static Commit? ResolveTip(Repository repo, string branchName)
    {
        return repo.Branches[branchName]?.Tip
            ?? repo.Branches[$"origin/{branchName}"]?.Tip;
    }

    /// <summary>
    /// 指定コミットを指すローカルブランチを作成する。
    /// 途中で失敗した処理を同じボタンで再開できるよう、既に同じコミットを指す
    /// ブランチが存在する場合は何もせず true(既存を再利用)を返す。
    /// 既存ブランチが別のコミットを指している場合は、意図しない上書きを避けるため例外にする。
    /// </summary>
    /// <returns>true: 既存ブランチをそのまま再利用した / false: 新規作成した</returns>
    public bool CreateBranchAt(string branchName, string commitSha)
    {
        using var repo = new Repository(_repoPath);

        var existing = repo.Branches[branchName];
        if (existing is not null)
        {
            if (existing.Tip.Sha == commitSha)
                return true; // 既に目的のコミットを指している → 再利用してスキップ

            throw new InvalidOperationException(
                $"ブランチが既に別のコミットを指して存在します: {branchName} " +
                $"(現在: {existing.Tip.Sha[..7]}, 目的: {commitSha[..7]})。手動で確認してください。");
        }

        var commit = repo.Lookup<Commit>(commitSha)
            ?? throw new InvalidOperationException($"コミットが見つかりません: {commitSha}");

        repo.CreateBranch(branchName, commit);
        return false;
    }

    /// <summary>
    /// newOrder の順にコミットを baseBranch の上へ cherry-pick で積み直す。
    /// 一時的に作業ツリーを detached HEAD @ baseBranch へ切り替えるため、
    /// 未コミットの変更がある場合は実行前に失敗させる。処理後は元のHEADへ必ず戻す。
    /// コンフリクトが起きた場合はそこで中断し、状態を元に戻したうえでエラーを返す。
    /// </summary>
    public ReorderResult ReorderAndRewrite(string baseBranch, IReadOnlyList<CommitInfo> newOrder)
    {
        using var repo = new Repository(_repoPath);
        var result = new ReorderResult();

        if (repo.RetrieveStatus().IsDirty)
        {
            result.Success = false;
            result.ErrorMessage = "作業ツリーに未コミットの変更があります。先にコミットまたはstashしてください。";
            return result;
        }

        var baseTip = repo.Branches[baseBranch]?.Tip
            ?? repo.Branches[$"origin/{baseBranch}"]?.Tip
            ?? throw new InvalidOperationException($"base branchが見つかりません: {baseBranch}");

        var headWasDetached = repo.Info.IsHeadDetached;
        var restoreTarget = headWasDetached ? repo.Head.Tip.Sha : repo.Head.FriendlyName;

        try
        {
            Commands.Checkout(repo, baseTip.Sha);

            var committer = repo.Config.BuildSignature(DateTimeOffset.Now)
                ?? new Signature("StackPrBuilder", "stackprbuilder@local", DateTimeOffset.Now);

            foreach (var commitInfo in newOrder)
            {
                var original = repo.Lookup<Commit>(commitInfo.Sha)
                    ?? throw new InvalidOperationException($"コミットが見つかりません: {commitInfo.Sha}");

                var cherryPickResult = repo.CherryPick(original, committer);

                if (cherryPickResult.Status == CherryPickStatus.Conflicts)
                {
                    result.Success = false;
                    result.ErrorMessage =
                        $"コミット {commitInfo.ShortSha} ({commitInfo.MessageShort}) の適用中にコンフリクトが発生したため、" +
                        "この並び替えは自動処理できません。並び順を変更するか、手動でrebaseしてください。";
                    return result;
                }

                var newTip = repo.Head.Tip;
                result.NewCommits.Add(new CommitInfo
                {
                    Sha = newTip.Sha,
                    MessageShort = newTip.MessageShort,
                    Author = newTip.Author.Name,
                    When = newTip.Author.When.LocalDateTime,
                    GroupNumber = commitInfo.GroupNumber,
                });
            }

            result.Success = true;
            return result;
        }
        finally
        {
            // conflict等で汚れたindex/working treeを掃除してから元の状態へ戻す。
            try
            {
                repo.Reset(ResetMode.Hard, repo.Head.Tip);
            }
            catch
            {
                // ignore cleanup failure; checkoutで上書きされる
            }

            Commands.Checkout(repo, restoreTarget);
        }
    }
}
