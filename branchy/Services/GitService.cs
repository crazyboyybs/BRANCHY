using System.Diagnostics;
using System.Text;
using Branchy.Models;

namespace Branchy.Services;

/// <summary>Executes the installed Git client and parses its stable porcelain output.</summary>
public sealed class GitService(ILocalizationService localizer) : IGitService
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private const string ManagedIgnoreBegin = "# BEGIN BRANCHY managed files";
    private const string ManagedIgnoreEnd = "# END BRANCHY managed files";
    private const string LegacyManagedIgnoreBegin = "# BEGIN Git GUI WinUI managed files";
    private const string LegacyManagedIgnoreEnd = "# END Git GUI WinUI managed files";

    /// <inheritdoc />
    public async Task<string> FindRepositoryRootAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            path,
            ["rev-parse", "--show-toplevel"],
            cancellationToken: cancellationToken);

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new GitRepositoryNotFoundException(localizer.GetString("Error_NotGitRepository"));
        }

        return result.StandardOutput.Trim();
    }

    /// <inheritdoc />
    public async Task InitializeRepositoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            path,
            ["init"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task<string> CloneRepositoryAsync(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException(localizer.GetString("Error_CloneSourceRequired"));
        }

        string destination = Path.GetFullPath(destinationPath);
        string? parentPath = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(parentPath) || !Directory.Exists(parentPath))
        {
            throw new DirectoryNotFoundException(
                localizer.GetString("Error_CloneDestinationMissing"));
        }

        if (File.Exists(destination) ||
            (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any()))
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_CloneDestinationNotEmpty"));
        }

        GitCommandResult result = await ExecuteAsync(
            parentPath,
            ["clone", "--progress", "--", source.Trim(), destination],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
        return await FindRepositoryRootAsync(destination, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GitRepositorySnapshot> GetSnapshotAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["status", "--porcelain=v2", "--branch", "-z", "--untracked-files=all"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);

        string branchName = string.Empty;
        string headOid = string.Empty;
        string? upstream = null;
        int ahead = 0;
        int behind = 0;
        var changes = new List<GitFileChange>();
        string[] records = result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index < records.Length; index++)
        {
            string record = records[index];
            if (record.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                branchName = record[14..];
                if (branchName == "(detached)")
                {
                    branchName = string.Empty;
                }
            }
            else if (record.StartsWith("# branch.oid ", StringComparison.Ordinal))
            {
                headOid = record[13..];
            }
            else if (record.StartsWith("# branch.upstream ", StringComparison.Ordinal))
            {
                upstream = record[18..];
            }
            else if (record.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                ParseAheadBehind(record[12..], out ahead, out behind);
            }
            else if (record.StartsWith("1 ", StringComparison.Ordinal))
            {
                string[] fields = record.Split(' ', 9, StringSplitOptions.None);
                if (fields.Length == 9)
                {
                    changes.Add(CreateChange(fields[1], fields[8]));
                }
            }
            else if (record.StartsWith("2 ", StringComparison.Ordinal))
            {
                string[] fields = record.Split(' ', 10, StringSplitOptions.None);
                if (fields.Length == 10)
                {
                    string? originalPath = index + 1 < records.Length ? records[++index] : null;
                    changes.Add(CreateChange(fields[1], fields[9], originalPath));
                }
            }
            else if (record.StartsWith("u ", StringComparison.Ordinal))
            {
                string[] fields = record.Split(' ', 11, StringSplitOptions.None);
                if (fields.Length == 11)
                {
                    changes.Add(CreateChange(fields[1], fields[10]));
                }
            }
            else if (record.StartsWith("? ", StringComparison.Ordinal))
            {
                changes.Add(new GitFileChange(record[2..], null, '?', '?'));
            }
        }

        var root = new DirectoryInfo(repositoryPath);
        return new GitRepositorySnapshot(
            repositoryPath,
            root.Name,
            branchName,
            headOid,
            upstream,
            ahead,
            behind,
            changes);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitBranch>> GetBranchesAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            [
                "for-each-ref",
                "--sort=refname",
                "--format=%(refname:short)%00%(HEAD)%00%(upstream:short)",
                "refs/heads",
            ],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);

        var branches = new List<GitBranch>();
        foreach (string record in result.StandardOutput.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = record.Split('\0');
            if (fields.Length >= 3 && !string.IsNullOrWhiteSpace(fields[0]))
            {
                branches.Add(new GitBranch(
                    fields[0],
                    fields[1] == "*",
                    string.IsNullOrWhiteSpace(fields[2]) ? null : fields[2]));
            }
        }

        return branches;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitRemote>> GetRemotesAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult namesResult = await ExecuteAsync(
            repositoryPath,
            ["remote"],
            cancellationToken: cancellationToken);
        EnsureSuccess(namesResult);

        var remotes = new List<GitRemote>();
        foreach (string name in namesResult.StandardOutput.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            GitCommandResult fetchResult = await ExecuteAsync(
                repositoryPath,
                ["remote", "get-url", "--", name],
                cancellationToken: cancellationToken);
            EnsureSuccess(fetchResult);

            GitCommandResult pushResult = await ExecuteAsync(
                repositoryPath,
                ["config", "--get", $"remote.{name}.pushurl"],
                cancellationToken: cancellationToken);
            string? pushUrl = pushResult.IsSuccess
                ? pushResult.StandardOutput.Trim()
                : null;
            remotes.Add(new GitRemote(
                name,
                fetchResult.StandardOutput.Trim(),
                string.IsNullOrWhiteSpace(pushUrl) ? null : pushUrl));
        }

        return remotes.OrderBy(remote => remote.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <inheritdoc />
    public async Task AddRemoteAsync(
        string repositoryPath,
        string name,
        string fetchUrl,
        string? pushUrl,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = await ValidateRemoteNameAsync(repositoryPath, name, cancellationToken);
        string normalizedFetchUrl = ValidateRemoteUrl(fetchUrl);
        string? normalizedPushUrl = NormalizePushUrl(normalizedFetchUrl, pushUrl);

        GitCommandResult addResult = await ExecuteAsync(
            repositoryPath,
            ["remote", "add", "--", normalizedName, normalizedFetchUrl],
            cancellationToken: cancellationToken);
        EnsureSuccess(addResult);

        try
        {
            await SetRemotePushUrlAsync(
                repositoryPath,
                normalizedName,
                normalizedPushUrl,
                cancellationToken);
        }
        catch
        {
            await ExecuteAsync(
                repositoryPath,
                ["remote", "remove", "--", normalizedName],
                cancellationToken: CancellationToken.None);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateRemoteAsync(
        string repositoryPath,
        string currentName,
        string newName,
        string fetchUrl,
        string? pushUrl,
        CancellationToken cancellationToken = default)
    {
        string normalizedCurrentName = currentName.Trim();
        string normalizedNewName = await ValidateRemoteNameAsync(repositoryPath, newName, cancellationToken);
        string normalizedFetchUrl = ValidateRemoteUrl(fetchUrl);
        string? normalizedPushUrl = NormalizePushUrl(normalizedFetchUrl, pushUrl);
        bool renamed = !string.Equals(
            normalizedCurrentName,
            normalizedNewName,
            StringComparison.Ordinal);

        if (renamed)
        {
            GitCommandResult renameResult = await ExecuteAsync(
                repositoryPath,
                ["remote", "rename", "--", normalizedCurrentName, normalizedNewName],
                cancellationToken: cancellationToken);
            EnsureSuccess(renameResult);
        }

        GitCommandResult urlResult = await ExecuteAsync(
            repositoryPath,
            ["remote", "set-url", "--", normalizedNewName, normalizedFetchUrl],
            cancellationToken: cancellationToken);
        EnsureSuccess(urlResult);
        await SetRemotePushUrlAsync(
            repositoryPath,
            normalizedNewName,
            normalizedPushUrl,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveRemoteAsync(
        string repositoryPath,
        string name,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["remote", "remove", "--", name],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task FetchRemoteAsync(
        string repositoryPath,
        string name,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["fetch", "--prune", "--", name],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitRepositoryFile>> GetRepositoryFilesAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> tracked = await ReadNullSeparatedPathsAsync(
            repositoryPath,
            ["ls-files", "-z", "--cached"],
            cancellationToken);
        IReadOnlyList<string> available = await ReadNullSeparatedPathsAsync(
            repositoryPath,
            ["ls-files", "-z", "--others", "--exclude-standard"],
            cancellationToken);
        IReadOnlyList<string> ignored = await ReadNullSeparatedPathsAsync(
            repositoryPath,
            ["ls-files", "-z", "--others", "--ignored", "--exclude-standard"],
            cancellationToken);

        var files = new Dictionary<string, GitRepositoryFile>(StringComparer.Ordinal);
        foreach (string path in tracked)
        {
            files[path] = new GitRepositoryFile(path, IsTracked: true, IsIgnored: false);
        }

        foreach (string path in available)
        {
            files.TryAdd(path, new GitRepositoryFile(path, IsTracked: false, IsIgnored: false));
        }

        foreach (string path in ignored)
        {
            files.TryAdd(path, new GitRepositoryFile(path, IsTracked: false, IsIgnored: true));
        }

        files.TryAdd(".gitignore", new GitRepositoryFile(".gitignore", IsTracked: false, IsIgnored: false));
        return files.Values
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task UpdateManagedGitIgnoreAsync(
        string repositoryPath,
        IReadOnlyCollection<string> includedPaths,
        CancellationToken cancellationToken = default)
    {
        string ignorePath = Path.Combine(repositoryPath, ".gitignore");
        string existing = File.Exists(ignorePath)
            ? await File.ReadAllTextAsync(ignorePath, cancellationToken)
            : string.Empty;
        string newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string preserved = RemoveManagedIgnoreBlock(existing).TrimEnd('\r', '\n');

        string[] selected = includedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeGitPath)
            .Where(path => path != ".gitignore")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var directories = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in selected)
        {
            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int length = 1; length < segments.Length; length++)
            {
                directories.Add(string.Join('/', segments.Take(length)));
            }
        }

        var managedLines = new List<string>
        {
            ManagedIgnoreBegin,
            "/*",
            "!/.gitignore",
        };
        foreach (string directory in directories
                     .OrderBy(path => path.Count(character => character == '/'))
                     .ThenBy(path => path, StringComparer.Ordinal))
        {
            string escaped = EscapeIgnorePath(directory);
            managedLines.Add($"!/{escaped}/");
            managedLines.Add($"/{escaped}/*");
        }

        managedLines.AddRange(selected.Select(path => $"!/{EscapeIgnorePath(path)}"));
        managedLines.Add(ManagedIgnoreEnd);

        string managed = string.Join(newline, managedLines);
        string contents = string.IsNullOrEmpty(preserved)
            ? managed + newline
            : preserved + newline + newline + managed + newline;
        await File.WriteAllTextAsync(ignorePath, contents, Utf8WithoutBom, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitCommit>> GetCommitsAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult head = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", "--quiet", "HEAD"],
            cancellationToken: cancellationToken);
        if (!head.IsSuccess)
        {
            return [];
        }

        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["log", "-n", "200", "--date=iso-strict", "--pretty=format:%H%x1f%h%x1f%an%x1f%aI%x1f%s%x1f%B%x1e"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);

        var commits = new List<GitCommit>();
        foreach (string record in result.StandardOutput.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = record.TrimStart('\r', '\n').Split('\x1f');
            if (fields.Length >= 6 &&
                DateTimeOffset.TryParse(fields[3], out DateTimeOffset authoredAt))
            {
                commits.Add(new GitCommit(
                    fields[0],
                    fields[1],
                    fields[2],
                    authoredAt,
                    NormalizeSubject(fields[4]),
                    fields[5].TrimEnd('\r', '\n')));
            }
        }

        return commits;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHistoryEntry>> GetHistoryAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult firstCommit = await ExecuteAsync(
            repositoryPath,
            ["rev-list", "--all", "--max-count=1"],
            cancellationToken: cancellationToken);
        EnsureSuccess(firstCommit);
        if (string.IsNullOrWhiteSpace(firstCommit.StandardOutput))
        {
            return [];
        }

        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            [
                "log",
                "--all",
                "--topo-order",
                "--no-ext-diff",
                "--no-textconv",
                "-n",
                "300",
                "--decorate=short",
                "--date=iso-strict",
                "--pretty=format:%H%x1f%h%x1f%P%x1f%D%x1f%an%x1f%ae%x1f%aI%x1f%s%x1f%B%x1e",
            ],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);

        var history = new List<GitHistoryEntry>();
        foreach (string record in result.StandardOutput.Split(
                     '\x1e',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = record.TrimStart('\r', '\n').Split('\x1f');
            if (fields.Length < 9 ||
                !DateTimeOffset.TryParse(fields[6], out DateTimeOffset authoredAt))
            {
                continue;
            }

            history.Add(new GitHistoryEntry(
                fields[0],
                fields[1],
                fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries),
                fields[3],
                fields[4],
                fields[5],
                authoredAt,
                NormalizeSubject(fields[7]),
                fields[8].TrimEnd('\r', '\n')));
        }

        return history;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> FindHistoryCommitsByDiffAsync(
        string repositoryPath,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            [
                "log",
                "--all",
                "--topo-order",
                "--no-ext-diff",
                "--no-textconv",
                "-n",
                "300",
                "--format=%H",
                "--extended-regexp",
                $"-G{EscapeGitExtendedRegex(query.Trim())}",
            ],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
        return ParseCommitHashes(result.StandardOutput);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> FindHistoryCommitsByBranchAsync(
        string repositoryPath,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        GitCommandResult referencesResult = await ExecuteAsync(
            repositoryPath,
            [
                "for-each-ref",
                "--format=%(refname)%00%(refname:short)",
                "refs/heads",
                "refs/remotes",
            ],
            cancellationToken: cancellationToken);
        EnsureSuccess(referencesResult);

        string searchText = query.Trim();
        var matchingReferences = new List<string>();
        foreach (string line in referencesResult.StandardOutput.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = line.Split('\0', 2);
            if (fields.Length == 2 &&
                !fields[0].EndsWith("/HEAD", StringComparison.OrdinalIgnoreCase) &&
                fields[1].Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
            {
                matchingReferences.Add(fields[0]);
            }
        }

        if (matchingReferences.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var arguments = new List<string>
        {
            "rev-list",
            "--topo-order",
            "--max-count=300",
        };
        arguments.AddRange(matchingReferences);
        GitCommandResult commitsResult = await ExecuteAsync(
            repositoryPath,
            arguments,
            cancellationToken: cancellationToken);
        EnsureSuccess(commitsResult);
        return ParseCommitHashes(commitsResult.StandardOutput);
    }

    /// <inheritdoc />
    public async Task<GitCommitDetails> GetCommitDetailsAsync(
        string repositoryPath,
        string commitHash,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult verified = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", $"{commitHash}^{{commit}}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(verified);
        string commit = verified.StandardOutput.Trim();

        IReadOnlyList<string> parents = await GetCommitParentsAsync(
            repositoryPath,
            commit,
            cancellationToken);
        IReadOnlyList<string> fileArguments = parents.Count == 0
            ? ["diff-tree", "--root", "--no-commit-id", "--name-status", "-r", "-z", "--find-renames", commit]
            : ["diff", "--name-status", "-z", "--find-renames", parents[0], commit, "--"];
        IReadOnlyList<string> diffArguments = parents.Count == 0
            ? ["show", "--format=", "--no-ext-diff", "--no-color", "--find-renames", "--root", commit, "--"]
            : ["diff", "--no-ext-diff", "--no-color", "--find-renames", parents[0], commit, "--"];

        GitCommandResult filesResult = await ExecuteAsync(
            repositoryPath,
            fileArguments,
            cancellationToken: cancellationToken);
        EnsureSuccess(filesResult);
        GitCommandResult diffResult = await ExecuteAsync(
            repositoryPath,
            diffArguments,
            cancellationToken: cancellationToken);
        EnsureSuccess(diffResult);

        return new GitCommitDetails(
            ParseCommitFiles(filesResult.StandardOutput),
            diffResult.StandardOutput);
    }

    /// <inheritdoc />
    public async Task ExportCommitAsync(
        string repositoryPath,
        string commitHash,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult verified = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", $"{commitHash}^{{commit}}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(verified);

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException(localizer.GetString("Error_ArchiveFolderMissing"));
        }

        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["archive", "--format=zip", $"--output={outputPath}", verified.StandardOutput.Trim()],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task SwitchBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        await ValidateBranchNameAsync(repositoryPath, branchName, cancellationToken);
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["switch", "--", branchName],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task CreateBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        await ValidateBranchNameAsync(repositoryPath, branchName, cancellationToken);
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["switch", "-c", branchName],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task RenameBranchAsync(
        string repositoryPath,
        string currentName,
        string newName,
        CancellationToken cancellationToken = default)
    {
        await ValidateBranchNameAsync(repositoryPath, currentName, cancellationToken);
        await ValidateBranchNameAsync(repositoryPath, newName, cancellationToken);
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["branch", "-m", currentName, newName],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task DeleteBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        await ValidateBranchNameAsync(repositoryPath, branchName, cancellationToken);
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["branch", "-d", branchName],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task<GitOperationResult> MergeBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        await ValidateBranchNameAsync(repositoryPath, branchName, cancellationToken);
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["merge", "--no-edit", "--", branchName],
            cancellationToken: cancellationToken);
        return await CompleteConflictAwareOperationAsync(
            repositoryPath,
            result,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GitOperationResult> RevertCommitAsync(
        string repositoryPath,
        string commitHash,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult verified = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", $"{commitHash}^{{commit}}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(verified);

        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["revert", "--no-edit", verified.StandardOutput.Trim()],
            cancellationToken: cancellationToken);
        return await CompleteConflictAwareOperationAsync(
            repositoryPath,
            result,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitTag>> GetTagsAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            [
                "for-each-ref",
                "--sort=refname",
                "--format=%(refname:short)%00%(objecttype)%00%(objectname)%00%(*objectname)%00%(subject)%00%(*subject)",
                "refs/tags",
            ],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);

        var tags = new List<GitTag>();
        foreach (string record in result.StandardOutput.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = record.Split('\0');
            if (fields.Length < 6 || string.IsNullOrWhiteSpace(fields[0]))
            {
                continue;
            }

            bool annotated = string.Equals(fields[1], "tag", StringComparison.Ordinal);
            string target = annotated && !string.IsNullOrWhiteSpace(fields[3])
                ? fields[3]
                : fields[2];
            string subject = annotated && !string.IsNullOrWhiteSpace(fields[5])
                ? fields[5]
                : fields[4];
            tags.Add(new GitTag(fields[0], target, NormalizeSubject(subject), annotated));
        }

        return tags;
    }

    /// <inheritdoc />
    public async Task CreateTagAsync(
        string repositoryPath,
        string name,
        string target,
        string? message,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = await ValidateTagNameAsync(
            repositoryPath,
            name,
            cancellationToken);
        string normalizedTarget = string.IsNullOrWhiteSpace(target) ? "HEAD" : target.Trim();
        GitCommandResult targetResult = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", $"{normalizedTarget}^{{commit}}"],
            cancellationToken: cancellationToken);
        if (!targetResult.IsSuccess)
        {
            throw new InvalidOperationException(
                localizer.Format("Error_InvalidTagTarget", normalizedTarget));
        }

        GitCommandResult result;
        if (string.IsNullOrWhiteSpace(message))
        {
            result = await ExecuteAsync(
                repositoryPath,
                ["tag", "--", normalizedName, targetResult.StandardOutput.Trim()],
                cancellationToken: cancellationToken);
        }
        else
        {
            result = await ExecuteAsync(
                repositoryPath,
                ["tag", "--annotate", "--file=-", "--", normalizedName, targetResult.StandardOutput.Trim()],
                CommitMessageNormalizer.Normalize(message),
                cancellationToken);
        }

        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task DeleteTagAsync(
        string repositoryPath,
        string name,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = await ValidateTagNameAsync(
            repositoryPath,
            name,
            cancellationToken);
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["tag", "--delete", "--", normalizedName],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task<string> GetDiffAsync(
        string repositoryPath,
        GitFileChange change,
        bool staged,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> arguments;
        bool acceptsDifferenceExitCode = false;

        if (change.IsUntracked)
        {
            arguments = ["diff", "--no-index", "--no-color", "--", "/dev/null", change.Path];
            acceptsDifferenceExitCode = true;
        }
        else if (staged)
        {
            arguments = ["diff", "--cached", "--no-ext-diff", "--no-color", "--", change.Path];
        }
        else
        {
            arguments = ["diff", "--no-ext-diff", "--no-color", "--", change.Path];
        }

        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            arguments,
            cancellationToken: cancellationToken);
        if (!result.IsSuccess && !(acceptsDifferenceExitCode && result.ExitCode == 1))
        {
            EnsureSuccess(result);
        }

        return string.IsNullOrWhiteSpace(result.StandardOutput)
            ? localizer.GetString("Diff_NoText")
            : result.StandardOutput;
    }

    /// <inheritdoc />
    public async Task StageAsync(
        string repositoryPath,
        string path,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["add", "--", path],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task StageAllAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["add", "--all"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task<GitPartialPatch> GetPartialStagePatchAsync(
        string repositoryPath,
        string path,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["diff", "--no-ext-diff", "--no-color", "--unified=3", "--", path],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
        return ParsePartialPatch(result.StandardOutput);
    }

    /// <inheritdoc />
    public async Task StagePatchAsync(
        string repositoryPath,
        string patch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(patch) ||
            !patch.StartsWith("diff --git ", StringComparison.Ordinal) ||
            !patch.Contains("\n@@", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_PartialStagePatchInvalid"));
        }

        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["apply", "--cached", "--recount", "--whitespace=nowarn", "-"],
            patch,
            cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task DiscardWorkingTreeChangeAsync(
        string repositoryPath,
        string path,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["restore", "--worktree", "--", path],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task UnstageAsync(
        string repositoryPath,
        string path,
        bool hasHead,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> arguments = hasHead
            ? ["reset", "--quiet", "--", path]
            : ["rm", "--cached", "--quiet", "--", path];
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            arguments,
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task UnstageAllAsync(
        string repositoryPath,
        bool hasHead,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> arguments = hasHead
            ? ["reset", "--quiet", "HEAD", "--", "."]
            : ["rm", "--cached", "--quiet", "-r", "--ignore-unmatch", "--", "."];
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            arguments,
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task CommitAsync(
        string repositoryPath,
        string message,
        CancellationToken cancellationToken = default)
    {
        string normalizedMessage = CommitMessageNormalizer.Normalize(message);
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["commit", "--file=-"],
            normalizedMessage,
            cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task AmendCommitAsync(
        string repositoryPath,
        string commitHash,
        string message,
        CancellationToken cancellationToken = default)
    {
        string normalizedMessage = CommitMessageNormalizer.Normalize(message);
        GitCommandResult originalHeadResult = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", "HEAD"],
            cancellationToken: cancellationToken);
        EnsureSuccess(originalHeadResult);
        string originalHead = originalHeadResult.StandardOutput.Trim();

        GitCommandResult targetResult = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", $"{commitHash}^{{commit}}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(targetResult);
        string target = targetResult.StandardOutput.Trim();

        GitCommandResult ancestorResult = await ExecuteAsync(
            repositoryPath,
            ["merge-base", "--is-ancestor", target, originalHead],
            cancellationToken: cancellationToken);
        if (!ancestorResult.IsSuccess)
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_AmendCommitNotInHistory"));
        }

        if (string.Equals(target, originalHead, StringComparison.OrdinalIgnoreCase))
        {
            GitCommandResult amendResult = await ExecuteAsync(
                repositoryPath,
                ["commit", "--amend", "--file=-"],
                normalizedMessage,
                cancellationToken);
            EnsureSuccess(amendResult);
            return;
        }

        GitCommandResult stagedDiffResult = await ExecuteAsync(
            repositoryPath,
            ["diff", "--cached", "--binary", "--unified=0", "HEAD"],
            cancellationToken: cancellationToken);
        EnsureSuccess(stagedDiffResult);
        if (string.IsNullOrWhiteSpace(stagedDiffResult.StandardOutput))
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_NoStagedChangesForAmend"));
        }

        GitCommandResult mergeCommitsResult = await ExecuteAsync(
            repositoryPath,
            ["rev-list", "--min-parents=2", $"{target}..{originalHead}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(mergeCommitsResult);
        if (!string.IsNullOrWhiteSpace(mergeCommitsResult.StandardOutput))
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_AmendMergeHistoryUnsupported"));
        }

        IReadOnlyList<string> targetParents = await GetCommitParentsAsync(
            repositoryPath,
            target,
            cancellationToken);
        string rewrittenCommit = await CreateRewrittenCommitAsync(
            repositoryPath,
            target,
            target,
            targetParents,
            stagedDiffResult.StandardOutput,
            normalizedMessage,
            "Error_AmendPatchNotApplicable",
            cancellationToken);

        GitCommandResult descendantsResult = await ExecuteAsync(
            repositoryPath,
            ["rev-list", "--reverse", "--ancestry-path", $"{target}..{originalHead}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(descendantsResult);
        string originalParent = target;
        foreach (string descendant in descendantsResult.StandardOutput.Split(
                     (char[]?)null,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            IReadOnlyList<string> originalParents = await GetCommitParentsAsync(
                repositoryPath,
                descendant,
                cancellationToken);
            if (originalParents.Count != 1 ||
                !string.Equals(
                    originalParents[0],
                    originalParent,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    localizer.GetString("Error_AmendMergeHistoryUnsupported"));
            }

            GitCommandResult descendantPatchResult = await ExecuteAsync(
                repositoryPath,
                ["diff", "--binary", "--unified=0", originalParent, descendant],
                cancellationToken: cancellationToken);
            EnsureSuccess(descendantPatchResult);
            rewrittenCommit = await CreateRewrittenCommitAsync(
                repositoryPath,
                rewrittenCommit,
                descendant,
                [rewrittenCommit],
                descendantPatchResult.StandardOutput,
                null,
                "Error_AmendDescendantNotApplicable",
                cancellationToken);
            originalParent = descendant;
        }

        GitCommandResult currentHeadResult = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", "HEAD"],
            cancellationToken: cancellationToken);
        EnsureSuccess(currentHeadResult);
        if (!string.Equals(
                currentHeadResult.StandardOutput.Trim(),
                originalHead,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_AmendHeadChanged"));
        }

        GitCommandResult resetResult = await ExecuteAsync(
            repositoryPath,
            ["reset", "--mixed", rewrittenCommit],
            cancellationToken: cancellationToken);
        EnsureSuccess(resetResult);
    }

    /// <inheritdoc />
    public async Task AmendCommitMessageAsync(
        string repositoryPath,
        string commitHash,
        string message,
        CancellationToken cancellationToken = default)
    {
        string normalizedMessage = CommitMessageNormalizer.Normalize(message);
        GitCommandResult originalHeadResult = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", "HEAD"],
            cancellationToken: cancellationToken);
        EnsureSuccess(originalHeadResult);
        string originalHead = originalHeadResult.StandardOutput.Trim();

        GitCommandResult targetResult = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", $"{commitHash}^{{commit}}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(targetResult);
        string target = targetResult.StandardOutput.Trim();

        GitCommandResult ancestorResult = await ExecuteAsync(
            repositoryPath,
            ["merge-base", "--is-ancestor", target, originalHead],
            cancellationToken: cancellationToken);
        if (!ancestorResult.IsSuccess)
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_AmendCommitNotInHistory"));
        }

        GitCommandResult mergeCommitsResult = await ExecuteAsync(
            repositoryPath,
            ["rev-list", "--min-parents=2", $"{target}..{originalHead}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(mergeCommitsResult);
        if (!string.IsNullOrWhiteSpace(mergeCommitsResult.StandardOutput))
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_AmendMergeHistoryUnsupported"));
        }

        IReadOnlyList<string> targetParents = await GetCommitParentsAsync(
            repositoryPath,
            target,
            cancellationToken);
        string rewrittenCommit = await CreateCommitWithOriginalTreeAsync(
            repositoryPath,
            target,
            targetParents,
            normalizedMessage,
            cancellationToken);

        GitCommandResult descendantsResult = await ExecuteAsync(
            repositoryPath,
            ["rev-list", "--reverse", "--ancestry-path", $"{target}..{originalHead}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(descendantsResult);
        string originalParent = target;
        foreach (string descendant in descendantsResult.StandardOutput.Split(
                     (char[]?)null,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            IReadOnlyList<string> originalParents = await GetCommitParentsAsync(
                repositoryPath,
                descendant,
                cancellationToken);
            if (originalParents.Count != 1 ||
                !string.Equals(originalParents[0], originalParent, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    localizer.GetString("Error_AmendMergeHistoryUnsupported"));
            }

            rewrittenCommit = await CreateCommitWithOriginalTreeAsync(
                repositoryPath,
                descendant,
                [rewrittenCommit],
                null,
                cancellationToken);
            originalParent = descendant;
        }

        GitCommandResult currentHeadResult = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", "HEAD"],
            cancellationToken: cancellationToken);
        EnsureSuccess(currentHeadResult);
        if (!string.Equals(currentHeadResult.StandardOutput.Trim(), originalHead, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(localizer.GetString("Error_AmendHeadChanged"));
        }

        GitCommandResult updateResult = await ExecuteAsync(
            repositoryPath,
            ["update-ref", "HEAD", rewrittenCommit, originalHead],
            cancellationToken: cancellationToken);
        EnsureSuccess(updateResult);
    }

    private async Task<string> CreateCommitWithOriginalTreeAsync(
        string repositoryPath,
        string originalCommit,
        IReadOnlyList<string> rewrittenParents,
        string? replacementMessage,
        CancellationToken cancellationToken)
    {
        GitCommandResult treeResult = await ExecuteAsync(
            repositoryPath,
            ["rev-parse", "--verify", $"{originalCommit}^{{tree}}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(treeResult);

        GitCommandResult metadataResult = await ExecuteAsync(
            repositoryPath,
            ["show", "-s", "--format=%an%x00%ae%x00%aI%x00%B", originalCommit],
            cancellationToken: cancellationToken);
        EnsureSuccess(metadataResult);
        string[] metadata = metadataResult.StandardOutput.Split('\0', 4);
        if (metadata.Length != 4)
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_AmendMetadataUnavailable"));
        }

        var arguments = new List<string> { "commit-tree", treeResult.StandardOutput.Trim() };
        foreach (string parent in rewrittenParents)
        {
            arguments.Add("-p");
            arguments.Add(parent);
        }

        var environment = new Dictionary<string, string>
        {
            ["GIT_AUTHOR_NAME"] = metadata[0],
            ["GIT_AUTHOR_EMAIL"] = metadata[1],
            ["GIT_AUTHOR_DATE"] = metadata[2],
        };
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            arguments,
            replacementMessage ?? metadata[3],
            cancellationToken,
            environment);
        EnsureSuccess(result);
        return result.StandardOutput.Trim();
    }

    private async Task<string> CreateRewrittenCommitAsync(
        string repositoryPath,
        string baseCommit,
        string originalCommit,
        IReadOnlyList<string> rewrittenParents,
        string patch,
        string? replacementMessage,
        string patchErrorKey,
        CancellationToken cancellationToken)
    {
        string temporaryIndexPath = Path.Combine(
            Path.GetTempPath(),
            $"BRANCHY-amend-index-{Guid.NewGuid():N}");
        var indexEnvironment = new Dictionary<string, string>
        {
            ["GIT_INDEX_FILE"] = temporaryIndexPath,
        };

        try
        {
            GitCommandResult readTreeResult = await ExecuteAsync(
                repositoryPath,
                ["read-tree", baseCommit],
                cancellationToken: cancellationToken,
                environmentVariables: indexEnvironment);
            EnsureSuccess(readTreeResult);

            GitCommandResult applyResult = await ExecuteAsync(
                repositoryPath,
                ["apply", "--cached", "--unidiff-zero", "--whitespace=nowarn", "-"],
                patch,
                cancellationToken,
                indexEnvironment);
            if (!applyResult.IsSuccess)
            {
                throw new InvalidOperationException(
                    localizer.Format(
                        patchErrorKey,
                        GetCommandDetails(applyResult)));
            }

            GitCommandResult treeResult = await ExecuteAsync(
                repositoryPath,
                ["write-tree"],
                cancellationToken: cancellationToken,
                environmentVariables: indexEnvironment);
            EnsureSuccess(treeResult);
            string amendedTree = treeResult.StandardOutput.Trim();

            GitCommandResult metadataResult = await ExecuteAsync(
                repositoryPath,
                ["show", "-s", "--format=%an%x00%ae%x00%aI%x00%B", originalCommit],
                cancellationToken: cancellationToken);
            EnsureSuccess(metadataResult);
            string[] metadata = metadataResult.StandardOutput.Split('\0', 4);
            if (metadata.Length != 4)
            {
                throw new InvalidOperationException(
                    localizer.GetString("Error_AmendMetadataUnavailable"));
            }

            var commitTreeArguments = new List<string> { "commit-tree", amendedTree };
            foreach (string parent in rewrittenParents)
            {
                commitTreeArguments.Add("-p");
                commitTreeArguments.Add(parent);
            }

            var authorEnvironment = new Dictionary<string, string>
            {
                ["GIT_AUTHOR_NAME"] = metadata[0],
                ["GIT_AUTHOR_EMAIL"] = metadata[1],
                ["GIT_AUTHOR_DATE"] = metadata[2],
            };
            GitCommandResult commitResult = await ExecuteAsync(
                repositoryPath,
                commitTreeArguments,
                replacementMessage ?? metadata[3],
                cancellationToken,
                authorEnvironment);
            EnsureSuccess(commitResult);
            return commitResult.StandardOutput.Trim();
        }
        finally
        {
            if (File.Exists(temporaryIndexPath))
            {
                File.Delete(temporaryIndexPath);
            }
        }
    }

    private async Task<IReadOnlyList<string>> GetCommitParentsAsync(
        string repositoryPath,
        string commit,
        CancellationToken cancellationToken)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["rev-list", "--parents", "-n", "1", commit],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
        return result.StandardOutput.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task FetchAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["fetch", "--all", "--prune"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task PullAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["pull", "--ff-only"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    /// <inheritdoc />
    public async Task PushAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["push"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
    }

    private static IReadOnlyList<GitCommitFileChange> ParseCommitFiles(string output)
    {
        string[] fields = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var files = new List<GitCommitFileChange>();
        for (int index = 0; index < fields.Length;)
        {
            string status = fields[index++];
            if (index >= fields.Length)
            {
                break;
            }

            bool hasOriginalPath = status.StartsWith('R') || status.StartsWith('C');
            string? originalPath = hasOriginalPath ? fields[index++] : null;
            if (index >= fields.Length && hasOriginalPath)
            {
                break;
            }

            string path = fields[index++];
            files.Add(new GitCommitFileChange(status, path, originalPath));
        }

        return files;
    }

    private static GitFileChange CreateChange(string status, string path, string? originalPath = null)
    {
        char indexStatus = status.Length > 0 ? status[0] : '.';
        char workTreeStatus = status.Length > 1 ? status[1] : '.';
        return new GitFileChange(path, originalPath, indexStatus, workTreeStatus);
    }

    private static string NormalizeSubject(string subject)
    {
        int lineBreak = subject.IndexOfAny(['\r', '\n']);
        return (lineBreak >= 0 ? subject[..lineBreak] : subject).Trim();
    }

    private static void ParseAheadBehind(string value, out int ahead, out int behind)
    {
        ahead = 0;
        behind = 0;
        foreach (string item in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (item.StartsWith('+'))
            {
                int.TryParse(item.AsSpan(1), out ahead);
            }
            else if (item.StartsWith('-'))
            {
                int.TryParse(item.AsSpan(1), out behind);
            }
        }
    }

    private async Task<IReadOnlyList<string>> ReadNullSeparatedPathsAsync(
        string repositoryPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            arguments,
            cancellationToken: cancellationToken);
        EnsureSuccess(result);
        return result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string RemoveManagedIgnoreBlock(string contents)
    {
        string beginMarker = ManagedIgnoreBegin;
        string endMarker = ManagedIgnoreEnd;
        int begin = contents.IndexOf(beginMarker, StringComparison.Ordinal);
        if (begin < 0)
        {
            beginMarker = LegacyManagedIgnoreBegin;
            endMarker = LegacyManagedIgnoreEnd;
            begin = contents.IndexOf(beginMarker, StringComparison.Ordinal);
        }

        if (begin < 0)
        {
            return contents;
        }

        int end = contents.IndexOf(endMarker, begin, StringComparison.Ordinal);
        if (end < 0)
        {
            return contents[..begin];
        }

        end += endMarker.Length;
        while (end < contents.Length && contents[end] is '\r' or '\n')
        {
            end++;
        }

        return contents.Remove(begin, end - begin);
    }

    private static string NormalizeGitPath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string EscapeIgnorePath(string path)
    {
        var escaped = new StringBuilder(path.Length);
        foreach (char character in path)
        {
            if (character is '\\' or '*' or '?' or '[' or ']' or '#' or '!')
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        if (escaped.Length > 0 && escaped[^1] == ' ')
        {
            escaped.Insert(escaped.Length - 1, '\\');
        }

        return escaped.ToString();
    }

    private GitPartialPatch ParsePartialPatch(string patch)
    {
        string normalized = patch.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        int firstHunk = normalized.StartsWith("@@", StringComparison.Ordinal)
            ? 0
            : normalized.IndexOf("\n@@", StringComparison.Ordinal) + 1;
        if (firstHunk <= 0 ||
            !normalized.StartsWith("diff --git ", StringComparison.Ordinal) ||
            !normalized[..firstHunk].Contains("--- ", StringComparison.Ordinal) ||
            !normalized[..firstHunk].Contains("+++ ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_PartialStageUnavailable"));
        }

        string fileHeader = normalized[..firstHunk];
        var hunks = new List<GitPatchHunk>();
        int cursor = firstHunk;
        while (cursor < normalized.Length)
        {
            int nextMarker = normalized.IndexOf("\n@@", cursor + 2, StringComparison.Ordinal);
            int nextHunk = nextMarker < 0 ? normalized.Length : nextMarker + 1;
            string hunkText = normalized[cursor..nextHunk];
            int lineEnd = hunkText.IndexOf('\n');
            string header = lineEnd < 0 ? hunkText.TrimEnd() : hunkText[..lineEnd];
            hunks.Add(new GitPatchHunk(
                header,
                hunkText,
                GitDiffLine.Parse(hunkText)));
            cursor = nextHunk;
        }

        if (hunks.Count == 0)
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_PartialStageUnavailable"));
        }

        return new GitPartialPatch(fileHeader, hunks);
    }

    private async Task<string> ValidateRemoteNameAsync(
        string repositoryPath,
        string name,
        CancellationToken cancellationToken)
    {
        string normalized = name.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.StartsWith('-') ||
            normalized.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_RemoteNameRequired"));
        }

        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["check-ref-format", $"refs/remotes/{normalized}"],
            cancellationToken: cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                localizer.Format("Error_InvalidRemoteName", normalized));
        }

        return normalized;
    }

    private string ValidateRemoteUrl(string url)
    {
        string normalized = url.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new InvalidOperationException(
                localizer.GetString("Error_RemoteUrlRequired"));
        }

        return normalized;
    }

    private static string? NormalizePushUrl(string fetchUrl, string? pushUrl)
    {
        string normalized = pushUrl?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(normalized) ||
               string.Equals(fetchUrl, normalized, StringComparison.Ordinal)
            ? null
            : normalized;
    }

    private async Task SetRemotePushUrlAsync(
        string repositoryPath,
        string name,
        string? pushUrl,
        CancellationToken cancellationToken)
    {
        string key = $"remote.{name}.pushurl";
        GitCommandResult result = string.IsNullOrWhiteSpace(pushUrl)
            ? await ExecuteAsync(
                repositoryPath,
                ["config", "--unset-all", key],
                cancellationToken: cancellationToken)
            : await ExecuteAsync(
                repositoryPath,
                ["config", "--replace-all", key, ValidateRemoteUrl(pushUrl)],
                cancellationToken: cancellationToken);
        if (!result.IsSuccess &&
            !(string.IsNullOrWhiteSpace(pushUrl) && result.ExitCode is 1 or 5))
        {
            EnsureSuccess(result);
        }
    }

    private async Task<GitOperationResult> CompleteConflictAwareOperationAsync(
        string repositoryPath,
        GitCommandResult result,
        CancellationToken cancellationToken)
    {
        if (result.IsSuccess)
        {
            return new GitOperationResult(
                true,
                [],
                string.IsNullOrWhiteSpace(result.StandardOutput)
                    ? result.StandardError.Trim()
                    : result.StandardOutput.Trim());
        }

        GitCommandResult conflictsResult = await ExecuteAsync(
            repositoryPath,
            ["diff", "--name-only", "--diff-filter=U", "-z"],
            cancellationToken: cancellationToken);
        EnsureSuccess(conflictsResult);
        IReadOnlyList<string> conflicts = conflictsResult.StandardOutput.Split(
            '\0',
            StringSplitOptions.RemoveEmptyEntries);
        if (conflicts.Count > 0)
        {
            return new GitOperationResult(false, conflicts, GetCommandDetails(result));
        }

        EnsureSuccess(result);
        throw new InvalidOperationException(localizer.GetString("Status_OperationFailed"));
    }

    private async Task<string> ValidateTagNameAsync(
        string repositoryPath,
        string name,
        CancellationToken cancellationToken)
    {
        string normalized = name.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(localizer.GetString("Error_TagNameRequired"));
        }

        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["check-ref-format", $"refs/tags/{normalized}"],
            cancellationToken: cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                localizer.Format("Error_InvalidTagName", normalized));
        }

        return normalized;
    }

    private async Task ValidateBranchNameAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            throw new InvalidOperationException(localizer.GetString("Error_BranchNameRequired"));
        }

        GitCommandResult result = await ExecuteAsync(
            repositoryPath,
            ["check-ref-format", "--branch", branchName],
            cancellationToken: cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                localizer.Format("Error_InvalidBranchName", branchName));
        }
    }

    private void EnsureSuccess(GitCommandResult result)
    {
        if (result.IsSuccess)
        {
            return;
        }

        string details = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        throw new InvalidOperationException(
            localizer.Format("Error_GitCommandFailed", result.ExitCode, details));
    }

    private static string GetCommandDetails(GitCommandResult result) =>
        string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

    private static IReadOnlySet<string> ParseCommitHashes(string output) =>
        output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(hash => hash.Trim())
            .Where(hash => hash.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string EscapeGitExtendedRegex(string value)
    {
        const string specialCharacters = @"\.^$|?*+()[]{}";
        var escaped = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (specialCharacters.Contains(character))
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return escaped.ToString();
    }

    private async Task<GitCommandResult> ExecuteAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string? standardInput = null,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            StandardOutputEncoding = Utf8WithoutBom,
            StandardErrorEncoding = Utf8WithoutBom,
        };
        if (standardInput is not null)
        {
            startInfo.StandardInputEncoding = Utf8WithoutBom;
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "auto";
        if (environmentVariables is not null)
        {
            foreach ((string name, string value) in environmentVariables)
            {
                startInfo.Environment[name] = value;
            }
        }

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };
        var processExited = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        process.Exited += (_, _) => processExited.TrySetResult();
        if (!process.Start())
        {
            throw new InvalidOperationException(localizer.GetString("Error_GitNotFound"));
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
                await process.StandardInput.FlushAsync(cancellationToken);
                process.StandardInput.Close();
            }

            await processExited.Task.WaitAsync(cancellationToken);
            return new GitCommandResult(
                process.ExitCode,
                await outputTask,
                await errorTask);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }
    }

    private sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public bool IsSuccess => ExitCode == 0;
    }
}
