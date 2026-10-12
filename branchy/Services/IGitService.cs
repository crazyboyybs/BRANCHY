using Branchy.Models;

namespace Branchy.Services;

/// <summary>Defines the Git operations used by the application.</summary>
public interface IGitService
{
    /// <summary>Finds the root repository that contains a path.</summary>
    Task<string> FindRepositoryRootAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Initializes a Git repository in an existing folder.</summary>
    Task InitializeRepositoryAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Clones a local or remote repository into a new working directory.</summary>
    Task<string> CloneRepositoryAsync(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the current branch and working tree state.</summary>
    Task<GitRepositorySnapshot> GetSnapshotAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Reads the local branches and their configured upstreams.</summary>
    Task<IReadOnlyList<GitBranch>> GetBranchesAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Reads configured remotes and their fetch and push URLs.</summary>
    Task<IReadOnlyList<GitRemote>> GetRemotesAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Adds a remote with optional separate push URL.</summary>
    Task AddRemoteAsync(string repositoryPath, string name, string fetchUrl, string? pushUrl, CancellationToken cancellationToken = default);

    /// <summary>Renames a remote and updates its transfer URLs.</summary>
    Task UpdateRemoteAsync(string repositoryPath, string currentName, string newName, string fetchUrl, string? pushUrl, CancellationToken cancellationToken = default);

    /// <summary>Removes a remote and its remote-tracking references.</summary>
    Task RemoveRemoteAsync(string repositoryPath, string name, CancellationToken cancellationToken = default);

    /// <summary>Fetches and prunes one configured remote.</summary>
    Task FetchRemoteAsync(string repositoryPath, string name, CancellationToken cancellationToken = default);

    /// <summary>Reads tracked, available, and ignored working-tree files.</summary>
    Task<IReadOnlyList<GitRepositoryFile>> GetRepositoryFilesAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Updates only the BRANCHY-managed section of the root .gitignore.</summary>
    Task UpdateManagedGitIgnoreAsync(string repositoryPath, IReadOnlyCollection<string> includedPaths, CancellationToken cancellationToken = default);

    /// <summary>Reads recent commits that can be exported.</summary>
    Task<IReadOnlyList<GitCommit>> GetCommitsAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Reads commits from all local references for the visual history.</summary>
    Task<IReadOnlyList<GitHistoryEntry>> GetHistoryAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default);

    /// <summary>Finds commits whose introduced patch contains the supplied text.</summary>
    Task<IReadOnlySet<string>> FindHistoryCommitsByDiffAsync(
        string repositoryPath,
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>Finds commits reachable from branches whose names contain the supplied text.</summary>
    Task<IReadOnlySet<string>> FindHistoryCommitsByBranchAsync(
        string repositoryPath,
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the paths and patch introduced by one commit.</summary>
    Task<GitCommitDetails> GetCommitDetailsAsync(
        string repositoryPath,
        string commitHash,
        CancellationToken cancellationToken = default);

    /// <summary>Exports the complete tree of a commit to a ZIP archive.</summary>
    Task ExportCommitAsync(string repositoryPath, string commitHash, string outputPath, CancellationToken cancellationToken = default);

    /// <summary>Switches the working tree to an existing local branch.</summary>
    Task SwitchBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default);

    /// <summary>Creates and switches to a local branch from the current HEAD.</summary>
    Task CreateBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default);

    /// <summary>Renames a local branch.</summary>
    Task RenameBranchAsync(string repositoryPath, string currentName, string newName, CancellationToken cancellationToken = default);

    /// <summary>Deletes a fully merged local branch.</summary>
    Task DeleteBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default);

    /// <summary>Merges a local branch into the current branch, preserving conflicts for resolution.</summary>
    Task<GitOperationResult> MergeBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default);

    /// <summary>Reverts a commit, preserving conflicts for resolution.</summary>
    Task<GitOperationResult> RevertCommitAsync(string repositoryPath, string commitHash, CancellationToken cancellationToken = default);

    /// <summary>Reads all local tags and their target commits.</summary>
    Task<IReadOnlyList<GitTag>> GetTagsAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Creates a lightweight tag or an annotated tag when a message is supplied.</summary>
    Task CreateTagAsync(string repositoryPath, string name, string target, string? message, CancellationToken cancellationToken = default);

    /// <summary>Deletes a local tag.</summary>
    Task DeleteTagAsync(string repositoryPath, string name, CancellationToken cancellationToken = default);

    /// <summary>Reads the staged or working-tree diff for a path.</summary>
    Task<string> GetDiffAsync(string repositoryPath, GitFileChange change, bool staged, CancellationToken cancellationToken = default);

    /// <summary>Adds a path to the index.</summary>
    Task StageAsync(string repositoryPath, string path, CancellationToken cancellationToken = default);

    /// <summary>Adds all working-tree changes to the index.</summary>
    Task StageAllAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Reads independently selectable hunks from a tracked working-tree change.</summary>
    Task<GitPartialPatch> GetPartialStagePatchAsync(string repositoryPath, string path, CancellationToken cancellationToken = default);

    /// <summary>Applies selected working-tree hunks to the index.</summary>
    Task StagePatchAsync(string repositoryPath, string patch, CancellationToken cancellationToken = default);

    /// <summary>Restores a tracked working-tree path from the current index.</summary>
    Task DiscardWorkingTreeChangeAsync(string repositoryPath, string path, CancellationToken cancellationToken = default);

    /// <summary>Removes a path from the index while preserving the working tree.</summary>
    Task UnstageAsync(string repositoryPath, string path, bool hasHead, CancellationToken cancellationToken = default);

    /// <summary>Removes every path from the index while preserving the working tree.</summary>
    Task UnstageAllAsync(string repositoryPath, bool hasHead, CancellationToken cancellationToken = default);

    /// <summary>Creates a commit using the provided message.</summary>
    Task CommitAsync(string repositoryPath, string message, CancellationToken cancellationToken = default);

    /// <summary>Adds the staged changes to an existing commit in the current history.</summary>
    Task AmendCommitAsync(string repositoryPath, string commitHash, string message, CancellationToken cancellationToken = default);

    /// <summary>Rewrites only an existing commit message without including staged changes.</summary>
    Task AmendCommitMessageAsync(string repositoryPath, string commitHash, string message, CancellationToken cancellationToken = default);

    /// <summary>Fetches and prunes configured remotes.</summary>
    Task FetchAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Fast-forwards the current branch from its configured upstream.</summary>
    Task PullAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Pushes the current branch using its configured upstream.</summary>
    Task PushAsync(string repositoryPath, CancellationToken cancellationToken = default);
}
